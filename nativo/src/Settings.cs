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

        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Crosshair Roulette");
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
