using System;
using System.Collections.Generic;
using System.Drawing;
using Mousetrap;

static class CoreTests
{
    static int passed, failed;

    static int Main()
    {
        Console.WriteLine("HoldDetector");
        Test("holding both buttons for 3 s fires at the 3 s mark", delegate
        {
            Equal("3000", FireTimes(new HoldDetector(3000), Hold(0, 3500, true, true)));
        });
        Test("releasing at 2.9 s does not fire", delegate
        {
            Equal("", FireTimes(new HoldDetector(3000), Hold(0, 2900, true, true), Hold(2950, 4000, false, false)));
        });
        Test("holding for 10 s fires only once", delegate
        {
            Equal("3000", FireTimes(new HoldDetector(3000), Hold(0, 10000, true, true)));
        });
        Test("a long left-button drag never fires", delegate
        {
            Equal("", FireTimes(new HoldDetector(3000), Hold(0, 10000, true, false)));
        });
        Test("a long right-button press never fires", delegate
        {
            Equal("", FireTimes(new HoldDetector(3000), Hold(0, 10000, false, true)));
        });
        Test("the count starts when the second button goes down", delegate
        {
            Equal("5000", FireTimes(new HoldDetector(3000), Hold(0, 1950, true, false), Hold(2000, 6000, true, true)));
        });
        Test("letting go of one button for an instant restarts the count", delegate
        {
            Equal("5550", FireTimes(new HoldDetector(3000),
                Hold(0, 2500, true, true), Hold(2500, 2500, true, false), Hold(2550, 6000, true, true)));
        });
        Test("a second hold after releasing fires again", delegate
        {
            Equal("3000,7000", FireTimes(new HoldDetector(3000),
                Hold(0, 3500, true, true), Hold(3550, 3950, false, false), Hold(4000, 7500, true, true)));
        });
        Test("the hold time is configurable", delegate
        {
            Equal("1000", FireTimes(new HoldDetector(1000), Hold(0, 3500, true, true)));
        });
        Test("a late tick still fires on the first tick past the threshold", delegate
        {
            HoldDetector d = new HoldDetector(3000);
            Equal(false, d.Update(true, true, 0));
            Equal(true, d.Update(true, true, 4200));
        });

        Test("any held state can be timed, not only the two buttons", delegate
        {
            HoldDetector d = new HoldDetector(1000);
            Equal(false, d.Update(true, 0));
            Equal(true, d.Update(true, 1000));
            Equal(false, d.Update(true, 1050));
            Equal(false, d.Update(false, 1100));
            Equal(false, d.Update(true, 1150));
            Equal(true, d.Update(true, 2150));
        });

        Console.WriteLine("Settings");
        Test("no config file means primary screen and 3 s", delegate
        {
            Settings s = Settings.Parse("");
            Equal("primary", s.Target);
            Equal(3000, s.HoldMs);
        });
        Test("reads target and hold time", delegate
        {
            Settings s = Settings.Parse("target=\\\\.\\DISPLAY2\r\nhold_ms=1500\r\n");
            Equal("\\\\.\\DISPLAY2", s.Target);
            Equal(1500, s.HoldMs);
        });
        Test("tolerates comments, blank lines, spaces and key casing", delegate
        {
            Settings s = Settings.Parse("# a comment\n\n  Target = cursor  \n HOLD_MS = 2000 \nunknown=1\nno equals sign\n");
            Equal("cursor", s.Target);
            Equal(2000, s.HoldMs);
        });
        Test("a hold time that is not a number falls back to 3 s", delegate
        {
            Equal(3000, Settings.Parse("hold_ms=three").HoldMs);
        });
        Test("a hold time too short to be safe is raised to half a second", delegate
        {
            Equal(500, Settings.Parse("hold_ms=0").HoldMs);
            Equal(500, Settings.Parse("hold_ms=-50").HoldMs);
        });
        Test("an empty target falls back to primary", delegate
        {
            Equal("primary", Settings.Parse("target=\n").Target);
        });
        Test("what is saved can be read back", delegate
        {
            Settings s = new Settings();
            s.Target = "\\\\.\\DISPLAY1";
            s.HoldMs = 4000;
            Settings back = Settings.Parse(s.ToText());
            Equal("\\\\.\\DISPLAY1", back.Target);
            Equal(4000, back.HoldMs);
        });

        Test("the mouse gesture is on and there is no shortcut unless the file says so", delegate
        {
            Settings s = Settings.Parse("");
            Equal(true, s.Mouse);
            Equal<Hotkey>(null, s.Hotkey);
        });
        Test("reads the mouse switch and the shortcut", delegate
        {
            Settings s = Settings.Parse("mouse=off\nhotkey=Ctrl+Alt+M\n");
            Equal(false, s.Mouse);
            Equal("Ctrl+Alt+M", s.Hotkey.ToString());
        });
        Test("a shortcut that cannot be used is ignored", delegate
        {
            Equal<Hotkey>(null, Settings.Parse("hotkey=M\n").Hotkey);
            Equal<Hotkey>(null, Settings.Parse("hotkey=banana\n").Hotkey);
            Equal<Hotkey>(null, Settings.Parse("hotkey=\n").Hotkey);
        });
        Test("with the mouse off and no usable shortcut the mouse gesture comes back", delegate
        {
            Equal(true, Settings.Parse("mouse=off\n").Mouse);
            Equal(true, Settings.Parse("mouse=off\nhotkey=Shift+M\n").Mouse);
        });
        Test("the mouse switch and the shortcut survive saving", delegate
        {
            Settings s = new Settings();
            s.Mouse = false;
            s.Hotkey = Hotkey.Parse("Ctrl+Shift+F9");
            Settings back = Settings.Parse(s.ToText());
            Equal(false, back.Mouse);
            Equal("Ctrl+Shift+F9", back.Hotkey.ToString());
        });
        Test("saving without a shortcut reads back as no shortcut", delegate
        {
            Settings back = Settings.Parse(new Settings().ToText());
            Equal(true, back.Mouse);
            Equal<Hotkey>(null, back.Hotkey);
        });

        Test("the language follows Windows unless the file names one", delegate
        {
            Equal("auto", Settings.Parse("").Language);
            Equal(true, Settings.Parse("").UsesSpanish("es"));
            Equal(false, Settings.Parse("").UsesSpanish("en"));
            Equal(false, Settings.Parse("").UsesSpanish("fr"));
        });
        Test("a language named in the file wins over the one of Windows", delegate
        {
            Equal(false, Settings.Parse("language=en\n").UsesSpanish("es"));
            Equal(true, Settings.Parse("language = ES\n").UsesSpanish("en"));
        });
        Test("a language the app does not speak falls back to following Windows", delegate
        {
            Equal("auto", Settings.Parse("language=klingon\n").Language);
            Equal("auto", Settings.Parse("language=\n").Language);
        });
        Test("the language survives saving", delegate
        {
            Settings s = new Settings();
            s.Language = "en";
            Equal("en", Settings.Parse(s.ToText()).Language);
        });

        Console.WriteLine("Hotkey");
        Test("reads modifiers in any order, case and spacing, and writes them back tidy", delegate
        {
            Equal("Ctrl+Alt+M", Hotkey.Parse(" alt + CTRL + m ").ToString());
            Equal("Ctrl+Alt+Shift+F12", Hotkey.Parse("shift+f12+alt+ctrl").ToString());
        });
        Test("knows letters, digits, function keys and a few named keys", delegate
        {
            Equal(0x4D, Hotkey.Parse("Ctrl+Alt+M").Key);
            Equal(0x37, Hotkey.Parse("Ctrl+Alt+7").Key);
            Equal(0x78, Hotkey.Parse("Alt+F9").Key);
            Equal(0x20, Hotkey.Parse("Ctrl+Space").Key);
            Equal(0x25, Hotkey.Parse("Ctrl+Alt+Left").Key);
        });
        Test("needs Ctrl or Alt, or the key could no longer be typed", delegate
        {
            Equal(HotkeyProblem.NeedsCtrlOrAlt, new Hotkey(false, false, false, 0x4D).Problem);
            Equal(HotkeyProblem.NeedsCtrlOrAlt, new Hotkey(false, false, true, 0x4D).Problem);
            Equal(HotkeyProblem.None, new Hotkey(true, false, true, 0x4D).Problem);
            Equal<Hotkey>(null, Hotkey.Parse("Shift+M"));
        });
        Test("needs exactly one key the app knows", delegate
        {
            Equal(HotkeyProblem.UnknownKey, new Hotkey(true, true, false, 0x1B).Problem);
            Equal<Hotkey>(null, Hotkey.Parse("Ctrl+Alt"));
            Equal<Hotkey>(null, Hotkey.Parse("Ctrl+Alt+M+N"));
            Equal<Hotkey>(null, Hotkey.Parse(""));
            Equal<Hotkey>(null, Hotkey.Parse(null));
        });
        Test("refuses shortcuts everybody already uses", delegate
        {
            Equal(HotkeyProblem.TooCommon, new Hotkey(true, false, false, 0x43).Problem);
            Equal(HotkeyProblem.TooCommon, new Hotkey(false, true, false, 0x73).Problem);
            Equal(HotkeyProblem.None, new Hotkey(true, true, false, 0x43).Problem);
            Equal<Hotkey>(null, Hotkey.Parse("Ctrl+C"));
        });
        Test("lists its keys one by one, ready to be drawn as keycaps", delegate
        {
            Equal("Ctrl|Shift|PageDown", string.Join("|", Hotkey.Parse("Ctrl+Shift+PageDown").Parts));
        });

        Console.WriteLine("Targeting");
        Test("primary goes to the middle of the main screen", delegate
        {
            Equal(new Point(960, 540), Targeting.Pick("primary", ThreeScreens(), new Point(5000, 1000)).Center);
        });
        Test("a chosen screen above the main one (negative Y) gets its own middle", delegate
        {
            Equal(new Point(3360, 536), Targeting.Pick("\\\\.\\DISPLAY1", ThreeScreens(), new Point(10, 10)).Center);
        });
        Test("the portrait screen gets its own middle", delegate
        {
            Equal(new Point(5520, 916), Targeting.Pick("\\\\.\\DISPLAY2", ThreeScreens(), new Point(10, 10)).Center);
        });
        Test("the screen name is matched ignoring case", delegate
        {
            Equal("\\\\.\\DISPLAY2", Targeting.Pick("\\\\.\\display2", ThreeScreens(), new Point(10, 10)).Name);
        });
        Test("a chosen screen that was unplugged falls back to the main one", delegate
        {
            Equal("\\\\.\\DISPLAY3", Targeting.Pick("\\\\.\\DISPLAY7", ThreeScreens(), new Point(5000, 1000)).Name);
        });
        Test("cursor mode centres on the screen the pointer is on", delegate
        {
            Equal("\\\\.\\DISPLAY2", Targeting.Pick("cursor", ThreeScreens(), new Point(6200, 2100)).Name);
            Equal("\\\\.\\DISPLAY1", Targeting.Pick("cursor", ThreeScreens(), new Point(1920, -364)).Name);
        });
        Test("cursor mode with the pointer outside every screen falls back to the main one", delegate
        {
            Equal("\\\\.\\DISPLAY3", Targeting.Pick("cursor", ThreeScreens(), new Point(100, 5000)).Name);
        });
        Test("a single-screen PC always gets that screen", delegate
        {
            List<Display> one = new List<Display>();
            one.Add(new Display("\\\\.\\DISPLAY1", new Rectangle(0, 0, 1366, 768), true));
            Equal(new Point(683, 384), Targeting.Pick("\\\\.\\DISPLAY2", one, new Point(5, 5)).Center);
            Equal(new Point(683, 384), Targeting.Pick("cursor", one, new Point(5, 5)).Center);
        });
        Test("screens are labelled with their Windows number", delegate
        {
            Equal(2, new Display("\\\\.\\DISPLAY2", new Rectangle(0, 0, 10, 10), false).Number);
            Equal(12, new Display("\\\\.\\DISPLAY12", new Rectangle(0, 0, 10, 10), false).Number);
            Equal(0, new Display("WEIRD", new Rectangle(0, 0, 10, 10), false).Number);
        });

        Console.WriteLine();
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }

