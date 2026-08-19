using System.Text.RegularExpressions;

namespace AutoDoomLauncher;

/// <summary>
/// Liga e desliga opcoes que moram na configuracao da engine, nao na linha de
/// comando: o pulo e o placar. Escreve sempre ANTES de subir o jogo -- a engine
/// le no inicio e reescreve o arquivo ao sair, entao mexer com ele aberto seria
/// trabalho perdido.
/// </summary>
internal static class GameConfig
{
   private const string JumpKey = "comp_aircontrol";

   private static IEnumerable<string> Profiles(string gameDir)
   {
      string root = Path.Combine(gameDir, "user");
      if (!Directory.Exists(root))
         return [];

      try
      {
         return Directory.GetDirectories(root);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         return [];
      }
   }

   // --------------------------------------------------------------- pulo

   /// <summary>
   /// O pulo esta liberado? `comp_aircontrol` e alias de `comp_jump`: 1 desativa.
   /// </summary>
   public static bool JumpEnabled(string gameDir)
   {
      foreach (string profile in Profiles(gameDir))
      {
         string? value = ReadValue(Path.Combine(profile, "eternity.cfg"), JumpKey);
         if (value is not null)
            return value == "0";
      }
      return false;
   }

   public static void SetJump(string gameDir, bool enabled)
   {
      foreach (string profile in Profiles(gameDir))
         WriteValue(Path.Combine(profile, "eternity.cfg"), JumpKey, enabled ? "0" : "1");
   }

   // ------------------------------------------------------------- placar

   /// <summary>
   /// O placar precisa de duas coisas: `show_scores 1` e uma tecla ligada a acao
   /// "frags". Sem a tecla nao ha o que segurar; sem a variavel nada e desenhado.
   /// </summary>
   public static bool ScoreboardEnabled(string gameDir)
   {
      foreach (string profile in Profiles(gameDir))
      {
         bool bound  = HasBind(Path.Combine(profile, "keys.csc"));
         bool shown  = ReadValue(Path.Combine(profile, "eternity.cfg"), "show_scores") == "1";
         if (bound && shown)
            return true;
      }
      return false;
   }

   public static void SetScoreboard(string gameDir, bool enabled)
   {
      foreach (string profile in Profiles(gameDir))
      {
         WriteValue(Path.Combine(profile, "eternity.cfg"), "show_scores", enabled ? "1" : "0");
         SetBind(Path.Combine(profile, "keys.csc"), enabled);
      }
   }

   // ------------------------------------------------------ destravar bot

   /// <summary>
   /// Backspace solta os bots que travaram. A tecla so estava ocupada no
   /// console, entao no jogo ela fica livre para isto.
   /// </summary>
   public static void EnsureUnstickBind(string gameDir)
   {
      foreach (string profile in Profiles(gameDir))
         AddBind(Path.Combine(profile, "keys.csc"), "bind backspace \"bot_unstick\"");
   }

   // -------------------------------------------------------- fogo amigo

   /// <summary>Jogadores se ferem no coop? Ligado e o comportamento classico.</summary>
   public static bool FriendlyFire(string gameDir)
   {
      foreach (string profile in Profiles(gameDir))
      {
         string? value = ReadValue(Path.Combine(profile, "eternity.cfg"), "bot_friendlyfire");
         if (value is not null)
            return value != "0";
      }
      return true;
   }

   public static void SetFriendlyFire(string gameDir, bool enabled)
   {
      foreach (string profile in Profiles(gameDir))
         WriteValue(Path.Combine(profile, "eternity.cfg"), "bot_friendlyfire", enabled ? "1" : "0");
   }

   // ------------------------------------------------------- camera do PIP

   /// <summary>0 = segue quem mais mata, 1 = segue quem esta mais perto da saida.</summary>
   public static int PipFollow(string gameDir)
   {
      foreach (string profile in Profiles(gameDir))
      {
         string? value = ReadValue(Path.Combine(profile, "eternity.cfg"), "pip_follow");
         if (value is not null && int.TryParse(value, out int parsed))
            return parsed;
      }
      return 0;
   }

   public static void SetPipFollow(string gameDir, int mode)
   {
      foreach (string profile in Profiles(gameDir))
         WriteValue(Path.Combine(profile, "eternity.cfg"), "pip_follow", mode.ToString());
   }

   // -------------------------------------------------------------- baixo nivel

   private static string? ReadValue(string cfg, string key)
   {
      try
      {
         if (!File.Exists(cfg))
            return null;

         foreach (string line in File.ReadLines(cfg))
         {
            Match m = Regex.Match(line, $@"^{Regex.Escape(key)}\s+(\S+)\s*$");
            if (m.Success)
               return m.Groups[1].Value;
         }
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         // config ilegivel nao derruba o launcher
      }

      return null;
   }

   private static void WriteValue(string cfg, string key, string value)
   {
      try
      {
         if (!File.Exists(cfg))
            return;

         string[] lines = File.ReadAllLines(cfg);
         bool found = false;

         for (int i = 0; i < lines.Length; i++)
         {
            if (Regex.IsMatch(lines[i], $@"^{Regex.Escape(key)}\s"))
            {
               lines[i] = $"{key,-29} {value}";
               found = true;
            }
         }

         File.WriteAllLines(cfg, found ? lines : [.. lines, $"{key,-29} {value}"]);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
      }
   }

   private static void AddBind(string keys, string line)
   {
      try
      {
         if (!File.Exists(keys))
            return;

         List<string> lines = [.. File.ReadAllLines(keys)];
         if (lines.Any(l => l.StartsWith(line, StringComparison.OrdinalIgnoreCase)))
            return;

         lines.Add(line);
         File.WriteAllLines(keys, lines);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
      }
   }

   private static bool HasBind(string keys)
   {
      try
      {
         return File.Exists(keys) &&
                File.ReadLines(keys).Any(l => l.StartsWith("bind f \"frags\"", StringComparison.OrdinalIgnoreCase));
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
         return false;
      }
   }

   private static void SetBind(string keys, bool enabled)
   {
      try
      {
         if (!File.Exists(keys))
            return;

         List<string> lines = [.. File.ReadAllLines(keys)];
         bool has = lines.Any(l => l.StartsWith("bind f \"frags\"", StringComparison.OrdinalIgnoreCase));

         if (enabled && !has)
            lines.Add("bind f \"frags\"");
         else if (!enabled && has)
            lines.RemoveAll(l => l.StartsWith("bind f \"frags\"", StringComparison.OrdinalIgnoreCase));
         else
            return;

         File.WriteAllLines(keys, lines);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException)
      {
      }
   }
}
