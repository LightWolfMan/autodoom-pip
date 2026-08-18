using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoDoomLauncher;

internal enum WadVerdict
{
   /// <summary>Carrega e o conteudo todo e entendido pela engine.</summary>
   Ok = 0,
   /// <summary>Carrega, mas parte do conteudo sera ignorada em silencio.</summary>
   Partial = 1,
   /// <summary>Nao carrega, ou carrega sem nada de util.</summary>
   Incompatible = 2,
}

internal sealed record WadReport(string Path, WadVerdict Verdict, string Summary, int MapCount)
{
   public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// Confere se um arquivo tem chance de funcionar neste Eternity, sem abrir o jogo.
/// Tudo aqui saiu da leitura do fonte em E:\Dev\AutoDoom\source:
///
///   - Formato e detectado por conteudo, nao por extensao (w_formats.cpp:182).
///     WAD e ZIP valem; ".pke" e ate a extensao recomendada para mods de EE
///     (w_formats.cpp:50) e ".pk3" e zip igual.
///   - Zip expoe .wad interno como wad de verdade (w_zip.cpp:411).
///   - UDMF so nos namespaces eternity/heretic/hexen/strife/doom, ou com
///     ee_compat=true no TEXTMAP (e_udmf.cpp:890-965).
///   - ZSCRIPT, DECORATE, GLDEFS, TEXTURES, MODELDEF e VOXELDEF nao existem no
///     fonte: conteudo ZDoom desse tipo e ignorado.
/// </summary>
internal static class WadValidator
{
   /// <summary>Lumps de ZDoom que esta engine nao le. Conferido por busca no fonte.</summary>
   private static readonly string[] ZdoomOnlyLumps =
   [
      "ZSCRIPT", "DECORATE", "GLDEFS", "TEXTURES", "MODELDEF", "VOXELDEF",
      "LOCKDEFS", "KEYCONF", "MENUDEF", "ZMAPINFO", "GAMEINFO", "ALTHUDCF",
   ];

   private static readonly string[] UdmfNamespaces =
      ["eternity", "heretic", "hexen", "strife", "doom"];

