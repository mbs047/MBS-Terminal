using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MbsTerminalRestore
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            RestoreForm form = new RestoreForm(args);
            Application.Run(form);

            return form.ExitCode;
        }
    }

    internal sealed class RestoreForm : Form
    {
        private static readonly Color BackgroundColor = Color.FromArgb(4, 4, 7);
        private static readonly Color SurfaceColor = Color.FromArgb(10, 10, 15);
        private static readonly Color SurfaceAltColor = Color.FromArgb(20, 20, 29);
        private static readonly Color FieldColor = Color.FromArgb(7, 7, 11);
        private static readonly Color BorderColor = Color.FromArgb(58, 60, 74);
        private static readonly Color TextColor = Color.FromArgb(248, 250, 252);
        private static readonly Color MutedTextColor = Color.FromArgb(154, 164, 178);
        private static readonly Color AccentColor = Color.FromArgb(168, 85, 247);
        private static readonly Color AccentAltColor = Color.FromArgb(34, 211, 238);
        private static readonly Color WarningColor = Color.FromArgb(245, 181, 84);
        private static readonly Color DangerColor = Color.FromArgb(248, 113, 113);

        private readonly string repositoryRoot;
        private readonly string restoreScriptPath;
        private readonly string iconPath;
        private readonly string avatarPath;
        private CheckBox keepStarshipBox;
        private Button restoreButton;
        private Button cancelButton;
        private Button closeButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private RichTextBox logBox;

        private Process restoreProcess;

        public RestoreForm(string[] args)
        {
            repositoryRoot = GetRepositoryRoot();
            restoreScriptPath = Path.Combine(repositoryRoot, "restore-default.ps1");
            iconPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-terminal.ico");
            avatarPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-pixel-avatar.png");

            ExitCode = 0;
            Text = "MBS Terminal Restore";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 620);
            Size = new Size(900, 660);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = LoadWindowIcon();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = BackgroundColor;
            layout.Padding = new Padding(24, 22, 24, 24);
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 222F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(layout);

            layout.Controls.Add(CreateHeroCard(), 0, 0);
            layout.Controls.Add(CreateRestoreCards(args), 0, 1);
            layout.Controls.Add(CreateActionsPanel(), 0, 2);
            layout.Controls.Add(CreateProgressPanel(), 0, 3);

            layout.Controls.Add(CreateLogPanel(), 0, 5);

            FormClosing += RestoreFormClosing;
            AppendLog("Ready. Backups are created before anything is changed.", MutedTextColor);
        }

        public int ExitCode { get; private set; }

        private static bool HasArgument(string[] args, string name)
        {
            foreach (string argument in args)
            {
                if (string.Equals(argument, "-" + name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "/" + name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        private Icon LoadWindowIcon()
        {
            try
            {
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch
            {
            }

            return Icon;
        }

        private Control CreateHeroCard()
        {
            RoundedPanel hero = CreateCard(12);
            hero.Dock = DockStyle.Fill;
            hero.Margin = new Padding(0, 0, 0, 14);
            hero.FillColor = SurfaceColor;

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.Transparent;
            content.Padding = new Padding(18);
            content.ColumnCount = 3;
            content.RowCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245F));
            hero.Controls.Add(content);

            PictureBox avatar = new PictureBox();
            avatar.Dock = DockStyle.Fill;
            avatar.Margin = new Padding(0, 4, 16, 4);
            avatar.SizeMode = PictureBoxSizeMode.Zoom;
            avatar.BackColor = Color.Transparent;

            if (File.Exists(avatarPath))
            {
                avatar.Image = Image.FromFile(avatarPath);
            }

            content.Controls.Add(avatar, 0, 0);

            Panel copy = new Panel();
            copy.Dock = DockStyle.Fill;
            copy.BackColor = Color.Transparent;
            content.Controls.Add(copy, 1, 0);

            copy.Controls.Add(CreateFloatingLabel("MBS Terminal Restore", 22F, FontStyle.Bold, TextColor, 0, 8, 430, 34));
            copy.Controls.Add(CreateFloatingLabel("Return Windows Terminal, PowerShell hooks, and MBS assets to a clean default state.", 9.5F, FontStyle.Regular, MutedTextColor, 2, 48, 500, 42));
            AddPill(copy, "Recovery mode", 2, 92, AccentColor);
            AddPill(copy, "Backups first", 124, 92, AccentAltColor);

            RoundedPanel safety = CreateCard(8);
            safety.Dock = DockStyle.Fill;
            safety.Margin = new Padding(12, 8, 0, 8);
            safety.FillColor = Color.FromArgb(13, 14, 20);
            content.Controls.Add(safety, 2, 0);

            safety.Controls.Add(CreateFloatingLabel("Safe restore", 11F, FontStyle.Bold, TextColor, 16, 14, 200, 24));
            safety.Controls.Add(CreateFloatingLabel("Timestamped backups are created before files are cleaned or moved.", 8.8F, FontStyle.Regular, MutedTextColor, 16, 42, 205, 42));

            return hero;
        }

        private Control CreateRestoreCards(string[] args)
        {
            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.BackColor = BackgroundColor;
            cards.ColumnCount = 2;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            RoundedPanel targetsCard = CreateCard(10);
            targetsCard.Dock = DockStyle.Fill;
            targetsCard.Margin = new Padding(0, 0, 12, 14);
            targetsCard.Controls.Add(CreateFloatingLabel("Restore targets", 14F, FontStyle.Bold, TextColor, 18, 14, 360, 28));
            targetsCard.Controls.Add(CreateFloatingLabel("The restore script resets only MBS-managed terminal customizations.", 9F, FontStyle.Regular, MutedTextColor, 18, 44, 420, 24));
            targetsCard.Controls.Add(CreateTargetRow("Windows Terminal settings", "Restores a plain default-style settings.json.", 18, 82, AccentColor));
            targetsCard.Controls.Add(CreateTargetRow("PowerShell startup hooks", "Removes MBS profile startup blocks safely.", 18, 124, AccentAltColor));
            targetsCard.Controls.Add(CreateTargetRow("MBS config and icons", "Moves helper files and icons into backups.", 18, 166, WarningColor));
            cards.Controls.Add(targetsCard, 0, 0);

            RoundedPanel optionsCard = CreateCard(10);
            optionsCard.Dock = DockStyle.Fill;
            optionsCard.Margin = new Padding(0, 0, 0, 14);
            optionsCard.Controls.Add(CreateFloatingLabel("Restore option", 14F, FontStyle.Bold, TextColor, 18, 14, 300, 28));
            optionsCard.Controls.Add(CreateFloatingLabel("Choose whether Starship should stay untouched during cleanup.", 9F, FontStyle.Regular, MutedTextColor, 18, 44, 285, 42));

            keepStarshipBox = new CheckBox();
            keepStarshipBox.AutoSize = false;
            keepStarshipBox.Text = "Keep existing Starship config";
            keepStarshipBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            keepStarshipBox.ForeColor = TextColor;
            keepStarshipBox.BackColor = SurfaceAltColor;
            keepStarshipBox.FlatStyle = FlatStyle.Flat;
            keepStarshipBox.FlatAppearance.BorderColor = BorderColor;
            keepStarshipBox.Checked = HasArgument(args, "KeepStarship");
            keepStarshipBox.Location = new Point(18, 98);
            keepStarshipBox.Size = new Size(292, 44);
            keepStarshipBox.Padding = new Padding(12, 0, 0, 0);
            optionsCard.Controls.Add(keepStarshipBox);

            optionsCard.Controls.Add(CreateFloatingLabel("When off, ~/.config/starship.toml is moved to a timestamped backup.", 8.8F, FontStyle.Regular, MutedTextColor, 20, 150, 290, 32));
            cards.Controls.Add(optionsCard, 1, 0);

            return cards;
        }

        private Control CreateTargetRow(string title, string body, int left, int top, Color color)
        {
            Panel row = new Panel();
            row.BackColor = Color.Transparent;
            row.Location = new Point(left, top);
            row.Size = new Size(430, 34);

            Panel marker = new Panel();
            marker.BackColor = color;
            marker.Location = new Point(0, 6);
            marker.Size = new Size(4, 22);
            row.Controls.Add(marker);

            row.Controls.Add(CreateFloatingLabel(title, 9.5F, FontStyle.Bold, TextColor, 16, 0, 360, 18));
            row.Controls.Add(CreateFloatingLabel(body, 8.5F, FontStyle.Regular, MutedTextColor, 16, 18, 390, 16));
            return row;
        }

        private Control CreateActionsPanel()
        {
            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.BackColor = BackgroundColor;
            actions.Padding = new Padding(0, 8, 0, 8);

            restoreButton = CreatePrimaryButton("Restore Defaults");
            restoreButton.Width = 170;
            restoreButton.Height = 40;
            restoreButton.Click += RestoreButtonClick;
            actions.Controls.Add(restoreButton);

            cancelButton = CreateSecondaryButton("Cancel");
            cancelButton.Width = 104;
            cancelButton.Height = 40;
            cancelButton.Enabled = false;
            cancelButton.Click += CancelButtonClick;
            actions.Controls.Add(cancelButton);

            closeButton = CreateTextButton("Close");
            closeButton.Width = 96;
            closeButton.Height = 40;
            closeButton.Click += delegate { Close(); };
            actions.Controls.Add(closeButton);

            return actions;
        }

        private Control CreateProgressPanel()
        {
            RoundedPanel panel = CreateCard(8);
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 10);
            panel.FillColor = Color.FromArgb(13, 14, 20);

            statusLabel = CreateFloatingLabel("Ready to restore", 10F, FontStyle.Bold, TextColor, 16, 10, 260, 22);
            panel.Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Location = new Point(280, 14);
            progressBar.Size = new Size(540, 12);
            progressBar.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            panel.Controls.Add(progressBar);

            return panel;
        }

        private Control CreateLogPanel()
        {
            RoundedPanel logPanel = CreateCard(10);
            logPanel.Dock = DockStyle.Fill;
            logPanel.FillColor = SurfaceColor;
            logPanel.Padding = new Padding(14);

            TableLayoutPanel logLayout = new TableLayoutPanel();
            logLayout.Dock = DockStyle.Fill;
            logLayout.BackColor = SurfaceColor;
            logLayout.ColumnCount = 1;
            logLayout.RowCount = 2;
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            logPanel.Controls.Add(logLayout);

            Label logTitle = CreateLabel("Activity log", 10.5F, FontStyle.Bold, TextColor);
            logLayout.Controls.Add(logTitle, 0, 0);

            logBox = new RichTextBox();
            logBox.BackColor = FieldColor;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Dock = DockStyle.Fill;
            logBox.ForeColor = TextColor;
            logBox.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.ReadOnly = true;
            logBox.DetectUrls = false;
            logLayout.Controls.Add(logBox, 0, 1);

            return logPanel;
        }

        private static RoundedPanel CreateCard(int radius)
        {
            RoundedPanel card = new RoundedPanel();
            card.BackColor = BackgroundColor;
            card.FillColor = SurfaceColor;
            card.BorderColor = BorderColor;
            card.Radius = radius;
            return card;
        }

        private static Label CreateFloatingLabel(string text, float size, FontStyle style, Color color, int left, int top, int width, int height)
        {
            Label label = CreateLabel(text, size, style, color);
            label.Dock = DockStyle.None;
            label.Location = new Point(left, top);
            label.Size = new Size(width, height);
            return label;
        }

        private static void AddPill(Control parent, string text, int left, int top, Color color)
        {
            Label pill = new Label();
            pill.AutoSize = false;
            pill.Text = text;
            pill.TextAlign = ContentAlignment.MiddleCenter;
            pill.Font = new Font("Segoe UI", 8.2F, FontStyle.Bold, GraphicsUnit.Point);
            pill.BackColor = Color.FromArgb(48, color);
            pill.ForeColor = TextColor;
            pill.Location = new Point(left, top);
            pill.Size = new Size(Math.Max(96, text.Length * 8 + 28), 26);
            parent.Controls.Add(pill);
        }

        private static Button CreatePrimaryButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = AccentColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = AccentColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(139, 92, 246);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 33, 182);
            return button;
        }

        private static Button CreateSecondaryButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = SurfaceAltColor;
            button.ForeColor = TextColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 36, 48);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(18, 18, 25);
            return button;
        }

        private static Button CreateTextButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = BackgroundColor;
            button.ForeColor = MutedTextColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = SurfaceAltColor;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(18, 18, 25);
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
            button.Height = 40;
            return button;
        }

        private void RestoreButtonClick(object sender, EventArgs e)
        {
            StartRestore();
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            if (restoreProcess == null || restoreProcess.HasExited)
            {
                return;
            }

            AppendLog("Cancel requested. Stopping restore process.", WarningColor);
            statusLabel.Text = "Cancelling...";
            cancelButton.Enabled = false;

            try
            {
                restoreProcess.Kill();
            }
            catch (Exception ex)
            {
                AppendLog("Could not stop restore process: " + ex.Message, DangerColor);
            }
        }

        private void StartRestore()
        {
            if (restoreProcess != null && !restoreProcess.HasExited)
            {
                return;
            }

            if (!File.Exists(restoreScriptPath))
            {
                ExitCode = 1;
                statusLabel.Text = "Restore script missing";
                AppendLog("restore-default.ps1 was not found next to this executable.", DangerColor);
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                this,
                "Restore Windows Terminal and shell files to a plain default-style setup? Timestamped backups will be created first.",
                "MBS Terminal Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            restoreButton.Enabled = false;
            cancelButton.Enabled = true;
            closeButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 24;
            statusLabel.Text = "Restoring...";
            AppendLog("Starting restore.", AccentColor);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetPowerShellPath();
            startInfo.Arguments = BuildPowerShellArguments();
            startInfo.WorkingDirectory = repositoryRoot;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            restoreProcess = new Process();
            restoreProcess.StartInfo = startInfo;
            restoreProcess.EnableRaisingEvents = true;
            restoreProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    AppendLog(e.Data, ColorForLine(e.Data, false));
                }
            };
            restoreProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    AppendLog(e.Data, ColorForLine(e.Data, true));
                }
            };
            restoreProcess.Exited += RestoreProcessExited;

            try
            {
                restoreProcess.Start();
                restoreProcess.BeginOutputReadLine();
                restoreProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                FinishRestore(1);
                AppendLog("Could not start PowerShell restore: " + ex.Message, DangerColor);
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

        private string BuildPowerShellArguments()
        {
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Quote(restoreScriptPath));

            if (keepStarshipBox.Checked)
            {
                arguments.Add("-KeepStarship");
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

        private void RestoreProcessExited(object sender, EventArgs e)
        {
            int exitCode = 1;

            try
            {
                exitCode = restoreProcess.ExitCode;
            }
            catch
            {
                exitCode = 1;
            }

            if (IsHandleCreated)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    FinishRestore(exitCode);
                }));
            }
        }

        private void FinishRestore(int exitCode)
        {
            ExitCode = exitCode;
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.Value = exitCode == 0 ? 100 : 0;
            restoreButton.Enabled = true;
            cancelButton.Enabled = false;
            closeButton.Enabled = true;

            if (exitCode == 0)
            {
                statusLabel.Text = "Restore complete";
                AppendLog("Restore complete. Open a new Windows Terminal tab to verify it.", AccentColor);
            }
            else
            {
                statusLabel.Text = "Restore failed";
                AppendLog("Restore exited with code " + exitCode + ".", DangerColor);
            }

            if (restoreProcess != null)
            {
                restoreProcess.Dispose();
                restoreProcess = null;
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
            UpdateStatusFromLog(message);
        }

        private void UpdateStatusFromLog(string message)
        {
            if (restoreProcess == null || restoreProcess.HasExited || statusLabel == null)
            {
                return;
            }

            string statusText = message == null ? string.Empty : message.Trim();

            if (statusText.StartsWith("[MBS-Terminal Restore]", StringComparison.OrdinalIgnoreCase))
            {
                statusText = statusText.Substring("[MBS-Terminal Restore]".Length).Trim();
            }

            if (statusText.Length == 0 || statusText.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (statusText.Length > 74)
            {
                statusText = statusText.Substring(0, 71).TrimEnd() + "...";
            }

            statusLabel.Text = statusText;
        }

        private void RestoreFormClosing(object sender, FormClosingEventArgs e)
        {
            if (restoreProcess == null || restoreProcess.HasExited)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "The restore process is still running. Stop it and close?",
                "MBS Terminal Restore",
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
                restoreProcess.Kill();
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
            FillColor = Color.FromArgb(10, 10, 15);
            BorderColor = Color.FromArgb(58, 60, 74);
            Radius = 10;
        }

        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public int Radius { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = CreateRoundRectangle(bounds, Radius))
            using (SolidBrush fill = new SolidBrush(FillColor))
            using (Pen border = new Pen(BorderColor, 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            base.OnPaint(e);
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
}
