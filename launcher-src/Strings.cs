using System.Globalization;

namespace AutoDoomLauncher;

/// <summary>
/// Textos da interface em portugues e ingles. O idioma sai da configuracao do
/// Windows; `Language` no launcher.json manda mais alto ("pt", "en" ou "auto").
///
/// Sem .resx de proposito: sao poucas dezenas de frases, e assim o launcher
/// continua um unico exe sem satelite de recurso ao lado.
/// </summary>
internal static class Strings
{
   /// <summary>
   /// O idioma vem sempre do Windows, sem opcao de forcar: uma configuracao a menos
   /// para o usuario errar, e o launcher fala a lingua da maquina onde abriu.
   /// </summary>
   private static readonly bool _portuguese = DetectPortuguese();

   public static bool IsPortuguese => _portuguese;

   private static bool DetectPortuguese()
   {
      try
      {
         return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("pt", StringComparison.OrdinalIgnoreCase);
      }
      catch (Exception)
      {
         return false; // ingles e o fallback seguro
      }
   }

   private static string Pick(string pt, string en) => _portuguese ? pt : en;

   // ------------------------------------------------------------- janela

   public static string AppTitle => "AutoDoom Launcher";

   public static string GroupMode => Pick("Modo", "Mode");
   public static string GroupIwad => "IWAD";
   public static string GroupPwad => Pick("PWAD de mapa (opcional)", "Map PWAD (optional)");
   public static string GroupExtras => Pick("Extras", "Extras");

   public static string PipOption => Pick(
      "&Ver a tela dos bots num quadrinho (usa o autodoom_pip.exe)",
      "&Show the bots' view in a corner box (uses autodoom_pip.exe)");

   public static string PipMissing => Pick(
      "autodoom_pip.exe nao esta nesta pasta",
      "autodoom_pip.exe is not in this folder");

   public static string WeaponsOption => Pick(
      "Armas s&omem ao pegar, como no single player",
      "&Weapons disappear when picked up, like single player");

   public static string WeaponsHint => Pick(
      "No Coop o padrao do Doom e a arma ficar no chao para todo mundo pegar.",
      "In Coop, Doom's default leaves weapons on the floor for everyone.");

   public static string FollowLabel => Pick("A camera segue:", "The camera follows:");

   public static string FollowKills => Pick("quem mais mata", "whoever kills the most");

   public static string FollowExit => Pick(
      "quem esta mais perto da saida",
      "whoever is closest to the exit");

   public static string JumpOption => Pick(
      "Liberar o &pulo (a engine vem com ele desativado)",
      "Enable &jumping (the engine ships with it disabled)");

   public static string FriendlyFireOption => Pick(
      "&Fogo amigo",
      "&Friendly fire");

   public static string FriendlyFireHint => Pick(
      "Jogadores se ferem entre si, como no Doom classico.",
      "Players can hurt each other, like classic Doom.");

   // Rotulos do painel de detalhes do IWAD
   public static string DetailFile    => Pick("Arquivo:", "File:");
   public static string DetailKind    => Pick("Tipo:", "Type:");
   public static string DetailSize    => Pick("Tamanho:", "Size:");
   public static string DetailMaps    => Pick("Mapas:", "Maps:");

   public static string PipOptionShort => Pick(
      "Ver a tela dos bots num quadrinho",
      "Show the bots' view in a corner box");

   public static string PipOptionHint => Pick(
      "Usa o autodoom_pip.exe para exibir as telas dos bots.",
      "Uses autodoom_pip.exe to draw the bots' views.");

   public static string WeaponsOptionShort => Pick(
      "Armas somem ao pegar, como no single player",
      "Weapons disappear when picked up, like single player");

   public static string JumpOptionShort => Pick(
      "Liberar o pulo",
      "Enable jumping");

   public static string JumpHint => Pick(
      "A engine vem com o pulo desativado.",
      "The engine ships with jumping disabled.");

   public static string ScoreboardOption => Pick(
      "&Placar de abates ao segurar F",
      "&Kill scoreboard while holding F");

   public static string GroupProgress => Pick("Procurando no disco", "Searching the disk");

   public static string ModeCopilot => Pick(
      "&Copiloto: o bot joga e devolve o controle quando voce mexe",
      "&Copilot: the bot plays and hands control back when you move");

   public static string ModeCoop => Pick("C&oop: voce joga com", "C&oop: you play with");

   // O v2 quebra a linha do modo em rotulo (negrito) e descricao (normal), entao
   // cada metade precisa existir separada. O mnemonico mora no rotulo.
   public static string ModeCopilotName => Pick("&Copiloto:", "&Copilot:");

   public static string ModeCopilotDesc => Pick(
      "o bot joga e devolve o controle quando voce mexe",
      "the bot plays and hands control back when you move");

   public static string ModeCoopName => Pick("C&oop:", "C&oop:");

