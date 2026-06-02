using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

[assembly: AssemblyProduct("MBS Terminal")]
[assembly: AssemblyTitle("MBS Terminal Setup")]
[assembly: AssemblyDescription("Graphical installer for the MBS Terminal profile, theme, and Laravel developer tools.")]
[assembly: AssemblyCompany("MBS Dev")]
[assembly: AssemblyCopyright("Copyright 2026 MBS Dev")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace MbsTerminalSetup
{
    // Modern Windows folder picker (the Vista/Win11 common item dialog) via IFileOpenDialog.
    // Falls back to the classic FolderBrowserDialog if the COM dialog is unavailable.
    internal static class ModernFolderDialog
    {
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        public static string Show(IntPtr owner, string title, string initialPath)
        {
            try
            {
                return ShowModern(owner, title, initialPath);
            }
            catch
            {
                return ShowClassic(title, initialPath);
            }
        }

        private static string ShowModern(IntPtr owner, string title, string initialPath)
        {
            IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialogClass();

            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);

                if (!string.IsNullOrEmpty(title))
                {
                    dialog.SetTitle(title);
                }

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    try
                    {
                        IShellItem startItem;
                        Guid shellItemGuid = typeof(IShellItem).GUID;
                        SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref shellItemGuid, out startItem);

                        if (startItem != null)
                        {
                            dialog.SetFolder(startItem);
                            Marshal.ReleaseComObject(startItem);
                        }
                    }
                    catch
                    {
                    }
                }

                int hr = dialog.Show(owner);

                if (hr != 0)
                {
                    return null; // user cancelled
                }

                IShellItem result;
                dialog.GetResult(out result);

                string path;
                result.GetDisplayName(SIGDN_FILESYSPATH, out path);
                Marshal.ReleaseComObject(result);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        private static string ShowClassic(string title, string initialPath)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = title;

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.SelectedPath = initialPath;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }

            return null;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialogClass
        {
        }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);

            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
        }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog : IFileDialog
        {
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // The installer runs as the normal (non-elevated) user on purpose. winget
            // installs portable packages such as PHP into the user profile and elevates
            // individual packages itself when they need it. Running the whole installer
            // elevated breaks winget portable installs with "Access is denied" (0x80070005).
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs te)
            {
                LogStartupError(te.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ue)
            {
                LogStartupError(ue.ExceptionObject as Exception);
            };

            try
            {
                SetupAppContext context = new SetupAppContext();
                Application.Run(context);
                return context.ExitCode;
            }
            catch (Exception ex)
            {
                LogStartupError(ex);
                throw;
            }
        }

        private static void LogStartupError(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            try
            {
                string path = Path.Combine(Path.GetTempPath(), "mbs-setup-error.log");
                File.WriteAllText(path, ex.ToString());
            }
            catch
            {
            }
        }
    }

    // Shows an animated splash for a short, deliberate delay, then opens the wizard.
    internal sealed class SetupAppContext : ApplicationContext
    {
        private SplashForm splash;
        private SetupForm main;

        public SetupAppContext()
        {
            splash = new SplashForm();
            splash.Finished += SplashFinished;
            splash.Show();
        }

        public int ExitCode
        {
            get { return main != null ? main.ExitCode : 1; }
        }

        private void SplashFinished(object sender, EventArgs e)
        {
            main = new SetupForm();
            main.FormClosed += delegate { ExitThread(); };
            MainForm = main;
            main.Show();
            main.Activate();

            if (splash != null)
            {
                splash.Close();
                splash.Dispose();
                splash = null;
            }
        }
    }

    internal static class Palette
    {
        public static readonly Color SidebarTop = Color.FromArgb(35, 25, 64);
        public static readonly Color SidebarBottom = Color.FromArgb(13, 11, 26);
        public static readonly Color SidebarText = Color.FromArgb(245, 244, 250);
        public static readonly Color SidebarMuted = Color.FromArgb(140, 138, 168);

        public static readonly Color Canvas = Color.FromArgb(255, 255, 255);
        public static readonly Color Subtle = Color.FromArgb(246, 247, 249);
        public static readonly Color Field = Color.FromArgb(249, 250, 251);
        public static readonly Color Border = Color.FromArgb(222, 226, 233);
        public static readonly Color BorderHover = Color.FromArgb(186, 191, 201);

        public static readonly Color TextDark = Color.FromArgb(28, 31, 46);
        public static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public static readonly Color Accent = Color.FromArgb(124, 58, 237);
        public static readonly Color AccentHover = Color.FromArgb(139, 92, 246);
        public static readonly Color AccentDown = Color.FromArgb(109, 40, 217);
        public static readonly Color AccentSoft = Color.FromArgb(244, 240, 255);

        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    }

    internal static class Fonts
    {
        private static string monoName;

        public static Font Ui(float size, FontStyle style)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static Font Mono(float size)
        {
            return new Font(MonoName, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static string MonoName
        {
            get
            {
                if (monoName == null)
                {
                    monoName = Resolve(new[] { "Cascadia Code", "Cascadia Mono", "Consolas", "Lucida Console" }, "Consolas");
                }

                return monoName;
            }
        }

        private static string Resolve(string[] candidates, string fallback)
        {
            try
            {
                using (InstalledFontCollection collection = new InstalledFontCollection())
                {
                    HashSet<string> available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (FontFamily family in collection.Families)
                    {
                        available.Add(family.Name);
                    }

                    foreach (string candidate in candidates)
                    {
                        if (available.Contains(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch
            {
            }

            return fallback;
        }
    }

    internal static class GraphicsHelper
    {
        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SplashForm : Form
    {
        private const int DurationMs = 1700;
        private readonly Timer timer;
        private int elapsed;
        private float angle;

        public event EventHandler Finished;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(400, 248);
            BackColor = Palette.SidebarBottom;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            TopMost = true;

            timer = new Timer();
            timer.Interval = 28;
            timer.Tick += TimerTick;
            timer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            using (GraphicsPath path = GraphicsHelper.RoundRect(new Rectangle(0, 0, Width, Height), 16))
            {
                Region = new Region(path);
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            elapsed += timer.Interval;
            angle += 11f;

            if (angle >= 360f)
            {
                angle -= 360f;
            }

            Invalidate();

            if (elapsed >= DurationMs)
            {
                timer.Stop();

                if (Finished != null)
                {
                    Finished(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush bg = new LinearGradientBrush(
                ClientRectangle, Palette.SidebarTop, Palette.SidebarBottom, LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            using (Pen edge = new Pen(Color.FromArgb(60, 255, 255, 255), 1F))
            {
                g.DrawRectangle(edge, 0, 0, Width - 1, Height - 1);
            }

            int centerX = Width / 2;

            Rectangle mark = new Rectangle(centerX - 30, 40, 60, 60);
            using (GraphicsPath markPath = GraphicsHelper.RoundRect(mark, 14))
            using (SolidBrush markFill = new SolidBrush(Palette.Accent))
            {
                g.FillPath(markFill, markPath);
            }

            using (Font promptFont = Fonts.Mono(17F))
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                center.LineAlignment = StringAlignment.Center;
                g.DrawString(">_", promptFont, white, mark, center);
            }

            using (Font titleFont = Fonts.Ui(15F, FontStyle.Bold))
            using (Font subFont = Fonts.Ui(9.5F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Palette.SidebarText))
            using (SolidBrush subBrush = new SolidBrush(Palette.SidebarMuted))
            {
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                g.DrawString("MBS Terminal", titleFont, titleBrush, new RectangleF(0, 112, Width, 26), center);
                g.DrawString("Preparing setup...", subFont, subBrush, new RectangleF(0, 142, Width, 20), center);
            }

            DrawSpinner(g, new Point(centerX, 196), 13F);
        }

        private void DrawSpinner(Graphics g, Point center, float radius)
        {
            using (Pen track = new Pen(Color.FromArgb(40, 255, 255, 255), 3F))
            {
                g.DrawEllipse(track, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }

            using (Pen arc = new Pen(Palette.AccentHover, 3F))
            {
                arc.StartCap = LineCap.Round;
                arc.EndCap = LineCap.Round;
                g.DrawArc(arc, center.X - radius, center.Y - radius, radius * 2, radius * 2, angle, 90F);
            }
        }
    }

    internal sealed class TooltipInfo
    {
        public TooltipInfo(string name, string url, string description)
        {
            Name = name;
            Url = url;
            Description = description;
        }

        public string Name { get; private set; }
        public string Url { get; private set; }
        public string Description { get; private set; }
    }

    // Borderless, non-activating popup that shows a tool's name, URL, and description.
    internal sealed class InfoTooltipWindow : Form
    {
        private static readonly Color BackgroundColor = Color.FromArgb(24, 24, 34);
        private static readonly Color BorderColor = Color.FromArgb(74, 74, 94);
        private static readonly Color UrlColor = Color.FromArgb(186, 156, 255);

        private const int Width0 = 304;
        private const int Pad = 14;

        private TooltipInfo info;

        public InfoTooltipWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = BackgroundColor;
            DoubleBuffered = true;
            TopMost = true;
            Width = Width0;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void ShowFor(TooltipInfo info, Rectangle anchorScreen)
        {
            this.info = info;

            int textWidth = Width0 - (Pad * 2);
            int height = Pad;

            using (Font titleFont = Fonts.Ui(10F, FontStyle.Bold))
            using (Font urlFont = Fonts.Ui(8.5F, FontStyle.Regular))
            using (Font descFont = Fonts.Ui(9F, FontStyle.Regular))
            {
                height += TextRenderer.MeasureText(info.Name, titleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height + 4;

                if (!string.IsNullOrEmpty(info.Url))
                {
                    height += TextRenderer.MeasureText(info.Url, urlFont, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height + 6;
                }

                height += TextRenderer.MeasureText(info.Description, descFont, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height;
            }

            height += Pad;
            Size = new Size(Width0, height);

            using (GraphicsPath path = GraphicsHelper.RoundRect(new Rectangle(0, 0, Width, Height), 8))
            {
                Region = new Region(path);
            }

            Rectangle screen = Screen.FromRectangle(anchorScreen).WorkingArea;
            int x = anchorScreen.Right + 12;

            if (x + Width > screen.Right)
            {
                x = anchorScreen.Left - Width - 12;
            }

            if (x < screen.Left)
            {
                x = screen.Left + 8;
            }

            int y = anchorScreen.Top;

            if (y + Height > screen.Bottom)
            {
                y = screen.Bottom - Height - 8;
            }

            Location = new Point(x, y);

            if (!Visible)
            {
                Show();
            }
            else
            {
                Invalidate();
            }
        }

        public void HideTip()
        {
            if (Visible)
            {
                Hide();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (info == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush fill = new SolidBrush(BackgroundColor))
            using (Pen border = new Pen(BorderColor, 1F))
            using (GraphicsPath path = GraphicsHelper.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int textWidth = Width - (Pad * 2);
            int y = Pad;

            using (Font titleFont = Fonts.Ui(10F, FontStyle.Bold))
            {
                Rectangle r = new Rectangle(Pad, y, textWidth, 1000);
                TextRenderer.DrawText(g, info.Name, titleFont, r, Color.White, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                y += TextRenderer.MeasureText(info.Name, titleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height + 4;
            }

            if (!string.IsNullOrEmpty(info.Url))
            {
                using (Font urlFont = Fonts.Ui(8.5F, FontStyle.Regular))
                {
                    Rectangle r = new Rectangle(Pad, y, textWidth, 1000);
                    TextRenderer.DrawText(g, info.Url, urlFont, r, UrlColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    y += TextRenderer.MeasureText(info.Url, urlFont, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height + 6;
                }
            }

            using (Font descFont = Fonts.Ui(9F, FontStyle.Regular))
            {
                Rectangle r = new Rectangle(Pad, y, textWidth, 1000);
                TextRenderer.DrawText(g, info.Description, descFont, r, Color.FromArgb(186, 192, 206), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            }
        }
    }

    // Rounded text input with a focus highlight. Can act as a read-only folder picker.
    internal sealed class ThemedTextField : Control
    {
        internal enum FieldMode
        {
            Text,
            FolderPicker
        }

        private readonly TextBox inner;
        private readonly FieldMode mode;
        private bool focusedState;
        private bool hovered;
        private string placeholder = string.Empty;
        private string pickerValue = string.Empty;

        public event EventHandler BrowseRequested;

        public ThemedTextField(FieldMode mode)
        {
            this.mode = mode;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            Height = 40;
            BackColor = Palette.Canvas;

            inner = new TextBox();
            inner.BorderStyle = BorderStyle.None;
            inner.BackColor = Palette.Field;
            inner.ForeColor = Palette.TextDark;
            inner.Font = Fonts.Ui(10.5F, FontStyle.Regular);
            inner.Enter += delegate { focusedState = true; Invalidate(); };
            inner.Leave += delegate { focusedState = false; Invalidate(); };
            inner.MouseEnter += delegate { hovered = true; Invalidate(); };
            inner.MouseLeave += delegate { hovered = false; Invalidate(); };
            Controls.Add(inner);

            MouseEnter += delegate { hovered = true; Invalidate(); };
            MouseLeave += delegate { hovered = false; Invalidate(); };

            if (mode == FieldMode.FolderPicker)
            {
                inner.ReadOnly = true;
                inner.Cursor = Cursors.Hand;
                inner.Click += delegate { RaiseBrowse(); };
                Cursor = Cursors.Hand;
                Click += delegate { RaiseBrowse(); };
                UpdatePickerDisplay();
            }
        }

        public string Placeholder
        {
            get { return placeholder; }
            set
            {
                placeholder = value == null ? string.Empty : value;

                if (mode == FieldMode.FolderPicker)
                {
                    UpdatePickerDisplay();
                }
            }
        }

        public override string Text
        {
            get
            {
                if (mode == FieldMode.FolderPicker)
                {
                    return pickerValue;
                }

                return inner.Text;
            }
            set
            {
                if (mode == FieldMode.FolderPicker)
                {
                    pickerValue = value == null ? string.Empty : value;
                    UpdatePickerDisplay();
                }
                else
                {
                    inner.Text = value;
                }
            }
        }

        private void RaiseBrowse()
        {
            if (BrowseRequested != null)
            {
                BrowseRequested(this, EventArgs.Empty);
            }
        }

        private void UpdatePickerDisplay()
        {
            if (string.IsNullOrEmpty(pickerValue))
            {
                inner.Text = placeholder;
                inner.ForeColor = Palette.TextMuted;
            }
            else
            {
                inner.Text = pickerValue;
                inner.ForeColor = Palette.TextDark;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (inner == null)
            {
                return;
            }

            int rightPad = (mode == FieldMode.FolderPicker) ? 44 : 14;
            int textHeight = inner.PreferredHeight;
            inner.SetBounds(14, (Height - textHeight) / 2, Width - 14 - rightPad, textHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Parent != null)
            {
                g.Clear(Parent.BackColor);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color borderColor = focusedState ? Palette.Accent : (hovered ? Palette.BorderHover : Palette.Border);

            using (GraphicsPath path = GraphicsHelper.RoundRect(bounds, 8))
            using (SolidBrush fill = new SolidBrush(Palette.Field))
            using (Pen border = new Pen(borderColor, focusedState ? 1.6F : 1F))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            if (mode == FieldMode.FolderPicker)
            {
                DrawFolderGlyph(g, new Rectangle(Width - 36, (Height / 2) - 8, 18, 16), borderColor == Palette.Accent ? Palette.Accent : Palette.TextMuted);
            }
        }

        private static void DrawFolderGlyph(Graphics g, Rectangle r, Color color)
        {
            using (Pen pen = new Pen(color, 1.6F))
            {
                pen.LineJoin = LineJoin.Round;
                Point[] points = new[]
                {
                    new Point(r.Left, r.Bottom),
                    new Point(r.Left, r.Top + 3),
                    new Point(r.Left + 6, r.Top + 3),
                    new Point(r.Left + 8, r.Top),
                    new Point(r.Right, r.Top),
                    new Point(r.Right, r.Bottom),
                    new Point(r.Left, r.Bottom)
                };
                g.DrawLines(pen, points);
            }
        }
    }

    // Selectable card used as a styled radio or checkbox, with optional helper text and hover tooltip.
    internal sealed class SelectCard : Control
    {
        internal enum CardMode
        {
            Radio,
            Check
        }

        private readonly CardMode mode;
        private bool selected;
        private bool hovered;
        private string titleText = string.Empty;
        private string helperText = string.Empty;

        public string Key;
        public TooltipInfo Info;

        public event EventHandler Activated;
        public event EventHandler<SelectCard> HoverInfo;
        public event EventHandler HoverInfoEnd;

        public SelectCard(CardMode mode)
        {
            this.mode = mode;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 44;

            MouseEnter += delegate { hovered = true; Invalidate(); };
            MouseLeave += delegate
            {
                hovered = false;
                Invalidate();

                if (HoverInfoEnd != null)
                {
                    HoverInfoEnd(this, EventArgs.Empty);
                }
            };
            MouseHover += delegate
            {
                if (Info != null && HoverInfo != null)
                {
                    HoverInfo(this, this);
                }
            };
            Click += delegate { Activate(); };
        }

        public string TitleText
        {
            get { return titleText; }
            set { titleText = value == null ? string.Empty : value; Invalidate(); }
        }

        public string HelperText
        {
            get { return helperText; }
            set { helperText = value == null ? string.Empty : value; Invalidate(); }
        }

        public bool Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        private void Activate()
        {
            if (mode == CardMode.Check)
            {
                selected = !selected;
            }
            else
            {
                selected = true;
            }

            Invalidate();

            if (Activated != null)
            {
                Activated(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (Parent != null)
            {
                g.Clear(Parent.BackColor);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = selected ? Palette.AccentSoft : (hovered ? Palette.Subtle : Palette.Canvas);
            Color border = selected ? Palette.Accent : (hovered ? Palette.BorderHover : Palette.Border);

            using (GraphicsPath path = GraphicsHelper.RoundRect(bounds, 9))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border, selected ? 1.6F : 1F))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            int indicatorSize = 20;
            int indicatorX = 16;
            int indicatorY = (Height - indicatorSize) / 2;
            Rectangle indicator = new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize);

            if (mode == CardMode.Radio)
            {
                DrawRadio(g, indicator);
            }
            else
            {
                DrawCheck(g, indicator);
            }

            int textLeft = indicatorX + indicatorSize + 14;
            int textWidth = Width - textLeft - 14;

            if (string.IsNullOrEmpty(helperText))
            {
                using (Font titleFont = Fonts.Ui(10.5F, FontStyle.Regular))
                {
                    Rectangle r = new Rectangle(textLeft, 0, textWidth, Height);
                    TextRenderer.DrawText(g, titleText, titleFont, r, Palette.TextDark,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
            }
            else
            {
                using (Font titleFont = Fonts.Ui(10.5F, FontStyle.Bold))
                using (Font helperFont = Fonts.Ui(9F, FontStyle.Regular))
                {
                    Rectangle titleRect = new Rectangle(textLeft, 12, textWidth, 22);
                    TextRenderer.DrawText(g, titleText, titleFont, titleRect, Palette.TextDark,
                        TextFormatFlags.Left | TextFormatFlags.Top);

                    Rectangle helperRect = new Rectangle(textLeft, 34, textWidth, Height - 38);
                    TextRenderer.DrawText(g, helperText, helperFont, helperRect, Palette.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                }
            }
        }

        private void DrawRadio(Graphics g, Rectangle r)
        {
            using (Pen ring = new Pen(selected ? Palette.Accent : Palette.BorderHover, 1.8F))
            {
                g.DrawEllipse(ring, r);
            }

            if (selected)
            {
                Rectangle dot = new Rectangle(r.Left + 5, r.Top + 5, r.Width - 10, r.Height - 10);
                using (SolidBrush fill = new SolidBrush(Palette.Accent))
                {
                    g.FillEllipse(fill, dot);
                }
            }
        }

        private void DrawCheck(Graphics g, Rectangle r)
        {
            using (GraphicsPath path = GraphicsHelper.RoundRect(r, 5))
            {
                if (selected)
                {
                    using (SolidBrush fill = new SolidBrush(Palette.Accent))
                    {
                        g.FillPath(fill, path);
                    }

                    using (Pen check = new Pen(Color.White, 2.1F))
                    {
                        check.StartCap = LineCap.Round;
                        check.EndCap = LineCap.Round;
                        g.DrawLines(check, new[]
                        {
                            new Point(r.Left + 5, r.Top + 10),
                            new Point(r.Left + 9, r.Top + 14),
                            new Point(r.Left + 15, r.Top + 6)
                        });
                    }
                }
                else
                {
                    using (Pen border = new Pen(Palette.BorderHover, 1.6F))
                    {
                        g.DrawPath(border, path);
                    }
                }
            }
        }
    }

    internal sealed class StepRail : Panel
    {
        private readonly string[] steps;
        private int activeIndex;

        public StepRail(string[] steps)
        {
            this.steps = steps;
            DoubleBuffered = true;
        }

        public int ActiveIndex
        {
            get { return activeIndex; }
            set { activeIndex = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush background = new LinearGradientBrush(
                ClientRectangle, Palette.SidebarTop, Palette.SidebarBottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(background, ClientRectangle);
            }

            int left = 34;

            Rectangle mark = new Rectangle(left, 34, 48, 48);
            using (GraphicsPath markPath = GraphicsHelper.RoundRect(mark, 12))
            using (SolidBrush markFill = new SolidBrush(Palette.Accent))
            {
                g.FillPath(markFill, markPath);
            }

            using (Font promptFont = Fonts.Mono(13F))
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                center.LineAlignment = StringAlignment.Center;
                g.DrawString(">_", promptFont, white, mark, center);
            }

            using (Font titleFont = Fonts.Ui(13.5F, FontStyle.Bold))
            using (Font subFont = Fonts.Ui(9F, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Palette.SidebarText))
            using (SolidBrush subBrush = new SolidBrush(Palette.SidebarMuted))
            {
                g.DrawString("MBS Terminal", titleFont, titleBrush, left + 60, 40);
                g.DrawString("Developer setup", subFont, subBrush, left + 61, 64);
            }

            int stepTop = 156;
            int rowHeight = 58;

            for (int i = 0; i < steps.Length; i++)
            {
                int y = stepTop + (i * rowHeight);
                bool isActive = (i == activeIndex);
                bool isDone = (i < activeIndex);

                if (i < steps.Length - 1)
                {
                    using (Pen connector = new Pen(Color.FromArgb(70, 255, 255, 255), 2F))
                    {
                        g.DrawLine(connector, left + 14, y + 30, left + 14, y + rowHeight);
                    }
                }

                Rectangle circle = new Rectangle(left, y, 28, 28);

                if (isActive || isDone)
                {
                    using (SolidBrush fill = new SolidBrush(Palette.Accent))
                    {
                        g.FillEllipse(fill, circle);
                    }
                }
                else
                {
                    using (Pen ring = new Pen(Color.FromArgb(90, 255, 255, 255), 2F))
                    {
                        g.DrawEllipse(ring, circle);
                    }
                }

                if (isDone)
                {
                    using (Pen check = new Pen(Color.White, 2.2F))
                    {
                        check.StartCap = LineCap.Round;
                        check.EndCap = LineCap.Round;
                        g.DrawLines(check, new[]
                        {
                            new Point(circle.Left + 8, circle.Top + 14),
                            new Point(circle.Left + 12, circle.Top + 19),
                            new Point(circle.Left + 20, circle.Top + 9)
                        });
                    }
                }
                else
                {
                    using (Font numberFont = Fonts.Ui(9.5F, FontStyle.Bold))
                    using (SolidBrush numberBrush = new SolidBrush(isActive ? Color.White : Palette.SidebarMuted))
                    {
                        StringFormat center = new StringFormat();
                        center.Alignment = StringAlignment.Center;
                        center.LineAlignment = StringAlignment.Center;
                        g.DrawString((i + 1).ToString(), numberFont, numberBrush, circle, center);
                    }
                }

                using (Font labelFont = Fonts.Ui(10.5F, isActive ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush labelBrush = new SolidBrush(isActive ? Palette.SidebarText : Palette.SidebarMuted))
                {
                    g.DrawString(steps[i], labelFont, labelBrush, left + 42, y + 4);
                }
            }

            using (Font versionFont = Fonts.Ui(8.5F, FontStyle.Regular))
            using (SolidBrush versionBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            {
                g.DrawString("v1.0", versionFont, versionBrush, left, Height - 40);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        internal enum Kind
        {
            Primary,
            Secondary,
            Ghost
        }

        private readonly Kind kind;
        private bool hovered;
        private bool pressed;

        public RoundedButton(string text, Kind kind)
        {
            this.kind = kind;
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Palette.Canvas;
            Font = Fonts.Ui(10F, FontStyle.Bold);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            MouseEnter += delegate { hovered = true; Invalidate(); };
            MouseLeave += delegate { hovered = false; pressed = false; Invalidate(); };
            MouseDown += delegate { pressed = true; Invalidate(); };
            MouseUp += delegate { pressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Palette.Canvas);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = 8;

            Color fill;
            Color textColor;
            Color borderColor = Color.Empty;

            if (kind == Kind.Primary)
            {
                fill = pressed ? Palette.AccentDown : (hovered ? Palette.AccentHover : Palette.Accent);
                textColor = Color.White;
            }
            else if (kind == Kind.Secondary)
            {
                fill = hovered ? Palette.Subtle : Palette.Canvas;
                textColor = Palette.TextDark;
                borderColor = hovered ? Palette.BorderHover : Palette.Border;
            }
            else
            {
                fill = hovered ? Palette.Subtle : Palette.Canvas;
                textColor = Palette.TextMuted;
            }

            if (!Enabled)
            {
                fill = Palette.Subtle;
                textColor = Palette.TextMuted;
                borderColor = Color.Empty;
            }

            using (GraphicsPath path = GraphicsHelper.RoundRect(bounds, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);

                if (borderColor != Color.Empty)
                {
                    using (Pen pen = new Pen(borderColor, 1F))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(g, Text, Font, bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class ToolEntry
    {
        public ToolEntry(string key, string label, string url, string description)
        {
            Key = key;
            Label = label;
            Url = url;
            Description = description;
        }

        public string Key { get; private set; }
        public string Label { get; private set; }
        public string Url { get; private set; }
        public string Description { get; private set; }
    }

    internal sealed class SetupForm : Form
    {
        private const int SidebarWidth = 250;
        private const int FooterHeight = 74;
        private const int PageCount = 4;

        private readonly string[] stepTitles = new[] { "Welcome", "Options", "Components", "Install" };

        private readonly string supportRoot;
        private readonly bool extractedSupportRoot;
        private readonly string installScriptPath;

        private readonly Dictionary<string, SelectCard> toolCards = new Dictionary<string, SelectCard>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SelectCard> presetCards = new List<SelectCard>();
        private readonly List<SelectCard> scopeCards = new List<SelectCard>();
        private readonly Panel[] pages = new Panel[PageCount];

        private StepRail stepRail;
        private Panel contentHost;
        private Panel footer;
        private RoundedButton backButton;
        private RoundedButton primaryButton;
        private RoundedButton cancelButton;

        private ThemedTextField displayNameField;
        private ThemedTextField startingDirectoryField;
        private ThemedTextField phpDirectoryField;
        private ComboBox phpVersionBox;
        private SelectCard scopeJustMeCard;
        private SelectCard scopeAllUsersCard;

        private Label installTitleLabel;
        private Label statusLabel;
        private ProgressBar progressBar;
        private RichTextBox logBox;

        private InfoTooltipWindow tooltip;
        private Process installProcess;
        private int currentPage;
        private RunState runState = RunState.Normal;

        private enum RunState
        {
            Normal,
            Installing,
            Done
        }

        public SetupForm()
        {
            string resolvedRoot = ResolveSupportRoot();

            if (string.IsNullOrWhiteSpace(resolvedRoot))
            {
                resolvedRoot = CreateExtractionRoot();

                try
                {
                    MbsTerminalInstall.EmbeddedTerminalInstallerSupportFiles.ExtractTo(resolvedRoot);
                    extractedSupportRoot = true;
                }
                catch
                {
                    extractedSupportRoot = false;
                }
            }

            supportRoot = resolvedRoot;
            installScriptPath = Path.Combine(supportRoot, "install.ps1");

            ExitCode = 0;
            Text = "MBS Terminal Setup";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(920, 660);
            Size = new Size(960, 700);
            BackColor = Palette.Canvas;
            ForeColor = Palette.TextDark;
            Font = Fonts.Ui(9.75F, FontStyle.Regular);
            Icon = LoadWindowIcon();
            DoubleBuffered = true;

            tooltip = new InfoTooltipWindow();

            BuildChrome();
            BuildPages();
            ApplyPreset("Recommended");

            FormClosing += SetupFormClosing;
            ShowPage(0);

            if (!File.Exists(installScriptPath))
            {
                AppendLog("install.ps1 was not found. The installer cannot run from this location.", Palette.Danger);
            }
        }

        public int ExitCode { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Keep the primary button as the default focus so Enter advances the wizard
            // instead of activating a focused Cancel/Back button.
            if (primaryButton != null && primaryButton.CanFocus)
            {
                ActiveControl = primaryButton;
            }
        }

        // ---- Chrome ----

        private void BuildChrome()
        {
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = FooterHeight;
            footer.BackColor = Palette.Canvas;
            footer.Paint += FooterPaint;
            Controls.Add(footer);

            cancelButton = new RoundedButton("Cancel", RoundedButton.Kind.Ghost);
            cancelButton.SetBounds(28, 18, 110, 38);
            cancelButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            cancelButton.Click += CancelClick;
            footer.Controls.Add(cancelButton);

            primaryButton = new RoundedButton("Next", RoundedButton.Kind.Primary);
            primaryButton.SetBounds(0, 18, 150, 38);
            primaryButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            primaryButton.Click += PrimaryClick;
            footer.Controls.Add(primaryButton);
            AcceptButton = primaryButton;

            backButton = new RoundedButton("Back", RoundedButton.Kind.Secondary);
            backButton.SetBounds(0, 18, 110, 38);
            backButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            backButton.Click += delegate { GoBack(); };
            footer.Controls.Add(backButton);

            stepRail = new StepRail(stepTitles);
            stepRail.Dock = DockStyle.Left;
            stepRail.Width = SidebarWidth;
            Controls.Add(stepRail);

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = Palette.Canvas;
            Controls.Add(contentHost);

            contentHost.BringToFront();
            footer.Resize += delegate { LayoutFooter(); };
            LayoutFooter();
        }

        private void FooterPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Palette.Border, 1F))
            {
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            }
        }

        private void LayoutFooter()
        {
            int right = footer.Width - 28;
            primaryButton.Left = right - primaryButton.Width;
            backButton.Left = primaryButton.Left - backButton.Width - 12;
        }

        // ---- Pages ----

        private void BuildPages()
        {
            pages[0] = BuildWelcomePage();
            pages[1] = BuildOptionsPage();
            pages[2] = BuildComponentsPage();
            pages[3] = BuildInstallPage();

            foreach (Panel page in pages)
            {
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                contentHost.Controls.Add(page);
            }
        }

        private Panel CreatePage()
        {
            Panel page = new Panel();
            page.BackColor = Palette.Canvas;
            page.Padding = new Padding(44, 36, 44, 16);
            return page;
        }

        private Panel BuildWelcomePage()
        {
            Panel page = CreatePage();

            page.Controls.Add(MakeLabel("Welcome", 24F, FontStyle.Bold, Palette.TextDark, 44, 34, 560, 40));
            page.Controls.Add(MakeLabel(
                "This installs the MBS Terminal profile, the Starship prompt, custom icons, and your Laravel developer toolchain on this PC.",
                11F, FontStyle.Regular, Palette.TextMuted, 44, 80, 600, 48));

            page.Controls.Add(MakeLabel("Choose a starting point", 11F, FontStyle.Bold, Palette.TextDark, 44, 146, 560, 24));

            int y = 178;
            y = AddPresetCard(page, "Recommended", "Everyday Laravel setup: PHP, Composer, Laravel, Node, Git, and core helpers.", y, true);
            y = AddPresetCard(page, "Full", "Everything, including Valet, Docker, Redis, Rector, and extra CLI tools.", y, false);
            y = AddPresetCard(page, "Minimal", "Just the terminal profile, prompt, and Git. Add tools later anytime.", y, false);

            page.Controls.Add(MakeLabel(
                "You can fine-tune every component on the next steps. Nothing is installed until you confirm.",
                9.5F, FontStyle.Italic, Palette.TextMuted, 44, y + 6, 600, 36));

            return page;
        }

        private int AddPresetCard(Panel page, string name, string description, int top, bool selected)
        {
            SelectCard card = new SelectCard(SelectCard.CardMode.Radio);
            card.Key = name;
            card.TitleText = name;
            card.HelperText = description;
            card.Selected = selected;
            card.SetBounds(44, top, 600, 58);
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Activated += PresetCardActivated;
            presetCards.Add(card);
            page.Controls.Add(card);

            return top + 68;
        }

        private void PresetCardActivated(object sender, EventArgs e)
        {
            SelectCard active = (SelectCard)sender;

            foreach (SelectCard card in presetCards)
            {
                if (!ReferenceEquals(card, active))
                {
                    card.Selected = false;
                }
            }

            ApplyPreset(active.Key);
        }

        private Panel BuildOptionsPage()
        {
            Panel page = CreatePage();

            page.Controls.Add(MakeLabel("Setup options", 24F, FontStyle.Bold, Palette.TextDark, 44, 34, 560, 40));
            page.Controls.Add(MakeLabel("Personalize the prompt and where new terminal tabs open.", 11F, FontStyle.Regular, Palette.TextMuted, 44, 80, 600, 24));

            page.Controls.Add(MakeLabel("Prompt display name", 9.5F, FontStyle.Bold, Palette.TextDark, 44, 122, 260, 20));
            displayNameField = new ThemedTextField(ThemedTextField.FieldMode.Text);
            displayNameField.Text = Environment.UserName;
            displayNameField.SetBounds(44, 144, 320, 40);
            page.Controls.Add(displayNameField);

            page.Controls.Add(MakeLabel("Starting directory", 9.5F, FontStyle.Bold, Palette.TextDark, 44, 196, 260, 20));
            startingDirectoryField = new ThemedTextField(ThemedTextField.FieldMode.FolderPicker);
            startingDirectoryField.Placeholder = "Click to choose a folder...";
            startingDirectoryField.Text = GetUserProfileDirectory();
            startingDirectoryField.SetBounds(44, 218, 560, 40);
            startingDirectoryField.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            startingDirectoryField.BrowseRequested += delegate { BrowseFolder(startingDirectoryField, "Choose the folder new terminal tabs should open in."); };
            page.Controls.Add(startingDirectoryField);

            page.Controls.Add(MakeLabel("Install for", 9.5F, FontStyle.Bold, Palette.TextDark, 44, 272, 260, 20));

            scopeJustMeCard = new SelectCard(SelectCard.CardMode.Radio);
            scopeJustMeCard.Key = "CurrentUser";
            scopeJustMeCard.TitleText = "Just me";
            scopeJustMeCard.HelperText = "Updates your user PATH and PowerShell profile.";
            scopeJustMeCard.Selected = true;
            scopeJustMeCard.SetBounds(44, 294, 278, 58);
            scopeJustMeCard.Activated += ScopeCardActivated;
            scopeCards.Add(scopeJustMeCard);
            page.Controls.Add(scopeJustMeCard);

            scopeAllUsersCard = new SelectCard(SelectCard.CardMode.Radio);
            scopeAllUsersCard.Key = "AllUsers";
            scopeAllUsersCard.TitleText = "All users on this PC";
            scopeAllUsersCard.HelperText = "Updates the machine-wide PATH for everyone.";
            scopeAllUsersCard.SetBounds(334, 294, 278, 58);
            scopeAllUsersCard.Activated += ScopeCardActivated;
            scopeCards.Add(scopeAllUsersCard);
            page.Controls.Add(scopeAllUsersCard);

            page.Controls.Add(MakeLabel("PHP", 9.5F, FontStyle.Bold, Palette.TextDark, 44, 372, 260, 20));

            SelectCard installPhpCard = new SelectCard(SelectCard.CardMode.Check);
            installPhpCard.Key = "InstallPhp";
            installPhpCard.TitleText = "Install PHP automatically";
            installPhpCard.SetBounds(44, 394, 360, 44);
            installPhpCard.Activated += delegate { UpdatePhpVersionEnabled(); };
            toolCards["InstallPhp"] = installPhpCard;
            page.Controls.Add(installPhpCard);

            page.Controls.Add(MakeLabel("Version", 9.5F, FontStyle.Regular, Palette.TextMuted, 420, 404, 60, 20));
            phpVersionBox = new ComboBox();
            phpVersionBox.DropDownStyle = ComboBoxStyle.DropDownList;
            phpVersionBox.FlatStyle = FlatStyle.Flat;
            phpVersionBox.BackColor = Palette.Field;
            phpVersionBox.ForeColor = Palette.TextDark;
            phpVersionBox.Font = Fonts.Ui(10F, FontStyle.Regular);
            phpVersionBox.Items.AddRange(new object[] { "8.2", "8.3", "8.4", "8.5" });
            phpVersionBox.SelectedItem = "8.4";
            phpVersionBox.SetBounds(478, 401, 96, 28);
            page.Controls.Add(phpVersionBox);

            page.Controls.Add(MakeLabel("Or use an existing PHP folder (Laragon, Herd, XAMPP) - optional", 9.5F, FontStyle.Regular, Palette.TextMuted, 44, 446, 560, 20));
            phpDirectoryField = new ThemedTextField(ThemedTextField.FieldMode.FolderPicker);
            phpDirectoryField.Placeholder = "Click to locate your PHP folder...";
            phpDirectoryField.SetBounds(44, 468, 528, 40);
            phpDirectoryField.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            phpDirectoryField.BrowseRequested += delegate { BrowseFolder(phpDirectoryField, "Choose the folder that contains php.exe."); };
            page.Controls.Add(phpDirectoryField);

            page.Resize += delegate { SizeOptionsFields(page); };

            return page;
        }

        private void SizeOptionsFields(Panel page)
        {
            int usable = page.ClientSize.Width - 44 - 44;

            if (usable < 220)
            {
                return;
            }

            startingDirectoryField.Width = usable;
            phpDirectoryField.Width = usable;
        }

        private void ScopeCardActivated(object sender, EventArgs e)
        {
            SelectCard active = (SelectCard)sender;

            foreach (SelectCard card in scopeCards)
            {
                if (!ReferenceEquals(card, active))
                {
                    card.Selected = false;
                }
            }
        }

        private void UpdatePhpVersionEnabled()
        {
            if (phpVersionBox != null && toolCards.ContainsKey("InstallPhp"))
            {
                phpVersionBox.Enabled = toolCards["InstallPhp"].Selected;
            }
        }

        private Panel BuildComponentsPage()
        {
            Panel page = CreatePage();
            page.Padding = new Padding(44, 36, 24, 16);

            page.Controls.Add(MakeLabel("Choose components", 24F, FontStyle.Bold, Palette.TextDark, 44, 34, 560, 40));
            page.Controls.Add(MakeLabel("Toggle anything you do not need. Hover any item for details.", 11F, FontStyle.Regular, Palette.TextMuted, 44, 80, 600, 24));

            Panel scroll = new Panel();
            scroll.SetBounds(44, 120, 600, 1);
            scroll.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            scroll.AutoScroll = true;
            scroll.BackColor = Palette.Canvas;
            page.Controls.Add(scroll);
            page.Resize += delegate { SizeComponentsScroll(page, scroll); };

            int y = 4;
            y = AddSection(scroll, "Laravel core", new[]
            {
                new ToolEntry("InstallDependencies", "Starship prompt", "https://starship.rs", "Fast, minimal, customizable cross-shell prompt."),
                new ToolEntry("InstallComposer", "Composer", "https://getcomposer.org", "Dependency manager for PHP projects."),
                new ToolEntry("InstallLaravel", "Laravel Installer", "https://laravel.com", "Scaffold new Laravel apps from the terminal."),
                new ToolEntry("InstallPint", "Laravel Pint", "https://laravel.com/docs/pint", "Opinionated PHP code-style fixer."),
                new ToolEntry("InstallValet", "Valet for Windows", "https://github.com/ycodetech/valet-windows", "Lightweight local Laravel dev environment."),
                new ToolEntry("InstallEnvoy", "Laravel Envoy", "https://laravel.com/docs/envoy", "Run common tasks on remote servers."),
                new ToolEntry("InstallVapor", "Laravel Vapor CLI", "https://vapor.laravel.com", "Serverless deployment platform for Laravel.")
            }, y);

            y = AddSection(scroll, "System", new[]
            {
                new ToolEntry("InstallGit", "Git", "https://git-scm.com", "Distributed version control system."),
                new ToolEntry("InstallNode", "Node.js LTS", "https://nodejs.org", "JavaScript runtime used by Vite and npm."),
                new ToolEntry("InstallNvm", "nvm-windows", "https://github.com/coreybutler/nvm-windows", "Switch between Node.js versions (replaces Node.js)."),
                new ToolEntry("InstallGhCli", "GitHub CLI", "https://cli.github.com", "Manage GitHub from the command line.")
            }, y);

            y = AddSection(scroll, "Code quality", new[]
            {
                new ToolEntry("InstallPest", "Pest PHP", "https://pestphp.com", "Elegant, expressive PHP testing framework."),
                new ToolEntry("InstallLarastan", "Larastan", "https://github.com/larastan/larastan", "PHPStan static analysis with Laravel rules."),
                new ToolEntry("InstallRector", "Rector", "https://getrector.com", "Automated refactoring and framework upgrades."),
                new ToolEntry("InstallRay", "Ray", "https://myray.app", "Debug with clean, structured dump output.")
            }, y);

            y = AddSection(scroll, "Local environment", new[]
            {
                new ToolEntry("InstallMkcert", "mkcert", "https://github.com/FiloSottile/mkcert", "Locally trusted HTTPS certificates for dev."),
                new ToolEntry("InstallRedis", "Memurai (Redis)", "https://www.memurai.com", "Redis-compatible server for Windows (queues, cache)."),
                new ToolEntry("InstallDocker", "Docker Desktop", "https://www.docker.com", "Containers for Laravel Sail. Needs WSL2 or Hyper-V."),
                new ToolEntry("InstallTablePlus", "TablePlus", "https://tableplus.com", "Modern GUI for MySQL, Postgres, and SQLite.")
            }, y);

            y = AddSection(scroll, "Terminal productivity", new[]
            {
                new ToolEntry("InstallFzf", "fzf", "https://github.com/junegunn/fzf", "Blazing-fast command-line fuzzy finder."),
                new ToolEntry("InstallBat", "bat", "https://github.com/sharkdp/bat", "A cat clone with syntax highlighting."),
                new ToolEntry("InstallRipgrep", "ripgrep", "https://github.com/BurntSushi/ripgrep", "Recursively search directories with regex, fast."),
                new ToolEntry("InstallLazygit", "lazygit", "https://github.com/jesseduffield/lazygit", "Simple terminal UI for git commands.")
            }, y);

            y = AddSection(scroll, "Install behaviour", new[]
            {
                new ToolEntry("UpdateTools", "Update tools that are already installed", "", "Upgrade matching tools to the latest version during install.")
            }, y);

            return page;
        }

        private void SizeComponentsScroll(Panel page, Panel scroll)
        {
            scroll.Width = page.ClientSize.Width - scroll.Left - 24;
            scroll.Height = page.ClientSize.Height - scroll.Top - page.Padding.Bottom;
        }

        private int AddSection(Panel parent, string title, ToolEntry[] entries, int top)
        {
            parent.Controls.Add(MakeLabel(title.ToUpperInvariant(), 9F, FontStyle.Bold, Palette.Accent, 2, top, 540, 18));

            int rowTop = top + 26;
            int cardWidth = 268;
            int gap = 12;

            for (int index = 0; index < entries.Length; index++)
            {
                ToolEntry entry = entries[index];
                SelectCard card = new SelectCard(SelectCard.CardMode.Check);
                card.Key = entry.Key;
                card.TitleText = entry.Label;

                if (!string.IsNullOrEmpty(entry.Url) || !string.IsNullOrEmpty(entry.Description))
                {
                    card.Info = new TooltipInfo(entry.Label, entry.Url, entry.Description);
                    card.HoverInfo += CardHoverInfo;
                    card.HoverInfoEnd += CardHoverInfoEnd;
                }

                bool isLeft = (index % 2) == 0;
                int x = isLeft ? 2 : (2 + cardWidth + gap);
                int yy = rowTop + ((index / 2) * 48);

                card.SetBounds(x, yy, cardWidth, 42);
                toolCards[entry.Key] = card;
                parent.Controls.Add(card);
            }

            int rows = (entries.Length + 1) / 2;
            return rowTop + (rows * 48) + 12;
        }

        private void CardHoverInfo(object sender, SelectCard card)
        {
            if (card.Info != null)
            {
                tooltip.ShowFor(card.Info, card.RectangleToScreen(card.ClientRectangle));
            }
        }

        private void CardHoverInfoEnd(object sender, EventArgs e)
        {
            tooltip.HideTip();
        }

        private Panel BuildInstallPage()
        {
            Panel page = CreatePage();

            installTitleLabel = MakeLabel("Ready to install", 24F, FontStyle.Bold, Palette.TextDark, 44, 34, 560, 40);
            page.Controls.Add(installTitleLabel);

            statusLabel = MakeLabel("Review your choices, then choose Install to begin.", 11F, FontStyle.Regular, Palette.TextMuted, 44, 80, 600, 24);
            page.Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.SetBounds(44, 120, 528, 8);
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(progressBar);

            Panel logFrame = new Panel();
            logFrame.SetBounds(44, 144, 528, 1);
            logFrame.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logFrame.BackColor = Palette.Subtle;
            logFrame.Padding = new Padding(12);
            logFrame.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Palette.Border, 1F))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, logFrame.Width - 1, logFrame.Height - 1);
                }
            };
            page.Controls.Add(logFrame);
            page.Resize += delegate { SizeLogFrame(page, logFrame); };

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.BackColor = Palette.Subtle;
            logBox.BorderStyle = BorderStyle.None;
            logBox.ForeColor = Palette.TextDark;
            logBox.Font = Fonts.Mono(9.5F);
            logBox.ReadOnly = true;
            logBox.DetectUrls = false;
            logFrame.Controls.Add(logBox);

            return page;
        }

        private void SizeLogFrame(Panel page, Panel logFrame)
        {
            logFrame.Width = page.ClientSize.Width - logFrame.Left - page.Padding.Right;
            logFrame.Height = page.ClientSize.Height - logFrame.Top - page.Padding.Bottom;
        }

        // ---- Navigation ----

        private void ShowPage(int index)
        {
            currentPage = index;

            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = (i == index);
            }

            stepRail.ActiveIndex = index;
            tooltip.HideTip();

            if (index == 1)
            {
                SizeOptionsFields(pages[1]);
            }

            if (index == 3 && runState == RunState.Normal)
            {
                WriteSummary();
            }

            UpdateNavigation();

            if (IsHandleCreated && primaryButton.CanFocus)
            {
                ActiveControl = primaryButton;
            }
        }

        private void GoBack()
        {
            if (runState == RunState.Installing)
            {
                return;
            }

            if (currentPage > 0)
            {
                ShowPage(currentPage - 1);
            }
        }

        private void PrimaryClick(object sender, EventArgs e)
        {
            if (runState == RunState.Done)
            {
                Close();
                return;
            }

            if (runState == RunState.Installing)
            {
                return;
            }

            if (currentPage < 3)
            {
                ShowPage(currentPage + 1);
                return;
            }

            StartInstall();
        }

        private void CancelClick(object sender, EventArgs e)
        {
            if (runState == RunState.Installing)
            {
                StopInstall();
                return;
            }

            Close();
        }

        private void UpdateNavigation()
        {
            if (runState == RunState.Installing)
            {
                backButton.Visible = false;
                primaryButton.Visible = false;
                cancelButton.Text = "Stop";
                cancelButton.Enabled = true;
                LayoutFooter();
                return;
            }

            if (runState == RunState.Done)
            {
                backButton.Visible = false;
                cancelButton.Visible = false;
                primaryButton.Visible = true;
                primaryButton.Text = "Close";
                LayoutFooter();
                return;
            }

            backButton.Visible = currentPage > 0;
            primaryButton.Visible = true;
            cancelButton.Visible = true;
            cancelButton.Text = "Cancel";
            primaryButton.Text = (currentPage == 3) ? "Install" : "Next";

            bool canInstall = (currentPage != 3) || File.Exists(installScriptPath);
            primaryButton.Enabled = canInstall;

            LayoutFooter();
        }

        private void WriteSummary()
        {
            logBox.Clear();
            AppendLog("The following will be configured:", Palette.TextMuted);
            AppendLog("", Palette.TextMuted);

            string scope = scopeAllUsersCard.Selected ? "all users" : "current user";
            AppendLog("  Profile name      " + SafeText(displayNameField.Text, Environment.UserName), Palette.TextDark);
            AppendLog("  Starting folder   " + SafeText(startingDirectoryField.Text, GetUserProfileDirectory()), Palette.TextDark);
            AppendLog("  PATH scope        " + scope, Palette.TextDark);
            AppendLog("", Palette.TextMuted);

            int count = 0;

            foreach (KeyValuePair<string, SelectCard> pair in toolCards)
            {
                if (pair.Value.Selected)
                {
                    count++;
                }
            }

            AppendLog("  " + count + " components selected.", Palette.Accent);
        }

        private static string SafeText(string value, string fallback)
        {
            if (value == null || value.Trim().Length == 0)
            {
                return fallback;
            }

            return value.Trim();
        }

        // ---- Preset / option helpers ----

        private void ApplyPreset(string preset)
        {
            Dictionary<string, bool> defaults = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            defaults["InstallDependencies"] = true;
            defaults["InstallPhp"] = true;
            defaults["InstallComposer"] = true;
            defaults["InstallLaravel"] = true;
            defaults["InstallPint"] = true;
            defaults["InstallValet"] = false;
            defaults["InstallEnvoy"] = false;
            defaults["InstallVapor"] = false;
            defaults["InstallGit"] = true;
            defaults["InstallNode"] = true;
            defaults["InstallNvm"] = false;
            defaults["InstallGhCli"] = true;
            defaults["InstallPest"] = true;
            defaults["InstallLarastan"] = true;
            defaults["InstallRector"] = false;
            defaults["InstallRay"] = false;
            defaults["InstallMkcert"] = true;
            defaults["InstallRedis"] = false;
            defaults["InstallDocker"] = false;
            defaults["InstallTablePlus"] = true;
            defaults["InstallFzf"] = true;
            defaults["InstallBat"] = true;
            defaults["InstallRipgrep"] = true;
            defaults["InstallLazygit"] = false;
            defaults["UpdateTools"] = false;

            if (string.Equals(preset, "Minimal", StringComparison.OrdinalIgnoreCase))
            {
                defaults["InstallPhp"] = false;
                defaults["InstallComposer"] = false;
                defaults["InstallLaravel"] = false;
                defaults["InstallPint"] = false;
                defaults["InstallNode"] = false;
                defaults["InstallGhCli"] = false;
                defaults["InstallPest"] = false;
                defaults["InstallLarastan"] = false;
                defaults["InstallMkcert"] = false;
                defaults["InstallTablePlus"] = false;
                defaults["InstallFzf"] = false;
                defaults["InstallBat"] = false;
                defaults["InstallRipgrep"] = false;
            }

            if (string.Equals(preset, "Full", StringComparison.OrdinalIgnoreCase))
            {
                defaults["InstallValet"] = true;
                defaults["InstallEnvoy"] = true;
                defaults["InstallVapor"] = true;
                defaults["InstallRector"] = true;
                defaults["InstallRedis"] = true;
                defaults["InstallLazygit"] = true;
                defaults["UpdateTools"] = true;
            }

            foreach (KeyValuePair<string, bool> pair in defaults)
            {
                SelectCard card;
                if (toolCards.TryGetValue(pair.Key, out card))
                {
                    card.Selected = pair.Value;
                }
            }

            UpdatePhpVersionEnabled();
        }

        private void BrowseFolder(ThemedTextField field, string description)
        {
            string initial = (!string.IsNullOrEmpty(field.Text) && Directory.Exists(field.Text)) ? field.Text : null;
            string picked = ModernFolderDialog.Show(Handle, description, initial);

            if (!string.IsNullOrEmpty(picked))
            {
                field.Text = picked;
            }
        }

        // ---- Install execution ----

        private void StartInstall()
        {
            if (installProcess != null && !installProcess.HasExited)
            {
                return;
            }

            if (!File.Exists(installScriptPath))
            {
                statusLabel.Text = "install.ps1 is missing.";
                AppendLog("install.ps1 was not found at: " + installScriptPath, Palette.Danger);
                return;
            }

            string startingDirectory = PrepareStartingDirectory();

            runState = RunState.Installing;
            installTitleLabel.Text = "Installing";
            statusLabel.Text = "Working...";
            statusLabel.ForeColor = Palette.TextMuted;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 24;
            UpdateNavigation();

            logBox.Clear();
            AppendLog("Starting installation.", Palette.Accent);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetPowerShellPath();
            startInfo.Arguments = BuildPowerShellArguments(startingDirectory);
            startInfo.WorkingDirectory = supportRoot;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            installProcess = new Process();
            installProcess.StartInfo = startInfo;
            installProcess.EnableRaisingEvents = true;
            installProcess.OutputDataReceived += delegate(object s, DataReceivedEventArgs args)
            {
                if (args.Data != null)
                {
                    AppendLog(args.Data, ColorForLine(args.Data, false));
                }
            };
            installProcess.ErrorDataReceived += delegate(object s, DataReceivedEventArgs args)
            {
                if (args.Data != null)
                {
                    AppendLog(args.Data, ColorForLine(args.Data, true));
                }
            };
            installProcess.Exited += InstallProcessExited;

            try
            {
                installProcess.Start();
                installProcess.BeginOutputReadLine();
                installProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLog("Could not start PowerShell: " + ex.Message, Palette.Danger);
                FinishInstall(1);
            }
        }

        private void StopInstall()
        {
            if (installProcess == null || installProcess.HasExited)
            {
                return;
            }

            AppendLog("Cancel requested. Stopping the installer.", Palette.Warning);
            statusLabel.Text = "Cancelling...";
            cancelButton.Enabled = false;

            try
            {
                installProcess.Kill();
            }
            catch (Exception ex)
            {
                AppendLog("Could not stop the installer: " + ex.Message, Palette.Danger);
            }
        }

        private string PrepareStartingDirectory()
        {
            string startingDirectory = startingDirectoryField.Text.Trim();

            if (string.IsNullOrWhiteSpace(startingDirectory))
            {
                return string.Empty;
            }

            if (!Directory.Exists(startingDirectory))
            {
                try
                {
                    Directory.CreateDirectory(startingDirectory);
                    AppendLog("Created starting directory: " + startingDirectory, Palette.TextMuted);
                }
                catch (Exception ex)
                {
                    AppendLog("Could not create starting directory, falling back to your profile folder. " + ex.Message, Palette.Warning);
                    return string.Empty;
                }
            }

            return startingDirectory;
        }

        private string BuildPowerShellArguments(string startingDirectory)
        {
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Quote(installScriptPath));

            string displayName = displayNameField.Text.Trim();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                arguments.Add("-DisplayName");
                arguments.Add(Quote(displayName));
            }

            if (!string.IsNullOrWhiteSpace(startingDirectory))
            {
                arguments.Add("-StartingDirectory");
                arguments.Add(Quote(startingDirectory));
            }

            arguments.Add("-InstallScope");
            arguments.Add(scopeAllUsersCard.Selected ? "AllUsers" : "CurrentUser");

            string phpDirectory = phpDirectoryField.Text.Trim();
            if (!string.IsNullOrWhiteSpace(phpDirectory))
            {
                arguments.Add("-PhpDirectory");
                arguments.Add(Quote(phpDirectory));
            }
            else if (IsChecked("InstallPhp"))
            {
                arguments.Add("-InstallPhp");
                arguments.Add("-PhpVersion");
                arguments.Add(Convert.ToString(phpVersionBox.SelectedItem));
            }

            string[] switchKeys = new[]
            {
                "InstallDependencies", "InstallComposer", "InstallLaravel", "InstallValet",
                "InstallPint", "InstallEnvoy", "InstallVapor", "InstallGit", "InstallNode",
                "InstallNvm", "InstallGhCli", "InstallPest", "InstallLarastan", "InstallRector",
                "InstallRay", "InstallMkcert", "InstallRedis", "InstallDocker", "InstallTablePlus",
                "InstallFzf", "InstallBat", "InstallRipgrep", "InstallLazygit", "UpdateTools"
            };

            foreach (string key in switchKeys)
            {
                if (IsChecked(key))
                {
                    arguments.Add("-" + key);
                }
            }

            return string.Join(" ", arguments.ToArray());
        }

        private bool IsChecked(string key)
        {
            SelectCard card;
            if (toolCards.TryGetValue(key, out card))
            {
                return card.Selected;
            }

            return false;
        }

        private void InstallProcessExited(object sender, EventArgs e)
        {
            int exitCode = 1;

            try
            {
                exitCode = installProcess.ExitCode;
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
            runState = RunState.Done;
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.Value = exitCode == 0 ? 100 : 0;

            if (exitCode == 0)
            {
                installTitleLabel.Text = "All done";
                statusLabel.Text = "Open a new Windows Terminal tab to see your setup.";
                statusLabel.ForeColor = Palette.Success;
                AppendLog("", Palette.TextMuted);
                AppendLog("Install complete.", Palette.Success);
            }
            else
            {
                installTitleLabel.Text = "Install incomplete";
                statusLabel.Text = "Some steps did not finish. Review the log below.";
                statusLabel.ForeColor = Palette.Danger;
                AppendLog("", Palette.TextMuted);
                AppendLog("Installer exited with code " + exitCode + ".", Palette.Danger);
            }

            if (installProcess != null)
            {
                installProcess.Dispose();
                installProcess = null;
            }

            UpdateNavigation();
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
            if (runState != RunState.Installing || statusLabel == null)
            {
                return;
            }

            string statusText = message == null ? string.Empty : message.Trim();

            if (statusText.StartsWith("[MBS-Terminal]", StringComparison.OrdinalIgnoreCase))
            {
                statusText = statusText.Substring("[MBS-Terminal]".Length).Trim();
            }

            if (statusText.Length == 0 || statusText.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (statusText.Length > 80)
            {
                statusText = statusText.Substring(0, 77).TrimEnd() + "...";
            }

            statusLabel.Text = statusText;
        }

        private static Color ColorForLine(string line, bool isError)
        {
            if (isError)
            {
                if (IsComposerProgressLine(line))
                {
                    return Palette.TextDark;
                }

                return Palette.Danger;
            }

            if (line.IndexOf("Warning:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Palette.Warning;
            }

            if (line.IndexOf("Done.", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("installed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Palette.Success;
            }

            return Palette.TextDark;
        }

        private static bool IsComposerProgressLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            string text = line.Trim();

            if (text.StartsWith("Changed current directory to ", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("composer.json has been created", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("- Installing ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("- Downloading ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("- Locking ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("- Upgrading ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Installing dependencies", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Writing lock file", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Generating autoload files", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Using version ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Package operations:", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("No security vulnerability advisories found", StringComparison.OrdinalIgnoreCase)
                || text.IndexOf(" package suggestions were added by new dependencies", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(" packages you are using are looking for funding", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("composer fund", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Extracting archive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            bool hasProgressCharacters = false;

            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];

                if (character == '[' || character == ']' || character == '=' || character == '>' || character == '-'
                    || character == ' ' || character == '%' || char.IsDigit(character) || character == '/')
                {
                    if (character == '[' || character == ']' || character == '=' || character == '>' || character == '%')
                    {
                        hasProgressCharacters = true;
                    }

                    continue;
                }

                return false;
            }

            return hasProgressCharacters;
        }

        private void SetupFormClosing(object sender, FormClosingEventArgs e)
        {
            if (installProcess != null && !installProcess.HasExited)
            {
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
                    installProcess.Kill();
                }
                catch
                {
                }
            }

            if (tooltip != null)
            {
                tooltip.Close();
                tooltip.Dispose();
                tooltip = null;
            }

            if (extractedSupportRoot)
            {
                TryDeleteDirectory(supportRoot);
            }
        }

        // ---- Infrastructure ----

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

        private static string ResolveSupportRoot()
        {
            string executablePath = typeof(SetupForm).Assembly.Location;
            string executableDirectory = Path.GetDirectoryName(executablePath);

            if (!string.IsNullOrWhiteSpace(executableDirectory)
                && File.Exists(Path.Combine(executableDirectory, "install.ps1")))
            {
                return executableDirectory;
            }

            return string.Empty;
        }

        private static string CreateExtractionRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "MBS-Terminal-Setup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Process.GetCurrentProcess().Id
            );

            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }

        private static string GetUserProfileDirectory()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrWhiteSpace(profile))
            {
                return string.Empty;
            }

            return profile;
        }

        private Icon LoadWindowIcon()
        {
            string repoIcon = Path.Combine(supportRoot, @"assets\terminal-icons\mbs-terminal.ico");

            try
            {
                if (File.Exists(repoIcon))
                {
                    return new Icon(repoIcon);
                }
            }
            catch
            {
            }

            try
            {
                return Icon.ExtractAssociatedIcon(typeof(SetupForm).Assembly.Location);
            }
            catch
            {
            }

            return null;
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static Label MakeLabel(string text, float size, FontStyle style, Color color, int left, int top, int width, int height)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Text = text;
            label.Font = Fonts.Ui(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.TopLeft;
            label.SetBounds(left, top, width, height);
            return label;
        }
    }
}
