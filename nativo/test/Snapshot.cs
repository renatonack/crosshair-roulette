using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    // Opens the real window off-screen with throwaway settings (no overlay, no
    // sound, no hotkeys, its own port and cfg folder) and saves pictures of it,
    // so the UI can be checked without touching your screen or your settings.
    //   SnapshotTest.exe <outDir> <roleta|fixa> <shape>
    static class SnapshotTest
    {
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

        [STAThread]
        static int Main(string[] args)
        {
            string outDir = Path.GetFullPath(args[0]), mode = args[1], shape = args[2];
            string data = Path.Combine(outDir, "data-" + mode), cfg = Path.Combine(data, "cfg");
            Directory.CreateDirectory(cfg);
            Environment.SetEnvironmentVariable("CROSSHAIR_ROULETTE_DATA", data);
            File.WriteAllText(Path.Combine(data, "settings.json"), Json.Write(new System.Collections.Generic.Dictionary<string, object>
            {
                ["overlayVisible"] = false, ["sound"] = false, ["gsiPort"] = 3749, ["gsiToken"] = "test", ["cfgDir"] = cfg,
                ["hotkeys"] = new System.Collections.Generic.Dictionary<string, object> { ["roll"] = "", ["toggle"] = "" },
                ["mode"] = mode,
                ["fixed"] = new System.Collections.Generic.Dictionary<string, object> { ["shape"] = shape, ["rainbow"] = true, ["size"] = 12, ["thick"] = 3, ["gap"] = 4, ["outline"] = true },
            }));

            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Dpi = g.DpiX / 96f;

            var app = new App(startHidden: true);
            var form = app.Window;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            ShowWindow(form.Handle, 4); // SW_SHOWNOACTIVATE: never takes focus from your game
            for (int i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(20); }
            form.SaveSnapshot(Path.Combine(outDir, $"ui-{mode}.png"));

            // every fixed shape, plus the rainbow at two moments
            string[] shapes = { "cruz", "cruz_ponto", "ponto", "t", "circulo", "circulo_ponto" };
            int cell = 140;
            using (var bmp = new Bitmap(cell * (shapes.Length + 2), cell))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(60, 70, 90));
                for (int i = 0; i < shapes.Length + 2; i++)
                {
                    var spec = i < shapes.Length
                        ? Crosshairs.Fixed(shapes[i], 12, 3, 4, true, "#3bff6b", false)
                        : Crosshairs.Fixed("cruz", 12, 3, 4, true, "#3bff6b", true);
                    if (i == shapes.Length + 1) Thread.Sleep(1000); // a third of the colour wheel later
                    var state = g.Save();
                    g.TranslateTransform(i * cell + cell / 2f, cell / 2f);
                    g.ScaleTransform(3, 3);
                    Crosshairs.Draw(g, spec, 0, new RectangleF(-cell / 6f, -cell / 6f, cell / 3f, cell / 3f));
                    g.Restore(state);
                }
                bmp.Save(Path.Combine(outDir, "shapes.png"));
            }

            app.Quit();
            return 0;
        }
    }
}
