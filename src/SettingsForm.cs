using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Mousetrap
{
    // The look of the settings window: warm paper, white cards, near-black ink and the
    // orange of the tray icon. Every measure is in 96-dpi pixels; whoever paints scales
    // the whole drawing by the zoom of the monitor it is on.
    static class Look
    {
        public static readonly Color Paper = Color.FromArgb(250, 247, 241);
        public static readonly Color Card = Color.White;
        public static readonly Color Line = Color.FromArgb(232, 226, 215);
        public static readonly Color Ink = Color.FromArgb(28, 25, 21);
        public static readonly Color Soft = Color.FromArgb(122, 115, 104);
        public static readonly Color Idle = Color.FromArgb(216, 210, 198);
        public static readonly Color Accent = Halo.Colour;
        public static readonly Color Warning = Color.FromArgb(186, 66, 18);

        // The app icon, large, for the head of the window.
        public static readonly Bitmap Emblem = Native.AppIcon(256).ToBitmap();

        public static readonly Font Title = Pixels("Georgia", 27);
        public static readonly Font Figure = Pixels("Segoe UI Semibold", 22);
        public static readonly Font Body = Pixels("Segoe UI", 15);
        public static readonly Font Strong = Pixels("Segoe UI Semibold", 14);
        public static readonly Font Small = Pixels("Segoe UI", 13);
        public static readonly Font Caps = Pixels("Segoe UI Semibold", 11);

        // Without the padding GDI+ adds around text, so that it lines up with what is drawn next to it.
        static readonly StringFormat Tight = new StringFormat(StringFormat.GenericTypographic);
        static readonly StringFormat Middle = new StringFormat(StringFormat.GenericTypographic);

        static Look()
        {
            Middle.Alignment = Middle.LineAlignment = StringAlignment.Center;
        }

        static Font Pixels(string family, float size)
        {
            return new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static void Prepare(Graphics g, float zoom)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(zoom, zoom);
        }

        static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Fill(Graphics g, Color colour, RectangleF r, float radius)
        {
            using (GraphicsPath path = Round(r, radius))
            using (Brush brush = new SolidBrush(colour)) g.FillPath(brush, path);
        }

        public static void Outline(Graphics g, Color colour, float width, RectangleF r, float radius)
        {
            using (GraphicsPath path = Round(r, radius))
            using (Pen pen = new Pen(colour, width)) g.DrawPath(pen, path);
        }

        public static float Width(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font, 2000, Tight).Width;
        }

        public static void Text(Graphics g, string text, Font font, Color colour, float x, float y)
        {
            using (Brush brush = new SolidBrush(colour)) g.DrawString(text, font, brush, x, y, Tight);
        }

        public static void Wrapped(Graphics g, string text, Font font, Color colour, RectangleF box)
        {
            using (Brush brush = new SolidBrush(colour)) g.DrawString(text, font, brush, box, Tight);
        }

        public static void Centered(Graphics g, string text, Font font, Color colour, RectangleF box)
        {
            using (Brush brush = new SolidBrush(colour)) g.DrawString(text, font, brush, box, Middle);
        }

        // A key as it looks on a keyboard, centred on the given height; moves x past it.
        public static void Keycap(Graphics g, string label, ref float x, float middle)
        {
            float width = Math.Max(32, Width(g, label, Strong) + 22);
            RectangleF cap = new RectangleF(x, middle - 14, width, 26);
            // The darker lip under the key is what makes it read as a key.
            Fill(g, Idle, new RectangleF(cap.X, cap.Y + 2.5f, cap.Width, cap.Height), 7);
            Fill(g, Card, cap, 7);
            Outline(g, Idle, 1, cap, 7);
            Centered(g, label, Strong, Ink, cap);
            x += width;
        }

        // How a key is written on its keycap.
        public static string KeyLabel(string name)
        {
            switch (name)
            {
                case "Shift": return Lang.T("Mayús", "Shift");
                case "Space": return Lang.T("Espacio", "Space");
                case "PageUp": return Lang.T("Re Pág", "PgUp");
                case "PageDown": return Lang.T("Av Pág", "PgDn");
                case "Home": return Lang.T("Inicio", "Home");
                case "End": return Lang.T("Fin", "End");
                case "Left": return "←";
                case "Up": return "↑";
                case "Right": return "→";
                case "Down": return "↓";
                default: return name;
            }
        }
    }

    // A control that paints itself in 96-dpi pixels, scaled by the zoom of its monitor.
    abstract class Drawn : Control
    {
        float zoom = 1f;

        protected Drawn()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
        }

        public float Zoom
        {
            get { return zoom; }
            set { zoom = value; Invalidate(); }
        }

        protected SizeF Area { get { return new SizeF(Width / zoom, Height / zoom); } }

        protected PointF At(MouseEventArgs e) { return new PointF(e.X / zoom, e.Y / zoom); }

        // The focus mark is only for someone finding their way with the keyboard.
        protected bool Ringed { get { return Focused && ShowFocusCues; } }

        protected abstract void Draw(Graphics g);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Look.Prepare(e.Graphics, zoom);
            Draw(e.Graphics);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    sealed class Toggle : Drawn
    {
        bool on;
        public event EventHandler Changed;

        public Toggle() { AccessibleRole = AccessibleRole.CheckButton; }

        public bool On
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        protected override void Draw(Graphics g)
        {
            RectangleF track = new RectangleF(2, 2, 44, 24);
            Look.Fill(g, on ? Look.Accent : Look.Idle, track, 12);
            using (Brush knob = new SolidBrush(Color.White)) g.FillEllipse(knob, on ? 24 : 4, 4, 20, 20);
            if (Ringed) Look.Outline(g, Look.Ink, 1.5f, RectangleF.Inflate(track, 1.25f, 1.25f), 13.25f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            On = !On;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) On = !On;
        }
    }

    sealed class Slider : Drawn
    {
        // Room for half the knob at each end of the track.
        const float Edge = 12;

        public int Minimum, Maximum = 100;
        int current;
        public event EventHandler Changed;

        public Slider() { AccessibleRole = AccessibleRole.Slider; }

        public int Value
        {
            get { return current; }
            set
            {
                int clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                if (clamped == current) return;
                current = clamped;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        protected override void Draw(Graphics g)
        {
            float middle = Area.Height / 2, length = Area.Width - 2 * Edge;
            float knob = Edge + length * (current - Minimum) / Math.Max(1, Maximum - Minimum);
            Look.Fill(g, Look.Idle, new RectangleF(Edge, middle - 2.5f, length, 5), 2.5f);
            if (knob - Edge >= 5) Look.Fill(g, Look.Accent, new RectangleF(Edge, middle - 2.5f, knob - Edge, 5), 2.5f);
            using (Brush shadow = new SolidBrush(Color.FromArgb(36, Look.Ink))) g.FillEllipse(shadow, knob - 11, middle - 9.5f, 22, 22);
            using (Brush face = new SolidBrush(Color.White)) g.FillEllipse(face, knob - 10, middle - 10.5f, 20, 20);
            using (Pen rim = new Pen(Ringed ? Look.Ink : Look.Accent, 2)) g.DrawEllipse(rim, knob - 10, middle - 10.5f, 20, 20);
        }

        void Follow(MouseEventArgs e)
        {
            float length = Area.Width - 2 * Edge;
            Value = Minimum + (int)Math.Round((At(e).X - Edge) / length * (Maximum - Minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Follow(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left) Follow(e);
        }

        // Otherwise the arrow keys would move the focus instead of the knob.
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value--;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value++;
            else if (e.KeyCode == Keys.PageDown) Value -= 10;
            else if (e.KeyCode == Keys.PageUp) Value += 10;
            else if (e.KeyCode == Keys.Home) Value = Minimum;
            else if (e.KeyCode == Keys.End) Value = Maximum;
        }
    }

    // Shows the keyboard shortcut as keycaps and records a new one: click it, press the keys.
    sealed class Recorder : Drawn
    {
        Hotkey hotkey;
        bool recording;
        // The modifiers held down so far while recording.
        Keys held;
        public event EventHandler Changed;

        // Why the last combination pressed was turned down; null when it was taken.
        public string Complaint { get; private set; }

        public Hotkey Hotkey
        {
            get { return hotkey; }
            set { hotkey = value; Invalidate(); }
        }

        public void Record()
        {
            if (!Enabled) return;
            Focus();
            recording = true;
            held = Keys.None;
            Invalidate();
        }

        void Stop()
        {
            recording = false;
            held = Keys.None;
            Invalidate();
        }

        protected override void Draw(Graphics g)
        {
            RectangleF field = new RectangleF(1, 1, Area.Width - 2, Area.Height - 2);
            Look.Fill(g, Look.Paper, field, 11);
            Look.Outline(g, recording ? Look.Accent : Ringed ? Look.Ink : Look.Line, recording ? 2 : 1, field, 11);

            float x = 12, middle = Area.Height / 2;
            List<string> keys = new List<string>();
            if (recording)
            {
                if ((held & Keys.Control) != 0) keys.Add("Ctrl");
                if ((held & Keys.Alt) != 0) keys.Add("Alt");
                if ((held & Keys.Shift) != 0) keys.Add("Shift");
            }
            else if (hotkey != null) keys.AddRange(hotkey.Parts);

            foreach (string key in keys)
            {
                if (x > 12) Plus(g, ref x, middle);
                Look.Keycap(g, Look.KeyLabel(key), ref x, middle);
            }
            if (recording && keys.Count > 0) Plus(g, ref x, middle);
            string hint = recording ? (keys.Count > 0 ? "…" : Lang.T("Pulsa las teclas…", "Press the keys…"))
                : hotkey == null ? Lang.T("Haz clic y pulsa las teclas", "Click and press the keys") : null;
            if (hint != null) Look.Text(g, hint, Look.Body, Look.Soft, x + 2, middle - 10);

            // Switched off: still readable, plainly not in use.
            if (!Enabled) Look.Fill(g, Color.FromArgb(170, Look.Card), new RectangleF(0, 0, Area.Width, Area.Height), 11);
        }

        static void Plus(Graphics g, ref float x, float middle)
        {
            Look.Centered(g, "+", Look.Body, Look.Soft, new RectangleF(x, middle - 14, 22, 26));
            x += 22;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Record();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Stop();
            base.OnLostFocus(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) Record();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (!recording) return;
            held = e.Modifiers;
            Invalidate();
        }

        // Every key press comes through here first, including the ones Windows or the
        // window would otherwise keep (Alt, Tab, Esc, F10), which is what recording needs.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!recording) return base.ProcessCmdKey(ref msg, keyData);

            Keys key = keyData & Keys.KeyCode;
            held = keyData & Keys.Modifiers;
            if (key == Keys.Escape && held == Keys.None)
            {
                Stop();
                return true;
            }
            if (key == Keys.ControlKey || key == Keys.Menu || key == Keys.ShiftKey || key == Keys.LWin || key == Keys.RWin)
            {
                Invalidate();
                return true;
            }

            Hotkey pressed = new Hotkey((held & Keys.Control) != 0, (held & Keys.Alt) != 0, (held & Keys.Shift) != 0, (int)key);
            Complaint = Objection(pressed);
            if (Complaint == null)
            {
                hotkey = pressed;
                Stop();
            }
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
            return true;
        }

        static string Objection(Hotkey pressed)
        {
            switch (pressed.Problem)
            {
                case HotkeyProblem.UnknownKey:
                    return Lang.T("Esa tecla no vale. Prueba con una letra, un número o una tecla de función.",
                                  "That key can't be used. Try a letter, a digit or a function key.");
                case HotkeyProblem.NeedsCtrlOrAlt:
                    return Lang.T("Añade Ctrl o Alt: una tecla sola dejaría de servir para escribir.",
                                  "Add Ctrl or Alt: a key on its own could no longer be typed.");
                case HotkeyProblem.TooCommon:
                    return Lang.T("Ese atajo ya significa algo en casi todos los programas. Elige otro.",
                                  "That shortcut already means something in most programs. Pick another.");
            }
            string typed = Native.TypedBy(pressed);
            if (typed == null) return null;
            return Lang.T("En tu teclado esa combinación escribe «" + typed + "». Elige otra.",
                          "On your keyboard that combination types “" + typed + "”. Pick another.");
        }
    }

    sealed class Pill : Drawn
    {
        public bool Primary;
        bool hover;

        public Pill() { AccessibleRole = AccessibleRole.PushButton; }

        protected override void Draw(Graphics g)
        {
            RectangleF shape = new RectangleF(1.5f, 1.5f, Area.Width - 3, Area.Height - 3);
            float radius = shape.Height / 2;
            if (Primary) Look.Fill(g, hover ? Color.FromArgb(66, 60, 52) : Look.Ink, shape, radius);
            else
            {
                Look.Fill(g, hover ? Look.Card : Look.Paper, shape, radius);
                Look.Outline(g, Look.Idle, 1, shape, radius);
            }
            Look.Centered(g, Text, Look.Strong, Primary ? Color.White : Look.Ink, shape);
            if (Ringed) Look.Outline(g, Look.Accent, 2, shape, radius);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            return (keyData & Keys.KeyCode) == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
        }
    }

    // A row of options, one of which is picked.
    sealed class Choice : Drawn
    {
        public string[] Options = new string[0];
        int picked;

        public Choice() { AccessibleRole = AccessibleRole.Grouping; }

        public int Picked
        {
            get { return picked; }
            set
            {
                int clamped = Math.Max(0, Math.Min(Options.Length - 1, value));
                if (clamped == picked) return;
                picked = clamped;
                Invalidate();
            }
        }

        protected override void Draw(Graphics g)
        {
            RectangleF track = new RectangleF(1, 1, Area.Width - 2, Area.Height - 2);
            Look.Fill(g, Look.Paper, track, 11);
            Look.Outline(g, Ringed ? Look.Ink : Look.Line, 1, track, 11);
            float each = (track.Width - 6) / Options.Length;
            for (int i = 0; i < Options.Length; i++)
            {
                RectangleF cell = new RectangleF(track.X + 3 + each * i, track.Y + 3, each, track.Height - 6);
                if (i == picked)
                {
                    Look.Fill(g, Look.Card, cell, 8);
                    Look.Outline(g, Look.Idle, 1, cell, 8);
                }
                Look.Centered(g, Options[i], Look.Strong, i == picked ? Look.Ink : Look.Soft, cell);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Picked = (int)((At(e).X - 4) / ((Area.Width - 8) / Options.Length));
        }

        // Otherwise the arrow keys would move the focus instead of the choice.
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) Picked--;
            else if (e.KeyCode == Keys.Right) Picked++;
        }
    }

    // The settings window: which ways of calling the pointer are on, the keyboard
    // shortcut, how long either has to be held, and the language. It lays itself out
    // and paints by hand so that it can follow the zoom of whichever monitor it is on.
    sealed class SettingsForm : Form
    {
        const int WM_SYSCOMMAND = 0x0112, SC_KEYMENU = 0xF100, WM_DPICHANGED = 0x02E0;
        const int LongestHoldMs = 4000;
        static readonly SizeF Design = new SizeF(420, 604);
        static readonly RectangleF Calling = new RectangleF(24, 96, 372, 226), Holding = new RectangleF(24, 338, 372, 104),
            Speaking = new RectangleF(24, 458, 372, 64);
        // In the order the language options are shown.
        static readonly string[] Languages = { Settings.Auto, "es", "en" };

        readonly Settings settings;
        readonly Func<Settings, bool> apply;
        readonly Toggle mouse = new Toggle(), keys = new Toggle();
        readonly Recorder recorder = new Recorder();
        readonly Slider hold = new Slider();
        readonly Choice language = new Choice();
        readonly Pill cancel = new Pill(), save = new Pill();
        float zoom = 1f;
        string complaint;

        // apply puts the edited settings to work and saves them. It returns false,
        // having changed nothing, when the shortcut already belongs to someone else.
        public SettingsForm(Settings settings, Icon icon, Func<Settings, bool> apply)
        {
            this.settings = settings;
            this.apply = apply;

            Text = "Mousetrap";
            Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Look.Paper;
            DoubleBuffered = true;
            // Born on the screen the pointer is on, so that it starts with that screen's zoom.
            StartPosition = FormStartPosition.Manual;
            Location = Screen.FromPoint(Cursor.Position).WorkingArea.Location;

            mouse.AccessibleName = Lang.T("Los dos botones del ratón", "Both mouse buttons");
            keys.AccessibleName = Lang.T("Un atajo de teclado", "A keyboard shortcut");
            recorder.AccessibleName = Lang.T("Atajo de teclado", "Keyboard shortcut");
            hold.AccessibleName = Lang.T("Cuánto mantenerlo", "How long to hold");
            language.AccessibleName = Lang.T("Idioma", "Language");
            // Each language under its own name, so it can be found from the wrong one.
            language.Options = new[] { "Auto", "Español", "English" };
            language.Picked = Math.Max(0, Array.IndexOf(Languages, settings.Language));
            cancel.Text = Lang.T("Cancelar", "Cancel");
            save.Text = Lang.T("Guardar", "Save");
            save.Primary = true;

            mouse.On = settings.Mouse;
            keys.On = settings.Hotkey != null;
            recorder.Hotkey = settings.Hotkey;
            recorder.Enabled = keys.On;
            hold.Minimum = Settings.MinHoldMs / 100;
            hold.Maximum = LongestHoldMs / 100;
            hold.Value = settings.HoldMs / 100;

            mouse.BackColor = keys.BackColor = recorder.BackColor = hold.BackColor = language.BackColor = Look.Card;
            cancel.BackColor = save.BackColor = Look.Paper;
            Controls.AddRange(new Control[] { mouse, keys, recorder, hold, language, cancel, save });

            mouse.Changed += delegate { Say(null); };
            keys.Changed += delegate
            {
                Say(null);
                recorder.Enabled = keys.On;
                // Nothing recorded yet: go straight to asking for the keys.
                if (keys.On && recorder.Hotkey == null) recorder.Record();
            };
            recorder.Changed += delegate { Say(recorder.Complaint); };
            hold.Changed += delegate { Invalidate(); };
            cancel.Click += delegate { Close(); };
            save.Click += delegate { Save(); };
        }

        void Save()
        {
            if (!mouse.On && !keys.On)
            {
                Say(Lang.T("Deja activada al menos una forma de llamarlo.", "Keep at least one way of calling it switched on."));
                return;
            }
            if (keys.On && recorder.Hotkey == null)
            {
                Say(Lang.T("Graba un atajo o desactívalo.", "Record a shortcut or switch it off."));
                recorder.Record();
                return;
            }
            settings.Mouse = mouse.On;
            settings.Hotkey = keys.On ? recorder.Hotkey : null;
            settings.HoldMs = hold.Value * 100;
            settings.Language = Languages[language.Picked];
            if (apply(settings)) Close();
            else Say(Lang.T("Ese atajo ya lo usa Windows u otro programa. Elige otro.",
                            "Windows or another program already uses that shortcut. Pick another."));
        }

        void Say(string text)
        {
            complaint = text;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            zoom = Native.ZoomOf(Handle);
            Arrange();
            Rectangle screen = Screen.FromHandle(Handle).WorkingArea;
            Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + (screen.Height - Height) / 2);
        }

        void Arrange()
        {
            ClientSize = new Size((int)Math.Round(Design.Width * zoom), (int)Math.Round(Design.Height * zoom));
            Place(mouse, 328, 139, 48, 28);
            Place(keys, 328, 191, 48, 28);
            Place(recorder, 44, 230, 332, 48);
            Place(hold, 32, 392, 356, 36);
            Place(language, 150, 471, 226, 38);
            Place(cancel, 176, 542, 106, 42);
            Place(save, 290, 542, 106, 42);
            Invalidate();
        }

        void Place(Drawn control, float x, float y, float width, float height)
        {
            control.Zoom = zoom;
            control.Bounds = Rectangle.Round(new RectangleF(x * zoom, y * zoom, width * zoom, height * zoom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Look.Prepare(g, zoom);

            g.DrawImage(Look.Emblem, new RectangleF(20, 20, 50, 50));
            Look.Text(g, "Mousetrap", Look.Title, Look.Ink, 76, 22);
            Look.Text(g, Lang.T("Tú decides cómo llamar al puntero.", "You decide how to call your pointer."), Look.Small, Look.Soft, 77, 57);

            Card(g, Calling, Lang.T("CÓMO LLAMARLO", "HOW TO CALL IT"));
            Look.Text(g, mouse.AccessibleName, Look.Body, Look.Ink, 44, 143);
            using (Pen rule = new Pen(Look.Line)) g.DrawLine(rule, 44, 179.5f, 376, 179.5f);
            Look.Text(g, keys.AccessibleName, Look.Body, Look.Ink, 44, 195);
            string note = complaint ?? Lang.T("Con Ctrl o Alt y una tecla más, por ejemplo Ctrl + Alt + M.",
                                              "Ctrl or Alt plus one more key, for example Ctrl + Alt + M.");
            Look.Wrapped(g, note, Look.Small, complaint != null ? Look.Warning : Look.Soft, new RectangleF(44, 284, 332, 36));

            Card(g, Holding, Lang.T("CUÁNTO MANTENERLO", "HOW LONG TO HOLD"));
            string seconds = Lang.Number(hold.Value / 10.0, "0.0") + " s";
            Look.Text(g, seconds, Look.Figure, Look.Ink, 376 - Look.Width(g, seconds, Look.Figure), 351);

            Card(g, Speaking, null);
            Look.Text(g, language.AccessibleName, Look.Body, Look.Ink, 44, 480);
        }

        static void Card(Graphics g, RectangleF card, string heading)
        {
            Look.Fill(g, Look.Card, card, 16);
            Look.Outline(g, Look.Line, 1, card, 16);
            if (heading != null) Look.Text(g, heading, Look.Caps, Look.Soft, card.X + 20, card.Y + 19);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            // Alt on its own would open the window menu and swallow the next key being recorded.
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt64() & 0xFFF0) == SC_KEYMENU && recorder.Focused) return;
            base.WndProc(ref m);
            if (m.Msg == WM_DPICHANGED)
            {
                zoom = (m.WParam.ToInt64() & 0xFFFF) / 96f;
                Arrange();
            }
        }
    }
}
