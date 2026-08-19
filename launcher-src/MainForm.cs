using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace AutoDoomLauncher;

internal sealed class MainForm : Form
{
   private const string GameExeName = "AutoDoom.exe";

   /// <summary>Build com o patch de picture-in-picture, compilado do fonte.</summary>
   private const string PipExeName = "autodoom_pip.exe";
   private const string DefaultPwadFolder = @"E:\Jogos\Doom Library\PWADs";

   private readonly string _gameDir;
   private readonly LauncherSettings _settings;

   private readonly CheckBox    _cbCopilot = new();
   private readonly ListBox     _lbIwads   = new();
   private readonly ComboBox    _cbPwad    = new();
   private readonly Button      _btnPlay   = new();
   private readonly Button      _btnDetect = new();
   private readonly TrackBar    _tbBots     = new();
   private readonly NumericUpDown _nudBots  = new();
   private readonly Label       _lblStatus = new();
   private readonly ToolTip     _tips      = new();
   private readonly Label       _lblBotHint = new();
   private readonly Label       _lblCopilotHint = new();
   private readonly CheckBox    _cbPip     = new();
   private readonly CheckBox    _cbWeapons = new();
   private readonly CheckBox    _cbJump    = new();
   private readonly CheckBox    _cbFriendly = new();
   private readonly ComboBox    _cbFollow  = new();
   private readonly Label       _lblFollow = new();
   private readonly GroupBox    _grpProgress = new();

   // Painel de detalhes do IWAD selecionado.
   private readonly PictureBox  _detIcon = new();
   private readonly Label       _detName = new();
   private readonly Label       _detFile = new();
   private readonly Label       _detKind = new();
   private readonly Label       _detSize = new();
   private readonly Label       _detMaps = new();

   /// <summary>Os quatro numeros sob o slider; o corrente vai em azul negrito.</summary>
   private readonly Label[]     _botTicks = new Label[MaxCompanions + 1];

   /// <summary>Slider e spinner mostram o mesmo valor; a trava evita o pingue-pongue.</summary>
   private bool _syncingBots;

   /// <summary>Inspect le o arquivo inteiro: uma vez por IWAD basta.</summary>
   private readonly Dictionary<string, int> _mapCounts = new(StringComparer.OrdinalIgnoreCase);
   private readonly TableLayoutPanel _progressRows = new();
   private readonly Dictionary<string, (ProgressBar Bar, Label Info)> _volumeRows = new(StringComparer.OrdinalIgnoreCase);

   /// <summary>
   /// Sobe quando os criterios de PWAD apertam. Lista salva com versao menor e
   /// repassada uma vez, em segundo plano.
   /// </summary>
   private const int FilterVersion = 1;

   /// <summary>Slots de jogador da engine: MAXPLAYERS 4 em doomdef.h:70.</summary>
   private const int MaxBots = 4;

   /// <summary>
   /// Companheiros possiveis: os slots 2 a 4, porque o 1 e sempre o seu. O
   /// copiloto nao gasta slot -- ele dirige o seu personagem -- entao os dois
   /// controles agora sao independentes.
   /// </summary>
   private const int MaxCompanions = MaxBots - 1;

   /// <summary>
   /// Medidas unicas de botao. Largura igual em toda a janela e o que faz a coluna
   /// da direita do IWAD, o "Procurar..." do PWAD e o rodape ficarem no mesmo prumo.
   /// Larga o bastante para o maior rotulo com icone ("Detectar WADs...").
   /// </summary>
   private const int ButtonWidth  = 152;
   private const int ButtonHeight = 32;
   private const int Gutter       = 8;
   /// <summary>Borda do GroupBox (3) + padding interno dos grupos (12).</summary>
   private const int GroupInset   = 15;

   // Icones desenhados uma vez e guardados: Button.Image nao descarta o bitmap, e
   // gerar a cada repaint vazaria handle de GDI.
   /// <summary>
   /// Os cartoes da secao Extras, guardados so para igualar a altura de todos
   /// depois que a janela mediu o texto.
   /// </summary>
   private readonly List<Control> _extraCards = [];

   private Bitmap? _icoFolder;
   private Bitmap? _icoScan;
   private Bitmap? _icoExit;
   private Bitmap? _icoPlayWhite;
   private Bitmap? _icoOne;
   private Bitmap? _icoTwo;
   private Bitmap? _icoPip;
   private Bitmap? _icoWeapon;
   private Bitmap? _icoCamera;
   private Bitmap? _icoJump;
   private Bitmap? _icoFire;
   private Bitmap? _icoWad;
   private Font?   _boldFont;
   private Font?   _sectionFont;
   private Font?   _titleFont;
   private Font?   _smallFont;
   private Font?   _nameFont;
   private Font?   _modeNameFont;
   private Font?   _modeDescFont;

   /// <summary>
   /// Um so lugar decide tamanho, icone e folga de qualquer botao da janela.
   /// O MinimumSize precisa ir ja em unidades de tela: o autoscale do WinForms nao
   /// escala essa propriedade, e a 125% o "Detectar WADs..." estourava a largura
   /// comum e ficava maior que os outros botoes.
   /// </summary>
   private Button StyleButton(Button button, string text, Image? icon, Padding margin)
   {
      button.Text         = text;
      button.AutoSize     = true;
      button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
      button.MinimumSize  = new Size(LogicalToDeviceUnits(ButtonWidth),
                                     LogicalToDeviceUnits(ButtonHeight));
      button.Margin       = margin;
      button.ForeColor    = SystemColors.ControlText;

      if (icon is not null)
      {
         button.Image             = icon;
         button.ImageAlign        = ContentAlignment.MiddleLeft;
         button.TextAlign         = ContentAlignment.MiddleLeft;
         button.TextImageRelation = TextImageRelation.ImageBeforeText;
         button.Padding           = new Padding(8, 0, 8, 0);
      }

      return button;
   }

   // ------------------------------------------------------------ cores do tema

   /// <summary>
   /// O tema escuro nao troca so o fundo: um azul fixo claro some no claro e um azul
   /// escuro some no escuro. Decidir por luminancia do fundo das janelas resolve os
   /// dois casos sem depender de API de tema.
   /// </summary>
   private static bool IsDarkTheme =>
      SystemColors.Window.R * 0.299 + SystemColors.Window.G * 0.587 + SystemColors.Window.B * 0.114 < 128;

   /// <summary>Azul de acento dos titulos de grupo e do circulo de informacao.</summary>
   private static Color AccentColor =>
      IsDarkTheme ? Color.FromArgb(118, 178, 255) : Color.FromArgb(0, 78, 152);

   private static Color InfoFillColor =>
      IsDarkTheme ? Blend(SystemColors.Control, Color.FromArgb(60, 120, 200), 0.28f)
                  : Color.FromArgb(240, 247, 255);

   private static Color InfoBorderColor =>
      IsDarkTheme ? Color.FromArgb(92, 132, 184) : Color.FromArgb(178, 209, 240);

   /// <summary>Fundo dos cartoes: branco no tema claro, o proprio fundo de janela.</summary>
   private static Color CardColor => SystemColors.Window;

   /// <summary>Fundo da pagina, um degrau abaixo do cartao para o cartao aparecer.</summary>
   private static Color PageColor => Blend(SystemColors.Window, SystemColors.ControlText, 0.04f);

   private static Color CardBorderColor => Blend(SystemColors.Window, SystemColors.ControlText, 0.17f);

   private static Color SeparatorColor =>
      Blend(SystemColors.Control, SystemColors.ControlDark, 0.65f);

