using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Mousetrap
{
    // Decides when "held long enough" has happened, for the two mouse buttons or for
    // the keyboard shortcut. Fed one reading per timer tick; fires once per uninterrupted hold.
    public sealed class HoldDetector
    {
        readonly long holdMs;
        long heldSince = -1;
        bool fired;

        public HoldDetector(long holdMs) { this.holdMs = holdMs; }

        public bool Update(bool left, bool right, long nowMs) { return Update(left && right, nowMs); }

        public bool Update(bool held, long nowMs)
        {
            if (!held)
            {
                heldSince = -1;
                fired = false;
                return false;
            }
            if (heldSince < 0) heldSince = nowMs;
            if (fired || nowMs - heldSince < holdMs) return false;
            fired = true;
            return true;
        }
    }

    public sealed class Settings
    {
        public const string Primary = "primary";
        public const string UnderCursor = "cursor";
        const int DefaultHoldMs = 3000;
        // Below this, ordinary two-button clicks would start throwing the pointer around.
        public const int MinHoldMs = 500;

        // "primary", "cursor", or a display device name such as \\.\DISPLAY2
        public string Target = Primary;
        public int HoldMs = DefaultHoldMs;
        // Whether holding both mouse buttons calls the pointer.
        public bool Mouse = true;
        // The keyboard shortcut that also calls it; null for none.
        public Hotkey Hotkey;
        // What the menu and the windows are written in: "es", "en", or "auto" to follow Windows.
        public string Language = Auto;
        public const string Auto = "auto";

        public Settings Copy() { return (Settings)MemberwiseClone(); }

        // Whether to speak Spanish, given the two-letter code of the language Windows is in.
        public bool UsesSpanish(string windowsLanguage)
        {
            return (Language == Auto ? windowsLanguage : Language) == "es";
        }

        public static Settings Parse(string text)
        {
            Settings s = new Settings();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.StartsWith("#") || eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (key == "target" && value.Length > 0) s.Target = value;
                int ms;
                if (key == "hold_ms" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
                    s.HoldMs = Math.Max(MinHoldMs, ms);
                if (key == "mouse") s.Mouse = value.ToLowerInvariant() != "off";
                if (key == "hotkey") s.Hotkey = Hotkey.Parse(value);
                if (key == "language")
                {
                    string language = value.ToLowerInvariant();
                    s.Language = language == "es" || language == "en" ? language : Auto;
                }
            }
            // With nothing left to call the pointer the app would be running for nothing.
            if (s.Hotkey == null) s.Mouse = true;
            return s;
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Mousetrap (restart the app after editing this file by hand)");
            sb.AppendLine("# target:  primary | cursor | a display name such as \\\\.\\DISPLAY2");
            sb.AppendLine("# hold_ms: how long the buttons or the shortcut must be held");
            sb.AppendLine("# mouse:   on | off, whether holding both mouse buttons calls the pointer");
            sb.AppendLine("# hotkey:  a keyboard shortcut that also calls it, such as Ctrl+Alt+M; empty for none");
            sb.AppendLine("# language: auto (the one Windows is in) | es | en");
            sb.AppendLine("target=" + Target);
            sb.AppendLine("hold_ms=" + HoldMs.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mouse=" + (Mouse ? "on" : "off"));
            sb.AppendLine("hotkey=" + Hotkey);
            sb.AppendLine("language=" + Language);
            return sb.ToString();
        }
    }

    public enum HotkeyProblem { None, NeedsCtrlOrAlt, UnknownKey, TooCommon }

    // A keyboard shortcut such as Ctrl+Alt+M: Ctrl and/or Alt, Shift if wanted, and one key.
    public sealed class Hotkey
    {
        const int Space = 0x20, F1 = 0x70, F4 = 0x73, F24 = 0x87;
        // Virtual-key codes 0x20 to 0x28, in order.
        static readonly string[] Named = { "Space", "PageUp", "PageDown", "End", "Home", "Left", "Up", "Right", "Down" };

        public readonly bool Ctrl, Alt, Shift;
        // Virtual-key code of the one key that is not a modifier.
        public readonly int Key;

        public Hotkey(bool ctrl, bool alt, bool shift, int key)
        {
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
            Key = key;
        }

        public HotkeyProblem Problem
        {
            get
            {
                if (KeyName(Key) == null) return HotkeyProblem.UnknownKey;
                // A key on its own, or with just Shift, could no longer be typed anywhere.
                if (!Ctrl && !Alt) return HotkeyProblem.NeedsCtrlOrAlt;
                // Ctrl+C, Ctrl+V, Ctrl+Z and the like, and Alt+F4: taking one would break it everywhere else.
                if (Ctrl && !Alt && !Shift && Key >= 'A' && Key <= 'Z') return HotkeyProblem.TooCommon;
                if (Alt && !Ctrl && !Shift && Key == F4) return HotkeyProblem.TooCommon;
                return HotkeyProblem.None;
            }
        }

        // What to show or save, modifiers first.
        public string[] Parts
        {
            get
            {
                List<string> parts = new List<string>();
                if (Ctrl) parts.Add("Ctrl");
                if (Alt) parts.Add("Alt");
                if (Shift) parts.Add("Shift");
                parts.Add(KeyName(Key) ?? "?");
                return parts.ToArray();
            }
        }

        public override string ToString() { return string.Join("+", Parts); }

        // Null unless the text is a shortcut the app can use.
        public static Hotkey Parse(string text)
        {
            bool ctrl = false, alt = false, shift = false;
            int key = 0, keys = 0;
            foreach (string raw in (text ?? "").Split('+'))
            {
                string part = raw.Trim().ToLowerInvariant();
                if (part == "ctrl") ctrl = true;
                else if (part == "alt") alt = true;
                else if (part == "shift") shift = true;
                else { key = KeyCode(part); keys++; }
            }
            Hotkey hotkey = new Hotkey(ctrl, alt, shift, key);
            return keys == 1 && hotkey.Problem == HotkeyProblem.None ? hotkey : null;
        }

        // Null for a key that cannot be part of a shortcut.
        public static string KeyName(int key)
        {
            if ((key >= 'A' && key <= 'Z') || (key >= '0' && key <= '9')) return ((char)key).ToString();
            if (key >= F1 && key <= F24) return "F" + (key - F1 + 1);
            if (key >= Space && key < Space + Named.Length) return Named[key - Space];
            return null;
        }

        static int KeyCode(string name)
        {
            for (int key = Space; key <= F24; key++)
                if (string.Equals(KeyName(key), name, StringComparison.OrdinalIgnoreCase)) return key;
            return 0;
        }
    }

    public sealed class Display
    {
        public readonly string Name;
        public readonly Rectangle Bounds;
        public readonly bool Primary;

        public Display(string name, Rectangle bounds, bool primary)
        {
            Name = name;
            Bounds = bounds;
            Primary = primary;
        }

        public Point Center
        {
            get { return new Point(Bounds.Left + Bounds.Width / 2, Bounds.Top + Bounds.Height / 2); }
        }

        // The number Windows shows for this screen (\\.\DISPLAY2 -> 2); 0 if the name has none.
        public int Number
        {
            get
            {
                int end = Name.Length, start = end;
                while (start > 0 && char.IsDigit(Name[start - 1])) start--;
                int n;
                return int.TryParse(Name.Substring(start, end - start), out n) ? n : 0;
            }
        }
    }

    public static class Targeting
    {
        public static Display Pick(string target, IList<Display> displays, Point cursor)
        {
            Display primary = displays[0];
            foreach (Display d in displays)
                if (d.Primary) primary = d;

            foreach (Display d in displays)
            {
                if (string.Equals(target, Settings.UnderCursor, StringComparison.OrdinalIgnoreCase))
                {
                    if (d.Bounds.Contains(cursor)) return d;
                }
                else if (string.Equals(target, d.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }
            return primary;
        }
    }
}
