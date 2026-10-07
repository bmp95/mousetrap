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

namespace Mousetrap
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool first;
            using (new Mutex(true, "Mousetrap.SingleInstance", out first))
            {
                if (!first) return;
                string configPath = args.Length == 2 && args[0] == "--config"
                    ? args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mousetrap", "config.ini");
                Application.EnableVisualStyles();
                using (new TrayApp(configPath)) Application.Run();
            }
        }
    }

    static class Lang
    {
        static readonly bool Spanish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";

        public static string T(string spanish, string english) { return Spanish ? spanish : english; }
    }

    // Lives in the notification area, watches the two mouse buttons and the keyboard
    // shortcut and, when either has been held long enough, puts the pointer in the
    // middle of a screen.
    sealed class TrayApp : IDisposable
    {
        const string AppName = "Mousetrap";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly string configPath;
        readonly Settings settings;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Halo halo = new Halo();
        readonly Shortcut shortcut = new Shortcut();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Icon icon = DrawIcon();
        HoldDetector mouseHold, keysHold;
        SettingsForm window;

        public TrayApp(string configPath)
        {
            this.configPath = configPath;
            settings = Settings.Parse(ReadOrEmpty(configPath));

            tray.Icon = icon;
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) OpenSettings(); };
            tray.Visible = true;
            // An empty menu cancels its own opening unless told otherwise.
            menu.Opening += (sender, e) => { BuildMenu(); e.Cancel = false; };

            if (!PutToWork())
                tray.ShowBalloonTip(8000, AppName, T("Windows u otro programa ya usa " + settings.Hotkey + ". Elige otro atajo en Ajustes.",
                                                     "Windows or another program already uses " + settings.Hotkey + ". Pick another shortcut in Settings."), ToolTipIcon.Warning);

            timer.Interval = 50;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        // Makes the current settings take effect: hold time, shortcut and tooltip.
        // False when the shortcut already belongs to Windows or to another program.
        bool PutToWork()
        {
            mouseHold = new HoldDetector(settings.HoldMs);
            keysHold = new HoldDetector(settings.HoldMs);
            tray.Text = Tooltip();
            return shortcut.Register(settings.Hotkey);
        }

        string Tooltip()
        {
            string time = (settings.HoldMs / 1000.0).ToString("0.#") + " s";
            string keys = settings.Hotkey == null ? null : settings.Hotkey.ToString();
            string text =
                keys == null ? T("Mousetrap: mantén los dos botones " + time, "Mousetrap: hold both mouse buttons for " + time)
                : settings.Mouse ? T("Mousetrap: dos botones o " + keys + ", " + time, "Mousetrap: both buttons or " + keys + ", " + time)
                : T("Mousetrap: mantén " + keys + " " + time, "Mousetrap: hold " + keys + " for " + time);
            // The notification area throws on anything longer.
            return text.Length > 63 ? text.Substring(0, 63) : text;
        }

        void Tick()
        {
            bool left = Native.IsDown(Native.VK_LBUTTON), right = Native.IsDown(Native.VK_RBUTTON);
            bool keys = shortcut.Held();
            long now = clock.ElapsedMilliseconds;
            bool byMouse = mouseHold.Update(settings.Mouse && left && right, now);
            bool byKeys = keysHold.Update(keys, now);
            if (byMouse || byKeys) Jump();
            halo.Update(left || right || keys, now);
        }

        void OpenSettings()
        {
            if (window != null)
            {
                window.Activate();
                return;
            }
            // Let go of the shortcut while the window is open, so that pressing it there records it.
            shortcut.Register(null);
            window = new SettingsForm(settings.Copy(), icon, Adopt);
            window.FormClosed += delegate
            {
                window = null;
                shortcut.Register(settings.Hotkey);
            };
            window.Show();
            window.Activate();
        }

        // What the settings window calls on Save. False, with nothing changed, when
        // the shortcut already belongs to Windows or to another program.
        bool Adopt(Settings wanted)
        {
            if (!shortcut.Register(wanted.Hotkey)) return false;
            settings.Mouse = wanted.Mouse;
            settings.Hotkey = wanted.Hotkey;
            settings.HoldMs = wanted.HoldMs;
            PutToWork();
            Save();
            return true;
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
            ToolStripMenuItem options = new ToolStripMenuItem(T("Ajustes…", "Settings…"));
            options.Click += delegate { OpenSettings(); };
            menu.Items.Add(options);
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

        static string T(string spanish, string english) { return Lang.T(spanish, english); }

        public void Dispose()
        {
            timer.Dispose();
            shortcut.Dispose();
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

    // The keyboard shortcut, registered with Windows as a system-wide hot key. Windows
    // keeps a registered hot key to itself, so holding it types nothing into the
    // program in front, and no keyboard hook is needed.
    sealed class Shortcut : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312, Id = 1;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4;
        static readonly IntPtr MessageOnly = new IntPtr(-3);

        Hotkey registered;
        bool pressed;

        public Shortcut()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = MessageOnly;
            CreateHandle(cp);
        }

        // Null registers nothing. False when the shortcut already belongs to someone else.
        public bool Register(Hotkey hotkey)
        {
            if (registered != null) Native.UnregisterHotKey(Handle, Id);
            registered = null;
            pressed = false;
            if (hotkey == null) return true;
            uint modifiers = (hotkey.Ctrl ? MOD_CONTROL : 0) | (hotkey.Alt ? MOD_ALT : 0) | (hotkey.Shift ? MOD_SHIFT : 0);
            if (!Native.RegisterHotKey(Handle, Id, modifiers, (uint)hotkey.Key)) return false;
            registered = hotkey;
            return true;
        }

        // True from the moment the shortcut is pressed until one of its keys is let go.
        public bool Held()
        {
            if (!pressed) return false;
            pressed = Native.IsDown(registered.Key)
                && (!registered.Ctrl || Native.IsDown(Native.VK_CONTROL))
                && (!registered.Alt || Native.IsDown(Native.VK_MENU))
                && (!registered.Shift || Native.IsDown(Native.VK_SHIFT));
            return pressed;
        }

        protected override void WndProc(ref Message m)
        {
            // Sent again and again while the keys stay down; the first one is all it takes.
            // One still in the queue when the shortcut was dropped must not count.
            if (m.Msg == WM_HOTKEY && registered != null) pressed = true;
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Register(null);
            DestroyHandle();
        }
    }

    static class Native
    {
        public const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
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

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicode(uint key, uint scanCode, byte[] keyState, [Out] char[] text, int capacity, uint flags);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        // How many times bigger than at 96 dpi things are on the monitor the window is on.
        public static float ZoomOf(IntPtr hwnd)
        {
            try { return GetDpiForWindow(hwnd) / 96f; }
            catch (EntryPointNotFoundException) { return 1f; }   // Windows 10 before 1607
        }

        // What Ctrl+Alt+key types on this keyboard (AltGr+2 is @ on a Spanish one), or
        // null. As a shortcut it would leave that character impossible to type.
        public static string TypedBy(Hotkey hotkey)
        {
            const uint LeaveKeyboardStateAlone = 4;
            if (!hotkey.Ctrl || !hotkey.Alt) return null;
            byte[] state = new byte[256];
            state[VK_CONTROL] = state[VK_MENU] = 0x80;
            if (hotkey.Shift) state[VK_SHIFT] = 0x80;
            char[] text = new char[8];
            // Negative for a dead key such as the tilde, which would be lost just the same.
            int typed = ToUnicode((uint)hotkey.Key, MapVirtualKey((uint)hotkey.Key, 0), state, text, text.Length, LeaveKeyboardStateAlone);
            return typed == 0 || char.IsControl(text[0]) ? null : text[0].ToString();
        }

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
