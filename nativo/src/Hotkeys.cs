using System;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    // Accelerator strings like "F7" or "Ctrl+Shift+K" (same format as before).
    static class Hotkeys
    {
        public static bool TryParse(string acc, out uint mods, out Keys key)
        {
            mods = 0;
            key = Keys.None;
            if (string.IsNullOrEmpty(acc)) return false;
            foreach (var part in acc.Split('+'))
            {
                switch (part)
                {
                    case "Ctrl": mods |= Native.MOD_CONTROL; break;
                    case "Alt": mods |= Native.MOD_ALT; break;
                    case "Shift": mods |= Native.MOD_SHIFT; break;
                    default:
                        if (part.Length == 1 && char.IsLetter(part[0])) key = (Keys)char.ToUpperInvariant(part[0]);
                        else if (part.Length == 1 && char.IsDigit(part[0])) key = Keys.D0 + (part[0] - '0');
                        else if (part.StartsWith("num") && part.Length == 4 && char.IsDigit(part[3])) key = Keys.NumPad0 + (part[3] - '0');
                        else if (!Enum.TryParse(part, out key)) return false;
                        break;
                }
            }
            return key != Keys.None;
        }

        // The string for a key press in the settings box; null = ignore, "" = needs a modifier
        public static string FromKeyEvent(KeyEventArgs e)
        {
            var k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return null;
            string name;
            bool plain = false;
            if (k >= Keys.F1 && k <= Keys.F24) name = k.ToString();
            else if (k >= Keys.A && k <= Keys.Z) { name = k.ToString(); plain = true; }
            else if (k >= Keys.D0 && k <= Keys.D9) { name = ((char)('0' + (k - Keys.D0))).ToString(); plain = true; }
            else if (k >= Keys.NumPad0 && k <= Keys.NumPad9) name = "num" + (k - Keys.NumPad0);
            else if (k == Keys.Space) { name = "Space"; plain = true; }
            else if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.Insert || k == Keys.Delete ||
                     k == Keys.Home || k == Keys.End || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Pause) name = k.ToString();
            else return null;
            string mods = (e.Control ? "Ctrl+" : "") + (e.Alt ? "Alt+" : "") + (e.Shift ? "Shift+" : "");
            if (plain && mods.Length == 0) return "";
            return mods + name;
        }
    }
}
