using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CrosshairRoulette
{
    // Settings in %APPDATA%\Crosshair Roulette\settings.json (same keys as the
    // first version of the app, so nothing is lost when switching).
    sealed class Settings
    {
        public bool OverlayVisible = true;
        public string Display;          // Screen.DeviceName, null = primary
        public int OffsetX, OffsetY;
        public double Scale = 1, Opacity = 1, Volume = 0.6;
        public bool Sound = true, IncludeRandom, StartWithWindows, TrayHintShown;
        public List<string> Disabled = new List<string>();
        public string HotkeyRoll = "F7", HotkeyToggle = "F8";
        public int GsiPort = 3748;
        public string GsiToken, CfgDir;
        public Spec Current;

        // "roleta" = a new crosshair on every kill; "fixa" = one crosshair, Crosshair V2 style
        public string Mode = "roleta";
        public string FixedShape = "cruz", FixedColor = "#3bff6b";
        public double FixedSize = 6, FixedThick = 2, FixedGap = 3;
        public bool FixedOutline = true, FixedRainbow;

        // CROSSHAIR_ROULETTE_DATA lets a test run use its own settings instead of yours
        public static readonly string Folder = Environment.GetEnvironmentVariable("CROSSHAIR_ROULETTE_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Crosshair Roulette");
        static readonly string FilePath = Path.Combine(Folder, "settings.json");

        public static Settings Load()
        {
            var s = new Settings();
            Dictionary<string, object> d = null;
            try { d = Json.Parse(File.ReadAllText(FilePath)); } catch { }
            if (d != null)
            {
                s.OverlayVisible = Json.Bool(d, "overlayVisible", true);
                s.Display = Json.Str(d, "display", null);
                s.OffsetX = (int)Json.Num(d, "offsetX", 0);
                s.OffsetY = (int)Json.Num(d, "offsetY", 0);
                s.Scale = Json.Num(d, "scale", 1);
                s.Opacity = Json.Num(d, "opacity", 1);
                s.Volume = Json.Num(d, "volume", 0.6);
                s.Sound = Json.Bool(d, "sound", true);
                s.IncludeRandom = Json.Bool(d, "includeRandom", false);
                s.StartWithWindows = Json.Bool(d, "startWithWindows", false);
                s.TrayHintShown = Json.Bool(d, "trayHintShown", false);
                if (Json.Get(d, "disabled") is object[] list) s.Disabled = list.Select(Json.Str).Where(x => x != null).ToList();
                var hk = Json.Get(d, "hotkeys");
                s.HotkeyRoll = Json.Str(hk, "roll", "F7");
                s.HotkeyToggle = Json.Str(hk, "toggle", "F8");
                s.GsiPort = (int)Json.Num(d, "gsiPort", 3748);
                s.GsiToken = Json.Str(d, "gsiToken", null);
                s.CfgDir = Json.Str(d, "cfgDir", null);
                s.Current = Spec.FromDict(Json.Get(d, "current"));
                s.Mode = Json.Str(d, "mode", "roleta") == "fixa" ? "fixa" : "roleta";
                var f = Json.Get(d, "fixed");
                s.FixedShape = Json.Str(f, "shape", "cruz");
                s.FixedColor = Json.Str(f, "color", "#3bff6b");
                s.FixedSize = Json.Num(f, "size", 6);
                s.FixedThick = Json.Num(f, "thick", 2);
                s.FixedGap = Json.Num(f, "gap", 3);
                s.FixedOutline = Json.Bool(f, "outline", true);
                s.FixedRainbow = Json.Bool(f, "rainbow", false);
            }
            if (string.IsNullOrEmpty(s.GsiToken))
            {
                var bytes = new byte[8];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
                s.GsiToken = "roulette-" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                s.Save();
            }
            return s;
        }

        public void Save()
        {
            var d = new Dictionary<string, object>
            {
                ["overlayVisible"] = OverlayVisible, ["display"] = Display, ["offsetX"] = OffsetX, ["offsetY"] = OffsetY,
                ["scale"] = Scale, ["opacity"] = Opacity, ["sound"] = Sound, ["volume"] = Volume,
                ["includeRandom"] = IncludeRandom, ["disabled"] = Disabled,
                ["hotkeys"] = new Dictionary<string, object> { ["roll"] = HotkeyRoll, ["toggle"] = HotkeyToggle },
                ["startWithWindows"] = StartWithWindows, ["trayHintShown"] = TrayHintShown,
                ["gsiPort"] = GsiPort, ["gsiToken"] = GsiToken, ["cfgDir"] = CfgDir,
                ["current"] = Current?.ToDict(),
                ["mode"] = Mode,
                ["fixed"] = new Dictionary<string, object>
                {
                    ["shape"] = FixedShape, ["color"] = FixedColor, ["size"] = FixedSize, ["thick"] = FixedThick,
                    ["gap"] = FixedGap, ["outline"] = FixedOutline, ["rainbow"] = FixedRainbow,
                },
            };
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, Json.Write(d));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { /* a failed save never crashes the app */ }
        }
    }
}
