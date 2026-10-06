using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Mousetrap
{
    // Decides when "both buttons held long enough" has happened.
    // Fed one reading per timer tick; fires once per uninterrupted hold.
    public sealed class HoldDetector
    {
        readonly long holdMs;
        long heldSince = -1;
        bool fired;

        public HoldDetector(long holdMs) { this.holdMs = holdMs; }

        public bool Update(bool left, bool right, long nowMs)
        {
            if (!(left && right))
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
        const int MinHoldMs = 500;

        // "primary", "cursor", or a display device name such as \\.\DISPLAY2
        public string Target = Primary;
        public int HoldMs = DefaultHoldMs;

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
            }
            return s;
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Mousetrap");
            sb.AppendLine("# target:  primary | cursor | a display name such as \\\\.\\DISPLAY2");
            sb.AppendLine("# hold_ms: how long both buttons must be held (restart the app after editing)");
            sb.AppendLine("target=" + Target);
            sb.AppendLine("hold_ms=" + HoldMs.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
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
