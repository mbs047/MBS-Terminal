using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private static readonly Color BackgroundColor = Color.FromArgb(9, 14, 23);
        private static readonly Color SurfaceColor = Color.FromArgb(17, 24, 39);
        private static readonly Color SurfaceAltColor = Color.FromArgb(22, 32, 48);
        private static readonly Color TextColor = Color.FromArgb(235, 241, 247);
        private static readonly Color MutedTextColor = Color.FromArgb(152, 163, 179);
        private static readonly Color AccentColor = Color.FromArgb(44, 190, 146);
        private static readonly Color WarningColor = Color.FromArgb(245, 181, 84);
        private static readonly Color DangerColor = Color.FromArgb(248, 113, 113);

        private readonly string repositoryRoot;
        private readonly string restoreScriptPath;
        private readonly string iconPath;
        private readonly CheckBox keepStarshipBox;
        private readonly Button restoreButton;
        private readonly Button cancelButton;
        private readonly Button closeButton;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly RichTextBox logBox;

        private Process restoreProcess;

        public RestoreForm(string[] args)
        {
            repositoryRoot = GetRepositoryRoot();
            restoreScriptPath = Path.Combine(repositoryRoot, "restore-default.ps1");
            iconPath = Path.Combine(repositoryRoot, @"assets\terminal-icons\mbs-terminal.ico");

            ExitCode = 0;
            Text = "MBS Terminal Restore";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(820, 620);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = LoadWindowIcon();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = BackgroundColor;
            layout.Padding = new Padding(24);
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(layout);

            Label title = CreateLabel("Restore Windows Terminal Defaults", 22F, FontStyle.Bold, TextColor);
            title.TextAlign = ContentAlignment.BottomLeft;
            layout.Controls.Add(title, 0, 0);

            Label description = CreateLabel(
                "This will reset Windows Terminal to a plain default-style config and move MBS Terminal files into timestamped backups.",
                10F,
                FontStyle.Regular,
                MutedTextColor
            );
            description.TextAlign = ContentAlignment.TopLeft;
            layout.Controls.Add(description, 0, 1);

            keepStarshipBox = new CheckBox();
            keepStarshipBox.Text = "Keep existing Starship config";
            keepStarshipBox.AutoSize = false;
            keepStarshipBox.Height = 40;
            keepStarshipBox.Dock = DockStyle.Fill;
            keepStarshipBox.FlatStyle = FlatStyle.Flat;
            keepStarshipBox.ForeColor = TextColor;
            keepStarshipBox.BackColor = BackgroundColor;
            keepStarshipBox.Checked = HasArgument(args, "KeepStarship");
            layout.Controls.Add(keepStarshipBox, 0, 2);

            Panel actionsPanel = new Panel();
            actionsPanel.Dock = DockStyle.Fill;
            actionsPanel.BackColor = BackgroundColor;

            restoreButton = CreatePrimaryButton("Restore Defaults");
            restoreButton.Width = 156;
            restoreButton.Height = 38;
            restoreButton.Left = 0;
            restoreButton.Top = 0;
            restoreButton.Click += RestoreButtonClick;
            actionsPanel.Controls.Add(restoreButton);

            cancelButton = CreateSecondaryButton("Cancel");
            cancelButton.Width = 96;
            cancelButton.Height = 38;
            cancelButton.Left = restoreButton.Right + 10;
            cancelButton.Top = 0;
            cancelButton.Enabled = false;
            cancelButton.Click += CancelButtonClick;
            actionsPanel.Controls.Add(cancelButton);

            closeButton = CreateTextButton("Close");
            closeButton.Width = 88;
            closeButton.Height = 38;
            closeButton.Left = cancelButton.Right + 10;
            closeButton.Top = 0;
            closeButton.Click += delegate { Close(); };
            actionsPanel.Controls.Add(closeButton);

            layout.Controls.Add(actionsPanel, 0, 3);

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 12;
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Margin = new Padding(0, 8, 0, 8);
            layout.Controls.Add(progressBar, 0, 4);

            Panel logPanel = new Panel();
            logPanel.Dock = DockStyle.Fill;
            logPanel.BackColor = SurfaceColor;
            logPanel.Padding = new Padding(14);

            TableLayoutPanel logLayout = new TableLayoutPanel();
            logLayout.Dock = DockStyle.Fill;
            logLayout.BackColor = SurfaceColor;
            logLayout.ColumnCount = 1;
            logLayout.RowCount = 2;
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            logPanel.Controls.Add(logLayout);

            statusLabel = CreateLabel("Ready to restore", 10F, FontStyle.Bold, TextColor);
            logLayout.Controls.Add(statusLabel, 0, 0);

            logBox = new RichTextBox();
            logBox.BackColor = Color.FromArgb(7, 10, 17);
            logBox.BorderStyle = BorderStyle.None;
            logBox.Dock = DockStyle.Fill;
            logBox.ForeColor = TextColor;
            logBox.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.ReadOnly = true;
            logBox.DetectUrls = false;
            logLayout.Controls.Add(logBox, 0, 1);

            layout.Controls.Add(logPanel, 0, 5);

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
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 45, 64);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 35, 51);
            return button;
        }

        private static Button CreateTextButton(string text)
        {
            Button button = CreateButton(text);
            button.BackColor = BackgroundColor;
            button.ForeColor = MutedTextColor;
            button.FlatAppearance.BorderColor = BackgroundColor;
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
}
