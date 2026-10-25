using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Crosshair Roulette")]
[assembly: System.Reflection.AssemblyProduct("Crosshair Roulette")]
[assembly: System.Reflection.AssemblyDescription("Cada abate no CS2 sorteia uma mira nova")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]

namespace CrosshairRoulette
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware(); // real pixels, so the crosshair stays sharp
            using (var mutex = new Mutex(true, "CrosshairRoulette.SingleInstance", out bool first))
            {
                if (!first)
                {
                    // already running: ask that copy to show its window
                    Native.PostMessage(Native.HWND_BROADCAST, App.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Dpi = g.DpiX / 96f;
                Application.Run(new App(args.Contains("--hidden")));
                GC.KeepAlive(mutex);
            }
        }
    }
}