   private static readonly Regex MapMarkerRx =
      new(@"^(E\dM\d|MAP\d\d)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

   public static WadReport Inspect(string path)
   {
      try
      {
         using FileStream file = File.OpenRead(path);
         Span<byte> magic = stackalloc byte[4];
         if (file.Read(magic) < 4)
            return new WadReport(path, WadVerdict.Incompatible, Strings.FileTruncated, 0);

         file.Position = 0;

         if (magic[0] == 'P' && magic[1] == 'K')
            return InspectZip(path, file);

         string tag = Encoding.ASCII.GetString(magic);
         if (tag is "IWAD" or "PWAD")
            return InspectWad(path, file);

         return new WadReport(path, WadVerdict.Incompatible,
            Strings.NotWadOrZip, 0);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
      {
         return new WadReport(path, WadVerdict.Incompatible, Strings.ReadError(e.Message), 0);
      }
   }

   // ------------------------------------------------------------------ WAD

   private static WadReport InspectWad(string path, FileStream file)
   {
      using var reader = new BinaryReader(file, Encoding.ASCII, leaveOpen: true);
      reader.ReadInt32(); // magic
      int count  = reader.ReadInt32();
      int offset = reader.ReadInt32();

      if (count <= 0 || count > 1_000_000 || offset < 12 || offset >= file.Length)
         return new WadReport(path, WadVerdict.Incompatible, Strings.BadDirectory, 0);

      var names   = new List<string>(count);
      var offsets = new List<(int Pos, int Size)>(count);

      file.Position = offset;
      for (int i = 0; i < count; i++)
      {
         int pos  = reader.ReadInt32();
         int size = reader.ReadInt32();
         string name = new string(reader.ReadChars(8)).TrimEnd('\0', ' ').ToUpperInvariant();
         names.Add(name);
         offsets.Add((pos, size));
      }

      int maps = CountMaps(names);
      List<string> zdoom = names.Distinct().Where(n => ZdoomOnlyLumps.Contains(n)).ToList();

      // UDMF: o TEXTMAP vem logo depois do marcador do mapa.
      int textmap = names.IndexOf("TEXTMAP");
      if (textmap >= 0)
      {
         (int Pos, int Size) lump = offsets[textmap];
         string? namespaceName = ReadUdmfNamespace(file, lump.Pos, lump.Size, out bool eeCompat);

         if (namespaceName is null)
            return new WadReport(path, WadVerdict.Incompatible, Strings.NoNamespace, maps);

         if (!UdmfNamespaces.Contains(namespaceName, StringComparer.OrdinalIgnoreCase) && !eeCompat)
         {
            return new WadReport(path, WadVerdict.Incompatible,
               Strings.BadUdmfNamespace(namespaceName), maps);
         }
      }

      return Conclude(path, maps, zdoom, textmap >= 0 ? Strings.KindUdmf : Strings.KindBinary, unit: Strings.UnitMaps);
   }

   private static int CountMaps(List<string> names)
   {
      int maps = 0;
      for (int i = 0; i < names.Count; i++)
      {
         if (!MapMarkerRx.IsMatch(names[i]))
            continue;

         // Marcador de mapa e seguido de THINGS (binario) ou TEXTMAP (UDMF).
         string next = i + 1 < names.Count ? names[i + 1] : "";
         if (next is "THINGS" or "TEXTMAP")
            maps++;
      }
      return maps;
   }

   /// <summary>Le so o inicio do TEXTMAP: a primeira atribuicao e o namespace.</summary>
   private static string? ReadUdmfNamespace(FileStream file, int position, int size, out bool eeCompat)
   {
      eeCompat = false;

      if (position < 0 || size <= 0 || position + size > file.Length)
         return null;

      int take = Math.Min(size, 64 * 1024);
      byte[] buffer = new byte[take];
      file.Position = position;
      if (file.Read(buffer, 0, take) != take)
         return null;

      string head = Encoding.ASCII.GetString(buffer);
      eeCompat = Regex.IsMatch(head, @"ee_compat\s*=\s*true", RegexOptions.IgnoreCase);

      Match m = Regex.Match(head, @"namespace\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
      return m.Success ? m.Groups[1].Value : null;
   }

   // ------------------------------------------------------------------ ZIP

   private static WadReport InspectZip(string path, FileStream file)
   {
      using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);

      var entries = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
      var zdoom   = new List<string>();
      int maps    = 0;

      foreach (string entry in entries)
      {
         string name = System.IO.Path.GetFileNameWithoutExtension(entry).ToUpperInvariant();
         string ext  = System.IO.Path.GetExtension(entry).ToLowerInvariant();

         if (ZdoomOnlyLumps.Contains(name) && !zdoom.Contains(name))
            zdoom.Add(name);

         // Wad embutido conta como mapa em potencial (w_zip.cpp:411).
         if (ext == ".wad")
            maps++;
      }

      string kind = maps > 0 ? Strings.KindZipWithWad : Strings.KindZipResources;
      return Conclude(path, maps, zdoom, kind, unit: Strings.UnitEmbedded);
   }

   // --------------------------------------------------------------- comum

   private static WadReport Conclude(string path, int maps, List<string> zdoomLumps, string kind, string unit)
   {
      if (zdoomLumps.Count > 0)
      {
         string list = string.Join(", ", zdoomLumps.Take(3));
         string more = zdoomLumps.Count > 3 ? $" (+{zdoomLumps.Count - 3})" : "";

         // Sem mapa e so com conteudo ZDoom nao sobra nada para a engine usar.
         return maps == 0
            ? new WadReport(path, WadVerdict.Incompatible,
                 Strings.ZdoomOnly(list + more), 0)
            : new WadReport(path, WadVerdict.Partial,
                 Strings.PartialSummary(maps, unit, list + more), maps);
      }

      return maps > 0
         ? new WadReport(path, WadVerdict.Ok, Strings.OkSummary(maps, unit, kind), maps)
         : new WadReport(path, WadVerdict.Partial, Strings.NoMapSummary(kind), 0);
   }
}
