using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace AutoDoomLauncher;

internal sealed record WadHit(string Path, bool IsIwad);

/// <summary>Andamento de uma unidade durante a varredura.</summary>
internal sealed record UsnProgress(string Volume, double Fraction, int Found, bool Done, string? Note = null);

internal sealed record UsnAvailability(bool Available, bool NeedsElevation, string Reason);

/// <summary>
/// Acha WADs lendo a MFT pelo journal USN do NTFS, em vez de andar pelas pastas.
/// Uma varredura normal de disco cheio leva horas; esta le a tabela de arquivos
/// direto e termina em segundos por volume.
///
/// Custo: exige volume NTFS com journal ativo e processo elevado -- abrir o
/// handle do volume (\\.\C:) e operacao privilegiada.
/// </summary>
internal static class UsnScanner
{
   private const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
   private const uint FSCTL_ENUM_USN_DATA     = 0x000900b3;
   private const uint FSCTL_GET_NTFS_VOLUME_DATA = 0x00090064;

   /// <summary>Numero do registro na MFT: os 48 bits baixos do FRN.</summary>
   private const ulong RecordNumberMask = 0x0000_FFFF_FFFF_FFFF;

   private const uint GENERIC_READ           = 0x80000000;
   private const uint FILE_SHARE_READ_WRITE  = 0x00000003;
   private const uint OPEN_EXISTING          = 3;
   private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

   private const int ERROR_ACCESS_DENIED     = 5;
   private const int ERROR_HANDLE_EOF        = 38;
   private const int ERROR_JOURNAL_NOT_ACTIVE = 1179;
   private const int ERROR_JOURNAL_DELETE_IN_PROGRESS = 1178;

   private static readonly string[] Extensions = [".wad", ".pk3", ".pke"];

