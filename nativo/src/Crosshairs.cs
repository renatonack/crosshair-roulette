using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace CrosshairRoulette
{
    // One crosshair. Sizes are pixels on a 1080p screen.
    //   cross   arms like CS2:  Len, Thick, Gap, Outline, Dot, TStyle, Alpha
    //   hollow  square outline: Size, Width, Corners (only the corners), TStyle (no top: a "U")
    //   grid    tic-tac-toe #:  Spacing, Len, Thick
    //   flood   paints the whole screen except a window that opens while you shoot
    // Any crosshair may set Dynamic (px of spread per shot) and DynamicMax.
    sealed class Spec
    {
        public string Id = "random", Name = "", Type = "cross", Color = "#ffffff";
        public double Alpha = 1, Len, Thick, Gap, Outline, Dot, Size, Width, Spacing, Dynamic, DynamicMax;
        public bool TStyle, Corners;

        public Spec Clone() => (Spec)MemberwiseClone();

        public Dictionary<string, object> ToDict()
        {
            var d = new Dictionary<string, object>
            {
                ["id"] = Id, ["name"] = Name, ["type"] = Type, ["color"] = Color, ["alpha"] = Alpha,
                ["len"] = Len, ["thick"] = Thick, ["gap"] = Gap, ["outline"] = Outline, ["dot"] = Dot,
                ["size"] = Size, ["width"] = Width, ["spacing"] = Spacing,
                ["dynamic"] = Dynamic, ["dynamicMax"] = DynamicMax, ["tStyle"] = TStyle, ["corners"] = Corners,
            };
            return d;
        }

        public static Spec FromDict(object o)
        {
            if (!(o is Dictionary<string, object>)) return null;
            var s = new Spec
            {
                Id = Json.Str(o, "id", "random"), Name = Json.Str(o, "name", ""), Type = Json.Str(o, "type", "cross"),
                Color = Json.Str(o, "color", "#ffffff"), Alpha = Json.Num(o, "alpha", 1),
                Len = Json.Num(o, "len", 0), Thick = Json.Num(o, "thick", 0), Gap = Json.Num(o, "gap", 0),
                Outline = Json.Num(o, "outline", 0), Dot = Json.Num(o, "dot", 0), Size = Json.Num(o, "size", 0),
                Width = Json.Num(o, "width", 0), Spacing = Json.Num(o, "spacing", 0), Dynamic = Json.Num(o, "dynamic", 0),
                DynamicMax = Json.Num(o, "dynamicMax", 0), TStyle = Json.Bool(o, "tStyle", false), Corners = Json.Bool(o, "corners", false),
            };
            return s.IsDrawable ? s : null;
        }

        // something a saved file could get wrong: a type without the sizes it needs
        public bool IsDrawable =>
            Type == "flood" ||
            (Type == "cross" && (Len > 0 && Thick > 0 || Dot > 0)) ||
            (Type == "hollow" && Size > 0 && Width > 0) ||
            (Type == "grid" && Len > 0 && Thick > 0);
    }

    static class Crosshairs
    {
        static readonly (string color, double alpha)[] FloodColors =
        {
            ("#22ff22", 1), ("#a020f0", 1), ("#ff5a5a", 1), ("#88ff00", 1),
            ("#1030ff", 1), ("#ff5070", 0.7), ("#ff00c8", 1), ("#00f0ff", 1),
        };

        static Spec Cross(string id, string name, string color, double len, double thick, double gap, double outline = 0, double dot = 0) =>
            new Spec { Id = id, Name = name, Type = "cross", Color = color, Len = len, Thick = thick, Gap = gap, Outline = outline, Dot = dot };

        static Spec Hollow(string id, string name, string color, double size, double width, bool corners = false, bool tStyle = false) =>
            new Spec { Id = id, Name = name, Type = "hollow", Color = color, Size = size, Width = width, Corners = corners, TStyle = tStyle };

        // Recreated frame by frame from the "CS2 crosshair roulette" clip.
        public static readonly List<Spec> Presets = new List<Spec>
        {
            Cross("linhas_vermelhas", "Linhas infinitas vermelhas", "#ff4d6d", 5000, 1.5, 5),
            Cross("cruz_verde_gigante", "Cruz verde gigante", "#2dff3a", 190, 6, 8, 1.5),
            Cross("quatro_quadrados", "Quatro quadrados", "#3dff3d", 22, 22, 70),
            Hollow("cantos_azuis", "Quadrado vazado azul", "#2a3dff", 40, 6, corners: true),
            Hollow("u_vermelho", "U vermelho", "#ff1f1f", 40, 6, tStyle: true),
            Hollow("quadrado_amarelo", "Quadrado vazado amarelo", "#ffe600", 46, 7),
            Cross("bloco_amarelo", "Bloco amarelo", "#ffee00", 55, 64, 0),
            new Spec { Id = "jogo_da_velha", Name = "Jogo da velha azul", Type = "grid", Color = "#1f2cff", Spacing = 80, Len = 250, Thick = 13 },
            new Spec { Id = "tela_pintada", Name = "Tela pintada (só enxerga atirando)", Type = "flood", Color = null, Gap = 0, Dynamic = 210, DynamicMax = 430 },
            Cross("mais_verde", "Mais verde gordo", "#33ff33", 28, 28, 5, 2),
            Cross("cruz_amarela", "Cruz amarela comprida", "#ffe000", 150, 4, 6),
            Cross("linhas_verdes", "Linhas infinitas verdes", "#5dff2a", 5000, 2, 5),
            Cross("cruz_azul_gigante", "Cruz azul gigante", "#2250ff", 700, 80, 6),
            Cross("verde_modesto", "Verde modesto", "#3bff6b", 7, 2, 3, 1),
            Cross("amarelo_pequeno", "Amarelo pequeno", "#ffe14d", 8, 2.5, 3, 1),
            Cross("branco_minusculo", "Branco minúsculo", "#ffffff", 4, 1.5, 2, 1, 1.5),
        };

        public static Spec ById(string id) => Presets.Find(p => p.Id == id);

        // a preset ready to draw: the flood gets a new colour every roll
        public static Spec Resolve(Spec preset, Random rnd)
        {
            var s = preset.Clone();
            if (s.Type == "flood" && s.Color == null)
            {
                var (color, alpha) = FloodColors[rnd.Next(FloodColors.Length)];
                s.Color = color;
                s.Alpha = alpha;
            }
            return s;
        }

        static readonly string[] RandomColors = { "#ff0000", "#00ff00", "#39ff14", "#ffff00", "#00ffff", "#ff00ff", "#ffffff", "#ff8800", "#2040ff", "#a020f0", "#ff4d6d", "#000000" };

        static double R1(double v) => Math.Round(v * 10) / 10;

        public static Spec RandomSpec(Random rnd)
        {
            string color = RandomColors[rnd.Next(RandomColors.Length)];
            string name = "Aleatória #" + rnd.Next(0x10000).ToString("X4");
            double r = rnd.NextDouble();
            if (r < 0.12)
                return new Spec { Name = name, Type = "hollow", Color = color, Size = 12 + rnd.NextDouble() * 90, Width = 2 + rnd.NextDouble() * 10, Corners = rnd.NextDouble() < 0.4, TStyle = rnd.NextDouble() < 0.3 };
            if (r < 0.2)
                return new Spec { Name = name, Type = "grid", Color = color, Spacing = 20 + rnd.NextDouble() * 130, Len = 60 + rnd.NextDouble() * 420, Thick = 2 + rnd.NextDouble() * 16 };
            double lr = rnd.NextDouble();
            double len = lr < 0.15 ? 5000 : lr < 0.5 ? 2 + rnd.NextDouble() * 12 : 10 + rnd.NextDouble() * 320;
            double thick = rnd.NextDouble() < 0.2 ? 18 + rnd.NextDouble() * 90 : 1 + rnd.NextDouble() * 8;
            double gr = rnd.NextDouble();
            double gap = gr < 0.15 ? 60 + rnd.NextDouble() * 140 : gr < 0.25 ? -thick / 2 : rnd.NextDouble() * 14;
            return new Spec
            {
                Name = name, Type = "cross", Color = color, Len = R1(len), Thick = R1(thick), Gap = R1(gap),
                Outline = rnd.NextDouble() < 0.5 ? R1(1 + rnd.NextDouble() * 2) : 0,
                Dot = rnd.NextDouble() < 0.3 ? R1(2 + rnd.NextDouble() * 8) : 0,
                TStyle = rnd.NextDouble() < 0.15,
                Alpha = rnd.NextDouble() < 0.2 ? Math.Round((0.4 + rnd.NextDouble() * 0.5) * 100) / 100 : 1,
                Dynamic = rnd.NextDouble() < 0.25 ? 6 + rnd.NextDouble() * 20 : 0,
                DynamicMax = 60,
            };
        }

        // how far from the aim point a crosshair paints (Infinity = anywhere)
        public static double Reach(Spec s)
        {
            if (s.Type == "flood") return double.PositiveInfinity;
            if (s.Type == "hollow") return s.Size / 2 + s.Width + 2;
            if (s.Type == "grid") return Math.Max(s.Len / 2, s.Spacing / 2 + s.Thick) + 2;
            double r = Math.Max(Math.Max(s.Gap + s.DynamicMax + s.Len, s.Thick / 2), s.Dot) + s.Outline + 2;
            return r > 1500 ? double.PositiveInfinity : r;
        }

        static Color ParseColor(string hex, double alpha)
        {
            int n = int.Parse((hex ?? "#ffffff").TrimStart('#'), NumberStyles.HexNumber);
            int a = (int)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            return System.Drawing.Color.FromArgb(a, (n >> 16) & 255, (n >> 8) & 255, n & 255);
        }

        // Draw with the Graphics origin on the aim point and units in 1080p px.
        public static void Draw(Graphics g, Spec s, double spread, RectangleF bounds, double opacity = 1)
        {
            if (s == null) return;
            double alpha = s.Alpha * opacity;
            var color = ParseColor(s.Color, alpha);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using (var brush = new SolidBrush(color))
            {
                if (s.Type == "flood")
                {
                    float gp = (float)Math.Max(0, s.Gap + spread);
                    var B = bounds;
                    g.FillRectangle(brush, B.Left, B.Top, B.Width, -gp - B.Top);
                    g.FillRectangle(brush, B.Left, gp, B.Width, B.Bottom - gp);
                    g.FillRectangle(brush, B.Left, -gp, -gp - B.Left, 2 * gp);
                    g.FillRectangle(brush, gp, -gp, B.Right - gp, 2 * gp);
                    return;
                }

                if (s.Type == "grid")
                {
                    float d = (float)s.Spacing / 2, L = (float)s.Len / 2, T = (float)s.Thick;
                    foreach (float x in new[] { -d, d }) g.FillRectangle(brush, x - T / 2, -L, T, 2 * L);
                    foreach (float y in new[] { -d, d }) g.FillRectangle(brush, -L, y - T / 2, 2 * L, T);
                    return;
                }

                if (s.Type == "hollow")
                {
                    float h = (float)(s.Size / 2 + spread);
                    using (var pen = new Pen(color, (float)s.Width) { LineJoin = LineJoin.Miter, StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                    {
                        if (s.Corners)
                        {
                            float k = (float)(s.Size * 0.32);
                            foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                                g.DrawLines(pen, new[] { new PointF(sx * h, sy * h - sy * k), new PointF(sx * h, sy * h), new PointF(sx * h - sx * k, sy * h) });
                        }
                        else if (s.TStyle)
                        {
                            g.DrawLines(pen, new[] { new PointF(-h, -h), new PointF(-h, h), new PointF(h, h), new PointF(h, -h) });
                        }
                        else
                        {
                            g.DrawRectangle(pen, -h, -h, 2 * h, 2 * h);
                        }
                    }
                    return;
                }

                // cross: CS2-style arms, black outline underneath, then the colour
                float Lc = (float)s.Len, Tc = (float)s.Thick, G = (float)(s.Gap + spread), o = (float)s.Outline, dot = (float)s.Dot;
                var arms = new List<RectangleF>();
                if (!s.TStyle) arms.Add(new RectangleF(-Tc / 2, -G - Lc, Tc, Lc));
                arms.Add(new RectangleF(-Tc / 2, G, Tc, Lc));
                arms.Add(new RectangleF(-G - Lc, -Tc / 2, Lc, Tc));
                arms.Add(new RectangleF(G, -Tc / 2, Lc, Tc));
                if (o > 0)
                {
                    using (var black = new SolidBrush(System.Drawing.Color.FromArgb((int)Math.Round(Math.Min(1, alpha) * 255), 0, 0, 0)))
                    {
                        foreach (var a in arms) g.FillRectangle(black, a.X - o, a.Y - o, a.Width + 2 * o, a.Height + 2 * o);
                        if (dot > 0) g.FillRectangle(black, -dot / 2 - o, -dot / 2 - o, dot + 2 * o, dot + 2 * o);
                    }
                }
                foreach (var a in arms) g.FillRectangle(brush, a);
                if (dot > 0) g.FillRectangle(brush, -dot / 2, -dot / 2, dot, dot);
            }
        }
    }
}
