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

   private readonly RadioButton _rbCopilot = new();
   private readonly RadioButton _rbCoop    = new();
   private readonly ListBox     _lbIwads   = new();
   private readonly ComboBox    _cbPwad    = new();
   private readonly Button      _btnPlay   = new();
   private readonly Button      _btnDetect = new();
   private readonly TrackBar    _tbBots     = new();
   private readonly Label       _lblBotCount = new();
   private readonly Label       _lblStatus = new();
   private readonly ToolTip     _tips      = new();
   private readonly Label       _lblBotHint = new();
   private readonly Label       _lblCopilotHint = new();
   private readonly CheckBox    _cbPip     = new();
   private readonly CheckBox    _cbWeapons = new();
   private readonly CheckBox    _cbJump    = new();
   private readonly CheckBox    _cbScores  = new();
   private readonly ComboBox    _cbFollow  = new();
   private readonly Label       _lblFollow = new();
   private readonly GroupBox    _grpProgress = new();
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
   private Bitmap? _icoFolder;
   private Bitmap? _icoScan;
   private Bitmap? _icoExit;
   private Bitmap? _icoPlay;
   private Font?   _boldFont;

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

   private static Color SeparatorColor =>
      Blend(SystemColors.Control, SystemColors.ControlDark, 0.65f);

   private static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
      (int)(a.R + (b.R - a.R) * amount),
      (int)(a.G + (b.G - a.G) * amount),
      (int)(a.B + (b.B - a.B) * amount));

   // ---------------------------------------------------------------- icones

   /// <summary>
   /// Os icones sao desenhados num quadro logico de 16x16 e a matriz do Graphics
   /// escala para o tamanho fisico: um so desenho serve para qualquer DPI.
   /// </summary>
   private static Bitmap MakeIcon(int size, Action<Graphics> draw)
   {
      var bmp = new Bitmap(size, size);
      using (Graphics g = Graphics.FromImage(bmp))
      {
         g.SmoothingMode = SmoothingMode.AntiAlias;
         g.Clear(Color.Transparent);
         g.ScaleTransform(size / 16f, size / 16f);
         draw(g);
      }
      return bmp;
   }

   private void BuildIcons()
   {
      int size = LogicalToDeviceUnits(16);
      Color stroke = SystemColors.ControlText;

      _icoFolder = MakeIcon(size, g =>
      {
         using var back  = new SolidBrush(Color.FromArgb(226, 168, 38));
         using var front = new SolidBrush(Color.FromArgb(255, 208, 94));
         using var edge  = new Pen(Color.FromArgb(180, 128, 20), 1f);

         // corpo da pasta, com a abinha do canto superior esquerdo
         var body = new[]
         {
            new PointF(1f, 4f), new PointF(6f, 4f), new PointF(7.2f, 5.6f),
            new PointF(14.6f, 5.6f), new PointF(14.6f, 13.4f), new PointF(1f, 13.4f),
         };
         g.FillPolygon(back, body);
         g.DrawPolygon(edge, body);

         // aba da frente, inclinada: e o que faz a pasta parecer aberta
         var flap = new[]
         {
            new PointF(3.1f, 7.4f), new PointF(15.4f, 7.4f),
            new PointF(13.2f, 13.4f), new PointF(1f, 13.4f),
         };
         g.FillPolygon(front, flap);
         g.DrawPolygon(edge, flap);
      });

      _icoScan = MakeIcon(size, g =>
      {
         var page = new[]
         {
            new PointF(3f, 1.2f), new PointF(9.4f, 1.2f), new PointF(13.2f, 5f),
            new PointF(13.2f, 14.8f), new PointF(3f, 14.8f),
         };
         using var white = new SolidBrush(Color.White);
         using var gray  = new Pen(Color.FromArgb(130, 138, 148), 1f);
         using var fold  = new SolidBrush(Color.FromArgb(214, 222, 232));
         g.FillPolygon(white, page);
         g.DrawPolygon(gray, page);
         g.FillPolygon(fold, new[] { new PointF(9.4f, 1.2f), new PointF(13.2f, 5f), new PointF(9.4f, 5f) });
         g.DrawPolygon(gray, new[] { new PointF(9.4f, 1.2f), new PointF(13.2f, 5f), new PointF(9.4f, 5f) });

         using var ink = new Pen(Color.FromArgb(48, 108, 190), 1.3f);
         g.DrawLine(ink, 5f,  7.4f, 11.2f, 7.4f);
         g.DrawLine(ink, 5f,  9.8f, 11.2f, 9.8f);
         g.DrawLine(ink, 5f, 12.2f,  9.2f, 12.2f);
      });

      _icoExit = MakeIcon(size, g =>
      {
         // porta: tres lados, o lado direito fica aberto para a seta sair por ele
         using var door = new Pen(stroke, 1.5f);
         g.DrawLines(door, new[]
         {
            new PointF(8.4f, 2f), new PointF(2.2f, 2f),
            new PointF(2.2f, 14f), new PointF(8.4f, 14f),
         });

         using var arrow = new Pen(Color.FromArgb(198, 62, 54), 1.7f);
         g.DrawLine(arrow, 6.6f, 8f, 12.4f, 8f);
         using var head = new SolidBrush(Color.FromArgb(198, 62, 54));
         g.FillPolygon(head, new[]
         {
            new PointF(15f, 8f), new PointF(11.4f, 5f), new PointF(11.4f, 11f),
         });
      });

      _icoPlay = MakeIcon(size, g =>
      {
         using var green = new SolidBrush(Color.FromArgb(38, 152, 70));
         g.FillPolygon(green, new[]
         {
            new PointF(3.4f, 2f), new PointF(14f, 8f), new PointF(3.4f, 14f),
         });
      });
   }

   protected override void Dispose(bool disposing)
   {
      if (disposing)
      {
         _icoFolder?.Dispose();
         _icoScan?.Dispose();
         _icoExit?.Dispose();
         _icoPlay?.Dispose();
         _boldFont?.Dispose();
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
      Text          = Strings.AppTitle;
      Font = SystemFonts.MessageBoxFont ?? Font;
      // Base de escala do Segoe UI 9pt a 100%. Sem isso o WinForms encolhe a janela.
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode       = AutoScaleMode.Font;
      StartPosition = FormStartPosition.CenterScreen;
      Icon          = TryGetOwnIcon();
      AllowDrop     = true;
      DragEnter    += OnDragEnter;
      DragDrop     += OnDragDrop;

      BuildIcons();

      var root = new TableLayoutPanel
      {
         Dock        = DockStyle.Fill,
         Padding     = new Padding(16),
         ColumnCount = 1,
         RowCount    = 4,
      };
      root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // Modo
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // Extras
      root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // IWAD, elastica
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      root.RowCount = 6;

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
      MinimumSize = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(560));
      Size        = new Size(LogicalToDeviceUnits(600), LogicalToDeviceUnits(680));
      CenterToScreen();

      RefreshDetectAvailability();
      PruneStoredPwads();

      if (_autoDetect && _btnDetect.Enabled)
         BeginInvoke(() => OnDetectWads(this, EventArgs.Empty));
   }

   private Control BuildModeGroup()
   {
      _rbCopilot.Text     = Strings.ModeCopilot;
      _rbCopilot.AutoSize = true;
      _rbCopilot.Anchor   = AnchorStyles.Left;
      _rbCopilot.Margin   = new Padding(0, 0, 0, 4);
      _rbCopilot.Checked  = true;
      // O GroupBox pinta o titulo com a cor de acento; sem isto os filhos herdariam
      // o azul junto.
      _rbCopilot.ForeColor = SystemColors.ControlText;

      _rbCoop.Text     = Strings.ModeCoop;
      _rbCoop.AutoSize = true;
      // Anchor.Left sozinho num TableLayoutPanel centraliza o controle na vertical
      // da celula: e assim que radio, spinner e "bots" ficam na mesma linha de base.
      _rbCoop.Anchor   = AnchorStyles.Left;
      _rbCoop.Margin   = new Padding(0, 0, Gutter, 0);
      _rbCoop.ForeColor = SystemColors.ControlText;
      _rbCoop.CheckedChanged += (_, _) => UpdateEnabledState();

      // Um slider de 1 a 4 diz mais que um spinner: os quatro valores cabem na
      // regua e o passo unico impede valor invalido sem precisar validar nada.
      _tbBots.Minimum       = 1;
      _tbBots.Maximum       = MaxBots;
      _tbBots.TickFrequency = 1;
      _tbBots.SmallChange   = 1;
      _tbBots.LargeChange   = 1;
      _tbBots.TickStyle     = TickStyle.BottomRight;
      _tbBots.AutoSize      = false;
      _tbBots.Height        = LogicalToDeviceUnits(40);
      // Left|Right sem Top|Bottom: estica na horizontal e continua centrado na
      // vertical em relacao ao radio e ao numero.
      _tbBots.Anchor        = AnchorStyles.Left | AnchorStyles.Right;
      _tbBots.Margin        = new Padding(0, 0, Gutter, 0);
      _tbBots.Value         = 3;

      _boldFont = new Font(Font, FontStyle.Bold);

      _lblBotCount.AutoSize  = true;
      _lblBotCount.Anchor    = AnchorStyles.Left;
      _lblBotCount.Margin    = new Padding(0, 0, 4, 0);
      _lblBotCount.Font      = _boldFont;
      _lblBotCount.ForeColor = SystemColors.ControlText;
      _lblBotCount.Text      = _tbBots.Value.ToString();

      // Ligado depois do valor inicial: o handler mexe no _lblBotCount, que precisa
      // ja existir quando o primeiro ValueChanged disparar.
      _tbBots.ValueChanged += (_, _) => UpdateBotHint();

      var botsAfter = new Label
      {
         Text      = Strings.BotsWord,
         AutoSize  = true,
         Anchor    = AnchorStyles.Left,
         Margin    = new Padding(0),
         ForeColor = SystemColors.ControlText,
      };

      // "bots" acompanha o slider: sem isso o numero fica cinza e a palavra preta
      // no modo Copiloto.
      _tbBots.EnabledChanged += (_, _) => botsAfter.Enabled = _tbBots.Enabled;

      // Numero e palavra andam juntos e ancoram na direita, na mesma coluna de
      // pixels da caixa de aviso e dos botoes.
      var botsValue = new TableLayoutPanel
      {
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Anchor       = AnchorStyles.Right,
         Margin       = new Padding(Gutter, 0, 0, 0),
      };
      botsValue.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      botsValue.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      botsValue.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      botsValue.Controls.Add(_lblBotCount, 0, 0);
      botsValue.Controls.Add(botsAfter,    1, 0);

      // Dock.Top para a linha ocupar a largura toda, e o slider na coluna Percent
      // com Anchor nos dois lados: a sobra vira regua em vez de vazio a direita.
      // Centralizar o conjunto aqui abriria um buraco entre "voce joga com" e o
      // slider e quebraria a frase no meio.
      // Os DOIS radios moram aqui, no mesmo painel: no WinForms a exclusao mutua
      // vale por container pai imediato. Com o Copiloto em outro painel, os dois
      // ficavam marcados ao mesmo tempo e nenhum desmarcava o outro.
      var modeRows = new TableLayoutPanel
      {
         Dock         = DockStyle.Top,
         ColumnCount  = 3,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
      };
      modeRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      modeRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      modeRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      modeRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      modeRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      modeRows.Controls.Add(_rbCopilot, 0, 0);
      modeRows.SetColumnSpan(_rbCopilot, 3);
      modeRows.Controls.Add(_rbCoop,   0, 1);
      modeRows.Controls.Add(_tbBots,   1, 1);
      modeRows.Controls.Add(botsValue, 2, 1);

      // Dock.Fill numa coluna Percent: o label recebe a largura real disponivel e
      // quebra a linha nela, em vez de num MaximumSize fixo em pixels.
      _lblBotHint.AutoSize  = true;
      _lblBotHint.Dock      = DockStyle.Fill;
      _lblBotHint.ForeColor = SystemColors.ControlText;
      _lblBotHint.Margin    = new Padding(0, 6, 0, 0);

      _lblCopilotHint.Text      = Strings.CopilotHint;
      _lblCopilotHint.AutoSize  = true;
      _lblCopilotHint.Dock      = DockStyle.Fill;
      _lblCopilotHint.ForeColor = SystemColors.ControlText;
      _lblCopilotHint.Margin    = new Padding(0);

      var hintText = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 1,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0),
         // Cor explicita em vez de Transparent: fundo transparente no WinForms
         // depende do repaint do pai e piscava sobre o desenho do Paint.
         BackColor    = InfoFillColor,
      };
      hintText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      hintText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      hintText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      hintText.Controls.Add(_lblCopilotHint, 0, 0);
      hintText.Controls.Add(_lblBotHint, 0, 1);

      // Caixa de aviso: Dock.Top estica ate a borda direita do grupo e o AutoSize
      // ainda encolhe a altura quando a segunda linha some. O padding da esquerda
      // abre o espaco onde o Paint desenha o circulo do "i".
      var infoBox = new TableLayoutPanel
      {
         Dock         = DockStyle.Top,
         ColumnCount  = 1,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Padding      = new Padding(38, 10, 12, 10),
         Margin       = new Padding(0, 12, 0, 0),
      };
      infoBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      infoBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      infoBox.Controls.Add(hintText, 0, 0);
      infoBox.Paint += OnPaintInfoBox;

      // Sem um teto de largura, o GroupBox mede os labels como se coubessem numa
      // linha so e fecha com a altura de uma linha a menos: a dica de duas linhas
      // entao cobre a borda de baixo do grupo. Amarrar o teto na largura real faz
      // a medida e o desenho concordarem. A guarda de igualdade evita o laco de
      // layout que a propria mudanca de MaximumSize dispararia.
      infoBox.SizeChanged += (_, _) =>
      {
         int usable = infoBox.ClientSize.Width - infoBox.Padding.Horizontal;
         if (usable <= 0)
            return;

         var cap = new Size(usable, 0);
         if (_lblCopilotHint.MaximumSize != cap)
            _lblCopilotHint.MaximumSize = cap;
         if (_lblBotHint.MaximumSize != cap)
            _lblBotHint.MaximumSize = cap;
      };

      // TableLayoutPanel no lugar do FlowLayoutPanel: o Flow so da a cada filho a
      // largura natural dele, e a caixa de aviso precisa esticar ate a borda.
      var layout = new TableLayoutPanel
      {
         Dock         = DockStyle.Top,
         ColumnCount  = 1,
         RowCount     = 2,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Padding      = new Padding(12, 10, 12, 12),
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      for (int i = 0; i < 2; i++)
         layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.Controls.Add(modeRows, 0, 0);
      layout.Controls.Add(infoBox,  0, 1);

      GroupBox box = MakeGroup(Strings.GroupMode);
      box.Dock = DockStyle.Top;
      box.Controls.Add(layout);
      FitGroupToContent(box, layout);
      return box;
   }

   /// <summary>Titulo do grupo no azul de acento, com os filhos de volta na cor normal.</summary>
   /// <summary>Opcoes que dependem de coisas fora do launcher: o exe com PIP e o
   /// dmflags do Coop.</summary>
   private Control BuildExtrasGroup()
   {
      _cbPip.Text     = Strings.PipOption;
      _cbPip.AutoSize = true;
      _cbPip.Margin   = new Padding(0, 0, 0, Gutter);
      _cbPip.ForeColor = SystemColors.ControlText;

      _cbWeapons.Text     = Strings.WeaponsOption;
      _cbWeapons.AutoSize = true;
      _cbWeapons.Margin   = new Padding(0, 0, 0, 2);
      _cbWeapons.ForeColor = SystemColors.ControlText;

      var hint = new Label
      {
         Text      = Strings.WeaponsHint,
         AutoSize  = true,
         ForeColor = SystemColors.GrayText,
         Margin    = new Padding(20, 0, 0, Gutter),
      };

      _cbJump.Text      = Strings.JumpOption;
      _cbJump.AutoSize  = true;
      _cbJump.Margin    = new Padding(0, 0, 0, Gutter);
      _cbJump.ForeColor = SystemColors.ControlText;

      _cbScores.Text      = Strings.ScoreboardOption;
      _cbScores.AutoSize  = true;
      _cbScores.Margin    = new Padding(0);
      _cbScores.ForeColor = SystemColors.ControlText;

      var layout = new FlowLayoutPanel
      {
         Dock          = DockStyle.Top,
         FlowDirection = FlowDirection.TopDown,
         WrapContents  = false,
         AutoSize      = true,
         AutoSizeMode  = AutoSizeMode.GrowAndShrink,
         Padding       = new Padding(12, 8, 12, 12),
      };
      layout.Controls.Add(_cbPip);
      layout.Controls.Add(BuildFollowRow());
      layout.Controls.Add(_cbWeapons);
      layout.Controls.Add(hint);
      layout.Controls.Add(_cbJump);
      layout.Controls.Add(_cbScores);

      GroupBox box = MakeGroup(Strings.GroupExtras);
      box.Dock = DockStyle.Top;
      box.Controls.Add(layout);
      FitGroupToContent(box, layout);
      return box;
   }

   /// <summary>Quem o quadrinho persegue. So faz sentido com o PIP ligado.</summary>
   private Control BuildFollowRow()
   {
      _lblFollow.Text      = Strings.FollowLabel;
      _lblFollow.AutoSize  = true;
      _lblFollow.Anchor    = AnchorStyles.Left;
      _lblFollow.ForeColor = SystemColors.ControlText;
      _lblFollow.Margin    = new Padding(20, 0, Gutter, 0);

      _cbFollow.DropDownStyle = ComboBoxStyle.DropDownList;
      _cbFollow.Anchor        = AnchorStyles.Left;
      _cbFollow.Width         = LogicalToDeviceUnits(230);
      _cbFollow.Margin        = new Padding(0);
      _cbFollow.Items.Add(Strings.FollowKills);
      _cbFollow.Items.Add(Strings.FollowExit);

      var row = new TableLayoutPanel
      {
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Margin       = new Padding(0, 0, 0, Gutter),
      };
      row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      row.Controls.Add(_lblFollow, 0, 0);
      row.Controls.Add(_cbFollow,  1, 0);
      return row;
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
   /// a reserva da faixa do titulo e o filho acaba pintado por cima da legenda --
   /// aconteceu tres vezes, e da ultima so no ingles, porque o texto mais curto
   /// mudava a conta por poucos pixels. Aqui a legenda esta reservada por
   /// construcao: content.Top ja e o topo do DisplayRectangle, abaixo do titulo.
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
      path.AddArc(r.Left,         r.Top,            d, d, 180, 90);
      path.AddArc(r.Right - d,    r.Top,            d, d, 270, 90);
      path.AddArc(r.Right - d,    r.Bottom - d,     d, d,   0, 90);
      path.AddArc(r.Left,         r.Bottom - d,     d, d,  90, 90);
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

   /// <summary>A lista de IWADs ocupa o corpo da janela: e a escolha principal.</summary>
   private Control BuildIwadGroup()
   {
      _lbIwads.Dock           = DockStyle.Fill;
      _lbIwads.IntegralHeight = false;
      _lbIwads.Margin         = new Padding(0, 0, Gutter, 0);
      _lbIwads.ForeColor      = SystemColors.WindowText;
      // Linhas altas e desenhadas a mao: no modo padrao elas ficam coladas e sem recuo.
      _lbIwads.DrawMode       = DrawMode.OwnerDrawFixed;
      _lbIwads.ItemHeight     = LogicalToDeviceUnits(26);
      _lbIwads.DrawItem      += OnDrawIwadItem;
      _lbIwads.SelectedIndexChanged += (_, _) => UpdateEnabledState();
      _lbIwads.DoubleClick += (_, _) => { if (_btnPlay.Enabled) OnPlay(this, EventArgs.Empty); };

      var browse = StyleButton(new Button(), Strings.Browse, _icoFolder, new Padding(0, 0, 0, Gutter));
      browse.Click += OnBrowseIwad;

      StyleButton(_btnDetect, Strings.Detect, _icoScan, new Padding(0));
      _btnDetect.Click += OnDetectWads;

      // Coluna da direita ancorada no topo: os dois botoes tem a mesma largura e a
      // mesma borda direita que o "Procurar..." do grupo PWAD logo abaixo.
      var side = new FlowLayoutPanel
      {
         FlowDirection = FlowDirection.TopDown,
         WrapContents  = false,
         AutoSize      = true,
         AutoSizeMode  = AutoSizeMode.GrowAndShrink,
         Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
         Margin        = new Padding(0),
      };
      side.Controls.Add(browse);
      side.Controls.Add(_btnDetect);

      // Coluna de botoes com largura fixa e igual a do grupo PWAD: e o que faz a
      // borda direita da lista cair na mesma coluna de pixels da borda da combo.
      // Medido: ColumnStyle Absolute NAO e reescalado pelo autoscale, entao o valor
      // vai ja em unidades de tela, igual ao MinimumSize dos botoes.
      var layout = new TableLayoutPanel
      {
         Dock        = DockStyle.Fill,
         ColumnCount = 2,
         RowCount    = 1,
         Padding     = new Padding(12, 10, 12, 12),
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(ButtonWidth)));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      layout.Controls.Add(_lbIwads, 0, 0);
      layout.Controls.Add(side,     1, 0);

      GroupBox box = MakeGroup(Strings.GroupIwad);
      box.Controls.Add(layout);
      return box;
   }

   /// <summary>Uma linha da lista: fundo de selecao, recuo a esquerda e texto centrado.</summary>
   private void OnDrawIwadItem(object? sender, DrawItemEventArgs e)
   {
      if (e.Index < 0)
      {
         e.DrawBackground();
         e.DrawFocusRectangle();
         return;
      }

      bool  picked = (e.State & DrawItemState.Selected) != 0;
      Color back   = picked ? SystemColors.Highlight     : _lbIwads.BackColor;
      Color fore   = picked ? SystemColors.HighlightText : _lbIwads.ForeColor;

      using (var brush = new SolidBrush(back))
         e.Graphics.FillRectangle(brush, e.Bounds);

      var text = new Rectangle(
         e.Bounds.Left  + LogicalToDeviceUnits(10), e.Bounds.Top,
         e.Bounds.Width - LogicalToDeviceUnits(14), e.Bounds.Height);

      TextRenderer.DrawText(e.Graphics, _lbIwads.Items[e.Index].ToString(), e.Font ?? Font,
         text, fore,
         TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
         | TextFormatFlags.NoPrefix);

      e.DrawFocusRectangle();
   }

   /// <summary>Um PWAD por vez, num dropdown.</summary>
   private Control BuildPwadGroup()
   {
      _cbPwad.DropDownStyle = ComboBoxStyle.DropDownList;
      // Anchor no lugar de Dock.Fill: a combo tem altura fixa e assim fica centrada
      // na vertical em relacao ao botao, que e mais alto.
      _cbPwad.Anchor        = AnchorStyles.Left | AnchorStyles.Right;
      _cbPwad.Margin        = new Padding(0, 0, Gutter, 0);
      _cbPwad.ForeColor     = SystemColors.WindowText;
      _cbPwad.MaxDropDownItems = 20;
      _cbPwad.SelectedIndexChanged += (_, _) => ReportPwadCompatibility();

      var browse = StyleButton(new Button(), Strings.BrowseAlt, _icoFolder, new Padding(0));
      browse.Anchor = AnchorStyles.Left;
      browse.Click += OnBrowsePwad;

      // Dock.Top, nao Fill: um TableLayoutPanel com AutoSize e Dock.Fill dentro de um
      // GroupBox com AutoSize ocupa tambem a faixa do titulo e apaga o rotulo do grupo.
      var layout = new TableLayoutPanel
      {
         Dock         = DockStyle.Top,
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         Padding      = new Padding(12, 10, 12, 12),
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      // AutoSize, e nao Absolute: uma coluna Absolute aqui faz o GroupBox com
      // AutoSize parar de pintar a propria legenda (medido, duas vezes). A largura
      // sai fixa do mesmo jeito, porque quem manda nela e o MinimumSize do botao,
      // identico ao da coluna do grupo IWAD.
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.Controls.Add(_cbPwad, 0, 0);
      layout.Controls.Add(browse,  1, 0);

      GroupBox box = MakeGroup(Strings.GroupPwad);
      box.Dock = DockStyle.Top;
      box.Controls.Add(layout);
      FitGroupToContent(box, layout);
      return box;
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
      StyleButton(_btnPlay, Strings.Play, _icoPlay, new Padding(Gutter, 0, 0, 0));
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

      var row = new TableLayoutPanel
      {
         Dock         = DockStyle.Fill,
         ColumnCount  = 2,
         RowCount     = 1,
         AutoSize     = true,
         AutoSizeMode = AutoSizeMode.GrowAndShrink,
         // 3 da borda do GroupBox + 12 do padding interno: o "Jogar" fica no mesmo
         // prumo dos botoes do IWAD/PWAD, e o status no prumo da lista.
         Margin       = new Padding(GroupInset, 0, GroupInset, 0),
      };
      row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
      _rbCoop.Checked    = _settings.Mode == PlayMode.Coop;
      _rbCopilot.Checked = !_rbCoop.Checked;
      _tbBots.Value      = Math.Clamp(_settings.BotCount, 1, MaxBots);
      _cbWeapons.Checked = _settings.WeaponsDisappear;
      _cbJump.Checked    = GameConfig.JumpEnabled(_gameDir);
      _cbFollow.SelectedIndex = Math.Clamp(GameConfig.PipFollow(_gameDir), 0, 1);
      _cbScores.Checked  = GameConfig.ScoreboardEnabled(_gameDir);

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
      _settings.Mode         = _rbCoop.Checked ? PlayMode.Coop : PlayMode.Copilot;
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
      _tbBots.Enabled      = _rbCoop.Checked;
      _cbFollow.Enabled    = _cbPip.Enabled && _cbPip.Checked;
      _lblFollow.Enabled   = _cbFollow.Enabled;
      _lblBotCount.Enabled = _rbCoop.Checked;
      UpdateBotHint();
   }

   /// <summary>
   /// O quarto slot e o seu: com 4 bots a engine liga bots[0].active e o seu
   /// personagem tambem anda sozinho (G_AdjustNetBotSettings, g_game.cpp:3232).
   /// </summary>
   private void UpdateBotHint()
   {
      _lblBotCount.Text = _tbBots.Value.ToString();

      // Um modo por vez na caixa: a explicacao do Copiloto so interessa a quem
      // esta no Copiloto. Invisivel em vez de vazio, senao a linha ocupa altura.
      _lblCopilotHint.Visible = !_rbCoop.Checked;

      if (!_rbCoop.Checked)
      {
         _lblBotHint.Text = "";
         // Escondido, e nao so vazio: um controle invisivel nao ocupa linha nem
         // margem no layout, entao a caixa de aviso encolhe junto.
         _lblBotHint.Visible = false;
         return;
      }

      _lblBotHint.Visible = true;
      _lblBotHint.Text = _tbBots.Value >= MaxBots
         ? Strings.BotHintFull(MaxBots)
         : Strings.BotHintPartial(_tbBots.Value, MaxBots);
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

      bool usePip = _cbPip.Checked && File.Exists(Path.Combine(_gameDir, PipExeName));
      string exeName = usePip ? PipExeName : GameExeName;
      string exe = Path.Combine(_gameDir, exeName);
      if (!File.Exists(exe))
      {
         MessageBox.Show(this, $"Nao encontrei o {GameExeName} em:\n{_gameDir}",
            "AutoDoom Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

      // Copiloto e a ausencia de -bots: sem ele o bot dirige o proprio jogador
      // (G_AdjustNetBotSettings nao roda e bots[0].active fica true).
      if (_rbCoop.Checked)
      {
         psi.ArgumentList.Add("-bots");
         psi.ArgumentList.Add(_tbBots.Value.ToString());
      }

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

      // Pulo e placar moram na config da engine: escrever agora, com o jogo
      // fechado, e o unico momento em que a mudanca sobrevive.
      GameConfig.SetJump(_gameDir, _cbJump.Checked);
      GameConfig.SetPipFollow(_gameDir, Math.Max(_cbFollow.SelectedIndex, 0));
      GameConfig.SetScoreboard(_gameDir, _cbScores.Checked);

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
               "AutoDoom Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
         }
      }
      catch (Win32Exception ex)
      {
         Show();
         MessageBox.Show(this, $"Nao consegui iniciar o jogo:\n{ex.Message}",
            "AutoDoom Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
   }
}
