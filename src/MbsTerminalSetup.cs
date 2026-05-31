using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MbsTerminalSetup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (!IsAdministrator())
            {
                return RelaunchAsAdministrator(args);
            }

            InstallerOptions options = InstallerOptions.FromArgs(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InstallerForm form = new InstallerForm(options);
            Application.Run(form);

            return form.ExitCode;
        }

        private static bool IsAdministrator()
        {
            System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static int RelaunchAsAdministrator(string[] args)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = BuildArgumentString(args);
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                Process.Start(startInfo);
                return 0;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(
                    "Administrator permission is required to run MBS Terminal Setup.",
                    "MBS Terminal Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return ex.NativeErrorCode == 1223 ? 1 : ex.NativeErrorCode;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not restart as administrator: " + ex.Message,
                    "MBS Terminal Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return 1;
            }
        }

        private static string BuildArgumentString(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }

            List<string> quotedArguments = new List<string>();

            foreach (string argument in args)
            {
                quotedArguments.Add(QuoteArgument(argument));
            }

            return string.Join(" ", quotedArguments.ToArray());
        }

        private static string QuoteArgument(string argument)
        {
            if (argument == null)
            {
                return "\"\"";
            }

            if (argument.Length == 0 || argument.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
            {
                return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }

            return argument;
        }
    }

    internal sealed class InstallerOptions
    {
        public string StartingDirectory { get; set; }
        public bool InstallDependencies { get; set; }
        public bool InstallPhp { get; set; }
        public string PhpDirectory { get; set; }
        public bool InstallComposer { get; set; }
        public bool InstallLaravel { get; set; }
        public bool InstallValet { get; set; }
        public bool UpdateTools { get; set; }
        public string InstallScope { get; set; }
        public string DisplayName { get; set; }

        public static InstallerOptions FromArgs(string[] args)
        {
            InstallerOptions options = new InstallerOptions();

            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];

                if (IsSwitch(argument, "InstallDependencies"))
                {
                    options.InstallDependencies = true;
                    continue;
                }

                if (IsSwitch(argument, "InstallPhp"))
                {
                    options.InstallPhp = true;
                    continue;
                }

                if (IsSwitch(argument, "InstallComposer"))
                {
                    options.InstallComposer = true;
                    continue;
                }

                if (IsSwitch(argument, "InstallLaravel"))
                {
                    options.InstallLaravel = true;
                    continue;
                }

                if (IsSwitch(argument, "InstallValet"))
                {
                    options.InstallValet = true;
                    continue;
                }

                if (IsSwitch(argument, "UpdateTools"))
                {
                    options.UpdateTools = true;
                    continue;
                }

                if (IsSwitch(argument, "StartingDirectory") && index + 1 < args.Length)
                {
                    options.StartingDirectory = args[index + 1];
                    index++;
                    continue;
                }

                if (IsSwitch(argument, "PhpDirectory") && index + 1 < args.Length)
                {
                    options.PhpDirectory = args[index + 1];
                    index++;
                    continue;
                }

                if (IsSwitch(argument, "InstallScope") && index + 1 < args.Length)
                {
                    string installScope = args[index + 1];
                    options.InstallScope = string.Equals(installScope, "AllUsers", StringComparison.OrdinalIgnoreCase)
                        ? "AllUsers"
                        : "CurrentUser";
                    index++;
                    continue;
                }

                if (IsSwitch(argument, "DisplayName") && index + 1 < args.Length)
                {
                    options.DisplayName = args[index + 1];
                    index++;
                }
            }

            if (string.IsNullOrWhiteSpace(options.InstallScope))
            {
                options.InstallScope = "CurrentUser";
            }

            return options;
        }

        private static bool IsSwitch(string argument, string name)
        {
            return string.Equals(argument, "-" + name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "/" + name, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class InstallerForm : Form
    {
        private const int SidebarWidth = 230;
        private const int WizardContentWidth = 500;
        private const int WideContentWidth = 1058;
        private const int StepCount = 6;

        private static readonly Color BackgroundColor = Color.FromArgb(4, 4, 7);
        private static readonly Color HeaderStartColor = Color.FromArgb(3, 3, 6);
        private static readonly Color HeaderEndColor = Color.FromArgb(21, 13, 32);
        private static readonly Color SurfaceColor = Color.FromArgb(10, 10, 15);
        private static readonly Color SurfaceAltColor = Color.FromArgb(20, 20, 29);
        private static readonly Color SelectedSurfaceColor = Color.FromArgb(31, 20, 45);
        private static readonly Color FieldColor = Color.FromArgb(7, 7, 11);
        private static readonly Color BorderColor = Color.FromArgb(58, 60, 74);
        private static readonly Color TextColor = Color.FromArgb(248, 250, 252);
        private static readonly Color MutedTextColor = Color.FromArgb(154, 164, 178);
        private static readonly Color AccentColor = Color.FromArgb(168, 85, 247);
        private static readonly Color AccentAltColor = Color.FromArgb(34, 211, 238);
        private static readonly Color WarningColor = Color.FromArgb(245, 158, 11);
        private static readonly Color DangerColor = Color.FromArgb(239, 68, 68);
        private static readonly Color DisabledTextColor = Color.FromArgb(126, 132, 148);
        private static readonly Color DisabledSurfaceColor = Color.FromArgb(18, 18, 25);

        private readonly string repositoryRoot;
        private readonly string installerPath;
        private readonly string iconPath;
        private readonly List<Control> wizardPages = new List<Control>();
        private readonly Label[] stepLabels = new Label[StepCount];
        private static readonly string[] TerminalProcessNames =
        {
            "WindowsTerminal",
            "wt",
            "OpenConsole",
            "powershell",
            "pwsh",
            "cmd",
            "ConEmu",
            "ConEmu64",
            "Cmder",
            "Tabby",
            "alacritty",
            "wezterm-gui",
            "mintty"
        };
        private readonly string[] stepTitles =
        {
            "Terms",
            "Profile",
            "Runtime",
            "Tooling",
            "Review",
            "Running"
        };
        private readonly string[] wizardTitles =
        {
            "Setup Terms",
            "Terminal Profile",
            "PHP Runtime",
            "Select Other Tooling",
            "Review & Install",
            "Running Installer"
        };
        private readonly string[] wizardDescriptions =
        {
            "Review what this installer can change before continuing.",
            "Set the Windows Terminal starting folder and optional prompt dependency.",
            "Install PHP automatically, use an existing PHP folder, and prepare Composer.",
            "See what is already installed, then choose Laravel, Valet, and update behavior.",
            "Confirm the plan before anything changes on the machine.",
            "PowerShell runs hidden here, with progress and command output visible."
        };

        private TextBox startingDirectoryBox;
        private TextBox displayNameBox;
        private TextBox phpDirectoryBox;
        private TableLayoutPanel bodyLayout;
        private Panel sidebarPanel;
        private Panel wizardPanel;
        private Panel runPanel;
        private AnimatedAccentPanel runningVisualPanel;
        private Timer animationTimer;
        private ModernCheckBox termsAcceptedBox;
        private ModernCheckBox installStarshipBox;
        private ModernCheckBox currentUserScopeBox;
        private ModernCheckBox allUsersScopeBox;
        private ModernCheckBox installPhpBox;
        private ModernCheckBox installComposerBox;
        private ModernCheckBox installLaravelBox;
        private ModernCheckBox installValetBox;
        private ModernCheckBox updateToolsBox;
        private Label runTitleLabel;
        private Label runTextLabel;
        private FlowLayoutPanel trustPanel;
        private FlowLayoutPanel actionPanel;
        private Control guidePanel;
        private Label wizardTitleLabel;
        private Label wizardDescriptionLabel;
        private Label reviewSummaryLabel;
        private Button backButton;
        private Button nextButton;
        private Button installButton;
        private Button cancelButton;
        private Button closeButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private RichTextBox logBox;
        private Process installerProcess;
        private int currentStep;
        private bool installHasRun;
        private bool syncingScopeOptions;

        public InstallerForm(InstallerOptions options)
        {
            repositoryRoot = GetRepositoryRoot();
            installerPath = Path.Combine(repositoryRoot, "install.ps1");
            iconPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-pixel-avatar.png");

            ExitCode = 0;
            Text = "MBS Terminal Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(820, 580);
            Size = new Size(820, 580);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = CreateFont(9F, FontStyle.Regular);
            Icon = LoadWindowIcon();

            TableLayoutPanel shell = CreateShellLayout();
            Controls.Add(shell);

            HeaderPanel header = CreateHeader();
            shell.Controls.Add(header, 0, 0);

            bodyLayout = CreateBodyLayout();
            shell.Controls.Add(bodyLayout, 0, 1);

            sidebarPanel = CreateSidebar();
            bodyLayout.Controls.Add(sidebarPanel, 0, 0);

            wizardPanel = CreatePanel();
            bodyLayout.Controls.Add(wizardPanel, 1, 0);

            TableLayoutPanel wizardLayout = CreateWizardLayout();
            wizardPanel.Controls.Add(wizardLayout);

            Panel titlePanel = new Panel();
            titlePanel.Dock = DockStyle.Fill;
            titlePanel.BackColor = SurfaceColor;
            titlePanel.Margin = new Padding(22, 8, 22, 0);
            wizardTitleLabel = CreateLabel(string.Empty, 20F, FontStyle.Bold, TextColor);
            wizardTitleLabel.Location = new Point(0, 0);
            wizardTitleLabel.Size = new Size(980, 34);
            wizardTitleLabel.Dock = DockStyle.None;
            titlePanel.Controls.Add(wizardTitleLabel);

            wizardDescriptionLabel = CreateLabel(string.Empty, 9.5F, FontStyle.Regular, MutedTextColor);
            wizardDescriptionLabel.Location = new Point(0, 34);
            wizardDescriptionLabel.Size = new Size(980, 28);
            wizardDescriptionLabel.Dock = DockStyle.None;
            titlePanel.Controls.Add(wizardDescriptionLabel);
            wizardLayout.Controls.Add(titlePanel, 0, 1);

            RoundedPanel wizardHost = new RoundedPanel();
            wizardHost.Dock = DockStyle.Fill;
            wizardHost.Margin = new Padding(22, 10, 22, 12);
            wizardHost.FillColor = Color.FromArgb(9, 10, 15);
            wizardHost.BorderColor = BorderColor;
            wizardHost.Radius = 8;
            wizardHost.Padding = new Padding(1);
            wizardLayout.Controls.Add(wizardHost, 0, 2);

            wizardPages.Add(CreateTermsPage());
            wizardPages.Add(CreateProfilePage(options));
            wizardPages.Add(CreateRuntimePage(options));
            wizardPages.Add(CreateLaravelPage(options));
            wizardPages.Add(CreateReviewPage());
            wizardPages.Add(CreateRunningPage());

            foreach (Control page in wizardPages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                wizardHost.Controls.Add(page);
            }

            wizardLayout.Controls.Add(CreateWizardNav(), 0, 3);

            runPanel = CreatePanel();
            bodyLayout.Controls.Add(runPanel, 2, 0);

            TableLayoutPanel runLayout = CreateRunLayout();
            runPanel.Controls.Add(runLayout);

            runTitleLabel = CreateLabel("Wizard Progress", 18F, FontStyle.Bold, TextColor);
            runLayout.Controls.Add(runTitleLabel, 0, 0);

            runTextLabel = CreateLabel(
                "Choose options on the left. The command log appears only on the Running step.",
                9.5F,
                FontStyle.Regular,
                MutedTextColor
            );
            runLayout.Controls.Add(runTextLabel, 0, 1);

            trustPanel = new FlowLayoutPanel();
            trustPanel.Dock = DockStyle.Fill;
            trustPanel.FlowDirection = FlowDirection.LeftToRight;
            trustPanel.WrapContents = false;
            trustPanel.BackColor = SurfaceColor;
            trustPanel.Margin = new Padding(0, 0, 0, 8);
            trustPanel.Controls.Add(CreateStatusBadge("Backups"));
            trustPanel.Controls.Add(CreateStatusBadge("PATH updates"));
            trustPanel.Controls.Add(CreateStatusBadge("Hidden shell"));

            runningVisualPanel = new AnimatedAccentPanel();
            runningVisualPanel.Dock = DockStyle.Fill;
            runningVisualPanel.Margin = new Padding(0, 4, 0, 14);
            runningVisualPanel.StatusText = "Now installing...";
            runningVisualPanel.Font = CreateFont(14F, FontStyle.Bold);
            runLayout.Controls.Add(runningVisualPanel, 0, 2);

            actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.LeftToRight;
            actionPanel.WrapContents = false;
            actionPanel.BackColor = SurfaceColor;
            actionPanel.Margin = new Padding(0, 4, 0, 10);
            runLayout.Controls.Add(actionPanel, 0, 3);

            cancelButton = CreateSecondaryButton("Cancel");
            cancelButton.Width = 118;
            cancelButton.Enabled = false;
            cancelButton.Click += CancelButtonClick;
            actionPanel.Controls.Add(cancelButton);

            closeButton = CreateTextButton("Close");
            closeButton.Width = 104;
            closeButton.Click += delegate { Close(); };
            actionPanel.Controls.Add(closeButton);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 14;
            progressBar.Margin = new Padding(0, 4, 0, 10);
            progressBar.Style = ProgressBarStyle.Blocks;
            runLayout.Controls.Add(progressBar, 0, 4);

            statusLabel = CreateLabel("Ready to configure", 11F, FontStyle.Bold, TextColor);
            runLayout.Controls.Add(statusLabel, 0, 5);

            guidePanel = CreateRunGuidePanel();
            runLayout.Controls.Add(guidePanel, 0, 6);

            logBox = new RichTextBox();
            logBox.BackColor = Color.FromArgb(7, 8, 12);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.Dock = DockStyle.Fill;
            logBox.ForeColor = TextColor;
            logBox.Font = CreateMonoFont(9F);
            logBox.ReadOnly = true;
            logBox.Margin = new Padding(0);
            logBox.DetectUrls = false;
            runLayout.Controls.Add(logBox, 0, 6);

            installLaravelBox.CheckedChanged += ToolingOptionChanged;
            installValetBox.CheckedChanged += ToolingOptionChanged;

            FormClosing += InstallerFormClosing;

            animationTimer = new Timer();
            animationTimer.Interval = 33;
            animationTimer.Tick += delegate
            {
                if (runningVisualPanel != null && runningVisualPanel.Visible)
                {
                    runningVisualPanel.Phase += 0.035F;
                    runningVisualPanel.IsRunning = IsInstalling();
                    runningVisualPanel.Invalidate();
                }
            };
            animationTimer.Start();

            AppendLog("Wizard ready. Configure each step, then install.", MutedTextColor);
            AppendLog("Tip: if you already use Laragon, XAMPP, or a custom PHP build, select its PHP folder instead of installing PHP.", MutedTextColor);

            UpdateWizard();
            FitWindowToFirstScreen();

            Shown += delegate
            {
                CenterWindowOnScreen();
                startingDirectoryBox.SelectionLength = 0;
                phpDirectoryBox.SelectionLength = 0;
                ActiveControl = nextButton;
            };
        }

        public int ExitCode { get; private set; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, int wParam, int lParam);

        private void TitleBarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        private void FitWindowToFirstScreen()
        {
            if (wizardPages.Count == 0)
            {
                return;
            }

            Size contentSize = MeasureChildBounds(wizardPages[0]);

            int wizardChromeWidth = SidebarWidth + 18 + 18 + 10 + 10 + 22 + 22 + 2;
            int wizardChromeHeight = 44 + 18 + 18 + 10 + 10 + 72 + 62 + 10 + 12 + 2;
            int desiredClientWidth = Math.Max(820, contentSize.Width + wizardChromeWidth);
            int desiredClientHeight = Math.Max(580, contentSize.Height + wizardChromeHeight);

            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            desiredClientWidth = Math.Min(desiredClientWidth, Math.Max(820, workingArea.Width - 80));
            desiredClientHeight = Math.Min(desiredClientHeight, Math.Max(580, workingArea.Height - 80));

            ClientSize = new Size(desiredClientWidth, desiredClientHeight);
            MinimumSize = SizeFromClientSize(new Size(desiredClientWidth, desiredClientHeight));
            CenterWindowOnScreen();
        }

        private void CenterWindowOnScreen()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2)
            );
        }

        private static Size MeasureChildBounds(Control parent)
        {
            int right = 0;
            int bottom = 0;

            foreach (Control child in parent.Controls)
            {
                right = Math.Max(right, child.Bounds.Right);
                bottom = Math.Max(bottom, child.Bounds.Bottom);
            }

            return new Size(right + 22, bottom + 22);
        }

        private static string GetRepositoryRoot()
        {
            string executablePath = typeof(Program).Assembly.Location;
            string root = Path.GetDirectoryName(executablePath);

            if (string.IsNullOrWhiteSpace(root))
            {
                return Environment.CurrentDirectory;
            }

            return root;
        }

        private Icon LoadWindowIcon()
        {
            if (!File.Exists(iconPath))
            {
                return null;
            }

            using (Bitmap bitmap = new Bitmap(iconPath))
            {
                IntPtr handle = bitmap.GetHicon();

                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static string ResolveStartingDirectory(string requestedPath)
        {
            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                return requestedPath;
            }

            const string defaultPortfolioPath = @"W:\GitHub\MBS-Portfolio";

            if (Directory.Exists(defaultPortfolioPath))
            {
                return defaultPortfolioPath;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static string ResolveDisplayName(string requestedName)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                return requestedName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(Environment.UserName))
            {
                return Environment.UserName;
            }

            return "Developer";
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            return new Font(ResolveFontFamily("IBM Plex Sans", "Aptos", "Segoe UI Variable Display", "Segoe UI"), size, style, GraphicsUnit.Point);
        }

        private static Font CreateMonoFont(float size)
        {
            return new Font(ResolveFontFamily("JetBrains Mono", "Cascadia Mono", "Consolas", "Courier New"), size, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static string ResolveFontFamily(params string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    using (FontFamily family = new FontFamily(name))
                    {
                        return family.Name;
                    }
                }
                catch
                {
                }
            }

            return FontFamily.GenericSansSerif.Name;
        }

        private TableLayoutPanel CreateShellLayout()
        {
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = BackgroundColor;
            shell.ColumnCount = 1;
            shell.RowCount = 2;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return shell;
        }

        private HeaderPanel CreateHeader()
        {
            HeaderPanel header = new HeaderPanel();
            header.Dock = DockStyle.Fill;
            header.StartColor = HeaderStartColor;
            header.EndColor = HeaderEndColor;
            header.Padding = new Padding(14, 0, 12, 0);
            header.MouseDown += TitleBarMouseDown;

            PictureBox icon = new PictureBox();
            icon.BackColor = Color.Transparent;
            icon.SizeMode = PictureBoxSizeMode.Zoom;
            icon.Location = new Point(14, 10);
            icon.Size = new Size(24, 24);
            icon.MouseDown += TitleBarMouseDown;

            if (File.Exists(iconPath))
            {
                icon.Image = Image.FromFile(iconPath);
            }

            header.Controls.Add(icon);

            Label title = CreateFloatingLabel("MBS Terminal Setup", 9.5F, FontStyle.Bold, TextColor, 48, 0, 240, 44);
            title.MouseDown += TitleBarMouseDown;
            header.Controls.Add(title);

            Label minimizeButton = CreateWindowButton("_", 0, 0);
            minimizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minimizeButton.Location = new Point(header.Width - 132, 0);
            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            header.Controls.Add(minimizeButton);

            Label maximizeButton = CreateWindowButton("□", 0, 0);
            maximizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            maximizeButton.Location = new Point(header.Width - 88, 0);
            maximizeButton.Click += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            header.Controls.Add(maximizeButton);

            Label closeButton = CreateWindowButton("X", 0, 0);
            closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeButton.Location = new Point(header.Width - 44, 0);
            closeButton.Click += delegate { Close(); };
            header.Controls.Add(closeButton);

            return header;
        }

        private static Label CreateWindowButton(string text, int left, int top)
        {
            Label button = new Label();
            button.AutoSize = false;
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = CreateFont(10.5F, FontStyle.Bold);
            button.ForeColor = TextColor;
            button.BackColor = Color.FromArgb(24, 24, 34);
            button.Location = new Point(left, top);
            button.Size = new Size(44, 44);
            button.Cursor = Cursors.Hand;
            button.MouseEnter += delegate
            {
                button.BackColor = text == "X" ? DangerColor : Color.FromArgb(37, 38, 50);
                button.ForeColor = Color.White;
            };
            button.MouseLeave += delegate
            {
                button.BackColor = Color.FromArgb(24, 24, 34);
                button.ForeColor = TextColor;
            };
            return button;
        }

        private static void AddPill(Control parent, string text, int left, int top, Color color)
        {
            Label pill = new Label();
            pill.AutoSize = false;
            pill.Text = text;
            pill.TextAlign = ContentAlignment.MiddleCenter;
            pill.Font = CreateFont(8.5F, FontStyle.Bold);
            pill.BackColor = Color.FromArgb(55, color);
            pill.ForeColor = Color.FromArgb(245, 247, 255);
            pill.Location = new Point(left, top);
            pill.Size = new Size(Math.Max(62, text.Length * 9 + 26), 30);
            parent.Controls.Add(pill);
        }

        private TableLayoutPanel CreateBodyLayout()
        {
            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = BackgroundColor;
            body.ColumnCount = 3;
            body.RowCount = 1;
            body.Padding = new Padding(18);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return body;
        }

        private static Panel CreatePanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = BackgroundColor;
            panel.FillColor = SurfaceColor;
            panel.BorderColor = Color.FromArgb(35, 37, 49);
            panel.Radius = 8;
            panel.Margin = new Padding(10);
            panel.Padding = new Padding(1);
            return panel;
        }

        private static TableLayoutPanel CreateWizardLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = SurfaceColor;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            return layout;
        }

        private Panel CreateSidebar()
        {
            RoundedPanel sidebar = new RoundedPanel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.Margin = new Padding(10, 10, 8, 10);
            sidebar.Padding = new Padding(18);
            sidebar.BackColor = BackgroundColor;
            sidebar.FillColor = Color.FromArgb(12, 12, 16);
            sidebar.BorderColor = Color.FromArgb(35, 37, 49);
            sidebar.Radius = 12;

            PictureBox logo = new PictureBox();
            logo.BackColor = Color.Transparent;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Location = new Point(18, 20);
            logo.Size = new Size(46, 46);

            if (File.Exists(iconPath))
            {
                logo.Image = Image.FromFile(iconPath);
            }

            sidebar.Controls.Add(logo);
            sidebar.Controls.Add(CreateFloatingLabel("MBS Terminal", 13F, FontStyle.Bold, TextColor, 74, 20, 126, 26));
            sidebar.Controls.Add(CreateFloatingLabel("Laravel Ready", 8.5F, FontStyle.Regular, MutedTextColor, 75, 46, 120, 20));

            Panel stepHost = new Panel();
            stepHost.BackColor = Color.Transparent;
            stepHost.Location = new Point(18, 96);
            stepHost.Size = new Size(190, 340);
            sidebar.Controls.Add(stepHost);

            for (int index = 0; index < StepCount; index++)
            {
                Label step = new Label();
                step.AutoSize = false;
                step.Location = new Point(0, index * 50);
                step.Size = new Size(190, 40);
                step.TextAlign = ContentAlignment.MiddleLeft;
                step.Padding = new Padding(38, 0, 8, 0);
                step.Font = CreateFont(9F, FontStyle.Bold);
                step.Text = stepTitles[index];
                step.Cursor = Cursors.Default;

                Label dot = new Label();
                dot.AutoSize = false;
                dot.Location = new Point(10, index * 50 + 11);
                dot.Size = new Size(18, 18);
                dot.TextAlign = ContentAlignment.MiddleCenter;
                dot.Font = CreateFont(8F, FontStyle.Bold);
                dot.Text = (index + 1).ToString();
                dot.Cursor = Cursors.Default;

                stepLabels[index] = step;
                stepHost.Controls.Add(dot);
                stepHost.Controls.Add(step);
            }

            sidebar.Controls.Add(CreateFloatingLabel("v1.0", 8F, FontStyle.Regular, MutedTextColor, 18, 446, 80, 20));
            sidebar.Controls.Add(CreateFloatingLabel("Admin installer", 8F, FontStyle.Bold, Color.FromArgb(186, 230, 253), 18, 468, 160, 20));
            return sidebar;
        }

        private TableLayoutPanel CreateStepper()
        {
            TableLayoutPanel stepper = new TableLayoutPanel();
            stepper.ColumnCount = StepCount;
            stepper.RowCount = 1;
            stepper.Dock = DockStyle.Fill;
            stepper.BackColor = SurfaceColor;
            stepper.Padding = new Padding(22, 16, 22, 8);

            for (int index = 0; index < StepCount; index++)
            {
                stepper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / StepCount));

                Label step = new Label();
                step.AutoSize = false;
                step.Dock = DockStyle.Fill;
                step.Margin = new Padding(0, 0, 10, 0);
                step.TextAlign = ContentAlignment.MiddleCenter;
                step.Font = CreateFont(9F, FontStyle.Bold);
                step.Text = (index + 1) + "  " + stepTitles[index];
                step.Cursor = Cursors.Hand;
                int capturedIndex = index;
                step.Click += delegate
                {
                    if (IsInstalling() || (capturedIndex == StepCount - 1 && !installHasRun))
                    {
                        return;
                    }

                    if (!TermsAccepted() && capturedIndex > 0)
                    {
                        return;
                    }

                    currentStep = capturedIndex;
                    UpdateWizard();
                };
                stepLabels[index] = step;
                stepper.Controls.Add(step, index, 0);
            }

            return stepper;
        }

        private TableLayoutPanel CreateRunLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 7;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = SurfaceColor;
            layout.Padding = new Padding(22);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return layout;
        }

        private static Control CreateRunGuidePanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.FillColor = FieldColor;
            panel.BorderColor = BorderColor;
            panel.Radius = 8;
            panel.BackColor = SurfaceColor;
            panel.Margin = new Padding(0);

            panel.Controls.Add(CreateFloatingLabel("No command output yet", 14F, FontStyle.Bold, TextColor, 20, 22, 440, 32));
            panel.Controls.Add(CreateFloatingLabel("Complete Profile, Runtime, Tooling, and Review. When you click Install, the wizard moves to Running and opens the terminal-style log here.", 9.4F, FontStyle.Regular, MutedTextColor, 20, 62, 460, 74));
            return panel;
        }

        private Control CreateTermsPage()
        {
            Panel page = CreateWideWizardPage();
            Control summaryCard = CreateSummaryGrid("Terms", "This setup can change terminal settings, PowerShell profile files, PATH entries, and developer tooling.");
            StretchCard(summaryCard, WideContentWidth);
            AddPageControl(page, summaryCard, 22, 22);

            RoundedPanel termsCard = new RoundedPanel();
            termsCard.Width = WideContentWidth;
            termsCard.Height = 226;
            termsCard.FillColor = Color.FromArgb(13, 14, 20);
            termsCard.BorderColor = BorderColor;
            termsCard.Radius = 8;
            termsCard.BackColor = Color.FromArgb(9, 10, 15);

            termsCard.Controls.Add(CreateFloatingLabel("Before you continue", 13F, FontStyle.Bold, TextColor, 18, 16, 1000, 28));
            termsCard.Controls.Add(CreateFloatingLabel("MBS Terminal Setup may copy or update Windows Terminal profile assets, PowerShell profile helper files, environment PATH entries, and optional developer tools selected in this wizard.", 9F, FontStyle.Regular, MutedTextColor, 18, 52, 1008, 44));
            termsCard.Controls.Add(CreateFloatingLabel("Some options use winget, Composer, or downloaded installers. Review each step before installing, and use All users only when you understand it may require administrator rights.", 9F, FontStyle.Regular, MutedTextColor, 18, 108, 1008, 44));
            termsCard.Controls.Add(CreateFloatingLabel("Nothing installs until the Review step and Install button.", 9.4F, FontStyle.Bold, Color.FromArgb(186, 230, 253), 18, 170, 1008, 26));
            AddPageControl(page, termsCard, 22, 126);

            termsAcceptedBox = CreateOptionRow(
                "I accept these terms",
                "Required before the setup options are shown.",
                false
            );
            termsAcceptedBox.CheckedChanged += delegate { UpdateWizard(); };
            StretchCard(termsAcceptedBox.Parent, WideContentWidth);
            AddPageControl(page, termsAcceptedBox.Parent, 22, 364);

            Control closeNote = CreateNoteCard("You can close this installer now if you do not want these changes prepared on this machine.");
            StretchCard(closeNote, WideContentWidth);
            AddPageControl(page, closeNote, 22, 446);
            return page;
        }

        private Control CreateProfilePage(InstallerOptions options)
        {
            Panel page = CreateWideWizardPage();
            AddPageControl(page, CreateSummaryGrid("Profile", "Terminal theme, PowerShell profile helpers, icons, Starship config, and Windows Terminal settings."), 22, 22);

            AddPageControl(page, CreateTextField("Display name for terminal welcome", ResolveDisplayName(options.DisplayName), out displayNameBox), 22, 126);
            displayNameBox.TextChanged += delegate { UpdateReviewSummary(); };

            currentUserScopeBox = CreateOptionRow(
                "Install for current user",
                "Recommended. Updates tools and PATH for this Windows account.",
                !string.Equals(options.InstallScope, "AllUsers", StringComparison.OrdinalIgnoreCase)
            );
            AddPageControl(page, currentUserScopeBox.Parent, 22, 216);

            allUsersScopeBox = CreateOptionRow(
                "Install for all users",
                "Uses machine scope where supported. Administrator rights may be required.",
                string.Equals(options.InstallScope, "AllUsers", StringComparison.OrdinalIgnoreCase)
            );
            AddPageControl(page, allUsersScopeBox.Parent, 22, 294);

            currentUserScopeBox.CheckedChanged += ScopeOptionChanged;
            allUsersScopeBox.CheckedChanged += ScopeOptionChanged;

            AddPageControl(page, CreatePathPicker("Starting directory (new terminal tabs open here)", ResolveStartingDirectory(options.StartingDirectory), BrowseStartingDirectoryClick, out startingDirectoryBox), 560, 22);
            AddPageControl(page, CreateNoteCard("This sets where the MBS Terminal profile opens by default. Pick your project folder, or leave the user profile default."), 560, 112);
            installStarshipBox = CreateOptionRow(
                "Install missing Starship with winget",
                "Keeps the terminal prompt fully themed when Starship is not already installed.",
                options.InstallDependencies
            );
            AddPageControl(page, installStarshipBox.Parent, 560, 192);
            return page;
        }

        private Control CreateRuntimePage(InstallerOptions options)
        {
            Panel page = CreateWideWizardPage();
            AddPageControl(page, CreateSummaryGrid("Runtime", "Choose how PHP and Composer should be prepared for Laravel tooling."), 22, 22);
            installPhpBox = CreateOptionRow(
                "Install PHP 8.4",
                "Uses winget package PHP.PHP.8.4.",
                options.InstallPhp
            );
            AddPageControl(page, installPhpBox.Parent, 22, 126);
            AddPageControl(page, CreatePathPicker("Existing PHP directory", options.PhpDirectory ?? string.Empty, BrowsePhpDirectoryClick, out phpDirectoryBox), 560, 22);
            AddPageControl(page, CreateNoteCard("Optional: select the folder that contains php.exe if PHP is already installed through Laragon, XAMPP, Herd, or a custom build."), 560, 112);
            installComposerBox = CreateOptionRow(
                "Install Composer",
                "Downloads Composer-Setup.exe and runs it silently with your PHP selection.",
                options.InstallComposer
            );
            AddPageControl(page, installComposerBox.Parent, 22, 204);
            return page;
        }

        private Control CreateLaravelPage(InstallerOptions options)
        {
            Panel page = CreateWideWizardPage();
            AddPageControl(page, CreateSummaryGrid("Tooling", "Install global developer tools after Composer is ready."), 22, 22);
            AddPageControl(page, CreateDetectedToolsCard(), 22, 126);
            updateToolsBox = CreateOptionRow(
                "Update tooling that is already installed",
                "Refreshes PHP with winget and Composer with self-update when selected.",
                options.UpdateTools
            );
            AddPageControl(page, updateToolsBox.Parent, 22, 220);
            installLaravelBox = CreateOptionRow(
                "Install Laravel Installer",
                "Runs composer global require laravel/installer.",
                options.InstallLaravel
            );
            AddPageControl(page, installLaravelBox.Parent, 560, 22);
            installValetBox = CreateOptionRow(
                "Install Valet for Windows",
                "Runs composer global require ycodetech/valet-windows, then valet install.",
                options.InstallValet
            );
            AddPageControl(page, installValetBox.Parent, 560, 100);
            AddPageControl(page, CreateNoteCard("Composer will be selected automatically when Laravel Installer or Valet is enabled."), 560, 190);
            return page;
        }

        private Control CreateReviewPage()
        {
            Panel page = CreateWideWizardPage();
            AddPageControl(page, CreateSummaryGrid("Review", "Confirm the exact command plan before anything changes on the machine."), 22, 22);

            RoundedPanel reviewCard = new RoundedPanel();
            reviewCard.Width = WizardContentWidth;
            reviewCard.Height = 300;
            reviewCard.Margin = new Padding(0, 0, 0, 12);
            reviewCard.FillColor = Color.FromArgb(13, 14, 20);
            reviewCard.BorderColor = BorderColor;
            reviewCard.Radius = 8;
            reviewCard.BackColor = Color.FromArgb(9, 10, 15);

            reviewSummaryLabel = CreateFloatingLabel(string.Empty, 9.1F, FontStyle.Regular, TextColor, 18, 14, 480, 270);
            reviewSummaryLabel.Font = CreateMonoFont(8.9F);
            reviewSummaryLabel.TextAlign = ContentAlignment.TopLeft;
            reviewCard.Controls.Add(reviewSummaryLabel);
            AddPageControl(page, reviewCard, 560, 22);
            AddPageControl(page, CreateNoteCard("Click Install only when this summary matches what you want for this Windows machine."), 22, 126);
            return page;
        }

        private Control CreateRunningPage()
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Running", "PowerShell is hidden. The animated installer panel and log appear on this final step only."));
            page.Controls.Add(CreateNoteCard("You can cancel while the process is running. Closing the window will ask before stopping the installer."));
            return page;
        }

        private static FlowLayoutPanel CreateWizardPage()
        {
            FlowLayoutPanel page = new FlowLayoutPanel();
            page.FlowDirection = FlowDirection.TopDown;
            page.WrapContents = false;
            page.AutoScroll = false;
            page.BackColor = Color.FromArgb(9, 10, 15);
            page.Padding = new Padding(22, 22, 22, 18);
            return page;
        }

        private static Panel CreateWideWizardPage()
        {
            Panel page = new Panel();
            page.BackColor = Color.FromArgb(9, 10, 15);
            page.AutoScroll = false;
            return page;
        }

        private static void AddPageControl(Control page, Control control, int left, int top)
        {
            control.Location = new Point(left, top);
            control.Margin = new Padding(0);
            page.Controls.Add(control);
        }

        private static void StretchCard(Control control, int width)
        {
            control.Width = width;

            foreach (Control child in control.Controls)
            {
                if (child is Label && child.Left > 0)
                {
                    child.Width = Math.Max(40, width - child.Left - 24);
                }
            }
        }

        private static Control CreateSummaryGrid(string title, string body)
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 88;
            card.Margin = new Padding(0, 0, 0, 12);
            card.FillColor = Color.FromArgb(13, 14, 20);
            card.BorderColor = BorderColor;
            card.Radius = 8;
            card.BackColor = Color.FromArgb(9, 10, 15);

            Panel accent = new Panel();
            accent.BackColor = AccentColor;
            accent.Location = new Point(18, 18);
            accent.Size = new Size(4, 50);
            card.Controls.Add(accent);

            card.Controls.Add(CreateFloatingLabel(title, 13F, FontStyle.Bold, TextColor, 34, 16, 450, 28));
            card.Controls.Add(CreateFloatingLabel(body, 9F, FontStyle.Regular, MutedTextColor, 34, 46, 454, 34));
            return card;
        }

        private static Control CreateNoteCard(string text)
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 66;
            card.Margin = new Padding(0, 0, 0, 12);
            card.FillColor = Color.FromArgb(7, 25, 34);
            card.BorderColor = Color.FromArgb(21, 94, 117);
            card.Radius = 8;
            card.BackColor = Color.FromArgb(9, 10, 15);
            card.Controls.Add(CreateFloatingLabel(text, 9.2F, FontStyle.Regular, Color.FromArgb(186, 230, 253), 18, 12, 480, 40));
            return card;
        }

        private static Control CreateDetectedToolsCard()
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 82;
            card.Margin = new Padding(0, 0, 0, 12);
            card.FillColor = Color.FromArgb(42, 29, 11);
            card.BorderColor = Color.FromArgb(245, 158, 11);
            card.Radius = 8;
            card.BackColor = Color.FromArgb(9, 10, 15);

            card.Controls.Add(CreateFloatingLabel("Detected installed tooling", 10.2F, FontStyle.Bold, Color.FromArgb(252, 211, 77), 18, 10, 460, 24));
            card.Controls.Add(CreateFloatingLabel(BuildDetectedToolsMessage(), 8.8F, FontStyle.Regular, Color.FromArgb(253, 230, 138), 18, 36, 478, 40));
            return card;
        }

        private static string BuildDetectedToolsMessage()
        {
            List<string> installed = new List<string>();

            AddDetectedTool(installed, "php", "PHP");
            AddDetectedTool(installed, "composer", "Composer");
            AddDetectedTool(installed, "laravel", "Laravel Installer");
            AddDetectedTool(installed, "valet", "Valet");
            AddDetectedTool(installed, "starship", "Starship");

            if (installed.Count == 0)
            {
                return "No PHP/Laravel tooling was detected on PATH. You can install it in this wizard.";
            }

            return "Already on PATH: " + string.Join(", ", installed.ToArray()) + ". Enable the update option to refresh selected tooling.";
        }

        private static void AddDetectedTool(List<string> installed, string command, string label)
        {
            if (!string.IsNullOrWhiteSpace(FindCommand(command)))
            {
                installed.Add(label);
            }
        }

        private static string FindCommand(string command)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/c where " + command;
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    string output = process.StandardOutput.ReadLine();
                    bool exited = process.WaitForExit(1500);

                    if (!exited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        return string.Empty;
                    }

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        return output.Trim();
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static Control CreateTextField(string title, string value, out TextBox textBox)
        {
            Panel panel = new Panel();
            panel.Width = WizardContentWidth;
            panel.Height = 78;
            panel.Margin = new Padding(0, 0, 0, 12);
            panel.BackColor = Color.FromArgb(9, 10, 15);

            Label label = CreateFloatingLabel(title, 8.8F, FontStyle.Bold, TextColor, 0, 0, 460, 22);
            panel.Controls.Add(label);

            RoundedPanel field = new RoundedPanel();
            field.Location = new Point(0, 28);
            field.Size = new Size(500, 42);
            field.FillColor = FieldColor;
            field.BorderColor = BorderColor;
            field.Radius = 7;
            field.BackColor = panel.BackColor;

            textBox = new TextBox();
            textBox.BackColor = FieldColor;
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = CreateFont(9.5F, FontStyle.Regular);
            textBox.ForeColor = TextColor;
            textBox.Location = new Point(14, 13);
            textBox.Size = new Size(470, 19);
            textBox.Text = value;
            field.Controls.Add(textBox);

            panel.Controls.Add(field);
            return panel;
        }

        private static Control CreatePathPicker(string title, string value, EventHandler browseHandler, out TextBox textBox)
        {
            Panel panel = new Panel();
            panel.Width = WizardContentWidth;
            panel.Height = 78;
            panel.Margin = new Padding(0, 0, 0, 12);
            panel.BackColor = Color.FromArgb(9, 10, 15);

            Label label = CreateFloatingLabel(title, 8.8F, FontStyle.Bold, TextColor, 0, 0, 460, 22);
            panel.Controls.Add(label);

            RoundedPanel picker = new RoundedPanel();
            picker.Location = new Point(0, 28);
            picker.Size = new Size(500, 42);
            picker.FillColor = FieldColor;
            picker.BorderColor = BorderColor;
            picker.Radius = 7;
            picker.BackColor = panel.BackColor;

            textBox = new TextBox();
            textBox.BackColor = FieldColor;
            textBox.BorderStyle = BorderStyle.None;
            textBox.Font = CreateFont(9.5F, FontStyle.Regular);
            textBox.ForeColor = TextColor;
            textBox.Location = new Point(14, 13);
            textBox.Size = new Size(350, 19);
            textBox.Text = value;
            picker.Controls.Add(textBox);

            RoundedPanel browseButton = new RoundedPanel();
            browseButton.Location = new Point(386, 1);
            browseButton.Size = new Size(113, 40);
            browseButton.FillColor = SurfaceAltColor;
            browseButton.BorderColor = Color.Transparent;
            browseButton.Radius = 6;
            browseButton.BackColor = FieldColor;
            browseButton.Cursor = Cursors.Hand;
            browseButton.Click += browseHandler;

            Label browseLabel = CreateFloatingLabel("Browse", 9.2F, FontStyle.Bold, TextColor, 0, 0, 113, 40);
            browseLabel.TextAlign = ContentAlignment.MiddleCenter;
            browseLabel.Cursor = Cursors.Hand;
            browseLabel.Click += browseHandler;
            browseButton.Controls.Add(browseLabel);
            picker.Controls.Add(browseButton);

            Panel separator = new Panel();
            separator.BackColor = BorderColor;
            separator.Location = new Point(384, 8);
            separator.Size = new Size(1, 26);
            picker.Controls.Add(separator);
            separator.BringToFront();

            panel.Controls.Add(picker);
            return panel;
        }

        private ModernCheckBox CreateOptionRow(string title, string description, bool isChecked)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Width = WizardContentWidth;
            panel.Height = 70;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.BackColor = Color.FromArgb(9, 10, 15);
            panel.FillColor = SurfaceAltColor;
            panel.BorderColor = BorderColor;
            panel.Radius = 8;
            panel.Padding = new Padding(12);

            ModernCheckBox checkbox = new ModernCheckBox();
            checkbox.AutoSize = false;
            checkbox.Checked = isChecked;
            checkbox.CheckedColor = AccentColor;
            checkbox.BorderColor = BorderColor;
            checkbox.SurfaceColor = FieldColor;
            checkbox.TickColor = Color.White;
            checkbox.Location = new Point(14, 16);
            checkbox.Size = new Size(28, 28);
            checkbox.Text = string.Empty;
            checkbox.UseVisualStyleBackColor = false;
            panel.Controls.Add(checkbox);

            Label titleLabel = CreateFloatingLabel(title, 10.2F, FontStyle.Bold, TextColor, 52, 10, 430, 24);
            panel.Controls.Add(titleLabel);

            Label descriptionLabel = CreateFloatingLabel(description, 8.7F, FontStyle.Regular, MutedTextColor, 52, 36, 432, 24);
            panel.Controls.Add(descriptionLabel);

            EventHandler updateCard = delegate
            {
                panel.FillColor = checkbox.Checked ? SelectedSurfaceColor : SurfaceAltColor;
                panel.BorderColor = checkbox.Checked ? AccentColor : BorderColor;
                checkbox.BackColor = panel.FillColor;
                checkbox.SurfaceColor = checkbox.Checked ? AccentColor : FieldColor;
                panel.Invalidate();
                checkbox.Invalidate();
                if (reviewSummaryLabel != null)
                {
                    UpdateReviewSummary();
                }
            };
            checkbox.CheckedChanged += updateCard;
            updateCard(checkbox, EventArgs.Empty);

            panel.Click += delegate { checkbox.Checked = !checkbox.Checked; };
            titleLabel.Click += delegate { checkbox.Checked = !checkbox.Checked; };
            descriptionLabel.Click += delegate { checkbox.Checked = !checkbox.Checked; };

            return checkbox;
        }

        private FlowLayoutPanel CreateWizardNav()
        {
            FlowLayoutPanel nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Fill;
            nav.FlowDirection = FlowDirection.RightToLeft;
            nav.WrapContents = false;
            nav.BackColor = SurfaceColor;
            nav.Padding = new Padding(22, 8, 22, 10);

            installButton = CreatePrimaryButton("Install");
            installButton.Width = 132;
            installButton.Click += InstallButtonClick;
            nav.Controls.Add(installButton);

            nextButton = CreatePrimaryButton("Next");
            nextButton.Width = 118;
            nextButton.Click += delegate
            {
                if (!TermsAccepted() && currentStep == 0)
                {
                    return;
                }

                if (currentStep < StepCount - 1)
                {
                    currentStep++;
                    UpdateWizard();
                }
            };
            nav.Controls.Add(nextButton);

            backButton = CreateSecondaryButton("Back");
            backButton.Width = 118;
            backButton.Click += delegate
            {
                if (currentStep > 0)
                {
                    currentStep--;
                    UpdateWizard();
                }
            };
            nav.Controls.Add(backButton);

            return nav;
        }

        private static Control CreateStatusBadge(string text)
        {
            Label badge = new Label();
            badge.AutoSize = false;
            badge.Text = text;
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Font = CreateFont(8.5F, FontStyle.Bold);
            badge.ForeColor = Color.FromArgb(206, 232, 221);
            badge.BackColor = Color.FromArgb(28, 63, 55);
            badge.Width = Math.Max(92, text.Length * 8 + 28);
            badge.Height = 28;
            badge.Margin = new Padding(0, 0, 8, 0);
            return badge;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Text = text;
            label.Font = CreateFont(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Label CreateFloatingLabel(string text, float size, FontStyle style, Color color, int left, int top, int width, int height)
        {
            Label label = CreateLabel(text, size, style, color);
            label.Dock = DockStyle.None;
            label.Location = new Point(left, top);
            label.Size = new Size(width, height);
            return label;
        }

        private static Button CreatePrimaryButton(string text)
        {
            Button button = CreateButton(text);
            ApplyButtonPalette(
                button,
                new ButtonPalette(
                    AccentColor,
                    Color.White,
                    AccentColor,
                    Color.FromArgb(139, 92, 246),
                    Color.FromArgb(91, 33, 182)
                )
            );
            return button;
        }

        private static Button CreateSecondaryButton(string text)
        {
            Button button = CreateButton(text);
            ApplyButtonPalette(
                button,
                new ButtonPalette(
                    SurfaceAltColor,
                    TextColor,
                    BorderColor,
                    Color.FromArgb(29, 30, 40),
                    Color.FromArgb(18, 19, 27)
                )
            );
            return button;
        }

        private static Button CreateTextButton(string text)
        {
            Button button = CreateButton(text);
            ApplyButtonPalette(
                button,
                new ButtonPalette(
                    SurfaceColor,
                    MutedTextColor,
                    BorderColor,
                    Color.FromArgb(18, 19, 27),
                    Color.FromArgb(12, 13, 19)
                )
            );
            return button;
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 112;
            button.Height = 42;
            button.Margin = new Padding(0, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = CreateFont(9.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static void ApplyButtonPalette(Button button, ButtonPalette palette)
        {
            button.Tag = palette;
            ApplyButtonEnabledState(button);
            button.EnabledChanged += delegate { ApplyButtonEnabledState(button); };
        }

        private static void ApplyButtonEnabledState(Button button)
        {
            ButtonPalette palette = button.Tag as ButtonPalette;

            if (palette == null)
            {
                return;
            }

            if (button.Enabled)
            {
                button.BackColor = palette.BackColor;
                button.ForeColor = palette.ForeColor;
                button.FlatAppearance.BorderColor = palette.BorderColor;
                button.FlatAppearance.MouseOverBackColor = palette.HoverColor;
                button.FlatAppearance.MouseDownBackColor = palette.DownColor;
                button.Cursor = Cursors.Hand;
                return;
            }

            button.BackColor = DisabledSurfaceColor;
            button.ForeColor = DisabledTextColor;
            button.FlatAppearance.BorderColor = Color.FromArgb(48, 50, 62);
            button.FlatAppearance.MouseOverBackColor = DisabledSurfaceColor;
            button.FlatAppearance.MouseDownBackColor = DisabledSurfaceColor;
            button.Cursor = Cursors.Default;
        }

        private void ToolingOptionChanged(object sender, EventArgs e)
        {
            if ((installLaravelBox.Checked || installValetBox.Checked) && !installComposerBox.Checked)
            {
                installComposerBox.Checked = true;
            }
        }

        private void ScopeOptionChanged(object sender, EventArgs e)
        {
            if (syncingScopeOptions)
            {
                return;
            }

            syncingScopeOptions = true;

            if (sender == allUsersScopeBox && allUsersScopeBox.Checked)
            {
                currentUserScopeBox.Checked = false;
            }
            else if (sender == currentUserScopeBox && currentUserScopeBox.Checked)
            {
                allUsersScopeBox.Checked = false;
            }

            if (!currentUserScopeBox.Checked && !allUsersScopeBox.Checked)
            {
                currentUserScopeBox.Checked = true;
            }

            syncingScopeOptions = false;
            UpdateReviewSummary();
        }

        private void BrowseStartingDirectoryClick(object sender, EventArgs e)
        {
            BrowseDirectory(startingDirectoryBox, "Choose the default starting directory for the terminal profile.");
        }

        private void BrowsePhpDirectoryClick(object sender, EventArgs e)
        {
            BrowseDirectory(phpDirectoryBox, "Choose the folder that contains php.exe.");
        }

        private void BrowseDirectory(TextBox target, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.SelectedPath = Directory.Exists(target.Text)
                    ? target.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.SelectedPath;
                    UpdateReviewSummary();
                }
            }
        }

        private void UpdateWizard()
        {
            if (currentStep < 0)
            {
                currentStep = 0;
            }

            if (currentStep >= StepCount)
            {
                currentStep = StepCount - 1;
            }

            wizardTitleLabel.Text = wizardTitles[currentStep];
            wizardDescriptionLabel.Text = wizardDescriptions[currentStep];

            for (int index = 0; index < wizardPages.Count; index++)
            {
                wizardPages[index].Visible = index == currentStep;
            }

            for (int index = 0; index < StepCount; index++)
            {
                bool isActive = index == currentStep;
                bool isComplete = index < currentStep;
                stepLabels[index].BackColor = isActive ? AccentColor : isComplete ? Color.FromArgb(16, 43, 52) : SurfaceAltColor;
                stepLabels[index].ForeColor = isActive ? Color.White : TextColor;
            }

            bool terminalVisible = currentStep == StepCount - 1 || IsInstalling();
            bool isReviewStep = currentStep == StepCount - 2;
            bool canLeaveTerms = currentStep != 0 || TermsAccepted();

            if (wizardPanel != null && runPanel != null && bodyLayout != null)
            {
                wizardPanel.Visible = !terminalVisible;
                runPanel.Visible = terminalVisible;
                bodyLayout.ColumnStyles[0].SizeType = SizeType.Absolute;
                bodyLayout.ColumnStyles[0].Width = SidebarWidth;
                bodyLayout.ColumnStyles[1].SizeType = SizeType.Percent;
                bodyLayout.ColumnStyles[1].Width = terminalVisible ? 0F : 100F;
                bodyLayout.ColumnStyles[2].SizeType = SizeType.Percent;
                bodyLayout.ColumnStyles[2].Width = terminalVisible ? 100F : 0F;
            }

            backButton.Enabled = currentStep > 0 && currentStep < StepCount - 1 && !IsInstalling();
            nextButton.Visible = currentStep < StepCount - 2;
            nextButton.Enabled = !IsInstalling() && canLeaveTerms;
            installButton.Visible = isReviewStep;
            installButton.Enabled = isReviewStep && !IsInstalling();
            cancelButton.Visible = terminalVisible;
            trustPanel.Visible = terminalVisible;
            progressBar.Visible = terminalVisible;
            logBox.Visible = terminalVisible;
            guidePanel.Visible = false;
            actionPanel.Visible = terminalVisible;
            runningVisualPanel.Visible = terminalVisible;
            runningVisualPanel.IsRunning = IsInstalling();
            runTitleLabel.Text = terminalVisible ? "Running Installer" : "Wizard Progress";
            runTextLabel.Text = terminalVisible
                ? "PowerShell is hidden. The visual installer stays animated while the script runs."
                : "Choose options in the wizard. The command log appears only on the Running step.";

            if (isReviewStep)
            {
                UpdateReviewSummary();
                statusLabel.Text = "Ready to install";
            }
            else if (currentStep == StepCount - 1)
            {
                if (IsInstalling())
                {
                    statusLabel.Text = "Installing...";
                    runningVisualPanel.StatusText = "Now installing...";
                }
                else if (installHasRun && ExitCode == 0)
                {
                    statusLabel.Text = "Installed successfully";
                    runningVisualPanel.StatusText = "Installed successfully";
                }
                else if (installHasRun)
                {
                    statusLabel.Text = "Installation failed";
                    runningVisualPanel.StatusText = "Installation needs attention";
                }
                else
                {
                    statusLabel.Text = "Ready to run";
                    runningVisualPanel.StatusText = "Ready to install";
                }
            }
            else
            {
                statusLabel.Text = currentStep == 0 && !TermsAccepted()
                    ? "Accept terms to continue"
                    : "Step " + (currentStep + 1) + " of " + StepCount + ": " + stepTitles[currentStep];
            }
        }

        private bool IsInstalling()
        {
            return installerProcess != null && !installerProcess.HasExited;
        }

        private bool TermsAccepted()
        {
            return termsAcceptedBox != null && termsAcceptedBox.Checked;
        }

        private void UpdateReviewSummary()
        {
            if (reviewSummaryLabel == null)
            {
                return;
            }

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Terminal profile");
            summary.AppendLine("  Display name:       " + CleanValue(displayNameBox.Text, Environment.UserName));
            summary.AppendLine("  Install scope:      " + GetInstallScopeLabel());
            summary.AppendLine("  Starting directory: " + CleanValue(startingDirectoryBox.Text, "User profile"));
            summary.AppendLine("  Starship install:   " + YesNo(installStarshipBox.Checked));
            summary.AppendLine();
            summary.AppendLine("Runtime");
            summary.AppendLine("  Install PHP 8.4:    " + YesNo(installPhpBox.Checked));
            summary.AppendLine("  Existing PHP path:  " + CleanValue(phpDirectoryBox.Text, "Not selected"));
            summary.AppendLine("  Composer:           " + YesNo(installComposerBox.Checked));
            summary.AppendLine();
            summary.AppendLine("Laravel tooling");
            summary.AppendLine("  Update installed:   " + YesNo(updateToolsBox.Checked));
            summary.AppendLine("  Laravel Installer:  " + YesNo(installLaravelBox.Checked));
            summary.AppendLine("  Valet for Windows:  " + YesNo(installValetBox.Checked));
            reviewSummaryLabel.Text = summary.ToString();
        }

        private static string CleanValue(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return value.Trim();
        }

        private static string YesNo(bool value)
        {
            return value ? "Yes" : "No";
        }

        private string GetInstallScopeArgument()
        {
            return allUsersScopeBox != null && allUsersScopeBox.Checked ? "AllUsers" : "CurrentUser";
        }

        private string GetInstallScopeLabel()
        {
            return string.Equals(GetInstallScopeArgument(), "AllUsers", StringComparison.Ordinal)
                ? "All users"
                : "Current user";
        }

        private void InstallButtonClick(object sender, EventArgs e)
        {
            if (currentStep != StepCount - 2)
            {
                currentStep = StepCount - 2;
                UpdateWizard();
                return;
            }

            StartInstall();
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            if (installerProcess == null || installerProcess.HasExited)
            {
                return;
            }

            AppendLog("Cancel requested. Stopping the installer process.", WarningColor);
            statusLabel.Text = "Cancelling...";
            cancelButton.Enabled = false;

            try
            {
                installerProcess.Kill();
            }
            catch (Exception ex)
            {
                AppendLog("Could not stop installer: " + ex.Message, DangerColor);
            }
        }

        private void StartInstall()
        {
            if (IsInstalling())
            {
                return;
            }

            if (!File.Exists(installerPath))
            {
                ExitCode = 1;
                statusLabel.Text = "Installer script missing";
                AppendLog("install.ps1 was not found next to this executable.", DangerColor);
                return;
            }

            string startingDirectory = startingDirectoryBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(startingDirectory))
            {
                startingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                startingDirectoryBox.Text = startingDirectory;
            }

            if (string.IsNullOrWhiteSpace(displayNameBox.Text))
            {
                displayNameBox.Text = ResolveDisplayName(string.Empty);
            }

            currentStep = StepCount - 1;
            installHasRun = true;
            UpdateWizard();
            SetWizardEnabled(false);
            installButton.Enabled = false;
            cancelButton.Enabled = true;
            closeButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 24;
            statusLabel.Text = "Installing...";
            AppendLog("Starting installation.", AccentColor);
            AppendSelectedOptions();
            CloseTerminalWindowsBeforeInstall();

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetPowerShellPath();
            startInfo.Arguments = BuildPowerShellArguments(startingDirectory);
            startInfo.WorkingDirectory = repositoryRoot;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            installerProcess = new Process();
            installerProcess.StartInfo = startInfo;
            installerProcess.EnableRaisingEvents = true;
            installerProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    AppendLog(e.Data, ColorForLine(e.Data, false));
                }
            };
            installerProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    AppendLog(e.Data, ColorForLine(e.Data, true));
                }
            };
            installerProcess.Exited += InstallerProcessExited;

            try
            {
                installerProcess.Start();
                installerProcess.BeginOutputReadLine();
                installerProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                FinishInstall(1);
                AppendLog("Could not start PowerShell installer: " + ex.Message, DangerColor);
            }
        }

        private void SetWizardEnabled(bool enabled)
        {
            backButton.Enabled = enabled && currentStep > 0;
            nextButton.Enabled = enabled && currentStep < StepCount - 1;

            for (int index = 0; index < stepLabels.Length; index++)
            {
                stepLabels[index].Enabled = enabled;
            }
        }

        private void CloseTerminalWindowsBeforeInstall()
        {
            AppendLog("Closing open terminal windows before installation.", MutedTextColor);

            int currentProcessId = Process.GetCurrentProcess().Id;
            HashSet<int> seenProcessIds = new HashSet<int>();
            List<Process> terminalProcesses = new List<Process>();

            foreach (string processName in TerminalProcessNames)
            {
                Process[] processes;

                try
                {
                    processes = Process.GetProcessesByName(processName);
                }
                catch
                {
                    continue;
                }

                foreach (Process process in processes)
                {
                    if (process.Id == currentProcessId || seenProcessIds.Contains(process.Id))
                    {
                        process.Dispose();
                        continue;
                    }

                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        process.Dispose();
                        continue;
                    }

                    seenProcessIds.Add(process.Id);
                    terminalProcesses.Add(process);
                }
            }

            if (terminalProcesses.Count == 0)
            {
                AppendLog("No open terminal windows were detected.", MutedTextColor);
                return;
            }

            int closedCount = 0;
            int forcedCount = 0;

            foreach (Process process in terminalProcesses)
            {
                try
                {
                    AppendLog("Closing terminal: " + process.ProcessName + " (" + process.Id + ")", MutedTextColor);
                    process.CloseMainWindow();
                    closedCount++;

                    if (!process.WaitForExit(2500) && !process.HasExited)
                    {
                        process.Kill();
                        forcedCount++;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Could not close " + process.ProcessName + ": " + ex.Message, WarningColor);
                }
                finally
                {
                    process.Dispose();
                }
            }

            AppendLog(
                forcedCount > 0
                    ? "Closed " + closedCount + " terminal window(s); forced " + forcedCount + " to exit."
                    : "Closed " + closedCount + " terminal window(s).",
                MutedTextColor
            );
        }

        private void AppendSelectedOptions()
        {
            AppendLog("Selected setup:", MutedTextColor);
            AppendLog("  Terminal profile: yes", MutedTextColor);
            AppendLog("  Display name: " + CleanValue(displayNameBox.Text, Environment.UserName), MutedTextColor);
            AppendLog("  Install scope: " + GetInstallScopeLabel(), MutedTextColor);
            AppendLog("  Starting directory: " + CleanValue(startingDirectoryBox.Text, "User profile"), MutedTextColor);

            if (installPhpBox.Checked)
            {
                AppendLog("  PHP 8.4: install with winget", MutedTextColor);
            }

            if (!string.IsNullOrWhiteSpace(phpDirectoryBox.Text))
            {
                AppendLog("  PHP directory: " + phpDirectoryBox.Text.Trim(), MutedTextColor);
            }

            if (installComposerBox.Checked)
            {
                AppendLog("  Composer: install or verify", MutedTextColor);
            }

            if (updateToolsBox.Checked)
            {
                AppendLog("  Update existing tooling: yes", MutedTextColor);
            }

            if (installLaravelBox.Checked)
            {
                AppendLog("  Laravel Installer: install globally", MutedTextColor);
            }

            if (installValetBox.Checked)
            {
                AppendLog("  Valet for Windows: install globally", MutedTextColor);
            }

            if (installStarshipBox.Checked)
            {
                AppendLog("  Starship: install if missing", MutedTextColor);
            }
        }

        private static string GetPowerShellPath()
        {
            string powershellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"
            );

            if (File.Exists(powershellPath))
            {
                return powershellPath;
            }

            return "powershell.exe";
        }

        private string BuildPowerShellArguments(string startingDirectory)
        {
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Quote(installerPath));
            arguments.Add("-StartingDirectory");
            arguments.Add(Quote(startingDirectory));
            arguments.Add("-InstallScope");
            arguments.Add(GetInstallScopeArgument());
            arguments.Add("-DisplayName");
            arguments.Add(Quote(CleanValue(displayNameBox.Text, Environment.UserName)));

            if (installStarshipBox.Checked)
            {
                arguments.Add("-InstallDependencies");
            }

            if (installPhpBox.Checked)
            {
                arguments.Add("-InstallPhp");
            }

            string phpDirectory = phpDirectoryBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(phpDirectory))
            {
                arguments.Add("-PhpDirectory");
                arguments.Add(Quote(phpDirectory));
            }

            if (installComposerBox.Checked)
            {
                arguments.Add("-InstallComposer");
            }

            if (updateToolsBox.Checked)
            {
                arguments.Add("-UpdateTools");
            }

            if (installLaravelBox.Checked)
            {
                arguments.Add("-InstallLaravel");
            }

            if (installValetBox.Checked)
            {
                arguments.Add("-InstallValet");
            }

            return string.Join(" ", arguments.ToArray());
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static Color ColorForLine(string line, bool isError)
        {
            if (isError)
            {
                return DangerColor;
            }

            if (line.IndexOf("Warning:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return WarningColor;
            }

            if (line.IndexOf("Done.", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("installed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AccentColor;
            }

            return TextColor;
        }

        private void InstallerProcessExited(object sender, EventArgs e)
        {
            int exitCode = 1;

            try
            {
                exitCode = installerProcess.ExitCode;
            }
            catch
            {
                exitCode = 1;
            }

            if (IsHandleCreated)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    FinishInstall(exitCode);
                }));
            }
        }

        private void FinishInstall(int exitCode)
        {
            ExitCode = exitCode;
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.Value = exitCode == 0 ? 100 : 0;
            SetWizardEnabled(true);
            cancelButton.Enabled = false;
            closeButton.Enabled = true;

            if (exitCode == 0)
            {
                statusLabel.Text = "Installed successfully";
                AppendLog("Installation complete. Open a new Windows Terminal tab to see it.", AccentColor);
            }
            else
            {
                statusLabel.Text = "Installation failed";
                AppendLog("Installer exited with code " + exitCode + ".", DangerColor);
            }

            if (installerProcess != null)
            {
                installerProcess.Dispose();
                installerProcess = null;
            }

            UpdateWizard();
        }

        private void AppendLog(string message, Color color)
        {
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new MethodInvoker(delegate
                {
                    AppendLog(message, color);
                }));
                return;
            }

            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = color;
            logBox.AppendText(message + Environment.NewLine);
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }

        private void InstallerFormClosing(object sender, FormClosingEventArgs e)
        {
            if (installerProcess == null || installerProcess.HasExited)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "The installer is still running. Stop it and close?",
                "MBS Terminal Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                installerProcess.Kill();
            }
            catch
            {
            }
        }
    }

    internal class RoundedPanel : Panel
    {
        public RoundedPanel()
        {
            DoubleBuffered = true;
            FillColor = BackColor;
            BorderColor = Color.Transparent;
            Radius = 8;
        }

        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public int Radius { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = CreateRoundRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor, 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernCheckBox : CheckBox
    {
        public ModernCheckBox()
        {
            DoubleBuffered = true;
            CheckedColor = Color.FromArgb(34, 197, 94);
            BorderColor = Color.FromArgb(58, 60, 74);
            SurfaceColor = Color.FromArgb(12, 19, 34);
            TickColor = Color.FromArgb(8, 25, 17);
            Cursor = Cursors.Hand;
        }

        public Color CheckedColor { get; set; }
        public Color BorderColor { get; set; }
        public Color SurfaceColor { get; set; }
        public Color TickColor { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Rectangle box = new Rectangle(3, 3, 22, 22);

            using (GraphicsPath path = CreateRoundRectangle(box, 5))
            using (SolidBrush brush = new SolidBrush(Checked ? CheckedColor : SurfaceColor))
            using (Pen pen = new Pen(Checked ? CheckedColor : BorderColor, 1.5F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (Checked)
            {
                using (Pen tick = new Pen(TickColor, 2.2F))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    e.Graphics.DrawLines(tick, new[]
                    {
                        new Point(8, 15),
                        new Point(13, 20),
                        new Point(21, 9)
                    });
                }
            }
        }

        private static GraphicsPath CreateRoundRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class AnimatedAccentPanel : Panel
    {
        public AnimatedAccentPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 10, 15);
            StatusText = "Now installing...";
        }

        public float Phase { get; set; }
        public bool IsRunning { get; set; }
        public string StatusText { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath cardPath = CreateRoundRectangle(bounds, 8))
            using (LinearGradientBrush cardBrush = new LinearGradientBrush(bounds, Color.FromArgb(8, 8, 12), Color.FromArgb(16, 10, 26), LinearGradientMode.Horizontal))
            using (Pen border = new Pen(Color.FromArgb(58, 60, 74), 1F))
            {
                e.Graphics.FillPath(cardBrush, cardPath);
                e.Graphics.DrawPath(border, cardPath);
            }

            float wave = (float)Math.Sin(Phase) * 18F;
            PointF[] purpleShape =
            {
                new PointF(Width * 0.48F + wave, 0),
                new PointF(Width, 0),
                new PointF(Width, Height),
                new PointF(Width * 0.63F - wave, Height)
            };

            using (LinearGradientBrush purpleBrush = new LinearGradientBrush(bounds, Color.FromArgb(88, 28, 135), Color.FromArgb(126, 34, 206), LinearGradientMode.Vertical))
            {
                e.Graphics.FillPolygon(purpleBrush, purpleShape);
            }

            using (SolidBrush orange = new SolidBrush(Color.FromArgb(220, 255, 121, 48)))
            using (SolidBrush pink = new SolidBrush(Color.FromArgb(190, 236, 30, 111)))
            using (SolidBrush violet = new SolidBrush(Color.FromArgb(165, 91, 33, 182)))
            {
                e.Graphics.FillEllipse(orange, Width * 0.43F + wave, Height * 0.25F, 160, 220);
                e.Graphics.FillEllipse(pink, Width * 0.48F - wave, Height * 0.43F, 210, 170);
                e.Graphics.FillEllipse(violet, Width * 0.56F + wave * 0.4F, Height * 0.02F, 260, 300);
            }

            using (Font titleFont = new Font(Font.FontFamily, 20F, FontStyle.Bold))
            using (Font bodyFont = new Font(Font.FontFamily, 10.5F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                e.Graphics.DrawString("Optimizing your", titleFont, titleBrush, 36, 44);
                e.Graphics.DrawString("developer setup.", titleFont, titleBrush, 36, 74);
                e.Graphics.DrawString("MBS Terminal is preparing your selected tools with PowerShell hidden in the background.", bodyFont, bodyBrush, new RectangleF(38, 120, Width * 0.38F, 68));
            }

            Rectangle track = new Rectangle(Math.Max(Width - 390, 520), Height / 2 - 12, 280, 16);
            using (GraphicsPath trackPath = CreateRoundRectangle(track, 3))
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(36, 37, 48)))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            int segmentWidth;
            int segmentLeft;

            if (!IsRunning && StatusText.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                segmentWidth = track.Width;
                segmentLeft = track.Left;
            }
            else if (!IsRunning && StatusText.IndexOf("attention", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                segmentWidth = track.Width;
                segmentLeft = track.Left;
            }
            else
            {
                float progress = Phase - (float)Math.Floor(Phase);
                segmentWidth = 84;
                segmentLeft = track.Left + (int)((track.Width - segmentWidth) * progress);
            }

            Rectangle segment = new Rectangle(segmentLeft, track.Top, segmentWidth, track.Height);
            using (GraphicsPath segmentPath = CreateRoundRectangle(segment, 3))
            using (LinearGradientBrush progressBrush = new LinearGradientBrush(segment, Color.FromArgb(14, 165, 233), Color.FromArgb(168, 85, 247), LinearGradientMode.Horizontal))
            {
                e.Graphics.FillPath(progressBrush, segmentPath);
            }

            using (Font statusFont = new Font(Font.FontFamily, 10.5F, FontStyle.Regular))
            using (SolidBrush statusBrush = new SolidBrush(Color.White))
            {
                e.Graphics.DrawString(StatusText, statusFont, statusBrush, track.Left + 30, track.Bottom + 16);
            }
        }

        private static GraphicsPath CreateRoundRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ButtonPalette
    {
        public ButtonPalette(Color backColor, Color foreColor, Color borderColor, Color hoverColor, Color downColor)
        {
            BackColor = backColor;
            ForeColor = foreColor;
            BorderColor = borderColor;
            HoverColor = hoverColor;
            DownColor = downColor;
        }

        public Color BackColor { get; private set; }
        public Color ForeColor { get; private set; }
        public Color BorderColor { get; private set; }
        public Color HoverColor { get; private set; }
        public Color DownColor { get; private set; }
    }

    internal sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            DoubleBuffered = true;
        }

        public Color StartColor { get; set; }
        public Color EndColor { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                ClientRectangle,
                StartColor,
                EndColor,
                LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(46, 124, 58, 237)))
            {
                e.Graphics.FillEllipse(overlay, Width - 250, -120, 380, 280);
            }

            using (SolidBrush cyan = new SolidBrush(Color.FromArgb(36, 14, 165, 233)))
            {
                e.Graphics.FillEllipse(cyan, Width - 370, 56, 220, 130);
            }

            using (Pen line = new Pen(Color.FromArgb(80, 58, 60, 74), 1F))
            {
                e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
            }

            base.OnPaint(e);
        }
    }
}
