using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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

                if (IsSwitch(argument, "StartingDirectory") && index + 1 < args.Length)
                {
                    options.StartingDirectory = args[index + 1];
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
        private static readonly Color BackgroundColor = Color.FromArgb(9, 14, 23);
        private static readonly Color SurfaceColor = Color.FromArgb(17, 24, 39);
        private static readonly Color SurfaceAltColor = Color.FromArgb(22, 32, 48);
        private static readonly Color BorderColor = Color.FromArgb(45, 58, 77);
        private static readonly Color TextColor = Color.FromArgb(235, 241, 247);
        private static readonly Color MutedTextColor = Color.FromArgb(152, 163, 179);
        private static readonly Color AccentColor = Color.FromArgb(44, 190, 146);
        private static readonly Color AccentAltColor = Color.FromArgb(80, 145, 255);
        private static readonly Color WarningColor = Color.FromArgb(245, 181, 84);
        private static readonly Color DangerColor = Color.FromArgb(248, 113, 113);

        private readonly string repositoryRoot;
        private readonly string installerPath;
        private readonly TextBox startingDirectoryBox;
        private readonly CheckBox installDependenciesBox;
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

            ExitCode = 0;
            Text = "MBS Terminal Setup";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 640);
            Size = new Size(980, 700);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            TableLayoutPanel body = CreateBodyLayout();
            Controls.Add(body);

            GradientHeader header = CreateHeader();
            Controls.Add(header);

            Panel optionsPanel = CreatePanel();
            body.Controls.Add(optionsPanel, 0, 0);

            TableLayoutPanel optionsLayout = CreateOptionsLayout();
            optionsPanel.Controls.Add(optionsLayout);

            Label optionsTitle = CreateLabel("Install Options", 16F, FontStyle.Bold, TextColor);
            optionsTitle.Height = 34;
            optionsLayout.Controls.Add(optionsTitle, 0, 0);

            Label optionsText = CreateLabel(
                "Choose where the terminal profile opens and whether the installer should try to fetch missing dependencies.",
                9F,
                FontStyle.Regular,
                MutedTextColor
            );
            optionsText.Height = 54;
            optionsLayout.Controls.Add(optionsText, 0, 1);

            Label directoryLabel = CreateLabel("Starting Directory", 9F, FontStyle.Bold, TextColor);
            directoryLabel.Height = 24;
            optionsLayout.Controls.Add(directoryLabel, 0, 2);

            TableLayoutPanel directoryLayout = new TableLayoutPanel();
            directoryLayout.ColumnCount = 2;
            directoryLayout.RowCount = 1;
            directoryLayout.Dock = DockStyle.Fill;
            directoryLayout.Margin = new Padding(0, 0, 0, 16);
            directoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            directoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));

            startingDirectoryBox = new TextBox();
            startingDirectoryBox.BorderStyle = BorderStyle.FixedSingle;
            startingDirectoryBox.BackColor = Color.FromArgb(12, 18, 28);
            startingDirectoryBox.ForeColor = TextColor;
            startingDirectoryBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            startingDirectoryBox.Dock = DockStyle.Fill;
            startingDirectoryBox.Margin = new Padding(0, 0, 10, 0);
            startingDirectoryBox.Text = ResolveStartingDirectory(options.StartingDirectory);
            directoryLayout.Controls.Add(startingDirectoryBox, 0, 0);

            Button browseButton = CreateSecondaryButton("Browse...");
            browseButton.Dock = DockStyle.Fill;
            browseButton.Click += BrowseButtonClick;
            directoryLayout.Controls.Add(browseButton, 1, 0);
            optionsLayout.Controls.Add(directoryLayout, 0, 3);

            installDependenciesBox = new CheckBox();
            installDependenciesBox.Text = "Install missing dependencies with winget";
            installDependenciesBox.Checked = options.InstallDependencies;
            installDependenciesBox.AutoSize = false;
            installDependenciesBox.Height = 36;
            installDependenciesBox.Dock = DockStyle.Fill;
            installDependenciesBox.FlatStyle = FlatStyle.Flat;
            installDependenciesBox.ForeColor = TextColor;
            installDependenciesBox.Margin = new Padding(0, 2, 0, 18);
            optionsLayout.Controls.Add(installDependenciesBox, 0, 4);

            Panel actionsPanel = new Panel();
            actionsPanel.Dock = DockStyle.Fill;
            actionsPanel.Margin = new Padding(0, 0, 0, 16);

            installButton = CreatePrimaryButton("Install");
            installButton.Width = 132;
            installButton.Height = 42;
            installButton.Location = new Point(0, 2);
            installButton.Click += InstallButtonClick;
            actionsPanel.Controls.Add(installButton);

            cancelButton = CreateSecondaryButton("Cancel");
            cancelButton.Width = 112;
            cancelButton.Height = 42;
            cancelButton.Left = installButton.Right + 10;
            cancelButton.Top = 2;
            cancelButton.Enabled = false;
            cancelButton.Click += CancelButtonClick;
            actionsPanel.Controls.Add(cancelButton);

            closeButton = CreateTextButton("Close");
            closeButton.Width = 94;
            closeButton.Height = 42;
            closeButton.Left = cancelButton.Right + 10;
            closeButton.Top = 2;
            closeButton.Click += delegate { Close(); };
            actionsPanel.Controls.Add(closeButton);

            optionsLayout.Controls.Add(actionsPanel, 0, 5);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 14;
            progressBar.Margin = new Padding(0, 2, 0, 18);
            progressBar.Style = ProgressBarStyle.Blocks;
            optionsLayout.Controls.Add(progressBar, 0, 6);

            statusLabel = CreateLabel("Ready to install", 10F, FontStyle.Bold, TextColor);
            statusLabel.Height = 36;
            optionsLayout.Controls.Add(statusLabel, 0, 7);

            Label installNote = CreateLabel(
                "The installer updates your Windows Terminal profile, Starship config, terminal icons, and PowerShell helpers.",
                9F,
                FontStyle.Regular,
                MutedTextColor
            );
            installNote.Height = 62;
            optionsLayout.Controls.Add(installNote, 0, 8);

            Panel logPanel = CreatePanel();
            body.Controls.Add(logPanel, 1, 0);

            TableLayoutPanel logLayout = new TableLayoutPanel();
            logLayout.ColumnCount = 1;
            logLayout.RowCount = 2;
            logLayout.Dock = DockStyle.Fill;
            logLayout.BackColor = SurfaceColor;
            logLayout.Padding = new Padding(20);
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            logPanel.Controls.Add(logLayout);

            Label logTitle = CreateLabel("Installer Log", 16F, FontStyle.Bold, TextColor);
            logTitle.Height = 34;
            logLayout.Controls.Add(logTitle, 0, 0);

            logBox = new RichTextBox();
            logBox.BackColor = Color.FromArgb(7, 10, 17);
            logBox.BorderStyle = BorderStyle.None;
            logBox.Dock = DockStyle.Fill;
            logBox.ForeColor = TextColor;
            logBox.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.ReadOnly = true;
            logBox.Margin = new Padding(0);
            logBox.DetectUrls = false;
            logLayout.Controls.Add(logBox, 0, 1);

            FormClosing += InstallerFormClosing;

            AppendLog("Ready. The setup will run without opening a terminal window.", MutedTextColor);
        }

        public int ExitCode { get; private set; }

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

        private GradientHeader CreateHeader()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 178;
            header.Padding = new Padding(30, 26, 30, 24);

            Label eyebrow = CreateLabel("MBS TERMINAL", 9F, FontStyle.Bold, Color.FromArgb(184, 232, 219));
            eyebrow.Dock = DockStyle.None;
            eyebrow.Location = new Point(32, 24);
            eyebrow.Size = new Size(420, 24);
            header.Controls.Add(eyebrow);

            Label title = CreateLabel("Modern Setup", 30F, FontStyle.Bold, Color.White);
            title.Dock = DockStyle.None;
            title.Location = new Point(30, 48);
            title.Size = new Size(520, 54);
            header.Controls.Add(title);

            Label subtitle = CreateLabel("Install the Windows Terminal theme, prompt, icons, and Laravel helpers from a desktop installer.", 11F, FontStyle.Regular, Color.FromArgb(213, 224, 234));
            subtitle.Dock = DockStyle.None;
            subtitle.Location = new Point(34, 105);
            subtitle.Size = new Size(650, 30);
            header.Controls.Add(subtitle);

            AddPill(header, "Windows Terminal", 34, 139, AccentColor);
            AddPill(header, "Starship", 178, 139, AccentAltColor);
            AddPill(header, "Laravel", 270, 139, WarningColor);

            Panel glassCard = new Panel();
            glassCard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            glassCard.BackColor = Color.FromArgb(31, 42, 60);
            glassCard.Location = new Point(734, 34);
            glassCard.Size = new Size(190, 110);
            header.Controls.Add(glassCard);

            Label cardTitle = CreateLabel("No Console", 17F, FontStyle.Bold, Color.White);
            cardTitle.Dock = DockStyle.None;
            cardTitle.Location = new Point(18, 18);
            cardTitle.Size = new Size(150, 32);
            glassCard.Controls.Add(cardTitle);

            Label cardText = CreateLabel("Progress and output stay inside this app.", 9F, FontStyle.Regular, Color.FromArgb(197, 210, 222));
            cardText.Dock = DockStyle.None;
            cardText.Location = new Point(20, 54);
            cardText.Size = new Size(148, 42);
            glassCard.Controls.Add(cardText);

            return header;
        }

        private static void AddPill(Control parent, string text, int left, int top, Color color)
        {
            Label pill = new Label();
            pill.AutoSize = false;
            pill.Text = text;
            pill.TextAlign = ContentAlignment.MiddleCenter;
            pill.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            pill.BackColor = Color.FromArgb(38, color);
            pill.ForeColor = Color.White;
            pill.Location = new Point(left, top);
            pill.Size = new Size(Math.Max(82, text.Length * 8 + 26), 27);
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
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61F));
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

        private static TableLayoutPanel CreateOptionsLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 9;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = SurfaceColor;
            layout.Padding = new Padding(22);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return layout;
        }

        private static Label CreateLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Text = text;
            label.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Button CreatePrimaryButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = AccentColor;
            button.ForeColor = Color.FromArgb(4, 15, 13);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 212, 171);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(32, 164, 124);
            return button;
        }

        private static Button CreateSecondaryButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = SurfaceAltColor;
            button.ForeColor = TextColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 45, 64);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 35, 51);
            return button;
        }

        private static Button CreateTextButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = SurfaceColor;
            button.ForeColor = MutedTextColor;
            button.FlatAppearance.BorderColor = SurfaceColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 33, 47);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(18, 27, 41);
            return button;
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void BrowseButtonClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the default starting directory for the terminal profile.";
                dialog.SelectedPath = Directory.Exists(startingDirectoryBox.Text)
                    ? startingDirectoryBox.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    startingDirectoryBox.Text = dialog.SelectedPath;
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

            if (installDependenciesBox.Checked)
            {
                arguments.Add("-InstallDependencies");
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

            if (line.IndexOf("Done.", StringComparison.OrdinalIgnoreCase) >= 0)
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

    internal sealed class GradientHeader : Panel
    {
        public GradientHeader()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(10, 24, 39),
                Color.FromArgb(39, 63, 88),
                LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(52, 44, 190, 146)))
            {
                e.Graphics.FillEllipse(overlay, Width - 260, -120, 380, 280);
            }

            using (Pen line = new Pen(Color.FromArgb(70, 255, 255, 255), 1F))
            {
                e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
            }

            base.OnPaint(e);
        }
    }
}
