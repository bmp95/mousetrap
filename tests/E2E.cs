using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// End-to-end check against the real exe, the real mouse and the real keyboard. It
// presses buttons and keys with synthetic input over a window of its own and watches
// where the pointer ends up, so it takes over both while it runs (about 30 s).
// Run it with: test.cmd e2e
static class E2E
{
    const uint LeftDown = 0x02, LeftUp = 0x04, RightDown = 0x08, RightUp = 0x10, KeyUp = 0x02;
    const int WM_CONTEXTMENU = 0x007B, WM_QUIT = 0x0012;
    const byte Control = 0x11, Alt = 0x12, F9 = 0x78;

    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(int threadId, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);

    // Something ordinary to press on: a button that would be clicked, inside a
    // window that hears about any context menu the press would have opened and
    // about every F9 that reaches it.
    sealed class Probe : Form
    {
        public int Clicks, ContextMenus, F9s;
        public readonly Button Target = new Button();

        public Probe(Point location)
        {
            Text = "Mousetrap E2E";
            StartPosition = FormStartPosition.Manual;
            Location = location;
            ClientSize = new Size(360, 220);
            TopMost = true;
            KeyPreview = true;
            Target.Text = "probe";
            Target.Dock = DockStyle.Fill;
            Target.Click += delegate { Clicks++; };
            Controls.Add(Target);
        }

