using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoDoomLauncher;

internal enum PlayMode
{
   Copilot = 0,
   Coop    = 1,
}

/// <summary>
/// Estado do launcher, gravado em user/launcher.json. Nunca toca em system.cfg
/// nem em eternity.cfg: esses pertencem a engine.
/// </summary>
internal sealed class LauncherSettings
{
   public const string DefaultSoundfont = @"E:\Jogos\GZDoom\soundfonts\ToH(XGM)4.00(G).sf2";

   /// <summary>
   /// Legado: ate a v2 o launcher tinha dois modos exclusivos. Continua sendo
   /// lido para converter o launcher.json antigo -- Coop virava "sem copiloto"
   /// -- e nada mais depende dele.
   /// </summary>
   public PlayMode Mode { get; set; } = PlayMode.Copilot;

   /// <summary>
   /// O bot dirige o SEU personagem? Deixou de ser um modo e virou um
   /// interruptor: da para ter copiloto e companheiros ao mesmo tempo.
   /// Nulo significa launcher.json antigo, sem a chave -- quem decide entao e
   /// o `Mode`.
   /// </summary>
   public bool? Copilot { get; set; }

   /// <summary>Quantos bots companheiros, de 0 a 3. A engine tem 4 slots
   /// (MAXPLAYERS em doomdef.h:70) e um deles e o seu.</summary>
   public int BotCount { get; set; } = 3;
   public string? LastIwadPath { get; set; }
   /// <summary>PWAD escolhido no dropdown; vazio ou nulo significa nenhum.</summary>
   public string? SelectedPwad { get; set; }

   /// <summary>PWADs achados pela varredura de disco. Passam pelos filtros.</summary>
   public List<string> ExtraPwads { get; set; } = [];

   /// <summary>PWADs escolhidos a mao no "Procurar...". Nao passam por filtro.</summary>
   public List<string> ManualPwads { get; set; } = [];

   /// <summary>Pastas extras varridas em busca de PWAD, alem das padrao.</summary>
   public List<string> PwadFolders { get; set; } = [];
   public List<string> ExtraIwads { get; set; } = [];

   /// <summary>Pastas extras varridas em busca de IWAD, alem das padrao.</summary>
   public List<string> IwadFolders { get; set; } = [];

   public string? LastIwadFolder { get; set; }
   public string? LastPwadFolder { get; set; }
   public string SoundfontPath { get; set; } = DefaultSoundfont;

   /// <summary>
   /// Versao dos filtros de PWAD ja aplicada a lista salva. Quando o launcher
   /// aperta os criterios, a lista antiga e repassada uma vez so.
   /// </summary>
   public int FilterVersion { get; set; }

   /// <summary>Usa o autodoom_pip.exe e liga o quadrinho com a visao dos bots.</summary>
   public bool Pip { get; set; }

   /// <summary>
   /// Armas somem ao serem pegas, como no single player. O padrao do Coop no Doom
   /// e a arma ficar no chao (DM_WEAPONSTAY), o que deixa o jogo bem mais facil.
   /// </summary>
   public bool WeaponsDisappear { get; set; }

   [JsonIgnore]
   public string? FilePath { get; private set; }

   private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

   public static LauncherSettings Load(string gameDir)
   {
      string path = Path.Combine(gameDir, "user", "launcher.json");
      LauncherSettings settings;
      try
      {
         settings = File.Exists(path)
            ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path)) ?? new LauncherSettings()
            : new LauncherSettings();
      }
      catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
      {
         settings = new LauncherSettings();
      }

      settings.FilePath = path;
      return settings;
   }

   /// <summary>Falha ao salvar nao pode derrubar o launcher nem impedir o jogo de subir.</summary>
   public void Save()
   {
      if (FilePath is null)
         return;

      try
      {
         string? dir = Path.GetDirectoryName(FilePath);
         if (dir is not null)
            Directory.CreateDirectory(dir);

         File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
      {
         // silencio proposital
      }
   }
}
