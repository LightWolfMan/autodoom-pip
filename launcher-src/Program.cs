namespace AutoDoomLauncher;

internal static class Program
{
   [STAThread]
   private static void Main(string[] args)
   {
      ApplicationConfiguration.Initialize();

      // Acompanha o modo claro/escuro do Windows. API experimental no .NET 9;
      // se falhar em alguma build futura, o launcher segue no tema claro.
      try
      {
         Application.SetColorMode(SystemColorMode.System);
      }
      catch (Exception)
      {
         // acabamento, nao requisito
      }

      // Reaberto elevado pelo proprio botao de detectar: ja emenda a varredura.
      bool autoDetect = args.Any(a => string.Equals(a, "--detectar-wads", StringComparison.OrdinalIgnoreCase));

      Application.Run(new MainForm(autoDetect));
   }
}
