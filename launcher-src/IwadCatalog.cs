using System.Text;
using System.Text.RegularExpressions;

namespace AutoDoomLauncher;

/// <summary>Um IWAD que o launcher pode oferecer, com o rotulo mostrado na combo.</summary>
internal sealed record IwadEntry(string Label, string Path)
{
   public override string ToString() => Label;
}

/// <summary>
/// Monta a lista de IWADs a partir de duas fontes: o user/system.cfg da engine
/// (somente leitura -- o Eternity reescreve esse arquivo ao sair) e uma varredura
/// das pastas de IWAD conhecidas. As entradas do cfg costumam envelhecer quando o
/// jogo muda de lugar, entao a varredura e o que mantem a lista util.
/// </summary>
internal static class IwadCatalog
{
   private static readonly Dictionary<string, string> CfgLabels = new()
   {
      ["iwad_doom_shareware"]    = "DOOM (Shareware)",
      ["iwad_doom"]              = "DOOM (Registered)",
      ["iwad_ultimate_doom"]     = "The Ultimate DOOM",
      ["iwad_doom2"]             = "DOOM II",
      ["iwad_bfgdoom2"]          = "DOOM II (BFG Edition)",
      ["iwad_tnt"]               = "Final DOOM: TNT - Evilution",
      ["iwad_plutonia"]          = "Final DOOM: The Plutonia Experiment",
      ["iwad_hacx"]              = "HACX",
      ["iwad_heretic_shareware"] = "Heretic (Shareware)",
      ["iwad_heretic"]           = "Heretic (Registered)",
      ["iwad_heretic_sosr"]      = "Heretic: Shadow of the Serpent Riders",
      ["iwad_freedoom"]          = "Freedoom (Doom II)",
      ["iwad_freedoomu"]         = "Freedoom (Ultimate Doom)",
      ["iwad_freedm"]            = "FreeDM",
      ["iwad_rekkr"]             = "Rekkr",
   };

   /// <summary>Nomes de arquivo que a varredura reconhece como IWAD.</summary>
   private static readonly Dictionary<string, string> FileLabels = new(StringComparer.OrdinalIgnoreCase)
   {
      ["doom.wad"]      = "DOOM",            // refinado depois: Ultimate tem episodio 4
      ["doom1.wad"]     = "DOOM (Shareware)",
      ["doomu.wad"]     = "The Ultimate DOOM",
      ["doom2.wad"]     = "DOOM II",
      ["doom2f.wad"]    = "DOOM II (frances)",
      ["tnt.wad"]       = "Final DOOM: TNT - Evilution",
      ["plutonia.wad"]  = "Final DOOM: The Plutonia Experiment",
      ["heretic.wad"]   = "Heretic",
      ["heretic1.wad"]  = "Heretic (Shareware)",
      ["hacx.wad"]      = "HACX",
      ["freedoom1.wad"] = "Freedoom (Ultimate Doom)",
      ["freedoom2.wad"] = "Freedoom (Doom II)",
      ["freedm.wad"]    = "FreeDM",
      ["rekkr.wad"]     = "Rekkr",
   };