   [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
   private static extern SafeFileHandle CreateFileW(
      string fileName, uint access, uint share, IntPtr security,
      uint creation, uint flags, IntPtr template);

   [DllImport("kernel32.dll", SetLastError = true)]
   private static extern bool DeviceIoControl(
      SafeFileHandle device, uint controlCode,
      byte[]? inBuffer, int inSize,
      byte[]? outBuffer, int outSize,
      out int bytesReturned, IntPtr overlapped);

   /// <summary>Roda elevado? Sem isso nao da nem para consultar o journal.</summary>
   public static bool IsElevated()
   {
      try
      {
         using WindowsIdentity identity = WindowsIdentity.GetCurrent();
         return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
      }
      catch (Exception)
      {
         return false;
      }
   }

   /// <summary>Volumes fixos NTFS da maquina.</summary>
   public static List<DriveInfo> NtfsVolumes()
   {
      try
      {
         return DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady &&
                        string.Equals(d.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            .ToList();
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         return [];
      }
   }

   /// <summary>
   /// Diz se a deteccao pode rodar. So responde "disponivel" com journal de fato
   /// consultado com sucesso em pelo menos um volume -- nunca por suposicao.
   /// </summary>
   public static UsnAvailability CheckAvailability()
   {
      List<DriveInfo> volumes = NtfsVolumes();
      if (volumes.Count == 0)
         return new UsnAvailability(false, false, Strings.NoNtfs);

      // Nao se decide por suposicao: tenta consultar o journal de verdade e so
      // fala em elevacao se o Windows negar acesso.
      var problems = new List<string>();
      bool denied  = false;
      foreach (DriveInfo drive in volumes)
      {
         string letter = drive.Name.TrimEnd('\\');
         using SafeFileHandle handle = OpenVolume(letter);
         if (handle.IsInvalid)
         {
            int openError = Marshal.GetLastWin32Error();
            denied |= openError == ERROR_ACCESS_DENIED;
            problems.Add(Strings.VolumeNoAccess(letter, openError));
            continue;
         }

         byte[] output = new byte[128];
         if (DeviceIoControl(handle, FSCTL_QUERY_USN_JOURNAL, null, 0, output, output.Length, out _, IntPtr.Zero))
            return new UsnAvailability(true, false, "");

         int error = Marshal.GetLastWin32Error();
         denied |= error == ERROR_ACCESS_DENIED;
         problems.Add(error switch
         {
            ERROR_JOURNAL_NOT_ACTIVE           => Strings.VolumeJournalOff(letter),
            ERROR_JOURNAL_DELETE_IN_PROGRESS   => Strings.VolumeJournalDeleting(letter),
            ERROR_ACCESS_DENIED                => Strings.VolumeDenied(letter),
            _                                  => Strings.VolumeError(letter, error),
         });
      }

      if (denied && !IsElevated())
      {
         return new UsnAvailability(false, true,
            Strings.NeedsAdmin);
      }

      return new UsnAvailability(false, false,
         Strings.JournalUnavailable(string.Join(", ", problems)));
   }

   /// <summary>
   /// Varre a MFT de todo volume NTFS com journal e devolve os WADs achados,
   /// ja separados entre IWAD e PWAD e conferidos com File.Exists.
   /// </summary>
   public static List<WadHit> Scan(IProgress<UsnProgress>? progress = null)
   {
      var hits = new List<WadHit>();

      foreach (DriveInfo drive in NtfsVolumes())
      {
         string letter = drive.Name.TrimEnd('\\');

         try
         {
            List<WadHit> volumeHits = ScanVolume(letter, progress);
            hits.AddRange(volumeHits);
            progress?.Report(new UsnProgress(letter, 1.0, volumeHits.Count, true));
         }
         catch (Exception e) when (e is IOException or UnauthorizedAccessException)
         {
            progress?.Report(new UsnProgress(letter, 1.0, 0, true, Strings.VolumeSkipped(e.Message)));
         }
      }

      return hits;
   }

   /// <summary>
   /// Quantos registros a MFT deste volume tem. E o denominador honesto da barra:
   /// o cursor da enumeracao anda justamente sobre esses registros.
   /// </summary>
   private static long TotalMftRecords(SafeFileHandle handle)
   {
      byte[] data = new byte[128];
      if (!DeviceIoControl(handle, FSCTL_GET_NTFS_VOLUME_DATA, null, 0, data, data.Length, out int returned, IntPtr.Zero)
          || returned < 64)
      {
         return 0;
      }

      int  bytesPerRecord     = BitConverter.ToInt32(data, 48);
      long mftValidDataLength = BitConverter.ToInt64(data, 56);

      return bytesPerRecord > 0 ? mftValidDataLength / bytesPerRecord : 0;
   }

   private static SafeFileHandle OpenVolume(string letter) =>
      CreateFileW($@"\\.\{letter}", GENERIC_READ, FILE_SHARE_READ_WRITE,
                  IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

   private static List<WadHit> ScanVolume(string letter, IProgress<UsnProgress>? progress)
   {
      var found = new List<WadHit>();

      using SafeFileHandle handle = OpenVolume(letter);
      if (handle.IsInvalid)
         return found;

      byte[] journal = new byte[128];
      if (!DeviceIoControl(handle, FSCTL_QUERY_USN_JOURNAL, null, 0, journal, journal.Length, out _, IntPtr.Zero))
         return found;

      long nextUsn      = BitConverter.ToInt64(journal, 16); // USN_JOURNAL_DATA.NextUsn
      long totalRecords = TotalMftRecords(handle);

      // FRN do diretorio -> (nome, FRN do pai). So diretorio entra: o caminho dos
      // arquivos e remontado subindo por essa cadeia.
      var directories = new Dictionary<ulong, (string Name, ulong Parent)>();
      var candidates  = new List<(string Name, ulong Parent)>();

      byte[] input  = new byte[24];
      byte[] buffer = new byte[1 << 20];
      ulong start   = 0;

      while (true)
      {
         BitConverter.TryWriteBytes(input.AsSpan(0, 8), start);
         BitConverter.TryWriteBytes(input.AsSpan(8, 8), 0L);
         BitConverter.TryWriteBytes(input.AsSpan(16, 8), nextUsn);

         if (!DeviceIoControl(handle, FSCTL_ENUM_USN_DATA, input, input.Length,
                              buffer, buffer.Length, out int returned, IntPtr.Zero))
         {
            if (Marshal.GetLastWin32Error() == ERROR_HANDLE_EOF)
               break;
            throw new IOException($"ENUM_USN_DATA falhou em {letter} (erro {Marshal.GetLastWin32Error()})");
         }

         if (returned <= 8)
            break;

         start = BitConverter.ToUInt64(buffer, 0);

         if (totalRecords > 0)
         {
            double fraction = Math.Clamp((double)(start & RecordNumberMask) / totalRecords, 0, 1);
            progress?.Report(new UsnProgress(letter, fraction, candidates.Count, false));
         }

         int offset = 8;
         while (offset + 60 <= returned)
         {
            int recordLength = BitConverter.ToInt32(buffer, offset);
            if (recordLength <= 0 || offset + recordLength > returned)
               break;

            ulong frn        = BitConverter.ToUInt64(buffer, offset + 8);
            ulong parentFrn  = BitConverter.ToUInt64(buffer, offset + 16);
            uint  attributes = BitConverter.ToUInt32(buffer, offset + 52);
            int   nameLength = BitConverter.ToUInt16(buffer, offset + 56);
            int   nameOffset = BitConverter.ToUInt16(buffer, offset + 58);

            if (nameOffset > 0 && nameLength > 0 && offset + nameOffset + nameLength <= returned)
            {
               string name = System.Text.Encoding.Unicode.GetString(buffer, offset + nameOffset, nameLength);

               if ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                  directories[frn] = (name, parentFrn);
               else if (Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                  candidates.Add((name, parentFrn));
            }

            offset += recordLength;
         }
      }

      progress?.Report(new UsnProgress(letter, 1.0, candidates.Count, false, Strings.RebuildingPaths));

      foreach ((string name, ulong parent) in candidates)
      {
         string? dir = ResolvePath(letter, parent, directories);
         if (dir is null)
            continue;

         string full = Path.Combine(dir, name);
         if (File.Exists(full))
            found.Add(new WadHit(full, IwadCatalog.IsKnownIwadName(name)));
      }

      return found;
   }

   /// <summary>Sobe a cadeia de pais ate a raiz do volume. Null se a cadeia quebrar.</summary>
   private static string? ResolvePath(string letter, ulong frn, Dictionary<ulong, (string Name, ulong Parent)> directories)
   {
      var parts = new List<string>();
      ulong current = frn;

      for (int depth = 0; depth < 64; depth++)
      {
         if (!directories.TryGetValue(current, out (string Name, ulong Parent) entry))
            break; // cadeia acabou: o resto e a raiz do volume

         if (entry.Name == ".")
            break; // a raiz do NTFS se chama "." e aponta para si mesma

         parts.Add(entry.Name);

         if (entry.Parent == current)
            break;

         current = entry.Parent;
      }

      if (parts.Count == 0)
         return letter + Path.DirectorySeparatorChar;

      parts.Reverse();
      return letter + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, parts);
   }
}
