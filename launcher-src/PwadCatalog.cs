using System.Text.RegularExpressions;

namespace AutoDoomLauncher;

/// <summary>Um PWAD oferecido no dropdown. Path vazio significa "nenhum".</summary>
internal sealed record PwadEntry(string Label, string Path)
{
   public static readonly PwadEntry None = new(Strings.PwadNone, "");

   public override string ToString() => Label;
}

/// <summary>
/// Varre as pastas de PWAD conhecidas. Ignora os prefabs do Obsidian, que sao centenas
/// de pecas de mapa e nao mapas jogaveis.
/// </summary>
internal static class PwadCatalog
{
   private static readonly string[] Extensions = [".wad", ".pk3", ".pke", ".zip"];

   /// <summary>
   /// Arquivos internos de engine: aparecem em toda instalacao de porta de Doom e
   /// nao sao mapa para jogar.
   /// </summary>
   private static readonly string[] EngineFiles =
   [
      "startup.wad", "eternity.pke", "eternity.wad", "brightmaps.pk3", "lights.pk3",
      "game_support.pk3", "game_widescreen_gfx.pk3", "gzdoom.pk3", "zandronum.pk3",
      "skulltag_actors.pk3", "skulltag_data.pk3", "prboom-plus.wad", "crispy-doom.wad",
   ];

   /// <summary>
   /// Nome que da para ler e reconhecer. A varredura por journal traz milhares de
   /// arquivos, e boa parte e despejo automatico -- hash, carimbo de data, sopa de
   /// digitos. Um nome que ninguem consegue ler nao ajuda a escolher um mapa.
   /// </summary>
   public static bool LooksReadable(string path)
   {
      string name = Path.GetFileNameWithoutExtension(path);

      if (name.Length is < 3 or > 32)
         return false;

      // Carimbo de data (2023-01-05-1407_algo) ou corrida longa de digitos.
      if (Regex.IsMatch(name, @"\d{4}[-_]?\d{2}[-_]?\d{2}") || Regex.IsMatch(name, @"\d{5,}"))
         return false;

      int letters = name.Count(char.IsLetter);
      int digits  = name.Count(char.IsDigit);

      if (letters < 3)
         return false;

      // Mais de um terco de digito ja e codigo, nao nome.
      if (digits * 3 > name.Length)
         return false;

      // Sem vogal nenhuma nao se pronuncia: "bbgrnw" fica de fora, "doom2" fica.
      if (!name.Any(c => "aeiouyAEIOUY".Contains(c)))
         return false;

      return true;
   }

   public static bool IsEngineFile(string path) =>
      EngineFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

   /// <summary>
   /// Pastas que so produzem ruido: prefabs do Obsidian, tripas de instalacao,
   /// lixeira, cache do sistema. A varredura por journal cai nelas as centenas.
   /// </summary>
   private static readonly string[] ExcludedSegments =
   [
      @"abs\", @"\games\", @"\data\", @"ddons\", @"\modules\",
      @"	emp\", @"	mp\", @"\cache\", @"\$RECYCLE.BIN\", @"\AppData\",
      @"\Windows\", @"
ode_modules\", @"\.git\", @"\engines\",
   ];

   /// <summary>Caminho que nao vale a pena oferecer, pela pasta em que mora.</summary>
   public static bool IsNoisePath(string path) =>
      ExcludedSegments.Any(seg => path.Contains(seg, StringComparison.OrdinalIgnoreCase));

   public static IEnumerable<string> DefaultFolders(string gameDir)
   {
      yield return gameDir;
      yield return @"E:\Jogos\Doom Library\PWADs\Collections\Zandronum";
      yield return @"E:\Jogos\Doom Library\PWADs\Collections\GZDoom";
      // Generated/ fica de fora: e a instalacao do Obsidian, cheia de addon e temp,
      // nao uma pasta de mapas. Quem quiser inclui em PwadFolders no launcher.json.
   }

   /// <summary>Lista final, sem repetir caminho e com "(nenhum)" na frente.</summary>
   public static List<PwadEntry> Build(string gameDir, IEnumerable<string> extraFolders, IEnumerable<string> extraFiles) =>
      Build(gameDir, extraFolders, extraFiles, []);

   /// <summary>
   /// `manualFiles` sao os escolhidos a mao pelo usuario: entram mesmo com nome
   /// ilegivel, porque ali a escolha ja foi dele.
   /// </summary>
   public static List<PwadEntry> Build(string gameDir, IEnumerable<string> extraFolders,
                                       IEnumerable<string> extraFiles, IEnumerable<string> manualFiles)
   {
      var byPath = new Dictionary<string, PwadEntry>(StringComparer.OrdinalIgnoreCase);

      void Add(string file, bool manual = false)
      {
         string full = Path.GetFullPath(file);
         string name = Path.GetFileName(full);

         if (IsEngineFile(full))
            return;

         if (!manual && (!LooksReadable(full) || IsNoisePath(full)))
            return;

         // Chave e o NOME, nao o caminho: a mesma wad em duas pastas e uma opcao so.
         if (!byPath.ContainsKey(name))
            byPath[name] = new PwadEntry(name, full);
      }

      // A pasta do jogo entra sem recursao: base/ e user/ tem wads internos da engine.
      foreach (string file in Scan(gameDir, recursive: false))
         Add(file);

      foreach (string folder in DefaultFolders(gameDir).Skip(1).Concat(extraFolders))
      {
         foreach (string file in Scan(folder, recursive: true))
            Add(file);
      }

      foreach (string file in extraFiles.Where(File.Exists))
         Add(file);

      foreach (string file in manualFiles.Where(File.Exists))
         Add(file, manual: true);

      List<PwadEntry> all = byPath.Values
         .OrderBy(e => e.Label, StringComparer.CurrentCultureIgnoreCase)
         .ToList();

      all.Insert(0, PwadEntry.None);
      return all;
   }

   private static IEnumerable<string> Scan(string folder, bool recursive)
   {
      if (!Directory.Exists(folder))
         return [];

      try
      {
         return Directory
            .EnumerateFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !IsNoisePath(f));
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         return [];
      }
   }
}