   private static readonly Regex CfgLineRx =
      new(@"^\s*(iwad_[a-z0-9_]+)\s+""(.*)""\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

   /// <summary>
   /// Lista final, sem repetir caminho e com rotulo desambiguado quando o mesmo jogo
   /// aparece em mais de uma pasta.
   /// </summary>
   public static List<IwadEntry> Build(string gameDir, IEnumerable<string> extraFolders) =>
      Build(gameDir, extraFolders, []);

   public static List<IwadEntry> Build(string gameDir, IEnumerable<string> extraFolders, IEnumerable<string> extraFiles)
   {
      // Um jogo, uma linha. A primeira pasta da ordem de busca vence: a lista e de
      // JOGOS, nao de arquivos, e ver "DOOM II" tres vezes nao ajuda ninguem.
      var byGame = new Dictionary<string, IwadEntry>(StringComparer.OrdinalIgnoreCase);

      void Add(IwadEntry entry)
      {
         if (!byGame.ContainsKey(entry.Label))
            byGame[entry.Label] = entry;
      }

      foreach (string folder in DefaultFolders(gameDir).Concat(extraFolders))
      {
         foreach (IwadEntry entry in ScanFolder(folder))
            Add(entry);
      }

      // O cfg da engine entra por ultimo: costuma estar desatualizado.
      foreach (IwadEntry entry in FromSystemCfg(gameDir))
         Add(entry);

      // Arquivos avulsos (escolhidos a mao ou herdados de versoes antigas) passam
      // pela MESMA regra de um jogo por linha.
      foreach (string file in extraFiles.Where(File.Exists))
         Add(FromFile(file));

      return byGame.Values.OrderBy(e => e.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
   }

   /// <summary>Pastas varridas por padrao: a do jogo e as bibliotecas vizinhas.</summary>
   public static IEnumerable<string> DefaultFolders(string gameDir)
   {
      gameDir = Path.TrimEndingDirectorySeparator(gameDir);

      yield return gameDir;
      yield return Path.Combine(gameDir, "IWAD");

      // Com barra no fim, GetParent devolveria a propria pasta.
      DirectoryInfo? parent = Directory.GetParent(gameDir);
      if (parent is not null)
         yield return Path.Combine(parent.FullName, "IWAD");

      yield return @"E:\Jogos\Doom Library\IWADs";
   }

   private static IEnumerable<IwadEntry> ScanFolder(string folder)
   {
      string[] files;
      try
      {
         if (!Directory.Exists(folder))
            yield break;
         files = Directory.GetFiles(folder, "*.wad");
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         yield break;
      }

      foreach (string file in files)
      {
         if (FileLabels.TryGetValue(Path.GetFileName(file), out string? label))
            yield return new IwadEntry(Refine(label, file), Path.GetFullPath(file));
      }
   }

   /// <summary>IWADs declarados no system.cfg cujo arquivo ainda existe no disco.</summary>
   public static List<IwadEntry> FromSystemCfg(string gameDir)
   {
      var found = new List<IwadEntry>();
      string cfg = Path.Combine(gameDir, "user", "system.cfg");

      string[] lines;
      try
      {
         if (!File.Exists(cfg))
            return found;
         lines = File.ReadAllLines(cfg);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         return found;
      }

      foreach (string line in lines)
      {
         Match m = CfgLineRx.Match(line);
         if (!m.Success)
            continue;

         string key  = m.Groups[1].Value.ToLowerInvariant();
         string path = m.Groups[2].Value.Trim();
         if (path.Length == 0 || !File.Exists(path))
            continue;

         string label = CfgLabels.TryGetValue(key, out string? friendly)
            ? friendly
            : Path.GetFileNameWithoutExtension(path).ToUpperInvariant();

         found.Add(new IwadEntry(label, Path.GetFullPath(path)));
      }

      return found;
   }

   /// <summary>O nome de arquivo e de um IWAD conhecido?</summary>
   public static bool IsKnownIwadName(string fileName) => FileLabels.ContainsKey(fileName);

   /// <summary>Rotulo para um IWAD escolhido a mao, que nao esta em nenhuma lista.</summary>
   public static IwadEntry FromFile(string path)
   {
      string name = Path.GetFileName(path);
      string label = FileLabels.TryGetValue(name, out string? known) ? known : name;
      return new IwadEntry(Refine(label, path), Path.GetFullPath(path));
   }

   /// <summary>doom.wad pode ser o registrado ou o Ultimate; o episodio 4 decide.</summary>
   private static string Refine(string label, string path) =>
      label == "DOOM"
         ? (HasLump(path, "E4M1") ? "The Ultimate DOOM" : "DOOM (Registered)")
         : label;

   /// <summary>Le so o diretorio de lumps do WAD, procurando um nome.</summary>
   private static bool HasLump(string path, string lumpName)
   {
      try
      {
         using var reader = new BinaryReader(File.OpenRead(path), Encoding.ASCII);
         string magic = new(reader.ReadChars(4));
         if (magic is not ("IWAD" or "PWAD"))
            return false;

         int count  = reader.ReadInt32();
         int offset = reader.ReadInt32();
         if (count <= 0 || count > 65536 || offset < 12)
            return false;

         reader.BaseStream.Seek(offset, SeekOrigin.Begin);
         for (int i = 0; i < count; i++)
         {
            reader.ReadInt32(); // posicao do lump
            reader.ReadInt32(); // tamanho do lump
            string name = new string(reader.ReadChars(8)).TrimEnd('\0');
            if (string.Equals(name, lumpName, StringComparison.OrdinalIgnoreCase))
               return true;
         }
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException or EndOfStreamException)
      {
         // IWAD ilegivel nao deve derrubar a lista
      }

      return false;
   }
}
