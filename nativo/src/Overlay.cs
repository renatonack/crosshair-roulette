using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    // The crosshair: a transparent, click-through, always-on-top window that
    // only covers the area the crosshair paints, so each update is tiny.
    sealed class Overlay : Form
    {
        Spec shown;
        double spread;
        Settings S;

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "Crosshair Roulette — mira";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        Screen TargetScreen() => Screen.AllScreens.FirstOrDefault(s => s.DeviceName == S?.Display) ?? Screen.PrimaryScreen;

        public void Render(Spec spec, double spreadPx, Settings settings)
        {
            shown = spec;
            spread = spreadPx;
            S = settings;
            if (!IsHandleCreated) CreateHandle();
            if (spec == null || !settings.OverlayVisible)
            {
                if (Visible) Hide();
                return;
            }

            var screen = TargetScreen().Bounds;
            double k = screen.Height / 1080.0 * settings.Scale;
            float cx = screen.X + screen.Width / 2f + settings.OffsetX;
            float cy = screen.Y + screen.Height / 2f + settings.OffsetY;

            double reach = Crosshairs.Reach(spec);
            Rectangle rect = double.IsInfinity(reach)
                ? screen
                : Rectangle.Intersect(screen, Rectangle.FromLTRB((int)Math.Floor(cx - reach * k) - 2, (int)Math.Floor(cy - reach * k) - 2, (int)Math.Ceiling(cx + reach * k) + 2, (int)Math.Ceiling(cy + reach * k) + 2));
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.TranslateTransform(cx - rect.X, cy - rect.Y);
                    g.ScaleTransform((float)k, (float)k);
                    var bounds = RectangleF.FromLTRB((float)((screen.Left - cx) / k), (float)((screen.Top - cy) / k), (float)((screen.Right - cx) / k), (float)((screen.Bottom - cy) / k));
                    Crosshairs.Draw(g, spec, spread, bounds, settings.Opacity);
                }
                if (Bounds != rect) Bounds = rect; // keep WinForms in sync with the layered window
                Native.Present(Handle, bmp, rect.Location);
            }
            if (!Visible) Show();
            KeepOnTop();
        }

        // borderless games like to climb on top; keep reclaiming the spot
        public void KeepOnTop()
        {
            if (Visible) Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }
}