   public static string ModeCoopDesc => Pick("voce joga com", "you play with");

   /// <summary>Rotulo da linha de companheiros, que agora e independente do copiloto.</summary>
   public static string CompanionsName => Pick("Bots &companheiros:", "Bot &companions:");

   public static string CompanionsDesc => Pick("entram como jogadores 2 a 4", "join as players 2 to 4");

   public static string CameraCardTitle => Pick("A camera segue:", "The camera follows:");

   public static string BotsWord => "bots";

   public static string CopilotHint => Pick(
      "Com o copiloto ligado o bot dirige o seu personagem e solta o controle por 1 segundo a cada toque seu nas teclas.",
      "With the copilot on, the bot drives your character and releases control for 1 second each time you touch the keys.");

   public static string CopilotOffHint => Pick(
      "Sem copiloto voce joga o seu personagem do inicio ao fim.",
      "With the copilot off you play your own character from start to finish.");

   public static string CompanionsHintNone => Pick(
      "Sem companheiros: so o seu personagem no mapa.",
      "No companions: your character alone on the map.");

   public static string CompanionsHint(int count, int max) => Pick(
      $"{count} companheiro(s) de bot, nos slots 2 a {count + 1}. Maximo de {max}.",
      $"{count} bot companion(s), in slots 2 to {count + 1}. {max} at most.");

   // ------------------------------------------------------------- botoes

   public static string Browse => Pick("&Procurar...", "&Browse...");
   public static string BrowseAlt => Pick("P&rocurar...", "B&rowse...");
   public static string Detect => Pick("&Detectar WADs", "&Detect WADs");
   public static string DetectElevated => Pick("&Detectar WADs...", "&Detect WADs...");
   public static string Play => Pick("&Jogar", "&Play");
   public static string Quit => Pick("&Sair", "&Quit");

   // ------------------------------------------------------------ dialogos

   public static string ChooseIwad => Pick("Escolher IWAD", "Choose IWAD");
   public static string ChoosePwad => Pick("Escolher PWAD", "Choose PWAD");

   public static string IwadFilter => Pick(
      "IWAD do Doom (*.wad)|*.wad|Todos os arquivos (*.*)|*.*",
      "Doom IWAD (*.wad)|*.wad|All files (*.*)|*.*");

   public static string PwadFilter => Pick(
      "Arquivos de mapa (*.wad;*.pk3;*.pke;*.zip)|*.wad;*.pk3;*.pke;*.zip|Todos os arquivos (*.*)|*.*",
      "Map files (*.wad;*.pk3;*.pke;*.zip)|*.wad;*.pk3;*.pke;*.zip|All files (*.*)|*.*");

   public static string DetectTitle => Pick("Detectar WADs", "Detect WADs");

   public static string DetectTooltip => Pick(
      "Le a tabela de arquivos NTFS (journal USN) e acha os WADs do disco.",
      "Reads the NTFS file table (USN journal) to find the WADs on your disks.");

   public static string ClickToElevate => Pick(
      " Clique para reabrir elevado.",
      " Click to reopen as administrator.");

   public static string AskElevate => Pick(
      "Reabrir o launcher como administrador e detectar agora?",
      "Reopen the launcher as administrator and detect now?");

   public static string ElevationCancelled => Pick("Elevacao cancelada.", "Elevation cancelled.");

   public static string JournalFailed => Pick(
      "A leitura do journal falhou:",
      "Reading the journal failed:");

   public static string ReadingVolumes => Pick(
      "Lendo a tabela de arquivos de cada unidade...",
      "Reading the file table of each drive...");

   public static string Checking(int done, int total) => Pick(
      $"Conferindo {done}/{total} arquivos...",
      $"Checking {done}/{total} files...");

   public static string Pruning => Pick(
      "Repassando a lista de PWADs com os criterios novos...",
      "Re-checking the PWAD list with the new criteria...");

   public static string Pruned(int removed, int kept) => Pick(
      $"Lista limpa: {removed} descartados, {kept} PWADs de mapa.",
      $"List cleaned: {removed} discarded, {kept} map PWADs.");

   public static string Waiting => Pick("aguardando", "waiting");

   public static string VolumeDone(int found) => Pick(
      $"pronto, {found} arquivo(s)",
      $"done, {found} file(s)");

   public static string VolumeBusy(double fraction, int found) => Pick(
      $"{fraction * 100:F0}%, {found} achado(s)",
      $"{fraction * 100:F0}%, {found} found");

   public static string ScanSummary(int total, int iwads, int rejected, int partial, int newPwads)
   {
      string nl = Environment.NewLine;
      return _portuguese
         ? $"Achados {total} arquivos no disco.{nl}" +
           $"Ignorados {iwads} que sao jogos principais (IWAD).{nl}" +
           $"Descartados {rejected} que esta engine nao carrega.{nl}" +
           $"Aceitos com ressalva: {partial}.{nl}{nl}" +
           $"Novos na lista de PWAD: {newPwads}."
         : $"Found {total} files on disk.{nl}" +
           $"Skipped {iwads} that are base games (IWAD).{nl}" +
           $"Discarded {rejected} that this engine cannot load.{nl}" +
           $"Accepted with caveats: {partial}.{nl}{nl}" +
           $"New in the PWAD list: {newPwads}.";
   }

