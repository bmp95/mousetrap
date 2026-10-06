using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CentrarRaton
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool first;
            using (new Mutex(true, "CentrarRaton.SingleInstance", out first))
            {
                if (!first) return;
                string configPath = args.Length == 2 && args[0] == "--config"
                    ? args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CentrarRaton", "config.ini");
                Application.EnableVisualStyles();
                using (new TrayApp(configPath)) Application.Run();
            }
        }
    }

    // Lives in the notification area, watches the two mouse buttons and, when they
    // have been held together long enough, puts the pointer in the middle of a screen.
    sealed class TrayApp : IDisposable
    {
        const string AppName = "CentrarRaton";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static readonly bool Spanish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";

        readonly string configPath;
        readonly Settings settings;
        readonly HoldDetector detector;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Halo halo = new Halo();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Icon icon = DrawIcon();

        public TrayApp(string configPath)
        {
            this.configPath = configPath;
            settings = Settings.Parse(ReadOrEmpty(configPath));
            detector = new HoldDetector(settings.HoldMs);

            string seconds = (settings.HoldMs / 1000.0).ToString("0.#");
            tray.Icon = icon;
            tray.Text = T("Centrar ratón: mantén los dos botones " + seconds + " s",
                          "Hold both mouse buttons for " + seconds + " s to centre the pointer");
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            // An empty menu cancels its own opening unless told otherwise.
            menu.Opening += (sender, e) => { BuildMenu(); e.Cancel = false; };

            timer.Interval = 50;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        void Tick()
        {
            bool left = Native.IsDown(Native.VK_LBUTTON), right = Native.IsDown(Native.VK_RBUTTON);
            long now = clock.ElapsedMilliseconds;
            if (detector.Update(left, right, now)) Jump();
            halo.Update(left || right, now);
        }

        void Jump()
        {
            Display target = Targeting.Pick(settings.Target, Displays(), Cursor.Position);
            Native.CancelPressInProgress();
            Native.SetCursorPos(target.Center.X, target.Center.Y);
            halo.ShowAt(target.Center, Math.Min(target.Bounds.Width, target.Bounds.Height) / 6);
        }

        static List<Display> Displays()
        {
            List<Display> all = new List<Display>();
            foreach (Screen screen in Screen.AllScreens)
            {
                // Some Windows versions leave junk after the terminator in the device name.
                string name = screen.DeviceName;
                int end = name.IndexOf('\0');
                all.Add(new Display(end < 0 ? name : name.Substring(0, end), screen.Bounds, screen.Primary));
            }
            all.Sort((a, b) => a.Number.CompareTo(b.Number));
            return all;
        }

        // Rebuilt each time it opens so it always lists the screens plugged in right now.
        void BuildMenu()
        {
            menu.Items.Clear();
            ToolStripMenuItem heading = new ToolStripMenuItem(T("Llevar el puntero al centro de:", "Send the pointer to the middle of:"));
            heading.Enabled = false;
            menu.Items.Add(heading);
            AddTarget(T("La pantalla principal", "The main screen"), Settings.Primary);
            AddTarget(T("La pantalla donde ya esté el puntero", "The screen the pointer is already on"), Settings.UnderCursor);
            foreach (Display d in Displays())
            {
                string size = d.Bounds.Width + " × " + d.Bounds.Height + (d.Primary ? T(", principal", ", main") : "");
                AddTarget(T("Pantalla ", "Screen ") + d.Number + "  (" + size + ")", d.Name);
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem startup = new ToolStripMenuItem(T("Iniciar con Windows", "Start with Windows"));
            startup.Checked = StartsWithWindows();
            startup.Click += delegate { SetStartsWithWindows(!StartsWithWindows()); };
            menu.Items.Add(startup);
            ToolStripMenuItem exit = new ToolStripMenuItem(T("Salir", "Exit"));
            exit.Click += delegate { Application.Exit(); };
            menu.Items.Add(exit);
        }

        void AddTarget(string label, string target)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Checked = string.Equals(settings.Target, target, StringComparison.OrdinalIgnoreCase);
            item.Click += delegate { settings.Target = target; Save(); };
            menu.Items.Add(item);
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, settings.ToText());
            }
            catch (Exception e)
            {
                // The choice still applies until the app closes; say why it won't be remembered.
                tray.ShowBalloonTip(5000, AppName, e.Message, ToolTipIcon.Warning);
            }
        }

        static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        static bool StartsWithWindows()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(AppName) != null;
        }

        static void SetStartsWithWindows(bool on)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(AppName, false);
            }
        }

        static Icon DrawIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                using (Pen ring = new Pen(Halo.Colour, 4))
                using (Brush dot = new SolidBrush(Halo.Colour))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawEllipse(ring, 3, 3, 26, 26);
                    g.FillEllipse(dot, 11, 11, 10, 10);
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }

        static string T(string spanish, string english) { return Spanish ? spanish : english; }

        public void Dispose()
        {
            timer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
            halo.Dispose();
            icon.Dispose();
        }
    }

    // A translucent disc shown under the pointer after a jump. It marks the spot, and
    // because it is the window under the pointer it also receives the button releases,
    // so they don't click or open a context menu on whatever lies beneath.
    sealed class Halo : Form
    {
        public static readonly Color Colour = Color.FromArgb(255, 150, 0);
        const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        const int LingerMs = 700;

        long releasedAt = -1;

        public Halo()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Colour;
            Opacity = 0.5;
        }

        // Must never take focus away from what the user was doing.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        public void ShowAt(Point centre, int diameter)
        {
            Bounds = new Rectangle(centre.X - diameter / 2, centre.Y - diameter / 2, diameter, diameter);
            Region old = Region;
            using (GraphicsPath disc = new GraphicsPath())
            {
                disc.AddEllipse(0, 0, diameter, diameter);
                Region = new Region(disc);
            }
            if (old != null) old.Dispose();
            releasedAt = -1;
            Show();
        }

        // Stays while a button is still down, then lingers a moment so the eye can find it.
        public void Update(bool buttonDown, long now)
        {
            if (!Visible) return;
            if (buttonDown) { releasedAt = -1; return; }
            if (releasedAt < 0) releasedAt = now;
            if (now - releasedAt >= LingerMs) Hide();
        }
    }

    static class Native
    {
        public const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
        const int WM_CANCELMODE = 0x001F;
        const uint SMTO_ABORTIFHUNG = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct GUITHREADINFO
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr w, IntPtr l, uint flags, uint timeoutMs, out IntPtr result);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        // Whatever the buttons were pressed on (a button, a text selection, a title bar)
        // is still mid-press. Tell it to give up, as Windows itself does when a dialog
        // pops up, so the jump isn't taken for a drag and the release for a click.
        public static void CancelPressInProgress()
        {
            GUITHREADINFO info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (!GetGUIThreadInfo(0, ref info)) return;
            IntPtr busy = info.hwndCapture != IntPtr.Zero ? info.hwndCapture : info.hwndMoveSize;
            if (busy == IntPtr.Zero) return;
            IntPtr ignored;
            SendMessageTimeout(busy, WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 200, out ignored);
        }
    }
}
