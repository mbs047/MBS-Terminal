using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
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
        private static readonly Color BackgroundColor = Color.FromArgb(8, 12, 20);
        private static readonly Color HeaderStartColor = Color.FromArgb(13, 25, 42);
        private static readonly Color HeaderEndColor = Color.FromArgb(30, 67, 88);
        private static readonly Color SurfaceColor = Color.FromArgb(16, 23, 36);
        private static readonly Color SurfaceAltColor = Color.FromArgb(22, 32, 48);
        private static readonly Color FieldColor = Color.FromArgb(10, 16, 26);
        private static readonly Color BorderColor = Color.FromArgb(44, 59, 80);
        private static readonly Color TextColor = Color.FromArgb(238, 244, 249);
        private static readonly Color MutedTextColor = Color.FromArgb(158, 171, 188);
        private static readonly Color AccentColor = Color.FromArgb(46, 204, 160);
        private static readonly Color AccentAltColor = Color.FromArgb(92, 153, 255);
        private static readonly Color WarningColor = Color.FromArgb(246, 190, 100);
        private static readonly Color DangerColor = Color.FromArgb(248, 113, 113);

        private readonly string repositoryRoot;
        private readonly string installerPath;
        private readonly string iconPath;
        private readonly TextBox startingDirectoryBox;
        private readonly TextBox phpDirectoryBox;
        private readonly CheckBox installStarshipBox;
        private readonly CheckBox installPhpBox;
        private readonly CheckBox installComposerBox;
        private readonly CheckBox installLaravelBox;
        private readonly CheckBox installValetBox;
        private readonly Button installButton;
        private readonly Button cancelButton;
        private readonly Button closeButton;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly RichTextBox logBox;

        private Process installerProcess;

        public InstallerForm(InstallerOptions options)
        {
            repositoryRoot = GetRepositoryRoot();
            installerPath = Path.Combine(repositoryRoot, "install.ps1");
            iconPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-pixel-avatar.png");

            ExitCode = 0;
            Text = "MBS Terminal Setup";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1040, 720);
            Size = new Size(1120, 780);
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

            Panel setupPanel = CreatePanel();
            body.Controls.Add(setupPanel, 0, 0);

            FlowLayoutPanel setupFlow = CreateSetupFlow();
            setupPanel.Controls.Add(setupFlow);

            setupFlow.Controls.Add(CreateSectionTitle("Terminal Profile", "Theme, icons, prompt, and the default folder for new tabs."));
            setupFlow.Controls.Add(CreatePathPicker("Starting directory", ResolveStartingDirectory(options.StartingDirectory), BrowseStartingDirectoryClick, out startingDirectoryBox));

            setupFlow.Controls.Add(CreateSectionTitle("PHP Runtime", "Install PHP automatically, or point the installer at an existing PHP folder."));
            installPhpBox = CreateOptionRow(
                "Install PHP 8.4",
                "Uses winget package PHP.PHP.8.4.",
                options.InstallPhp
            );
            setupFlow.Controls.Add(installPhpBox.Parent);
            setupFlow.Controls.Add(CreatePathPicker("Existing PHP directory", options.PhpDirectory ?? string.Empty, BrowsePhpDirectoryClick, out phpDirectoryBox));

            setupFlow.Controls.Add(CreateSectionTitle("Laravel Tooling", "Composer is required for the Laravel Installer and Valet for Windows."));
            installComposerBox = CreateOptionRow(
                "Install Composer",
                "Downloads Composer-Setup.exe and runs it silently with your PHP selection.",
                options.InstallComposer
            );
            setupFlow.Controls.Add(installComposerBox.Parent);

            installLaravelBox = CreateOptionRow(
                "Install Laravel Installer",
                "Runs composer global require laravel/installer.",
                options.InstallLaravel
            );
            setupFlow.Controls.Add(installLaravelBox.Parent);

            installValetBox = CreateOptionRow(
                "Install Valet for Windows",
                "Runs composer global require ycodetech/valet-windows, then valet install.",
                options.InstallValet
            );
            setupFlow.Controls.Add(installValetBox.Parent);

            setupFlow.Controls.Add(CreateSectionTitle("Prompt Dependency", "Starship is optional but recommended for the bundled prompt."));
            installStarshipBox = CreateOptionRow(
                "Install missing Starship with winget",
                "Keeps the terminal prompt fully themed when Starship is not already installed.",
                options.InstallDependencies
            );
            setupFlow.Controls.Add(installStarshipBox.Parent);

            installLaravelBox.CheckedChanged += ToolingOptionChanged;
            installValetBox.CheckedChanged += ToolingOptionChanged;

            Panel runPanel = CreatePanel();
            body.Controls.Add(runPanel, 1, 0);

            TableLayoutPanel runLayout = CreateRunLayout();
            runPanel.Controls.Add(runLayout);

            Label runTitle = CreateLabel("Install Run", 18F, FontStyle.Bold, TextColor);
            runLayout.Controls.Add(runTitle, 0, 0);

            Label runText = CreateLabel(
                "Review your choices, then run the setup. Progress and command output stay here.",
                9.5F,
                FontStyle.Regular,
                MutedTextColor
            );
            runLayout.Controls.Add(runText, 0, 1);

            FlowLayoutPanel actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.LeftToRight;
            actionPanel.WrapContents = false;
            actionPanel.BackColor = SurfaceColor;
            actionPanel.Margin = new Padding(0, 4, 0, 12);
            runLayout.Controls.Add(actionPanel, 0, 2);

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
            runLayout.Controls.Add(progressBar, 0, 3);

            statusLabel = CreateLabel("Ready to install", 11F, FontStyle.Bold, TextColor);
            runLayout.Controls.Add(statusLabel, 0, 4);

            logBox = new RichTextBox();
            logBox.BackColor = Color.FromArgb(6, 10, 17);
            logBox.BorderStyle = BorderStyle.None;
            logBox.Dock = DockStyle.Fill;
            logBox.ForeColor = TextColor;
            logBox.Font = CreateMonoFont(9F);
            logBox.ReadOnly = true;
            logBox.Margin = new Padding(0);
            logBox.DetectUrls = false;
            runLayout.Controls.Add(logBox, 0, 5);

            FormClosing += InstallerFormClosing;

            AppendLog("Ready. Select what this machine needs, then install.", MutedTextColor);
            AppendLog("Tip: if you already use Laragon, XAMPP, or a custom PHP build, select its PHP folder instead of installing PHP.", MutedTextColor);
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
            return new Font(ResolveFontFamily("Aptos", "Segoe UI Variable Display", "Segoe UI"), size, style, GraphicsUnit.Point);
        }

        private static Font CreateMonoFont(float size)
        {
            return new Font(ResolveFontFamily("Cascadia Mono", "Consolas", "Courier New"), size, FontStyle.Regular, GraphicsUnit.Point);
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

            Label eyebrow = CreateFloatingLabel("MBS TERMINAL", 9F, FontStyle.Bold, Color.FromArgb(194, 239, 226), 132, 30, 440, 24);
            header.Controls.Add(eyebrow);

            Label title = CreateFloatingLabel("Laravel Ready Setup", 30F, FontStyle.Bold, Color.White, 128, 54, 560, 54);
            header.Controls.Add(title);

            Label subtitle = CreateFloatingLabel("A desktop installer for your terminal profile, PHP runtime, Composer, Laravel, and Valet for Windows.", 10.5F, FontStyle.Regular, Color.FromArgb(216, 227, 238), 132, 108, 760, 28);
            header.Controls.Add(subtitle);

            AddPill(header, "PHP", 770, 40, AccentColor);
            AddPill(header, "Composer", 842, 40, AccentAltColor);
            AddPill(header, "Valet", 950, 40, WarningColor);

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
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return body;
        }

        private static Panel CreatePanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = SurfaceColor;
            panel.Margin = new Padding(10);
            panel.Padding = new Padding(1);
            return panel;
        }

        private static FlowLayoutPanel CreateSetupFlow()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.BackColor = SurfaceColor;
            flow.Padding = new Padding(20, 18, 20, 18);
            return flow;
        }

        private static TableLayoutPanel CreateRunLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = SurfaceColor;
            layout.Padding = new Padding(22);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return layout;
        }

        private static Control CreateSectionTitle(string title, string subtitle)
        {
            Panel panel = new Panel();
            panel.Width = 462;
            panel.Height = 72;
            panel.Margin = new Padding(0, 4, 0, 2);
            panel.BackColor = SurfaceColor;

            Label titleLabel = CreateFloatingLabel(title, 13F, FontStyle.Bold, TextColor, 0, 4, 420, 26);
            panel.Controls.Add(titleLabel);

            Label subtitleLabel = CreateFloatingLabel(subtitle, 8.8F, FontStyle.Regular, MutedTextColor, 0, 32, 440, 34);
            panel.Controls.Add(subtitleLabel);

            return panel;
        }

        private static Control CreatePathPicker(string title, string value, EventHandler browseHandler, out TextBox textBox)
        {
            Panel panel = new Panel();
            panel.Width = 462;
            panel.Height = 78;
            panel.Margin = new Padding(0, 0, 0, 12);
            panel.BackColor = SurfaceColor;

            Label label = CreateFloatingLabel(title, 8.8F, FontStyle.Bold, TextColor, 0, 0, 420, 22);
            panel.Controls.Add(label);

            TableLayoutPanel picker = new TableLayoutPanel();
            picker.ColumnCount = 2;
            picker.RowCount = 1;
            picker.Location = new Point(0, 28);
            picker.Size = new Size(440, 40);
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

        private CheckBox CreateOptionRow(string title, string description, bool isChecked)
        {
            Panel panel = new Panel();
            panel.Width = 462;
            panel.Height = 74;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.BackColor = SurfaceAltColor;
            panel.Padding = new Padding(12);

            CheckBox checkbox = new CheckBox();
            checkbox.AutoSize = false;
            checkbox.Checked = isChecked;
            checkbox.FlatStyle = FlatStyle.Flat;
            checkbox.ForeColor = TextColor;
            checkbox.Location = new Point(14, 18);
            checkbox.Size = new Size(28, 28);
            checkbox.Text = string.Empty;
            checkbox.UseVisualStyleBackColor = false;
            panel.Controls.Add(checkbox);

            Label titleLabel = CreateFloatingLabel(title, 10.2F, FontStyle.Bold, TextColor, 52, 12, 370, 24);
            panel.Controls.Add(titleLabel);

            Label descriptionLabel = CreateFloatingLabel(description, 8.7F, FontStyle.Regular, MutedTextColor, 52, 38, 378, 24);
            panel.Controls.Add(descriptionLabel);

            panel.Click += delegate
            {
                checkbox.Checked = !checkbox.Checked;
            };
            titleLabel.Click += delegate
            {
                checkbox.Checked = !checkbox.Checked;
            };
            descriptionLabel.Click += delegate
            {
                checkbox.Checked = !checkbox.Checked;
            };

            return checkbox;
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
                }
            }
        }

        private void InstallButtonClick(object sender, EventArgs e)
        {
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
            if (installerProcess != null && !installerProcess.HasExited)
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

        private void AppendSelectedOptions()
        {
            AppendLog("Selected setup:", MutedTextColor);
            AppendLog("  Terminal profile: yes", MutedTextColor);

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
            installButton.Enabled = true;
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

            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(50, 46, 204, 160)))
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