   private static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
      (int)(a.R + (b.R - a.R) * amount),
      (int)(a.G + (b.G - a.G) * amount),
      (int)(a.B + (b.B - a.B) * amount));

   // ---------------------------------------------------------------- icones

   /// <summary>
   /// Um icone Fluent na cor pedida, ja convertido para o tamanho fisico do
   /// monitor. O desenho vem de `Icons/`; ver `FluentIcons.cs` para o porque de
   /// eles serem mascaras pretas em vez de arte ja colorida.
   /// </summary>
   private Bitmap? Glyph(string name, int logicalSize, Color color) =>
      FluentIcons.Get(name, LogicalToDeviceUnits(logicalSize), color);

   private void BuildIcons()
   {
      // Botao segue a cor do texto do sistema; cartao e modo usam o azul de
      // acento, que e o que separa o icone do rotulo ao lado sem pesar.
      Color ink = SystemColors.ControlText;

      _icoFolder = Glyph("folder", 16, ink);
      _icoScan   = Glyph("scan",   16, ink);
      _icoExit   = Glyph("exit",   16, ink);

      // O "Jogar" e o botao primario azul: o triangulo so aparece em branco.
      _icoPlayWhite = Glyph("play", 16, Color.White);

      _icoOne = Glyph("person", 26, AccentColor);
      _icoTwo = Glyph("people", 26, AccentColor);

      _icoPip    = Glyph("pip",    28, AccentColor);
      _icoWeapon = Glyph("weapon", 28, AccentColor);
      _icoCamera = Glyph("camera", 28, AccentColor);
      _icoJump   = Glyph("jump",   28, AccentColor);
      _icoFire   = Glyph("fire",   28, AccentColor);

      // Reserva para o IWAD que nao entrega logo proprio.
      _icoWad = Glyph("wad", 24, Blend(AccentColor, SystemColors.Window, 0.25f));
   }

   protected override void Dispose(bool disposing)
   {
      if (disposing)
      {
         // Os icones NAO entram aqui: quem e dono deles e o cache do
         // FluentIcons, que devolve o mesmo bitmap para todo mundo que pedir a
         // mesma cor e tamanho. Descartar aqui deixaria o cache com handle
         // morto na mao.
         _boldFont?.Dispose();
         _sectionFont?.Dispose();
         _titleFont?.Dispose();
         _smallFont?.Dispose();
         _nameFont?.Dispose();
         _modeNameFont?.Dispose();
         _modeDescFont?.Dispose();
      }
      base.Dispose(disposing);
   }

   private bool _scanning;

   private readonly bool _autoDetect;

   public MainForm(bool autoDetect = false)
   {
      _autoDetect = autoDetect;
      _gameDir    = FindGameDir();
      _settings   = LauncherSettings.Load(_gameDir);

      BuildUi();
      LoadState();
      UpdateEnabledState();
   }

   /// <summary>
   /// A pasta do jogo e onde vive o AutoDoom.exe. Em publish o launcher fica la mesmo;
   /// rodando de bin/Debug, sobe os diretorios ate achar.
   /// </summary>
   private static string FindGameDir()
   {
      var dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir is not null)
      {
         if (File.Exists(Path.Combine(dir.FullName, GameExeName)))
            return Path.TrimEndingDirectorySeparator(dir.FullName);
         dir = dir.Parent;
      }
      // Sem a barra final: com ela, Directory.GetParent devolve a propria pasta e a
      // varredura das bibliotecas vizinhas nao acontece.
      return Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
   }

   // ------------------------------------------------------------------ UI

   private void BuildUi()
   {
      Text = Strings.AppTitle;
      Font = SystemFonts.MessageBoxFont ?? Font;
      // Base de escala do Segoe UI 9pt a 100%. Sem isso o WinForms encolhe a janela.
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode       = AutoScaleMode.Font;
      StartPosition = FormStartPosition.CenterScreen;
      Icon          = TryGetOwnIcon();
      AllowDrop     = true;
      BackColor     = PageColor;
      DragEnter    += OnDragEnter;
      DragDrop     += OnDragDrop;

      _sectionFont = new Font(Font.FontFamily, Font.Size * 0.86f, FontStyle.Bold);
      _titleFont   = new Font(Font, FontStyle.Bold);
      _boldFont    = new Font(Font, FontStyle.Bold);
      _smallFont   = new Font(Font.FontFamily, Font.Size * 0.88f, FontStyle.Regular);
      _nameFont    = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold);
      // A secao Modo e a mais larga e a mais vazia: corpo maior no nome e na
      // descricao para o texto ocupar o cartao em vez de boiar no meio dele.
      _modeNameFont = new Font(Font.FontFamily, Font.Size * 1.30f, FontStyle.Bold);
      _modeDescFont = new Font(Font.FontFamily, Font.Size * 1.20f, FontStyle.Regular);

      BuildIcons();

      var root = new TableLayoutPanel
      {
         Dock        = DockStyle.Fill,
         Padding     = new Padding(LogicalToDeviceUnits(14)),
         ColumnCount = 1,
         RowCount    = 6,
         BackColor   = PageColor,
      };
      root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Modo
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Extras
      root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // IWAD, elastica
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // PWAD
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // progresso
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // rodape

      root.Controls.Add(BuildModeGroup(),     0, 0);
      root.Controls.Add(BuildExtrasGroup(),   0, 1);
      root.Controls.Add(BuildIwadGroup(),     0, 2);
      root.Controls.Add(BuildPwadGroup(),     0, 3);
      root.Controls.Add(BuildProgressGroup(), 0, 4);
      root.Controls.Add(BuildFooter(),        0, 5);

      Controls.Add(root);
   }

   /// <summary>
   /// Tamanho definido depois do autoscale: no construtor o WinForms reescalava a
   /// janela para baixo e cortava os botoes da direita.
   /// </summary>
   protected override void OnLoad(EventArgs e)
   {
      base.OnLoad(e);
      MinimumSize = new Size(LogicalToDeviceUnits(1040), LogicalToDeviceUnits(740));
      Size        = new Size(LogicalToDeviceUnits(1250), LogicalToDeviceUnits(880));
      CenterToScreen();

      EqualizeExtraCards();
      RefreshDetectAvailability();
      PruneStoredPwads();
      UpdateIwadDetails();

      if (_autoDetect && _btnDetect.Enabled)
         BeginInvoke(() => OnDetectWads(this, EventArgs.Empty));
   }

   // ------------------------------------------------------- cartoes e secoes

   /// <summary>
   /// Cartao branco de canto arredondado. O fundo do painel fica na cor da pagina
   /// para os cantos vazados nao virarem quadradinhos brancos; quem pinta o branco
   /// e o Paint, dentro do caminho arredondado.
   /// </summary>
   private void PaintCard(object? sender, PaintEventArgs e)
   {
      if (sender is not Control card)
         return;

      e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
      var area = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
      using GraphicsPath path = RoundedRect(area, LogicalToDeviceUnits(8));
      using var fill = new SolidBrush(CardColor);
      using var edge = new Pen(CardBorderColor);
      e.Graphics.FillPath(fill, path);
      e.Graphics.DrawPath(edge, path);
   }

   private TableLayoutPanel MakeCard(Control inner, int pad, bool fill)
   {
      inner.BackColor = CardColor;
      inner.Dock      = DockStyle.Fill;
      inner.Margin    = new Padding(0);

      var card = new TableLayoutPanel
      {
         ColumnCount  = 1,
         RowCount     = 1,
         BackColor    = PageColor,
         Padding      = new Padding(LogicalToDeviceUnits(pad)),
         Margin       = new Padding(0),
         Dock         = fill ? DockStyle.Fill : DockStyle.Top,
         AutoSize     = !fill,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
      };
      card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      card.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100f) : new RowStyle(SizeType.AutoSize));
      card.Controls.Add(inner, 0, 0);
      card.Paint += PaintCard;
      return card;
   }

   /// <summary>Titulo em maiusculas azuis acima do cartao branco da secao.</summary>
   private Control MakeSection(string title, Control inner, bool fill, int pad = 14)
   {
      var caption = new Label
      {
         Text      = title.ToUpperInvariant(),
         AutoSize  = true,
         Font      = _sectionFont,
         ForeColor = AccentColor,
         BackColor = PageColor,
         Margin    = new Padding(LogicalToDeviceUnits(4), 0, 0, LogicalToDeviceUnits(4)),
      };

      var host = new TableLayoutPanel
      {
         ColumnCount  = 1,
         RowCount     = 2,
         BackColor    = PageColor,
         Dock         = fill ? DockStyle.Fill : DockStyle.Top,
         AutoSize     = !fill,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0, 0, 0, LogicalToDeviceUnits(10)),
      };
      host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      host.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100f) : new RowStyle(SizeType.AutoSize));
      host.Controls.Add(caption, 0, 0);
      host.Controls.Add(MakeCard(inner, pad, fill), 0, 1);
      return host;
   }

   private Label MakeText(string text, Font? font, Color color) => new()
   {
      Text      = text,
      AutoSize  = true,
      Font      = font ?? Font,
      ForeColor = color,
      Margin    = new Padding(0),
      Anchor    = AnchorStyles.Left,
   };

   private static PictureBox MakeGlyph(Bitmap? bitmap, Padding margin) => new()
   {
      Image    = bitmap,
      SizeMode = PictureBoxSizeMode.AutoSize,
      Margin   = margin,
      Anchor   = AnchorStyles.Left,
   };

   // ------------------------------------------------------------------ modo

   private Control BuildModeGroup()
   {
      _cbCopilot.Text      = "";
      _cbCopilot.AutoSize  = true;
      _cbCopilot.Anchor    = AnchorStyles.Left;
      _cbCopilot.Margin    = new Padding(0, 0, LogicalToDeviceUnits(6), 0);
      _cbCopilot.Checked   = true;
      _cbCopilot.ForeColor = SystemColors.ControlText;
      _cbCopilot.CheckedChanged += (_, _) => UpdateEnabledState();

      var rows = new TableLayoutPanel
      {
         ColumnCount  = 3,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      // Linha 1: o interruptor do copiloto. Linha 2: quantos companheiros. As
      // duas eram exclusivas ate a v2 e agora se combinam livremente -- e o
      // -copilot da engine que separa uma coisa da outra.
      rows.Controls.Add(_cbCopilot, 0, 0);
      rows.Controls.Add(MakeGlyph(_icoOne, new Padding(0, 0, LogicalToDeviceUnits(10), 0)), 1, 0);
      rows.Controls.Add(BuildModeText(Strings.ModeCopilotName, Strings.ModeCopilotDesc,
                                      () => _cbCopilot.Checked = !_cbCopilot.Checked, null), 2, 0);

      rows.Controls.Add(MakeGlyph(_icoTwo, new Padding(0, LogicalToDeviceUnits(12), LogicalToDeviceUnits(10), 0)), 1, 1);
      rows.Controls.Add(BuildModeText(Strings.CompanionsName, Strings.CompanionsDesc,
                                      null, BuildBotsRow()), 2, 1);

      var rule = new Panel
      {
         Dock      = DockStyle.Fill,
         Width     = 1,
         BackColor = CardBorderColor,
         Margin    = new Padding(LogicalToDeviceUnits(16), LogicalToDeviceUnits(2),
                                 LogicalToDeviceUnits(16), LogicalToDeviceUnits(2)),
      };

      var body = new TableLayoutPanel
      {
         ColumnCount  = 3,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(330)));
      body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      // Centrado na coluna elastica: a sobra do cartao vira folga dos dois lados
      // em vez de uma faixa vazia so a direita. As duas linhas continuam alinhadas
      // entre si porque quem centraliza e o bloco inteiro, nao cada linha.
      rows.Anchor = AnchorStyles.None;
      body.Controls.Add(rows,           0, 0);
      body.Controls.Add(rule,           1, 0);
      body.Controls.Add(BuildInfoBox(), 2, 0);

      return MakeSection(Strings.GroupMode, body, fill: false);
   }

   /// <summary>
   /// Rotulo em negrito, descricao normal e, no Coop, os controles de bot na mesma
   /// linha. Clicar em qualquer parte do texto marca o radio, que fica sem texto
   /// proprio; o mnemonico vive no rotulo e cai no radio pela ordem de tabulacao.
   /// </summary>
   private Control BuildModeText(string name, string description, Action? onClick, Control? extra)
   {
      Label title = MakeText(name, _modeNameFont, SystemColors.ControlText);
      title.UseMnemonic = true;
      title.Margin      = new Padding(0, 0, LogicalToDeviceUnits(7), 0);

      Label desc = MakeText(description, _modeDescFont, SystemColors.ControlText);
      desc.Margin = new Padding(0, 0, LogicalToDeviceUnits(14), 0);

      if (onClick is not null)
      {
         foreach (Label label in new[] { title, desc })
            label.Click += (_, _) => onClick();
      }

      var line = new TableLayoutPanel
      {
         ColumnCount  = extra is null ? 3 : 4,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4)),
      };
      line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      if (extra is not null)
         line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      line.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      line.Controls.Add(title, 0, 0);
      line.Controls.Add(desc,  1, 0);
      if (extra is not null)
         line.Controls.Add(extra, 2, 0);
      return line;
   }

   /// <summary>Slider com a regua 1..4 embaixo, o spinner do valor e a palavra "bots".</summary>
   private Control BuildBotsRow()
   {
      _tbBots.Minimum       = 0;
      _tbBots.Maximum       = MaxCompanions;
      _tbBots.TickFrequency = 1;
      _tbBots.SmallChange   = 1;
      _tbBots.LargeChange   = 1;
      _tbBots.TickStyle     = TickStyle.BottomRight;
      _tbBots.AutoSize      = false;
      _tbBots.Dock          = DockStyle.Fill;
      _tbBots.Margin        = new Padding(0);
      _tbBots.Value         = 3;

      var scale = new TableLayoutPanel
      {
         ColumnCount = MaxCompanions + 1,
         RowCount    = 1,
         Dock        = DockStyle.Fill,
         Margin      = new Padding(0),
      };
      scale.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      for (int i = 0; i <= MaxCompanions; i++)
      {
         scale.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / (MaxCompanions + 1)));
         _botTicks[i] = new Label
         {
            Text      = i.ToString(),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Font      = _smallFont,
            ForeColor = SystemColors.GrayText,
            Margin    = new Padding(0),
         };
         scale.Controls.Add(_botTicks[i], i, 0);
      }

      var slider = new TableLayoutPanel
      {
         ColumnCount = 1,
         RowCount    = 2,
         Width       = LogicalToDeviceUnits(160),
         Height      = LogicalToDeviceUnits(52),
         Margin      = new Padding(0, 0, LogicalToDeviceUnits(10), 0),
         Anchor      = AnchorStyles.Left,
      };
      slider.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      slider.RowStyles.Add(new RowStyle(SizeType.Absolute, LogicalToDeviceUnits(34)));
      slider.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      slider.Controls.Add(_tbBots, 0, 0);
      slider.Controls.Add(scale,   0, 1);

      _nudBots.Minimum   = 0;
      _nudBots.Maximum   = MaxCompanions;
      _nudBots.Value     = _tbBots.Value;
      _nudBots.Width     = LogicalToDeviceUnits(56);
      _nudBots.TextAlign = HorizontalAlignment.Center;
      _nudBots.Anchor    = AnchorStyles.Left;
      _nudBots.Margin    = new Padding(0, 0, LogicalToDeviceUnits(6), 0);

      // Fonte unica de verdade e o slider; o spinner so espelha. A trava evita que
      // um ValueChanged chame o outro em cascata.
      _tbBots.ValueChanged += (_, _) =>
      {
         if (_syncingBots)
            return;
         _syncingBots = true;
         _nudBots.Value = _tbBots.Value;
         _syncingBots = false;
         UpdateBotHint();
      };
      _nudBots.ValueChanged += (_, _) =>
      {
         if (_syncingBots)
            return;
         _syncingBots = true;
         _tbBots.Value = (int)_nudBots.Value;
         _syncingBots = false;
         UpdateBotHint();
      };

      Label word = MakeText(Strings.BotsWord, _modeDescFont, SystemColors.ControlText);
      _tbBots.EnabledChanged += (_, _) =>
      {
         _nudBots.Enabled = _tbBots.Enabled;
         word.Enabled     = _tbBots.Enabled;
         foreach (Label tick in _botTicks)
            tick.Enabled = _tbBots.Enabled;
      };

      var row = new TableLayoutPanel
      {
         ColumnCount  = 3,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
         Anchor       = AnchorStyles.Left,
      };
      for (int i = 0; i < 3; i++)
         row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      row.Controls.Add(slider,   0, 0);
      row.Controls.Add(_nudBots, 1, 0);
      row.Controls.Add(word,     2, 0);
      return row;
   }

   /// <summary>Caixa azul de dica, na direita do painel de modo.</summary>
   private Control BuildInfoBox()
   {
      foreach (Label label in new[] { _lblCopilotHint, _lblBotHint })
      {
         label.AutoSize  = true;
         label.Dock      = DockStyle.Fill;
         label.ForeColor = SystemColors.ControlText;
         label.Margin    = new Padding(0);
      }
      _lblCopilotHint.Text = Strings.CopilotHint;

      var text = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 1,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
         BackColor    = InfoFillColor,
      };
      text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      text.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      text.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      text.Controls.Add(_lblCopilotHint, 0, 0);
      text.Controls.Add(_lblBotHint,     0, 1);

      var box = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 1,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Padding      = new Padding(LogicalToDeviceUnits(36), LogicalToDeviceUnits(10),
                                    LogicalToDeviceUnits(12), LogicalToDeviceUnits(10)),
         Margin       = new Padding(0),
      };
      box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      box.Controls.Add(text, 0, 0);
      box.Paint += OnPaintInfoBox;

      // Sem teto de largura o painel mede o texto como uma linha so e fecha curto.
      box.SizeChanged += (_, _) =>
      {
         int usable = box.ClientSize.Width - box.Padding.Horizontal;
         if (usable <= 0)
            return;

         var cap = new Size(usable, 0);
         if (_lblCopilotHint.MaximumSize != cap)
            _lblCopilotHint.MaximumSize = cap;
         if (_lblBotHint.MaximumSize != cap)
            _lblBotHint.MaximumSize = cap;
      };
      return box;
   }

   // ---------------------------------------------------------------- extras

   private Control BuildExtrasGroup()
   {
      foreach (CheckBox check in new[] { _cbPip, _cbWeapons, _cbJump, _cbFriendly })
      {
         check.Text      = "";
         check.AutoSize  = true;
         check.Anchor    = AnchorStyles.Left;
         check.Margin    = new Padding(0, 0, LogicalToDeviceUnits(6), 0);
         check.ForeColor = SystemColors.ControlText;
      }

      Control pip     = MakeOptionCard(_cbPip,      _icoPip,    Strings.PipOptionShort,     Strings.PipOptionHint,    null);
      Control weapons = MakeOptionCard(_cbWeapons,  _icoWeapon, Strings.WeaponsOptionShort, Strings.WeaponsHint,      null);
      Control camera  = MakeOptionCard(null,        _icoCamera, Strings.CameraCardTitle,    null,                     BuildFollowRow(), _lblFollow);
      Control jump    = MakeOptionCard(_cbJump,     _icoJump,   Strings.JumpOptionShort,    Strings.JumpHint,         null);
      Control fire    = MakeOptionCard(_cbFriendly, _icoFire,   Strings.FriendlyFireOption, Strings.FriendlyFireHint, null);

      var grid = new TableLayoutPanel
      {
         ColumnCount  = 2,
         RowCount     = 3,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Dock         = DockStyle.Fill,
         Margin       = new Padding(0),
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
      for (int i = 0; i < 3; i++)
         grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      _extraCards.AddRange([pip, weapons, camera, jump, fire]);

      grid.Controls.Add(pip,     0, 0);
      grid.Controls.Add(weapons, 1, 0);
      grid.Controls.Add(camera,  0, 1);
      grid.Controls.Add(jump,    1, 1);
      grid.Controls.Add(fire,    0, 2);
      grid.SetColumnSpan(fire, 2);

      // Sobrou uma celula: o cartao de fogo amigo fica com a largura de uma coluna
      // e centrado nas duas, em vez de meia grade vazia.
      fire.Anchor = AnchorStyles.None;
      grid.SizeChanged += (_, _) =>
      {
         int half = grid.ClientSize.Width / 2;
         if (half > 0 && fire.Width != half)
            fire.Width = half;
      };

      return MakeSection(Strings.GroupExtras, grid, fill: false, pad: 8);
   }

   /// <summary>
   /// Deixa todos os cartoes de Extras com a altura do mais alto. Sem isto o da
   /// camera fica um degrau mais baixo que os vizinhos, porque tem uma linha de
   /// texto a menos: o AutoSize mede cada cartao sozinho e nao sabe dos outros.
   /// Roda depois do Load, quando o texto ja foi medido na fonte e na escala
   /// reais -- medir antes daria a altura do layout de projeto, nao a da tela.
   /// </summary>
   private void EqualizeExtraCards()
   {
      int tallest = 0;
      foreach (Control card in _extraCards)
         tallest = Math.Max(tallest, card.PreferredSize.Height);

      if (tallest <= 0)
         return;

      foreach (Control card in _extraCards)
         card.MinimumSize = new Size(card.MinimumSize.Width, tallest);
   }

   /// <summary>
   /// Um cartao de opcao: caixa de marcar, icone, titulo em negrito e, embaixo, a
   /// descricao em cinza ou um controle (o caso da camera).
   /// </summary>
   private Control MakeOptionCard(CheckBox? check, Bitmap? glyph, string title, string? description,
                                  Control? control, Label? captionField = null)
   {
      Label caption = captionField ?? MakeText(title, _titleFont, SystemColors.ControlText);
      caption.Text      = title;
      caption.AutoSize  = true;
      caption.Font      = _titleFont;
      caption.ForeColor = SystemColors.ControlText;
      caption.Anchor    = AnchorStyles.Left;
      caption.Margin    = new Padding(0, 0, LogicalToDeviceUnits(10), LogicalToDeviceUnits(2));
      if (check is not null)
         caption.Click += (_, _) => check.Checked = !check.Checked;

      var stack = new TableLayoutPanel
      {
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         // Sem Top nem Bottom: o WinForms centra na vertical o que so esta preso
         // pelos lados, e e assim que o texto fica no meio do cartao esticado.
         Anchor       = AnchorStyles.Left | AnchorStyles.Right,
         Margin       = new Padding(0),
      };

      if (control is not null)
      {
         // Cartao com controle (a camera): rotulo a esquerda, controle a direita.
         stack.ColumnCount = 2;
         stack.RowCount    = 1;
         stack.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
         stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
         stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
         control.Margin = new Padding(0);
         control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
         stack.Controls.Add(caption, 0, 0);
         stack.Controls.Add(control, 1, 0);
      }
      else
      {
         Label below = MakeText(description ?? "", _smallFont, SystemColors.GrayText);
         below.Margin = new Padding(0);
         stack.ColumnCount = 1;
         stack.RowCount    = 2;
         stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
         stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
         stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
         stack.Controls.Add(caption, 0, 0);
         stack.Controls.Add(below,   0, 1);
      }

      var inner = new TableLayoutPanel
      {
         ColumnCount  = 3,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      inner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      // Linha elastica em vez de AutoSize: e ela que sobra quando o cartao e
      // esticado para a altura comum, e e nela que o conteudo se centra.
      inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

      if (check is not null)
         inner.Controls.Add(check, 0, 0);

      PictureBox icon = MakeGlyph(glyph, new Padding(0, 0, LogicalToDeviceUnits(10), 0));
      icon.Anchor = AnchorStyles.None;   // centrado na altura do cartao
      inner.Controls.Add(icon, 1, 0);
      inner.Controls.Add(stack, 2, 0);

      TableLayoutPanel card = MakeCard(inner, 10, fill: false);
      card.Margin = new Padding(LogicalToDeviceUnits(4));
      return card;
   }

   /// <summary>Quem o quadrinho persegue. So faz sentido com o PIP ligado.</summary>
   /// <summary>
   /// So a combo: o rotulo "A camera segue:" e o proprio titulo do cartao, e e o
   /// mesmo _lblFollow que o UpdateEnabledState acinzenta junto com ela.
   /// </summary>
   private Control BuildFollowRow()
   {
      _cbFollow.DropDownStyle = ComboBoxStyle.DropDownList;
      _cbFollow.Items.Add(Strings.FollowKills);
      _cbFollow.Items.Add(Strings.FollowExit);
      return _cbFollow;
   }

   private static GroupBox MakeGroup(string text) => new()
   {
      Text      = text,
      Dock      = DockStyle.Fill,
      Margin    = new Padding(0, 0, 0, 12),
      ForeColor = AccentColor,
   };

   /// <summary>
   /// Altura do grupo tirada do conteudo, sem AutoSize. O AutoSize do GroupBox erra
   /// a reserva da faixa do titulo e o filho acaba pintado por cima da legenda.
   /// </summary>
   private static void FitGroupToContent(GroupBox box, Control content)
   {
      void Apply()
      {
         int chrome = box.Height - box.ClientSize.Height;
         int wanted = content.Top + content.Height + box.Padding.Bottom + chrome;
         if (wanted > 0 && box.Height != wanted)
            box.Height = wanted;
      }

      content.SizeChanged     += (_, _) => Apply();
      content.LocationChanged += (_, _) => Apply();
      box.HandleCreated       += (_, _) => Apply();
      Apply();
   }

   /// <summary>Retangulo de cantos arredondados; o GDI+ nao tem primitiva para isso.</summary>
   private static GraphicsPath RoundedRect(Rectangle r, int radius)
   {
      int d = radius * 2;
      var path = new GraphicsPath();
      path.AddArc(r.Left,      r.Top,        d, d, 180, 90);
      path.AddArc(r.Right - d, r.Top,        d, d, 270, 90);
      path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
      path.AddArc(r.Left,      r.Bottom - d, d, d,  90, 90);
      path.CloseFigure();
      return path;
   }

   /// <summary>Fundo arredondado da caixa de aviso mais o circulo de informacao.</summary>
   private void OnPaintInfoBox(object? sender, PaintEventArgs e)
   {
      if (sender is not Control box)
         return;

      Graphics g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;

      var area = new Rectangle(0, 0, box.Width - 1, box.Height - 1);
      using (GraphicsPath path = RoundedRect(area, LogicalToDeviceUnits(6)))
      using (var fill = new SolidBrush(InfoFillColor))
      using (var edge = new Pen(InfoBorderColor))
      {
         g.FillPath(fill, path);
         g.DrawPath(edge, path);
      }

      int d  = LogicalToDeviceUnits(17);
      int cx = LogicalToDeviceUnits(11);
      int cy = area.Top + (area.Height - d) / 2;
      var disc = new Rectangle(cx, cy, d, d);

      using (var accent = new SolidBrush(AccentColor))
         g.FillEllipse(accent, disc);

      using var glyph = new Font(Font.FontFamily, Font.Size * 0.95f, FontStyle.Bold);
      using var format = new StringFormat
      {
         Alignment     = StringAlignment.Center,
         LineAlignment = StringAlignment.Center,
      };
      using var ink = new SolidBrush(InfoFillColor);
      g.DrawString("i", glyph, ink, disc, format);
   }

   // ------------------------------------------------------------------ iwad

   private Control BuildIwadGroup()
   {
      _lbIwads.Dock           = DockStyle.Fill;
      _lbIwads.IntegralHeight = false;
      _lbIwads.BorderStyle    = BorderStyle.FixedSingle;
      _lbIwads.Margin         = new Padding(0, 0, LogicalToDeviceUnits(12), 0);
      _lbIwads.ForeColor      = SystemColors.WindowText;
      _lbIwads.DrawMode       = DrawMode.OwnerDrawFixed;
      _lbIwads.ItemHeight     = LogicalToDeviceUnits(34);
      _lbIwads.DrawItem      += OnDrawIwadItem;
      _lbIwads.SelectedIndexChanged += (_, _) => { UpdateEnabledState(); UpdateIwadDetails(); };
      _lbIwads.DoubleClick += (_, _) => { if (_btnPlay.Enabled) OnPlay(this, EventArgs.Empty); };

      var browse = StyleButton(new Button(), Strings.Browse, _icoFolder, new Padding(0, 0, 0, Gutter));
      browse.Click += OnBrowseIwad;

      StyleButton(_btnDetect, Strings.Detect, _icoScan, new Padding(0));
      _btnDetect.Click += OnDetectWads;

      var side = new FlowLayoutPanel
      {
         FlowDirection = FlowDirection.TopDown,
         WrapContents  = false,
         AutoSize      = true,
         AutoSizeMode  = AutoSizeMode.GrowAndShrink,
         // Centrados na altura da lista, nao grudados no topo.
         Anchor        = AnchorStyles.Left | AnchorStyles.Right,
         Margin        = new Padding(0, 0, LogicalToDeviceUnits(12), 0),
      };
      side.Controls.Add(browse);
      side.Controls.Add(_btnDetect);

      var layout = new TableLayoutPanel
      {
         Dock        = DockStyle.Fill,
         ColumnCount = 3,
         RowCount    = 1,
         Margin      = new Padding(0),
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(ButtonWidth + 12)));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(250)));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      layout.Controls.Add(_lbIwads,           0, 0);
      layout.Controls.Add(side,               1, 0);
      layout.Controls.Add(BuildDetailPanel(), 2, 0);

      return MakeSection(Strings.GroupIwad, layout, fill: true);
   }

   /// <summary>Ficha do IWAD selecionado: logo grande, nome e quatro linhas de dado.</summary>
   private Control BuildDetailPanel()
   {
      _detIcon.SizeMode = PictureBoxSizeMode.CenterImage;
      _detIcon.Size     = new Size(LogicalToDeviceUnits(110), LogicalToDeviceUnits(60));
      _detIcon.Anchor   = AnchorStyles.None;
      _detIcon.Margin   = new Padding(0, 0, 0, LogicalToDeviceUnits(6));

      _detName.AutoSize  = true;
      _detName.Font      = _nameFont;
      _detName.ForeColor = AccentColor;
      _detName.Anchor    = AnchorStyles.None;
      _detName.Margin    = new Padding(0, 0, 0, LogicalToDeviceUnits(10));

      // Ancorado pelos lados em vez de Dock=Fill: a ficha e mais baixa que a
      // lista ao lado, e assim ela fica no meio da altura do cartao em vez de
      // pendurada no topo.
      var panel = new TableLayoutPanel
      {
         Anchor      = AnchorStyles.Left | AnchorStyles.Right,
         AutoSize    = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         ColumnCount = 2,
         RowCount    = 7,
         Margin      = new Padding(0),
         BackColor   = CardColor,
      };
      panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      panel.Controls.Add(_detIcon, 0, 0);
      panel.SetColumnSpan(_detIcon, 2);
      panel.Controls.Add(_detName, 0, 1);
      panel.SetColumnSpan(_detName, 2);

      var rows = new (string Key, Label Value)[]
      {
         (Strings.DetailFile, _detFile),
         (Strings.DetailKind, _detKind),
         (Strings.DetailSize, _detSize),
         (Strings.DetailMaps, _detMaps),
      };

      for (int i = 0; i < rows.Length; i++)
      {
         Label key = MakeText(rows[i].Key, _boldFont, SystemColors.ControlText);
         key.Margin = new Padding(0, 0, LogicalToDeviceUnits(6), LogicalToDeviceUnits(4));

         Label value = rows[i].Value;
         value.AutoSize     = true;
         value.MaximumSize  = new Size(LogicalToDeviceUnits(150), 0);
         value.ForeColor    = SystemColors.GrayText;
         value.Anchor       = AnchorStyles.Left;
         value.Margin       = new Padding(0, 0, 0, LogicalToDeviceUnits(4));

         panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
         panel.Controls.Add(key,   0, i + 2);
         panel.Controls.Add(value, 1, i + 2);
      }

      panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      return panel;
   }

   /// <summary>Uma linha da lista: logo do jogo, faixa de selecao e o nome.</summary>
   private void OnDrawIwadItem(object? sender, DrawItemEventArgs e)
   {
      if (e.Index < 0)
      {
         e.DrawBackground();
         e.DrawFocusRectangle();
         return;
      }

      bool  picked = (e.State & DrawItemState.Selected) != 0;
      Color back   = picked ? Blend(SystemColors.Window, SystemColors.Highlight, 0.14f) : _lbIwads.BackColor;
      Color fore   = picked ? AccentColor : _lbIwads.ForeColor;

      using (var brush = new SolidBrush(back))
         e.Graphics.FillRectangle(brush, e.Bounds);

      if (picked)
      {
         using var bar = new SolidBrush(AccentColor);
         e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, LogicalToDeviceUnits(3), e.Bounds.Height);
      }

      int pad  = LogicalToDeviceUnits(12);
      int icon = LogicalToDeviceUnits(26);

      Bitmap? logo = null;
      if (_lbIwads.Items[e.Index] is IwadEntry entry)
         logo = IwadIcon.Load(entry.Path, icon) ?? _icoWad;

      if (logo is not null)
      {
         int ly = e.Bounds.Top + (e.Bounds.Height - logo.Height) / 2;
         e.Graphics.DrawImage(logo, e.Bounds.Left + pad, ly, logo.Width, logo.Height);
      }

      var text = new Rectangle(
         e.Bounds.Left  + pad + icon + LogicalToDeviceUnits(10), e.Bounds.Top,
         e.Bounds.Width - pad - icon - LogicalToDeviceUnits(18), e.Bounds.Height);

      TextRenderer.DrawText(e.Graphics, _lbIwads.Items[e.Index].ToString(),
         picked ? (_titleFont ?? Font) : (e.Font ?? Font), text, fore,
         TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
         | TextFormatFlags.NoPrefix);
   }

   /// <summary>Preenche a ficha com o IWAD selecionado agora.</summary>
   private void UpdateIwadDetails()
   {
      if (_lbIwads.SelectedItem is not IwadEntry entry || !File.Exists(entry.Path))
      {
         _detIcon.Image = null;
         _detName.Text  = "";
         _detFile.Text  = "";
         _detKind.Text  = "";
         _detSize.Text  = "";
         _detMaps.Text  = "";
         return;
      }

      _detIcon.Image = IwadIcon.Load(entry.Path, LogicalToDeviceUnits(100)) ?? _icoWad;
      _detName.Text  = entry.Label;
      _detFile.Text  = Path.GetFileName(entry.Path);
      _detKind.Text  = Strings.GroupIwad;
      _detSize.Text  = $"{new FileInfo(entry.Path).Length / (1024.0 * 1024.0):F1} MB";

      // Inspect le o arquivo inteiro: guarda o resultado por caminho.
      if (!_mapCounts.TryGetValue(entry.Path, out int maps))
      {
         maps = WadValidator.Inspect(entry.Path).MapCount;
         _mapCounts[entry.Path] = maps;
      }
      _detMaps.Text = maps.ToString();
   }

   // ------------------------------------------------------------------ pwad

   private Control BuildPwadGroup()
   {
      _cbPwad.DropDownStyle = ComboBoxStyle.DropDownList;
      _cbPwad.Anchor        = AnchorStyles.Left | AnchorStyles.Right;
      _cbPwad.Margin        = new Padding(0, 0, LogicalToDeviceUnits(12), 0);
      _cbPwad.ForeColor     = SystemColors.WindowText;
      _cbPwad.MaxDropDownItems = 20;
      _cbPwad.SelectedIndexChanged += (_, _) => ReportPwadCompatibility();

      var browse = StyleButton(new Button(), Strings.BrowseAlt, _icoFolder, new Padding(0));
      browse.Anchor = AnchorStyles.Left;
      browse.Click += OnBrowsePwad;

      var layout = new TableLayoutPanel
      {
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.Controls.Add(_cbPwad, 0, 0);
      layout.Controls.Add(browse,  1, 0);

      return MakeSection(Strings.GroupPwad, layout, fill: false);
   }

   /// <summary>Uma barra por unidade de disco, visivel so durante a deteccao.</summary>
   private Control BuildProgressGroup()
   {
      // Mesmo motivo do grupo PWAD: com Dock.Fill o painel cobriria o titulo do grupo.
      _progressRows.Dock         = DockStyle.Top;
      _progressRows.ColumnCount  = 3;
      _progressRows.AutoSize     = true;
      _progressRows.AutoSizeMode = AutoSizeMode.GrowAndShrink;
      _progressRows.Padding      = new Padding(12, 8, 12, 12);
      _progressRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      _progressRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      _progressRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

      _grpProgress.Text         = Strings.GroupProgress;
      _grpProgress.ForeColor    = AccentColor;
      _grpProgress.Dock         = DockStyle.Top;
      _grpProgress.Margin       = new Padding(0, 0, 0, 12);
      _grpProgress.Visible      = false;
      _grpProgress.Controls.Add(_progressRows);
      FitGroupToContent(_grpProgress, _progressRows);

      return _grpProgress;
   }

   /// <summary>Monta as linhas de progresso, uma para cada unidade que sera lida.</summary>
   private void PrepareProgressRows()
   {
      _progressRows.SuspendLayout();
      _progressRows.Controls.Clear();
      _progressRows.RowStyles.Clear();
      _volumeRows.Clear();

      List<DriveInfo> volumes = UsnScanner.NtfsVolumes();
      _progressRows.RowCount = Math.Max(volumes.Count, 1);

      for (int i = 0; i < volumes.Count; i++)
      {
         string letter = volumes[i].Name.TrimEnd(Path.DirectorySeparatorChar);

         var name = new Label
         {
            Text      = letter,
            AutoSize  = true,
            Anchor    = AnchorStyles.Left,
            ForeColor = SystemColors.ControlText,
            Margin    = new Padding(0, 4, 8, 4),
         };

         var bar = new ProgressBar
         {
            Minimum = 0,
            Maximum = 1000,
            Value   = 0,
            Height  = 16,
            Dock    = DockStyle.Fill,
            Margin  = new Padding(0, 4, 8, 4),
         };

         var info = new Label
         {
            Text      = Strings.Waiting,
            AutoSize  = true,
            Anchor    = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin    = new Padding(0, 4, 0, 4),
         };

         _progressRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
         _progressRows.Controls.Add(name, 0, i);
         _progressRows.Controls.Add(bar,  1, i);
         _progressRows.Controls.Add(info, 2, i);

         _volumeRows[letter] = (bar, info);
      }

      _progressRows.ResumeLayout();
      _grpProgress.Visible = true;
   }

   private void UpdateProgressRow(UsnProgress update)
   {
      if (!_volumeRows.TryGetValue(update.Volume, out (ProgressBar Bar, Label Info) row))
         return;

      row.Bar.Value = (int)Math.Clamp(update.Fraction * 1000, 0, 1000);
      row.Info.Text = update.Note is not null
         ? update.Note
         : update.Done
            ? Strings.VolumeDone(update.Found)
            : Strings.VolumeBusy(update.Fraction, update.Found);
   }

   private Control BuildFooter()
   {
      StyleButton(_btnPlay, Strings.Play, _icoPlayWhite, new Padding(Gutter, 0, 0, 0));
      // Botao primario: azul cheio com texto branco, sem borda de sistema.
      _btnPlay.FlatStyle = FlatStyle.Flat;
      _btnPlay.FlatAppearance.BorderSize = 0;
      _btnPlay.FlatAppearance.MouseOverBackColor = Blend(AccentColor, Color.White, 0.14f);
      _btnPlay.FlatAppearance.MouseDownBackColor = Blend(AccentColor, Color.Black, 0.14f);
      _btnPlay.BackColor = AccentColor;
      _btnPlay.ForeColor = Color.White;
      _btnPlay.TextAlign = ContentAlignment.MiddleCenter;
      // Botao Flat desabilitado mantem o BackColor: sem isto o azul continuaria
      // cheio com a lista vazia, prometendo um clique que nao faz nada.
      _btnPlay.EnabledChanged += (_, _) =>
      {
         _btnPlay.BackColor = _btnPlay.Enabled
            ? AccentColor
            : Blend(AccentColor, SystemColors.Control, 0.62f);
      };
      _btnPlay.Click += OnPlay;

      var quit = StyleButton(new Button(), Strings.Quit, _icoExit, new Padding(0));
      quit.Click += (_, _) => Close();

      _lblStatus.AutoSize  = false;
      _lblStatus.Dock      = DockStyle.Fill;
      _lblStatus.ForeColor = SystemColors.GrayText;
      _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
      _lblStatus.AutoEllipsis = true;
      _lblStatus.Margin    = new Padding(0, 0, Gutter, 0);

      var buttons = new FlowLayoutPanel
      {
         FlowDirection = FlowDirection.RightToLeft,
         WrapContents  = false,   // sem isso o "Sair" quebra linha e some
         AutoSize      = true,
         AutoSizeMode  = AutoSizeMode.GrowAndShrink,
         Margin        = new Padding(0),
      };
      buttons.Controls.Add(_btnPlay);
      buttons.Controls.Add(quit);

      // O rodape mora fora dos cartoes; o recuo iguala a borda de conteudo deles.
      var row = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      // Tres colunas com as pontas do mesmo tamanho: o par de botoes fica no
      // centro da janela, e a mensagem de estado ocupa a folga da esquerda.
      row.ColumnCount = 3;
      row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
      row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
      row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      row.Controls.Add(_lblStatus, 0, 0);
      row.Controls.Add(buttons,    1, 0);

      // Linha de 1px atravessando a largura util: a linha absoluta de 13 da o
      // respiro de 12 abaixo dela sem depender do PreferredSize de um Panel vazio.
      var rule = new Panel
      {
         Dock      = DockStyle.Top,
         Height    = 1,
         Margin    = new Padding(0),
         BackColor = SeparatorColor,
      };

      var footer = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 1,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 13F));
      footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      footer.Controls.Add(rule, 0, 0);
      footer.Controls.Add(row,  0, 1);

      AcceptButton = _btnPlay;
      return footer;
   }

   /// <summary>O icone do proprio launcher, que ja e o do Eternity herdado do jogo.</summary>
   private Icon? TryGetOwnIcon()
   {
      foreach (string? candidate in new[] { Environment.ProcessPath, Path.Combine(_gameDir, GameExeName) })
      {
         if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
            continue;

         try
         {
            return Icon.ExtractAssociatedIcon(candidate);
         }
         catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
         {
            // tenta o proximo
         }
      }
      return null;
   }

   // -------------------------------------------------------------- estado

   private void LoadState()
   {
      // launcher.json sem a chave Copilot vem da v2, quando os modos eram
      // exclusivos: la, Coop significava exatamente "sem copiloto".
      _cbCopilot.Checked = _settings.Copilot ?? _settings.Mode != PlayMode.Coop;
      _tbBots.Value      = Math.Clamp(_settings.BotCount, 0, MaxCompanions);
      _cbWeapons.Checked = _settings.WeaponsDisappear;
      _cbJump.Checked    = GameConfig.JumpEnabled(_gameDir);
      _cbFollow.SelectedIndex = Math.Clamp(GameConfig.PipFollow(_gameDir), 0, 1);
      _cbFriendly.Checked = GameConfig.FriendlyFire(_gameDir);

      // So oferece o PIP se o executavel com o patch estiver do lado.
      bool hasPip = File.Exists(Path.Combine(_gameDir, PipExeName));
      _cbPip.Enabled = hasPip;
      _cbPip.Checked = hasPip && _settings.Pip;
      _cbPip.CheckedChanged += (_, _) => UpdateEnabledState();
      if (!hasPip)
         _tips.SetToolTip(_cbPip, Strings.PipMissing);

      ReloadIwads(_settings.LastIwadPath);
      ReloadPwads(_settings.SelectedPwad);
   }

   private void ReloadIwads(string? selectPath)
   {
      _lbIwads.BeginUpdate();
      _lbIwads.Items.Clear();

      foreach (IwadEntry entry in IwadCatalog.Build(_gameDir, _settings.IwadFolders, _settings.ExtraIwads))
         _lbIwads.Items.Add(entry);

      _lbIwads.EndUpdate();
      SelectIwad(selectPath);
   }

   private void ReloadPwads(string? selectPath)
   {
      _cbPwad.BeginUpdate();
      _cbPwad.Items.Clear();

      foreach (PwadEntry entry in PwadCatalog.Build(_gameDir, _settings.PwadFolders,
                                                   _settings.ExtraPwads, _settings.ManualPwads))
         _cbPwad.Items.Add(entry);

      _cbPwad.EndUpdate();
      SelectPwad(selectPath);
   }

   private bool HasIwad(string path) =>
      _lbIwads.Items.Cast<IwadEntry>()
         .Any(e => string.Equals(e.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

   private void SelectIwad(string? path)
   {
      if (!string.IsNullOrEmpty(path))
      {
         for (int i = 0; i < _lbIwads.Items.Count; i++)
         {
            if (_lbIwads.Items[i] is IwadEntry e &&
                string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase))
            {
               _lbIwads.SelectedIndex = i;
               return;
            }
         }
      }

      if (_lbIwads.Items.Count > 0)
         _lbIwads.SelectedIndex = 0;
   }

   private void SelectPwad(string? path)
   {
      if (!string.IsNullOrEmpty(path))
      {
         for (int i = 0; i < _cbPwad.Items.Count; i++)
         {
            if (_cbPwad.Items[i] is PwadEntry e &&
                string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase))
            {
               _cbPwad.SelectedIndex = i;
               return;
            }
         }
      }

      if (_cbPwad.Items.Count > 0)
         _cbPwad.SelectedIndex = 0; // (nenhum)
   }

   private void SaveState()
   {
      _settings.Copilot      = _cbCopilot.Checked;
      _settings.Mode         = _cbCopilot.Checked ? PlayMode.Copilot : PlayMode.Coop;
      _settings.BotCount     = _tbBots.Value;
      _settings.Pip          = _cbPip.Checked;
      _settings.WeaponsDisappear = _cbWeapons.Checked;
      _settings.LastIwadPath = (_lbIwads.SelectedItem as IwadEntry)?.Path;
      _settings.SelectedPwad = (_cbPwad.SelectedItem as PwadEntry)?.Path;
      _settings.Save();
   }

   protected override void OnFormClosing(FormClosingEventArgs e)
   {
      SaveState();
      base.OnFormClosing(e);
   }

   private void UpdateEnabledState()
   {
      _btnPlay.Enabled = _lbIwads.SelectedItem is IwadEntry && !_scanning;
      // O numero de companheiros nao depende mais do copiloto: as duas escolhas
      // sao independentes, entao o slider fica sempre ativo.
      _tbBots.Enabled      = true;
      _cbFollow.Enabled    = _cbPip.Enabled && _cbPip.Checked;
      _lblFollow.Enabled   = _cbFollow.Enabled;
      UpdateBotHint();
   }

   /// <summary>
   /// O quarto slot e o seu: com 4 bots a engine liga bots[0].active e o seu
   /// personagem tambem anda sozinho (G_AdjustNetBotSettings, g_game.cpp:3232).
   /// </summary>
   private void UpdateBotHint()
   {
      HighlightBotTick();

      // As duas linhas da caixa contam as duas escolhas, que agora sao
      // independentes: a de cima diz quem dirige o seu personagem, a de baixo
      // quantos companheiros entram.
      _lblCopilotHint.Visible = true;
      _lblCopilotHint.Text = _cbCopilot.Checked ? Strings.CopilotHint : Strings.CopilotOffHint;

      _lblBotHint.Visible = true;
      _lblBotHint.Text = _tbBots.Value == 0
         ? Strings.CompanionsHintNone
         : Strings.CompanionsHint(_tbBots.Value, MaxCompanions);
   }

   /// <summary>O numero corrente da regua em azul negrito, o resto em cinza.</summary>
   private void HighlightBotTick()
   {
      for (int i = 0; i < _botTicks.Length; i++)
      {
         Label tick = _botTicks[i];
         if (tick is null)
            continue;

         bool current = i == _tbBots.Value;
         tick.Font      = current ? (_titleFont ?? Font) : (_smallFont ?? Font);
         tick.ForeColor = current ? AccentColor : SystemColors.GrayText;
      }
   }

   /// <summary>Confere o PWAD escolhido na hora e diz no rodape o que esperar dele.</summary>
   private void ReportPwadCompatibility()
   {
      if (_scanning)
         return;

      if (_cbPwad.SelectedItem is not PwadEntry { Path.Length: > 0 } pwad)
      {
         _lblStatus.Text = "";
         return;
      }

      WadReport report = WadValidator.Inspect(pwad.Path);
      string mark = report.Verdict switch
      {
         WadVerdict.Ok           => Strings.VerdictOk,
         WadVerdict.Partial      => Strings.VerdictPartial,
         _                       => Strings.VerdictIncompatible,
      };

      _lblStatus.Text    = $"{mark}: {report.Summary}";
      _lblStatus.ForeColor = report.Verdict == WadVerdict.Incompatible
         ? Color.FromArgb(192, 32, 32)
         : SystemColors.GrayText;
   }

   /// <summary>
   /// Passa a lista guardada pelos filtros atuais uma unica vez. Roda em segundo
   /// plano porque abre cada arquivo para ver se tem mapa dentro.
   /// </summary>
   private async void PruneStoredPwads()
   {
      if (_settings.FilterVersion >= FilterVersion || _settings.ExtraPwads.Count == 0)
         return;

      List<string> stored = [.. _settings.ExtraPwads];
      _lblStatus.Text = Strings.Pruning;

      List<string> kept = await Task.Run(() => stored
         .Where(File.Exists)
         .Where(p => !PwadCatalog.IsEngineFile(p) && !PwadCatalog.IsNoisePath(p) && PwadCatalog.LooksReadable(p))
         .Where(p => WadValidator.Inspect(p).MapCount > 0)
         .ToList());

      int removed = stored.Count - kept.Count;

      _settings.ExtraPwads   = kept;
      _settings.FilterVersion = FilterVersion;
      _settings.Save();

      string? selected = (_cbPwad.SelectedItem as PwadEntry)?.Path;
      ReloadPwads(selected);
      _lblStatus.Text = Strings.Pruned(removed, _cbPwad.Items.Count - 1);
   }

   /// <summary>Le o journal e libera (ou nao) o botao de detectar.</summary>
   private void RefreshDetectAvailability()
   {
      UsnAvailability status = UsnScanner.CheckAvailability();

      if (status.Available)
      {
         _btnDetect.Enabled = true;
         _tips.SetToolTip(_btnDetect, Strings.DetectTooltip);
         return;
      }

      if (status.NeedsElevation)
      {
         _btnDetect.Enabled = true;
         _btnDetect.Text    = Strings.DetectElevated;
         _tips.SetToolTip(_btnDetect, status.Reason + Strings.ClickToElevate);
         return;
      }

      // Sem journal nao ha atalho, e varredura de disco inteiro levaria horas:
      // melhor desabilitar do que prometer o que nao da para cumprir.
      _btnDetect.Enabled = false;
      _tips.SetToolTip(_btnDetect, status.Reason);
      _lblStatus.Text    = status.Reason;
   }

   // ------------------------------------------------------------- handlers

   private void OnBrowseIwad(object? sender, EventArgs e)
   {
      using var dlg = new OpenFileDialog
      {
         Title            = Strings.ChooseIwad,
         Filter           = Strings.IwadFilter,
         InitialDirectory = FirstExistingFolder(_settings.LastIwadFolder, _gameDir),
      };
      if (dlg.ShowDialog(this) != DialogResult.OK)
         return;

      string full = Path.GetFullPath(dlg.FileName);
      _settings.LastIwadFolder = Path.GetDirectoryName(full);

      if (!_settings.ExtraIwads.Contains(full, StringComparer.OrdinalIgnoreCase))
         _settings.ExtraIwads.Add(full);

      ReloadIwads(full);
   }

   private void OnBrowsePwad(object? sender, EventArgs e)
   {
      using var dlg = new OpenFileDialog
      {
         Title            = Strings.ChoosePwad,
         Filter           = Strings.PwadFilter,
         InitialDirectory = FirstExistingFolder(_settings.LastPwadFolder, DefaultPwadFolder, _gameDir),
      };
      if (dlg.ShowDialog(this) != DialogResult.OK)
         return;

      AddAndSelectPwad(dlg.FileName);
   }

   private void AddAndSelectPwad(string path)
   {
      if (!File.Exists(path))
         return;

      string full = Path.GetFullPath(path);
      _settings.LastPwadFolder = Path.GetDirectoryName(full);

      // Escolha a mao passa por cima de qualquer filtro: a decisao ja foi do usuario.
      if (!_settings.ManualPwads.Contains(full, StringComparer.OrdinalIgnoreCase))
         _settings.ManualPwads.Add(full);

      ReloadPwads(full);
   }

   private static string FirstExistingFolder(params string?[] candidates) =>
      candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && Directory.Exists(c)) ?? "";

   private static void OnDragEnter(object? sender, DragEventArgs e)
   {
      e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
         ? DragDropEffects.Copy
         : DragDropEffects.None;
   }

   /// <summary>Arrastar um arquivo para a janela escolhe ele como PWAD.</summary>
   private void OnDragDrop(object? sender, DragEventArgs e)
   {
      if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
         AddAndSelectPwad(files[0]);
   }


   // ------------------------------------------------- deteccao via journal NTFS

   private async void OnDetectWads(object? sender, EventArgs e)
   {
      if (_scanning)
         return;

      UsnAvailability status = UsnScanner.CheckAvailability();

      if (!status.Available)
      {
         if (!status.NeedsElevation)
         {
            RefreshDetectAvailability();
            MessageBox.Show(this, status.Reason, Strings.DetectTitle,
               MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
         }

         DialogResult answer = MessageBox.Show(this,
            status.Reason + Environment.NewLine + Environment.NewLine +
            Strings.AskElevate,
            Strings.DetectTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

         if (answer == DialogResult.Yes)
            RelaunchElevated();
         return;
      }

      _scanning = true;
      _btnDetect.Enabled = false;
      UpdateEnabledState();
      UseWaitCursor = true;
      PrepareProgressRows();
      _lblStatus.Text = Strings.ReadingVolumes;

      var progress = new Progress<UsnProgress>(UpdateProgressRow);

      try
      {
         var validation = new Progress<string>(text => _lblStatus.Text = text);

         List<(WadHit Hit, WadReport Report)> inspected = await Task.Run(() =>
         {
            List<WadHit> hits = UsnScanner.Scan(progress);
            var results = new List<(WadHit, WadReport)>(hits.Count);

            for (int i = 0; i < hits.Count; i++)
            {
               if (i % 200 == 0)
                  ((IProgress<string>)validation).Report(Strings.Checking(i, hits.Count));

               results.Add((hits[i], WadValidator.Inspect(hits[i].Path)));
            }

            return results;
         });

         ApplyHits(inspected);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
         _lblStatus.Text = "";
         MessageBox.Show(this, Strings.JournalFailed + Environment.NewLine + ex.Message,
            Strings.DetectTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
         UseWaitCursor = false;
         _scanning = false;
         _btnDetect.Enabled = true;
         _grpProgress.Visible = false;
         UpdateEnabledState();
      }
   }

   /// <summary>
   /// Guarda o que passou na validacao e recarrega as duas listas. O que a engine
   /// nao carrega fica de fora: lista longa de arquivo que nao abre e ruido.
   /// </summary>
   private void ApplyHits(List<(WadHit Hit, WadReport Report)> results)
   {
      int newPwads = 0;
      int rejected = 0;
      int partial  = 0;
      int iwads    = 0;
      int noise    = 0;

      var seenNames = new HashSet<string>(
         _settings.ExtraPwads.Select(Path.GetFileName).OfType<string>(),
         StringComparer.OrdinalIgnoreCase);

      foreach ((WadHit hit, WadReport report) in results)
      {
         // IWAD achado no disco nao entra aqui: a lista de IWAD e dos jogos
         // principais, montada das pastas conhecidas, e nao aceita repeticao.
         if (hit.IsIwad)
         {
            iwads++;
            continue;
         }

         // Ruido da varredura: prefab de gerador de mapa, tripa de instalacao,
         // lixeira, e nome que ninguem consegue ler. Nao entra na lista.
         if (PwadCatalog.IsEngineFile(hit.Path) ||
             PwadCatalog.IsNoisePath(hit.Path) ||
             !PwadCatalog.LooksReadable(hit.Path))
         {
            noise++;
            continue;
         }

         // O dropdown e de PWAD DE MAPA: arquivo sem mapa dentro nao serve.
         if (report.MapCount == 0)
         {
            noise++;
            continue;
         }

         if (report.Verdict == WadVerdict.Incompatible)
         {
            rejected++;
            continue;
         }

         if (report.Verdict == WadVerdict.Partial)
            partial++;

         // Mesmo nome de arquivo em outra pasta e a mesma opcao: nao duplica.
         if (!seenNames.Add(Path.GetFileName(hit.Path)))
            continue;

         _settings.ExtraPwads.Add(hit.Path);
         newPwads++;
      }

      _settings.Save();

      string? pwad = (_cbPwad.SelectedItem as PwadEntry)?.Path;
      ReloadPwads(pwad);

      _lblStatus.Text = Strings.ScanStatus(results.Count, newPwads);

      MessageBox.Show(this,
         Strings.ScanSummary(results.Count, iwads, rejected + noise, partial, newPwads),
         Strings.DetectTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
   }

   /// <summary>Reabre o launcher elevado, ja pedindo a varredura.</summary>
   private void RelaunchElevated()
   {
      SaveState();

      string? exe = Environment.ProcessPath;
      if (string.IsNullOrEmpty(exe))
         return;

      try
      {
         Process.Start(new ProcessStartInfo(exe, "--detectar-wads")
         {
            UseShellExecute = true,
            Verb            = "runas",
            WorkingDirectory = _gameDir,
         });
         Close();
      }
      catch (Win32Exception)
      {
         // usuario recusou o UAC
         _lblStatus.Text = Strings.ElevationCancelled;
      }
   }

   // -------------------------------------------------------------- launch

   private async void OnPlay(object? sender, EventArgs e)
   {
      if (_lbIwads.SelectedItem is not IwadEntry iwad)
         return;

      // O autodoom_pip.exe e o executavel do projeto e um superconjunto do
      // original: sem -pip ele se comporta igual. Rodar sempre por ele mantem o
      // -copilot valendo mesmo com o quadrinho desligado -- o AutoDoom.exe de
      // fabrica ignora o parametro em silencio, e a combinacao "copiloto com 1
      // companheiro" sairia errada sem aviso.
      bool hasPipExe = File.Exists(Path.Combine(_gameDir, PipExeName));
      bool usePip    = _cbPip.Checked && hasPipExe;
      string exeName = hasPipExe ? PipExeName : GameExeName;
      string exe = Path.Combine(_gameDir, exeName);
      if (!File.Exists(exe))
      {
         MessageBox.Show(this, Strings.ExeNotFound(GameExeName, _gameDir),
            Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
         return;
      }

      var psi = new ProcessStartInfo(exe)
      {
         UseShellExecute  = false,
         WorkingDirectory = _gameDir,
      };

      psi.ArgumentList.Add("-iwad");
      psi.ArgumentList.Add(iwad.Path);

      if (usePip)
         psi.ArgumentList.Add("-pip");

      // Companheiros e copiloto viajam separados. O -bots so diz quantos
      // jogadores-bot entram; quem decide se o SEU personagem tambem anda
      // sozinho e o -copilot, que existe justamente porque no -bots puro
      // qualquer numero de 1 a 3 desligava o copiloto.
      if (_tbBots.Value > 0)
      {
         psi.ArgumentList.Add("-bots");
         psi.ArgumentList.Add(_tbBots.Value.ToString());
      }

      psi.ArgumentList.Add("-copilot");
      psi.ArgumentList.Add(_cbCopilot.Checked ? "1" : "0");

      // dmflags 0 tira o DM_WEAPONSTAY que o Coop liga por padrao (g_dmflag.h:47),
      // fazendo a arma sumir ao ser pega, como no single player.
      if (_cbWeapons.Checked)
      {
         psi.ArgumentList.Add("-dmflags");
         psi.ArgumentList.Add("0");
      }

      if (_cbPwad.SelectedItem is PwadEntry { Path.Length: > 0 } pwad)
      {
         psi.ArgumentList.Add("-file");
         psi.ArgumentList.Add(pwad.Path);
      }

      // Pulo, camera e fogo amigo moram na config da engine: escrever agora, com
      // o jogo fechado, e o unico momento em que a mudanca sobrevive. O placar
      // deixou de ser opcao e vai sempre ligado.
      GameConfig.SetJump(_gameDir, _cbJump.Checked);
      // Perfil novo nasce com o teclado do projeto; perfil que ja existe so
      // ganha o bind do bot_unstick, sem perder o que o dono escolheu.
      GameConfig.EnsureProjectKeys(_gameDir);
      GameConfig.EnsureUnstickBind(_gameDir);
      GameConfig.SetPipFollow(_gameDir, Math.Max(_cbFollow.SelectedIndex, 0));
      GameConfig.SetFriendlyFire(_gameDir, _cbFriendly.Checked);
      GameConfig.SetScoreboard(_gameDir, true);

      // O que o AutoDoom ToH.cmd fazia antes de subir o jogo.
      psi.Environment["TIMIDITY_CFG"] = Path.Combine(_gameDir, "timidity.cfg");
      if (File.Exists(_settings.SoundfontPath))
         psi.Environment["SDL_SOUNDFONTS"] = _settings.SoundfontPath;

      SaveState();

      try
      {
         using Process? game = Process.Start(psi);
         if (game is null)
            return;

         Hide();
         await game.WaitForExitAsync();
         Show();

         if (game.ExitCode != 0)
         {
            MessageBox.Show(this, Strings.ExitedWithCode(game.ExitCode),
               Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
         }
      }
      catch (Win32Exception ex)
      {
         Show();
         MessageBox.Show(this, Strings.LaunchFailed(ex.Message),
            Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
   }
}