    // The author's real desk: a 1080p main screen, a 2880x1800 one to its right
    // sitting higher, and a portrait 1440x2560 one at the far right.
    static List<Display> ThreeScreens()
    {
        List<Display> all = new List<Display>();
        all.Add(new Display("\\\\.\\DISPLAY1", new Rectangle(1920, -364, 2880, 1800), false));
        all.Add(new Display("\\\\.\\DISPLAY2", new Rectangle(4800, -364, 1440, 2560), false));
        all.Add(new Display("\\\\.\\DISPLAY3", new Rectangle(0, 0, 1920, 1080), true));
        return all;
    }

    struct Span { public long From, To; public bool Left, Right; }

    static Span Hold(long from, long to, bool left, bool right)
    {
        Span s; s.From = from; s.To = to; s.Left = left; s.Right = right;
        return s;
    }

    // Feeds the detector one reading every 50 ms, as the app's timer does,
    // and returns the moments it fired.
    static string FireTimes(HoldDetector detector, params Span[] timeline)
    {
        List<string> fired = new List<string>();
        foreach (Span span in timeline)
            for (long t = span.From; t <= span.To; t += 50)
                if (detector.Update(span.Left, span.Right, t)) fired.Add(t.ToString());
        return string.Join(",", fired.ToArray());
    }

    static void Test(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("  ok    " + name); }
        catch (Exception e) { failed++; Console.WriteLine("  FAIL  " + name + ": " + e.Message); }
    }

    static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("expected <" + expected + "> but got <" + actual + ">");
    }
}
