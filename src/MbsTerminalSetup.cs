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
            InstallerOptions options = InstallerOptions.FromArgs(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InstallerForm form = new InstallerForm(options);
            Application.Run(form);

            return form.ExitCode;
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
                }
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
        private const int WizardContentWidth = 520;
        private const int StepCount = 5;

        private static readonly Color BackgroundColor = Color.FromArgb(15, 23, 42);
        private static readonly Color HeaderStartColor = Color.FromArgb(8, 14, 26);
        private static readonly Color HeaderEndColor = Color.FromArgb(19, 58, 72);
        private static readonly Color SurfaceColor = Color.FromArgb(27, 35, 54);
        private static readonly Color SurfaceAltColor = Color.FromArgb(39, 47, 66);
        private static readonly Color SelectedSurfaceColor = Color.FromArgb(24, 48, 49);
        private static readonly Color FieldColor = Color.FromArgb(12, 19, 34);
        private static readonly Color BorderColor = Color.FromArgb(71, 85, 105);
        private static readonly Color TextColor = Color.FromArgb(248, 250, 252);
        private static readonly Color MutedTextColor = Color.FromArgb(148, 163, 184);
        private static readonly Color AccentColor = Color.FromArgb(34, 197, 94);
        private static readonly Color AccentAltColor = Color.FromArgb(59, 130, 246);
        private static readonly Color WarningColor = Color.FromArgb(245, 158, 11);
        private static readonly Color DangerColor = Color.FromArgb(239, 68, 68);

        private readonly string repositoryRoot;
        private readonly string installerPath;
        private readonly string iconPath;
        private readonly List<Control> wizardPages = new List<Control>();
        private readonly Label[] stepLabels = new Label[StepCount];
        private readonly string[] stepTitles =
        {
            "Profile",
            "Runtime",
            "Tooling",
            "Review",
            "Running"
        };
        private readonly string[] wizardTitles =
        {
            "Terminal Profile",
            "PHP Runtime",
            "Select Other Tooling",
            "Review & Install",
            "Running Installer"
        };
        private readonly string[] wizardDescriptions =
        {
            "Set the Windows Terminal starting folder and optional prompt dependency.",
            "Install PHP automatically, use an existing PHP folder, and prepare Composer.",
            "See what is already installed, then choose Laravel, Valet, and update behavior.",
            "Confirm the plan before anything changes on the machine.",
            "PowerShell runs hidden here, with progress and command output visible."
        };

        private TextBox startingDirectoryBox;
        private TextBox phpDirectoryBox;
        private ModernCheckBox installStarshipBox;
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

        public InstallerForm(InstallerOptions options)
        {
            repositoryRoot = GetRepositoryRoot();
            installerPath = Path.Combine(repositoryRoot, "install.ps1");
            iconPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-pixel-avatar.png");

            ExitCode = 0;
            Text = "MBS Terminal Setup";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 760);
            Size = new Size(1240, 820);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = CreateFont(9F, FontStyle.Regular);
            Icon = LoadWindowIcon();

            TableLayoutPanel shell = CreateShellLayout();
            Controls.Add(shell);

            HeaderPanel header = CreateHeader();
            shell.Controls.Add(header, 0, 0);

            TableLayoutPanel body = CreateBodyLayout();
            shell.Controls.Add(body, 0, 1);

            Panel wizardPanel = CreatePanel();
            body.Controls.Add(wizardPanel, 0, 0);

            TableLayoutPanel wizardLayout = CreateWizardLayout();
            wizardPanel.Controls.Add(wizardLayout);

            wizardLayout.Controls.Add(CreateStepper(), 0, 0);

            Panel titlePanel = new Panel();
            titlePanel.Dock = DockStyle.Fill;
            titlePanel.BackColor = SurfaceColor;
            titlePanel.Margin = new Padding(22, 8, 22, 0);
            wizardTitleLabel = CreateLabel(string.Empty, 20F, FontStyle.Bold, TextColor);
            wizardTitleLabel.Location = new Point(0, 0);
            wizardTitleLabel.Size = new Size(WizardContentWidth, 36);
            wizardTitleLabel.Dock = DockStyle.None;
            titlePanel.Controls.Add(wizardTitleLabel);

            wizardDescriptionLabel = CreateLabel(string.Empty, 9.5F, FontStyle.Regular, MutedTextColor);
            wizardDescriptionLabel.Location = new Point(0, 38);
            wizardDescriptionLabel.Size = new Size(WizardContentWidth, 28);
            wizardDescriptionLabel.Dock = DockStyle.None;
            titlePanel.Controls.Add(wizardDescriptionLabel);
            wizardLayout.Controls.Add(titlePanel, 0, 1);

            RoundedPanel wizardHost = new RoundedPanel();
            wizardHost.Dock = DockStyle.Fill;
            wizardHost.Margin = new Padding(22, 10, 22, 12);
            wizardHost.FillColor = Color.FromArgb(21, 30, 47);
            wizardHost.BorderColor = Color.FromArgb(54, 68, 91);
            wizardHost.Radius = 8;
            wizardHost.Padding = new Padding(1);
            wizardLayout.Controls.Add(wizardHost, 0, 2);

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

            Panel runPanel = CreatePanel();
            body.Controls.Add(runPanel, 1, 0);

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
            runLayout.Controls.Add(trustPanel, 0, 2);

            actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.LeftToRight;
            actionPanel.WrapContents = false;
            actionPanel.BackColor = SurfaceColor;
            actionPanel.Margin = new Padding(0, 4, 0, 12);
            runLayout.Controls.Add(actionPanel, 0, 3);

            installButton = CreatePrimaryButton("Install");
            installButton.Width = 136;
            installButton.Click += InstallButtonClick;
            actionPanel.Controls.Add(installButton);

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
            logBox.BackColor = Color.FromArgb(6, 10, 17);
            logBox.BorderStyle = BorderStyle.None;
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

            AppendLog("Wizard ready. Configure each step, then install.", MutedTextColor);
            AppendLog("Tip: if you already use Laragon, XAMPP, or a custom PHP build, select its PHP folder instead of installing PHP.", MutedTextColor);

            UpdateWizard();

            Shown += delegate
            {
                startingDirectoryBox.SelectionLength = 0;
                phpDirectoryBox.SelectionLength = 0;
                ActiveControl = nextButton;
            };
        }

        public int ExitCode { get; private set; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

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
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 162F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return shell;
        }

        private HeaderPanel CreateHeader()
        {
            HeaderPanel header = new HeaderPanel();
            header.Dock = DockStyle.Fill;
            header.StartColor = HeaderStartColor;
            header.EndColor = HeaderEndColor;
            header.Padding = new Padding(30, 24, 30, 24);

            PictureBox icon = new PictureBox();
            icon.BackColor = Color.Transparent;
            icon.SizeMode = PictureBoxSizeMode.Zoom;
            icon.Location = new Point(32, 32);
            icon.Size = new Size(82, 82);

            if (File.Exists(iconPath))
            {
                icon.Image = Image.FromFile(iconPath);
            }

            header.Controls.Add(icon);

            header.Controls.Add(CreateFloatingLabel("MBS TERMINAL / DEV TOOLCHAIN", 9F, FontStyle.Bold, Color.FromArgb(194, 239, 226), 132, 30, 520, 24));
            header.Controls.Add(CreateFloatingLabel("Laravel Ready Wizard", 31F, FontStyle.Bold, Color.White, 128, 54, 600, 54));
            header.Controls.Add(CreateFloatingLabel("A staged desktop setup for your terminal profile, PHP runtime, Composer, Laravel, and Valet for Windows.", 10.5F, FontStyle.Regular, Color.FromArgb(216, 227, 238), 132, 108, 840, 28));

            AddPill(header, "Dark Minimal", 790, 40, AccentColor);
            AddPill(header, "Wizard Flow", 910, 40, AccentAltColor);
            AddPill(header, "WCAG AA", 1030, 40, WarningColor);

            return header;
        }

        private static void AddPill(Control parent, string text, int left, int top, Color color)
        {
            Label pill = new Label();
            pill.AutoSize = false;
            pill.Text = text;
            pill.TextAlign = ContentAlignment.MiddleCenter;
            pill.Font = CreateFont(8.5F, FontStyle.Bold);
            pill.BackColor = Color.FromArgb(42, color);
            pill.ForeColor = Color.White;
            pill.Location = new Point(left, top);
            pill.Size = new Size(Math.Max(62, text.Length * 9 + 26), 30);
            parent.Controls.Add(pill);
        }

        private TableLayoutPanel CreateBodyLayout()
        {
            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = BackgroundColor;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.Padding = new Padding(18);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return body;
        }

        private static Panel CreatePanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = BackgroundColor;
            panel.FillColor = SurfaceColor;
            panel.BorderColor = Color.FromArgb(57, 72, 96);
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            return layout;
        }

        private TableLayoutPanel CreateStepper()
        {
            TableLayoutPanel stepper = new TableLayoutPanel();
            stepper.ColumnCount = StepCount;
            stepper.RowCount = 1;
            stepper.Dock = DockStyle.Fill;
            stepper.BackColor = SurfaceColor;
            stepper.Padding = new Padding(22, 20, 22, 8);

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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return layout;
        }

        private static Control CreateRunGuidePanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.FillColor = FieldColor;
            panel.BorderColor = Color.FromArgb(54, 68, 91);
            panel.Radius = 8;
            panel.BackColor = SurfaceColor;
            panel.Margin = new Padding(0);

            panel.Controls.Add(CreateFloatingLabel("No command output yet", 14F, FontStyle.Bold, TextColor, 20, 22, 440, 32));
            panel.Controls.Add(CreateFloatingLabel("Complete Profile, Runtime, Tooling, and Review. When you click Install, the wizard moves to Running and opens the terminal-style log here.", 9.4F, FontStyle.Regular, MutedTextColor, 20, 62, 460, 74));
            return panel;
        }

        private Control CreateProfilePage(InstallerOptions options)
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Profile", "Terminal theme, PowerShell profile helpers, icons, Starship config, and Windows Terminal settings."));
            page.Controls.Add(CreatePathPicker("Starting directory", ResolveStartingDirectory(options.StartingDirectory), BrowseStartingDirectoryClick, out startingDirectoryBox));
            installStarshipBox = CreateOptionRow(
                "Install missing Starship with winget",
                "Keeps the terminal prompt fully themed when Starship is not already installed.",
                options.InstallDependencies
            );
            page.Controls.Add(installStarshipBox.Parent);
            return page;
        }

        private Control CreateRuntimePage(InstallerOptions options)
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Runtime", "Choose how PHP and Composer should be prepared for Laravel tooling."));
            installPhpBox = CreateOptionRow(
                "Install PHP 8.4",
                "Uses winget package PHP.PHP.8.4.",
                options.InstallPhp
            );
            page.Controls.Add(installPhpBox.Parent);
            page.Controls.Add(CreatePathPicker("Existing PHP directory", options.PhpDirectory ?? string.Empty, BrowsePhpDirectoryClick, out phpDirectoryBox));
            installComposerBox = CreateOptionRow(
                "Install Composer",
                "Downloads Composer-Setup.exe and runs it silently with your PHP selection.",
                options.InstallComposer
            );
            page.Controls.Add(installComposerBox.Parent);
            return page;
        }

        private Control CreateLaravelPage(InstallerOptions options)
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Tooling", "Install global developer tools after Composer is ready."));
            page.Controls.Add(CreateDetectedToolsCard());
            updateToolsBox = CreateOptionRow(
                "Update tooling that is already installed",
                "Refreshes PHP with winget and Composer with self-update when selected.",
                options.UpdateTools
            );
            page.Controls.Add(updateToolsBox.Parent);
            installLaravelBox = CreateOptionRow(
                "Install Laravel Installer",
                "Runs composer global require laravel/installer.",
                options.InstallLaravel
            );
            page.Controls.Add(installLaravelBox.Parent);
            installValetBox = CreateOptionRow(
                "Install Valet for Windows",
                "Runs composer global require ycodetech/valet-windows, then valet install.",
                options.InstallValet
            );
            page.Controls.Add(installValetBox.Parent);
            page.Controls.Add(CreateNoteCard("Composer will be selected automatically when Laravel Installer or Valet is enabled."));
            return page;
        }

        private Control CreateReviewPage()
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Review", "Confirm the exact command plan before anything changes on the machine."));

            RoundedPanel reviewCard = new RoundedPanel();
            reviewCard.Width = WizardContentWidth;
            reviewCard.Height = 250;
            reviewCard.Margin = new Padding(0, 0, 0, 12);
            reviewCard.FillColor = FieldColor;
            reviewCard.BorderColor = Color.FromArgb(54, 68, 91);
            reviewCard.Radius = 8;
            reviewCard.BackColor = Color.FromArgb(21, 30, 47);

            reviewSummaryLabel = CreateFloatingLabel(string.Empty, 9.4F, FontStyle.Regular, TextColor, 18, 16, 480, 216);
            reviewSummaryLabel.Font = CreateMonoFont(9.2F);
            reviewSummaryLabel.TextAlign = ContentAlignment.TopLeft;
            reviewCard.Controls.Add(reviewSummaryLabel);
            page.Controls.Add(reviewCard);
            page.Controls.Add(CreateNoteCard("Click Install only when this summary matches what you want for this Windows machine."));
            return page;
        }

        private Control CreateRunningPage()
        {
            FlowLayoutPanel page = CreateWizardPage();
            page.Controls.Add(CreateSummaryGrid("Running", "The installer is running separately now. Watch the log panel on the right for live output."));
            page.Controls.Add(CreateNoteCard("You can cancel while the process is running. Closing the window will ask before stopping the installer."));
            return page;
        }

        private static FlowLayoutPanel CreateWizardPage()
        {
            FlowLayoutPanel page = new FlowLayoutPanel();
            page.FlowDirection = FlowDirection.TopDown;
            page.WrapContents = false;
            page.AutoScroll = true;
            page.BackColor = Color.FromArgb(21, 30, 47);
            page.Padding = new Padding(22, 22, 22, 18);
            return page;
        }

        private static Control CreateSummaryGrid(string title, string body)
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 92;
            card.Margin = new Padding(0, 0, 0, 14);
            card.FillColor = Color.FromArgb(18, 27, 43);
            card.BorderColor = Color.FromArgb(54, 68, 91);
            card.Radius = 8;
            card.BackColor = Color.FromArgb(21, 30, 47);

            Panel accent = new Panel();
            accent.BackColor = AccentColor;
            accent.Location = new Point(18, 20);
            accent.Size = new Size(4, 50);
            card.Controls.Add(accent);

            card.Controls.Add(CreateFloatingLabel(title, 13F, FontStyle.Bold, TextColor, 34, 18, 450, 28));
            card.Controls.Add(CreateFloatingLabel(body, 9F, FontStyle.Regular, MutedTextColor, 34, 48, 454, 34));
            return card;
        }

        private static Control CreateNoteCard(string text)
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 72;
            card.Margin = new Padding(0, 0, 0, 12);
            card.FillColor = Color.FromArgb(28, 47, 58);
            card.BorderColor = Color.FromArgb(53, 88, 104);
            card.Radius = 8;
            card.BackColor = Color.FromArgb(21, 30, 47);
            card.Controls.Add(CreateFloatingLabel(text, 9.2F, FontStyle.Regular, Color.FromArgb(210, 232, 238), 18, 14, 480, 40));
            return card;
        }

        private static Control CreateDetectedToolsCard()
        {
            RoundedPanel card = new RoundedPanel();
            card.Width = WizardContentWidth;
            card.Height = 96;
            card.Margin = new Padding(0, 0, 0, 12);
            card.FillColor = Color.FromArgb(55, 43, 25);
            card.BorderColor = Color.FromArgb(135, 94, 38);
            card.Radius = 8;
            card.BackColor = Color.FromArgb(21, 30, 47);

            card.Controls.Add(CreateFloatingLabel("Detected installed tooling", 10.2F, FontStyle.Bold, WarningColor, 18, 12, 460, 24));
            card.Controls.Add(CreateFloatingLabel(BuildDetectedToolsMessage(), 8.8F, FontStyle.Regular, Color.FromArgb(255, 224, 178), 18, 40, 478, 44));
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

        private static Control CreatePathPicker(string title, string value, EventHandler browseHandler, out TextBox textBox)
        {
            Panel panel = new Panel();
            panel.Width = WizardContentWidth;
            panel.Height = 78;
            panel.Margin = new Padding(0, 0, 0, 12);
            panel.BackColor = Color.FromArgb(21, 30, 47);

            Label label = CreateFloatingLabel(title, 8.8F, FontStyle.Bold, TextColor, 0, 0, 460, 22);
            panel.Controls.Add(label);

            TableLayoutPanel picker = new TableLayoutPanel();
            picker.ColumnCount = 2;
            picker.RowCount = 1;
            picker.Location = new Point(0, 28);
            picker.Size = new Size(500, 40);
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));

            textBox = new TextBox();
            textBox.BackColor = FieldColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Dock = DockStyle.Fill;
            textBox.Font = CreateFont(9.5F, FontStyle.Regular);
            textBox.ForeColor = TextColor;
            textBox.Margin = new Padding(0, 4, 10, 0);
            textBox.Text = value;
            picker.Controls.Add(textBox, 0, 0);

            Button browseButton = CreateSecondaryButton("Browse");
            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(0);
            browseButton.Click += browseHandler;
            picker.Controls.Add(browseButton, 1, 0);

            panel.Controls.Add(picker);
            return panel;
        }

        private ModernCheckBox CreateOptionRow(string title, string description, bool isChecked)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Width = WizardContentWidth;
            panel.Height = 74;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.BackColor = Color.FromArgb(21, 30, 47);
            panel.FillColor = SurfaceAltColor;
            panel.BorderColor = Color.FromArgb(54, 68, 91);
            panel.Radius = 8;
            panel.Padding = new Padding(12);

            ModernCheckBox checkbox = new ModernCheckBox();
            checkbox.AutoSize = false;
            checkbox.Checked = isChecked;
            checkbox.CheckedColor = AccentColor;
            checkbox.BorderColor = BorderColor;
            checkbox.SurfaceColor = FieldColor;
            checkbox.TickColor = Color.FromArgb(8, 25, 17);
            checkbox.Location = new Point(14, 18);
            checkbox.Size = new Size(28, 28);
            checkbox.Text = string.Empty;
            checkbox.UseVisualStyleBackColor = false;
            panel.Controls.Add(checkbox);

            Label titleLabel = CreateFloatingLabel(title, 10.2F, FontStyle.Bold, TextColor, 52, 12, 430, 24);
            panel.Controls.Add(titleLabel);

            Label descriptionLabel = CreateFloatingLabel(description, 8.7F, FontStyle.Regular, MutedTextColor, 52, 38, 432, 24);
            panel.Controls.Add(descriptionLabel);

            EventHandler updateCard = delegate
            {
                panel.FillColor = checkbox.Checked ? SelectedSurfaceColor : SurfaceAltColor;
                panel.BorderColor = checkbox.Checked ? AccentColor : Color.FromArgb(54, 68, 91);
                checkbox.BackColor = panel.FillColor;
                checkbox.SurfaceColor = checkbox.Checked ? Color.FromArgb(16, 72, 48) : FieldColor;
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

            nextButton = CreatePrimaryButton("Next");
            nextButton.Width = 118;
            nextButton.Click += delegate
            {
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
            button.BackColor = AccentColor;
            button.ForeColor = Color.FromArgb(4, 15, 13);
            button.FlatAppearance.BorderColor = AccentColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 222, 180);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(32, 169, 129);
            return button;
        }

        private static Button CreateSecondaryButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = SurfaceAltColor;
            button.ForeColor = TextColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 46, 66);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 35, 51);
            return button;
        }

        private static Button CreateTextButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = SurfaceColor;
            button.ForeColor = MutedTextColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 33, 47);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(18, 27, 41);
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

        private void ToolingOptionChanged(object sender, EventArgs e)
        {
            if ((installLaravelBox.Checked || installValetBox.Checked) && !installComposerBox.Checked)
            {
                installComposerBox.Checked = true;
            }
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
                stepLabels[index].BackColor = isActive ? AccentColor : isComplete ? Color.FromArgb(28, 63, 55) : SurfaceAltColor;
                stepLabels[index].ForeColor = isActive ? Color.FromArgb(4, 15, 13) : TextColor;
            }

            bool terminalVisible = currentStep == StepCount - 1 || IsInstalling();
            bool isReviewStep = currentStep == StepCount - 2;

            backButton.Enabled = currentStep > 0 && currentStep < StepCount - 1 && !IsInstalling();
            nextButton.Visible = currentStep < StepCount - 2;
            nextButton.Enabled = !IsInstalling();
            installButton.Visible = isReviewStep;
            installButton.Enabled = isReviewStep && !IsInstalling();
            cancelButton.Visible = terminalVisible;
            trustPanel.Visible = terminalVisible;
            progressBar.Visible = terminalVisible;
            logBox.Visible = terminalVisible;
            guidePanel.Visible = !terminalVisible;
            actionPanel.Visible = isReviewStep || terminalVisible;
            runTitleLabel.Text = terminalVisible ? "Running Installer" : "Wizard Progress";
            runTextLabel.Text = terminalVisible
                ? "PowerShell is hidden. Output from install.ps1 appears below."
                : "Choose options on the left. The command log appears only on the Running step.";

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
                }
                else if (installHasRun && ExitCode == 0)
                {
                    statusLabel.Text = "Installed successfully";
                }
                else if (installHasRun)
                {
                    statusLabel.Text = "Installation failed";
                }
                else
                {
                    statusLabel.Text = "Ready to run";
                }
            }
            else
            {
                statusLabel.Text = "Step " + (currentStep + 1) + " of " + StepCount + ": " + stepTitles[currentStep];
            }
        }

        private bool IsInstalling()
        {
            return installerProcess != null && !installerProcess.HasExited;
        }

        private void UpdateReviewSummary()
        {
            if (reviewSummaryLabel == null)
            {
                return;
            }

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Terminal profile");
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

        private void AppendSelectedOptions()
        {
            AppendLog("Selected setup:", MutedTextColor);
            AppendLog("  Terminal profile: yes", MutedTextColor);
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
            BorderColor = Color.FromArgb(71, 85, 105);
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

            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(50, 34, 197, 94)))
            {
                e.Graphics.FillEllipse(overlay, Width - 250, -120, 380, 280);
            }

            using (Pen line = new Pen(Color.FromArgb(70, 255, 255, 255), 1F))
            {
                e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
            }

            base.OnPaint(e);
        }
    }
}