        public Point Middle
        {
            get { return Target.PointToScreen(new Point(Target.Width / 2, Target.Height / 2)); }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CONTEXTMENU) ContextMenus++;
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9) F9s++;
            base.OnKeyDown(e);
        }
    }

    static int failed;
    static bool leftHeld, rightHeld;
    static readonly System.Collections.Generic.List<byte> keysHeld = new System.Collections.Generic.List<byte>();
    static Process app;
    static string exe, config;

    [STAThread]
    static int Main(string[] args)
    {
        exe = Path.GetFullPath(args[0]);
        config = Path.Combine(Path.GetTempPath(), "mousetrap-e2e.ini");
        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)).Length > 0)
        {
            Console.WriteLine("Mousetrap is already running. Exit it from its tray icon and try again.");
            return 2;
        }
        if (Process.GetProcessesByName("LogonUI").Length > 0)
        {
            Console.WriteLine("The Windows session is locked, so there is no desktop to test on. Unlock it and try again.");
            return 2;
        }

        Point before = Cursor.Position;
        string startup = StartupEntry();
        try
        {
            Scenario("Default settings: hold both buttons for 3 s", null, HoldingThreeSecondsJumpsToTheMainScreen);
            Scenario("Default settings: let go after 1.5 s", null, LettingGoEarlyLeavesThePointerAlone);
            // Only the installed app sets itself up on its first run; one run with --config must not.
            Check("no settings file was written on its own", !File.Exists(config), config);
            Check("the Windows startup list was left alone", StartupEntry() == startup, StartupEntry());
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.Primary) continue;
                Screen chosen = screen;
                Scenario("Config file: target=" + chosen.DeviceName + " (" + chosen.Bounds.Width + "x" + chosen.Bounds.Height + "), hold_ms=1000",
                    "target=" + chosen.DeviceName + "\r\nhold_ms=1000\r\n",
                    delegate(Probe probe, Point origin) { AChosenScreenAndHoldTimeAreHonoured(probe, origin, chosen); });
            }
            Scenario("Config file: hotkey=Ctrl+Alt+F9, hold_ms=1000",
                "hotkey=Ctrl+Alt+F9\r\nhold_ms=1000\r\n", false, HoldingTheShortcutJumpsToo);
            Scenario("Config file: mouse=off, hotkey=Ctrl+Alt+F9, hold_ms=1000",
                "mouse=off\r\nhotkey=Ctrl+Alt+F9\r\nhold_ms=1000\r\n", TheMouseGestureCanBeSwitchedOff);
        }
        finally
        {
            File.Delete(config);
            SetCursorPos(before.X, before.Y);
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "E2E passed" : "E2E FAILED (" + failed + ")");
        return failed == 0 ? 0 : 1;
    }

    static string StartupEntry()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            return key == null ? null : key.GetValue("Mousetrap") as string;
    }

    static void Scenario(string title, string configText, Action<Probe, Point> body)
    {
        Scenario(title, configText, true, body);
    }

    // Starts the app with the given config, puts the pointer over a fresh probe window,
    // pressing both buttons there unless told not to, runs the body, and always lets go
    // of the buttons and the keys and closes the app.
    static void Scenario(string title, string configText, bool pressButtons, Action<Probe, Point> body)
    {
        Console.WriteLine(title);
        if (configText == null) File.Delete(config); else File.WriteAllText(config, configText);
        StartApp();
        try
        {
            using (Probe probe = ShowProbe())
            {
                Point origin = probe.Middle;
                if (PointAt(probe, origin))
                {
                    if (pressButtons) PressBoth();
                    body(probe, origin);
                }
            }
        }
        finally
        {
            ReleaseButtons();
            ReleaseKeys();
            StopApp();
        }
    }

    static void HoldingThreeSecondsJumpsToTheMainScreen(Probe probe, Point origin)
    {
        Pump(2500);
        Check("the pointer has not moved after 2.5 s", Cursor.Position == origin, Cursor.Position);
        Pump(1000);
        Check("the pointer is in the middle of the main screen after 3.5 s", Near(Cursor.Position, Centre(Screen.PrimaryScreen)), Cursor.Position);
        Check("the halo is under the pointer, ready to swallow the releases", OwnerOf(Cursor.Position) == app.Id, Describe(Cursor.Position));

        // Right first: the order most likely to pop a context menu up.
        Release(RightUp);
        Pump(60);
        Release(LeftUp);
        Pump(400);
        Check("the button that was under the press did not get clicked", probe.Clicks == 0, probe.Clicks);
        Check("no context menu was asked for", probe.ContextMenus == 0, probe.ContextMenus);

        Pump(1500);
        Check("the halo is gone shortly after letting go", OwnerOf(Cursor.Position) != app.Id, Describe(Cursor.Position));
    }

    static void LettingGoEarlyLeavesThePointerAlone(Probe probe, Point origin)
    {
        Pump(1500);
        Release(LeftUp);
        Pump(60);
        Release(RightUp);
        Pump(2500);
        Check("the pointer stays where it was", Cursor.Position == origin, Cursor.Position);
        Check("the ordinary click still goes through", probe.Clicks == 1, probe.Clicks);
        Check("the ordinary context menu request still goes through", probe.ContextMenus == 1, probe.ContextMenus);
    }

    static void AChosenScreenAndHoldTimeAreHonoured(Probe probe, Point origin, Screen screen)
    {
        Pump(600);
        Check("the pointer has not moved after 0.6 s", Cursor.Position == origin, Cursor.Position);
        Pump(1000);
        Check("the pointer is in the middle of that screen after 1.6 s", Near(Cursor.Position, Centre(screen)), Cursor.Position);
        Check("the halo is under the pointer", OwnerOf(Cursor.Position) == app.Id, Describe(Cursor.Position));

        Release(LeftUp);
        Pump(60);
        Release(RightUp);
        Pump(400);
        Check("no click and no context menu", probe.Clicks == 0 && probe.ContextMenus == 0, probe.Clicks + "/" + probe.ContextMenus);
    }

    static void HoldingTheShortcutJumpsToo(Probe probe, Point origin)
    {
        // A click first, so that the probe is the window the keyboard is talking to,
        // and one F9 on its own to see that keys do get there.
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        Pump(200);
        Check("the probe window has the keyboard", GetForegroundWindow() == probe.Handle, Describe(Cursor.Position));
        Press(F9);
        ReleaseKeys();
        Pump(200);
        Check("a plain F9 reaches the window in front", probe.F9s == 1, probe.F9s);

        Press(Control);
        Press(Alt);
        Press(F9);
        Pump(600);
        Check("the pointer has not moved after 0.6 s", Cursor.Position == origin, Cursor.Position);
        Pump(1000);
        Check("the pointer is in the middle of the main screen after 1.6 s", Near(Cursor.Position, Centre(Screen.PrimaryScreen)), Cursor.Position);
        Check("the halo is under the pointer", OwnerOf(Cursor.Position) == app.Id, Describe(Cursor.Position));

        ReleaseKeys();
        Pump(400);
        Check("the shortcut never reached the window in front", probe.F9s == 1, probe.F9s);
        Pump(1500);
        Check("the halo is gone shortly after letting go", OwnerOf(Cursor.Position) != app.Id, Describe(Cursor.Position));
    }

    static void TheMouseGestureCanBeSwitchedOff(Probe probe, Point origin)
    {
        Pump(1600);
        Check("holding both buttons does not move the pointer", Cursor.Position == origin, Cursor.Position);
    }

    static Probe ShowProbe()
    {
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        Probe probe = new Probe(new Point(area.Left + 80, area.Top + 80));
        probe.Show();
        probe.Activate();
        Pump(300);
        return probe;
    }

    // False, so that nothing gets pressed, unless the probe really is what is under the pointer.
    static bool PointAt(Probe probe, Point origin)
    {
        SetCursorPos(origin.X, origin.Y);
        Pump(150);
        if (WindowFromPoint(Cursor.Position) == probe.Target.Handle) return true;
        Check("the probe window is under the pointer", false, Describe(Cursor.Position));
        return false;
    }

    static void PressBoth()
    {
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        leftHeld = true;
        Pump(60);
        mouse_event(RightDown, 0, 0, 0, UIntPtr.Zero);
        rightHeld = true;
    }

    static void Press(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keysHeld.Add(key);
        Pump(40);
    }

    // Last pressed, first let go, as fingers do.
    static void ReleaseKeys()
    {
        for (int i = keysHeld.Count - 1; i >= 0; i--) keybd_event(keysHeld[i], 0, KeyUp, UIntPtr.Zero);
        keysHeld.Clear();
    }

    static void Release(uint button)
    {
        mouse_event(button, 0, 0, 0, UIntPtr.Zero);
        if (button == LeftUp) leftHeld = false; else rightHeld = false;
    }

    static void ReleaseButtons()
    {
        if (leftHeld) Release(LeftUp);
        if (rightHeld) Release(RightUp);
    }

    static void StartApp()
    {
        // Through the shell so the app does not inherit this console's output handles.
        ProcessStartInfo info = new ProcessStartInfo(exe, "--config \"" + config + "\"");
        info.UseShellExecute = true;
        app = Process.Start(info);
        Pump(1000);
    }

    // Asks the app to leave through its own message loop so its tray icon is removed.
    static void StopApp()
    {
        if (app == null) return;
        if (!app.HasExited)
        {
            foreach (ProcessThread thread in app.Threads)
                PostThreadMessage(thread.Id, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            if (!app.WaitForExit(3000))
            {
                Check("the app closes when asked to quit", false, "had to be killed");
                app.Kill();
                app.WaitForExit(3000);
            }
        }
        app = null;
    }

    static int OwnerOf(Point p)
    {
        uint pid;
        GetWindowThreadProcessId(WindowFromPoint(p), out pid);
        return (int)pid;
    }

    static string Describe(Point p)
    {
        IntPtr window = WindowFromPoint(p);
        StringBuilder className = new StringBuilder(256);
        GetClassName(window, className, className.Capacity);
        string process = "?";
        try { process = Process.GetProcessById(OwnerOf(p)).ProcessName; }
        catch (ArgumentException) { }
        return "pointer at " + p + " over a " + className + " window of " + process;
    }

    static Point Centre(Screen s)
    {
        return new Point(s.Bounds.Left + s.Bounds.Width / 2, s.Bounds.Top + s.Bounds.Height / 2);
    }

    static bool Near(Point a, Point b)
    {
        return Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
    }

    static void Pump(int ms)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    static void Check(string what, bool ok, object actual)
    {
        if (ok) { Console.WriteLine("  ok    " + what); return; }
        failed++;
        Console.WriteLine("  FAIL  " + what + " (got: " + actual + ")");
    }
}
