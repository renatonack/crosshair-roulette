using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CrosshairRoulette
{
    // Finds the CS2 cfg folder through Steam and writes the Game State
    // Integration file that tells CS2 where to send its data.
    static class Cs2
    {
        public const string CfgName = "gamestate_integration_crosshairroulette.cfg";

        static string SteamRoot()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key?.GetValue("SteamPath") is string p && p.Length > 0) return Path.GetFullPath(p.Replace('/', '\\'));
                }
            }
            catch { }
            foreach (var guess in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
                if (Directory.Exists(guess)) return guess;
            return null;
        }

        public static string FindCfgDir()
        {
            string root = SteamRoot();
            if (root == null) return null;
            var libs = new List<string> { root };
            try
            {
                string vdf = File.ReadAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
                foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch { }
            foreach (var lib in libs)
            {
                string cfg = Path.Combine(lib, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                if (Directory.Exists(cfg)) return cfg;
            }
            return null;
        }

        // People pick the CS2 folder, "game" or "csgo" instead of "game\csgo\cfg":
        // find the real cfg folder under whatever they chose.
        public static string NormalizeCfgDir(string picked)
        {
            if (string.IsNullOrEmpty(picked)) return picked;
            foreach (var c in new[] { picked, Path.Combine(picked, "cfg"), Path.Combine(picked, "csgo", "cfg"), Path.Combine(picked, "game", "csgo", "cfg") })
                if (Directory.Exists(c) && c.TrimEnd('\\').EndsWith(@"csgo\cfg", StringComparison.OrdinalIgnoreCase)) return c;
            return picked;
        }

        public static string ConfigText(int port, string token) =>
            "\"Crosshair Roulette\"\n{\n" +
            $"\t\"uri\"\t\t\"http://127.0.0.1:{port}/gsi\"\n" +
            "\t\"timeout\"\t\"1.0\"\n\t\"buffer\"\t\"0.0\"\n\t\"throttle\"\t\"0.0\"\n\t\"heartbeat\"\t\"10.0\"\n" +
            "\t\"auth\"\n\t{\n" + $"\t\t\"token\"\t\"{token}\"\n" + "\t}\n" +
            "\t\"data\"\n\t{\n" +
            "\t\t\"provider\"\t\t\"1\"\n\t\t\"map\"\t\t\t\"1\"\n\t\t\"round\"\t\t\t\"1\"\n\t\t\"player_id\"\t\t\"1\"\n" +
            "\t\t\"player_state\"\t\t\"1\"\n\t\t\"player_weapons\"\t\"1\"\n\t\t\"player_match_stats\"\t\"1\"\n" +
            "\t}\n}\n";

        public sealed class Status
        {
            public bool Found, Installed, UpToDate;
            public string CfgDir, File;
        }

        public static Status Check(string cfgDir, int port, string token)
        {
            var s = new Status { CfgDir = cfgDir };
            if (string.IsNullOrEmpty(cfgDir) || !Directory.Exists(cfgDir)) return s;
            s.Found = true;
            s.File = Path.Combine(cfgDir, CfgName);
            if (System.IO.File.Exists(s.File))
            {
                s.Installed = true;
                try { s.UpToDate = System.IO.File.ReadAllText(s.File) == ConfigText(port, token); } catch { }
            }
            return s;
        }

        public static void Install(string cfgDir, int port, string token) =>
            System.IO.File.WriteAllText(Path.Combine(cfgDir, CfgName), ConfigText(port, token));
    }
}