   public static string ScanStatus(int total, int newPwads) => Pick(
      $"{total} arquivos no disco, {newPwads} PWADs novos na lista.",
      $"{total} files on disk, {newPwads} new PWADs in the list.");

   public static string ExeNotFound(string exe, string dir) => Pick(
      $"Nao encontrei o {exe} em:{Environment.NewLine}{dir}",
      $"Could not find {exe} in:{Environment.NewLine}{dir}");

   public static string LaunchFailed(string message) => Pick(
      $"Nao consegui iniciar o jogo:{Environment.NewLine}{message}",
      $"Could not start the game:{Environment.NewLine}{message}");

   public static string ExitedWithCode(int code) => Pick(
      $"O AutoDoom saiu com codigo {code}.",
      $"AutoDoom exited with code {code}.");

   // ------------------------------------------------------------ veredito

   public static string VerdictOk => "OK";
   public static string VerdictPartial => Pick("Atencao", "Caution");
   public static string VerdictIncompatible => Pick("Incompativel", "Incompatible");

   // ---------------------------------------------------------- disponibilidade

   public static string NoNtfs => Pick(
      "Nenhum volume NTFS fixo encontrado.",
      "No fixed NTFS volume found.");

   public static string NeedsAdmin => Pick(
      "Ler o journal NTFS aqui exige executar o launcher como administrador.",
      "Reading the NTFS journal here requires running the launcher as administrator.");

   public static string JournalUnavailable(string problems) => Pick(
      "Journal USN indisponivel: " + problems + ".",
      "USN journal unavailable: " + problems + ".");

   public static string VolumeNoAccess(string letter, int error) => Pick(
      $"{letter} sem acesso ao volume (erro {error})",
      $"{letter} no access to the volume (error {error})");

   public static string VolumeJournalOff(string letter) => Pick(
      $"{letter} com journal desativado", $"{letter} has the journal turned off");

   public static string VolumeJournalDeleting(string letter) => Pick(
      $"{letter} apagando o journal", $"{letter} is deleting its journal");

   public static string VolumeDenied(string letter) => Pick(
      $"{letter} negou acesso", $"{letter} denied access");

   public static string VolumeError(string letter, int error) => Pick(
      $"{letter} erro {error}", $"{letter} error {error}");

   public static string VolumeSkipped(string message) => Pick(
      "ignorado: " + message, "skipped: " + message);

   public static string RebuildingPaths => Pick("remontando caminhos", "rebuilding paths");

   // ------------------------------------------------------------- validador

   public static string PwadNone => Pick("(nenhum)", "(none)");

   public static string FileTruncated => Pick(
      "arquivo vazio ou truncado", "empty or truncated file");

   public static string NotWadOrZip => Pick(
      "nao e WAD nem ZIP; a engine detecta formato pelo conteudo",
      "neither WAD nor ZIP; the engine detects format by content");

   public static string ReadError(string message) => Pick(
      "erro ao ler: " + message, "read error: " + message);

   public static string BadDirectory => Pick(
      "diretorio de lumps invalido", "invalid lump directory");

   public static string NoNamespace => Pick(
      "TEXTMAP sem namespace legivel", "TEXTMAP without a readable namespace");

   public static string BadUdmfNamespace(string ns) => Pick(
      $"mapa UDMF no namespace '{ns}', que a engine recusa sem ee_compat",
      $"UDMF map in namespace '{ns}', which the engine refuses without ee_compat");

   public static string ZdoomOnly(string list) => Pick(
      $"mod ZDoom: {list}, e nenhum mapa",
      $"ZDoom mod: {list}, and no maps");

   public static string PartialSummary(int count, string unit, string list) => Pick(
      $"{count} {unit}, mas {list} sera ignorado",
      $"{count} {unit}, but {list} will be ignored");

   public static string OkSummary(int count, string unit, string kind) => $"{count} {unit}, {kind}";

   public static string NoMapSummary(string kind) => Pick(
      $"{kind} sem mapa (so recursos)", $"{kind} with no maps (resources only)");

   public static string UnitMaps => Pick("mapa(s)", "map(s)");
   public static string UnitEmbedded => Pick("wad(s) embutido(s)", "embedded wad(s)");
   public static string KindBinary => Pick("binario", "binary");
   public static string KindUdmf => "UDMF";
   public static string KindZipWithWad => Pick("zip com wad embutido", "zip with embedded wad");
   public static string KindZipResources => Pick("zip de recursos", "resource zip");
}
