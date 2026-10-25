using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    // The control window: mode, current crosshair, the fixed-crosshair editor,
    // console commands, the roulette list, settings and the CS2 connection.
    // One scrolling page made of sections that show/hide with the mode.
    sealed class MainForm : Form
    {
        readonly App app;
        readonly Panel body;
        readonly StatusLabel status = new StatusLabel();
        readonly Preview preview = new Preview();
        readonly List<Card> cards = new List<Card>();
        readonly Dictionary<string, Btn> shapeBtns = new Dictionary<string, Btn>();
        readonly List<Swatch> swatches = new List<Swatch>();
        readonly ToolTip tips = new ToolTip();
        readonly Timer toastTimer = new Timer { Interval = 2400 };
        Panel secMode, secNow, secFixed, secConsole, secMiras, secBottom;
        Label spacer, toast;
        Lbl curName, nowHint, countLbl, hkWarn, gsiText, gsiPath, scaleVal, opacityVal, volumeVal, fSizeVal, fThickVal, fGapVal;
        Btn modeRoulette, modeFixed, rollBtn, hideBtn, installBtn, pickBtn, openDirBtn, display;
        Toggle randomToggle, soundToggle, startupToggle, fOutline;
        Slider scale, opacity, volume, fSize, fThick, fGap;
        HotkeyBox hkRoll, hkToggle;
        int x0, w;
        bool? laidOutFixed;

        static int D(double v) => Theme.D(v);

        public MainForm(App app)
        {
            this.app = app;
            Text = "Crosshair Roulette";
            Icon = App.AppIcon(32);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            ClientSize = new Size(D(920), D(800));

            var header = new Panel { Dock = DockStyle.Top, Height = D(56), BackColor = Theme.Bg };
            header.Paint += (s, e) => { using (var p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1); };
            header.Controls.Add(new Lbl("Crosshair Roulette", Theme.H1, Theme.Text) { Location = new Point(D(24), D(15)) });
            header.Controls.Add(status);
            header.Resize += (s, e) => PlaceStatus(header);

            body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
            Controls.Add(body);
            Controls.Add(header);

            toast = new Label { AutoSize = true, BackColor = Theme.Panel2, ForeColor = Theme.Text, Padding = new Padding(D(14), D(8), D(14), D(8)), Visible = false, Font = Theme.Body };
            Controls.Add(toast);
            toast.BringToFront();
            toastTimer.Tick += (s, e) => { toast.Visible = false; toastTimer.Stop(); };

            Build();
            MinimumSize = new Size(Width, D(520));
            MaximumSize = new Size(Width, Screen.PrimaryScreen.WorkingArea.Height + D(40));

            app.Changed += RefreshState;
            app.Frame += () => { preview.Spec = app.Display; preview.Spread = PreviewSpread(); preview.Invalidate(); };
            FormClosing += (s, e) => app.WindowClosing(e);
            RefreshState();
            PlaceStatus(header);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(Handle);
        }

        void PlaceStatus(Control header) => status.Location = new Point(header.Width - status.Width - D(24), (header.Height - status.Height) / 2);

        static T Put<T>(Control parent, T c, int x, int y) where T : Control
        {
            c.Location = new Point(x, y);
            parent.Controls.Add(c);
            return c;
        }

        Panel Section(bool boxed = false)
        {
            Panel p = boxed ? new Box(Theme.Panel) : new Panel { BackColor = Theme.Bg };
            p.Width = w;
            body.Controls.Add(p);
            return p;
        }

        void Toast(string text)
        {
            toast.Text = text;
            toast.Location = new Point((ClientSize.Width - toast.PreferredSize.Width) / 2, ClientSize.Height - toast.PreferredSize.Height - D(18));
            toast.Visible = true;
            toast.BringToFront();
            toastTimer.Stop();
            toastTimer.Start();
        }

        double PreviewSpread() => app.Display != null && app.Display.Type == "flood" && app.Spread < 70 ? 70 : app.Spread;

        static string Pct(double v) => Math.Round(v * 100) + "%";
        static string Num(double v) => v.ToString("0.#");
        static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

        // stack the visible sections; the editor and the roulette list swap with the mode
        void Relayout()
        {
            bool fx = app.FixedMode;
            secFixed.Visible = fx;
            secMiras.Visible = !fx;
            int y = D(20) + body.AutoScrollPosition.Y;
            foreach (var sec in new[] { secMode, secNow, secFixed, secConsole, secMiras, secBottom })
            {
                if (!sec.Visible) continue;
                sec.Location = new Point(x0, y);
                y += sec.Height + D(24);
            }
            spacer.Location = new Point(x0, y);
        }

        // the whole scrolling page as one picture (used by the snapshot test)
        internal void SaveSnapshot(string path)
        {
            var secs = new List<Panel> { secMode, secNow };
            secs.Add(app.FixedMode ? secFixed : secConsole);
            secs.Add(app.FixedMode ? secConsole : secMiras);
            secs.Add(secBottom);
            int height = D(20) + secs.Sum(s => s.Height + D(24));
            using (var bmp = new Bitmap(w + 2 * x0, height))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Theme.Bg);
                int y = D(20);
                foreach (var s in secs)
                {
                    s.DrawToBitmap(bmp, new Rectangle(x0, y, s.Width, s.Height));
                    y += s.Height + D(24);
                }
                bmp.Save(path);
            }
        }

        void Build()
        {
            x0 = D(24);
            w = ClientSize.Width - D(48) - SystemInformation.VerticalScrollBarWidth;

            // ---- mode
            secMode = Section();
            int bw = (w - D(10)) / 2;
            modeRoulette = Put(secMode, new Btn("Roleta: muda a cada abate"), 0, 0);
            modeFixed = Put(secMode, new Btn("Mira fixa (estilo Crosshair V2)"), bw + D(10), 0);
            modeRoulette.Size = modeFixed.Size = new Size(bw, D(42));
            modeRoulette.Click += (s, e) => app.SetMode("roleta");
            modeFixed.Click += (s, e) => app.SetMode("fixa");
            secMode.Height = D(42);

            BuildNow();
            BuildFixedEditor();
            BuildConsole();
            BuildMiras();
            BuildBottom();
            spacer = new Label { Size = new Size(1, D(4)), BackColor = Theme.Bg };
            body.Controls.Add(spacer);
        }

        void BuildNow()
        {
            secNow = Section();
            preview.Size = new Size(w, D(260));
            Put(secNow, preview, 0, 0);
            int y = preview.Height + D(12);
            Put(secNow, new Lbl("Mira atual", Theme.Small, Theme.Muted), 0, y);
            curName = Put(secNow, new Lbl("", Theme.Big, Theme.Text) { AutoSize = false, Size = new Size(w - D(330), D(30)) }, 0, y + D(16));
            hideBtn = Put(secNow, new Btn("Esconder  (F8)"), 0, y + D(6));
            rollBtn = Put(secNow, new Btn("Sortear mira  (F7)", accent: true), 0, y + D(6));
            rollBtn.Click += (s, e) => app.Roll();
            hideBtn.Click += (s, e) => app.ToggleOverlay();
            y += D(52);
            nowHint = Put(secNow, new Lbl("Cada abate no CS2 sorteia uma mira nova. O preview mostra o tamanho real no centro da tela.", Theme.Small, Theme.Muted, w), 0, y);
            secNow.Height = nowHint.Bottom;
        }

        void BuildFixedEditor()
        {
            var S = app.S;
            secFixed = Section(boxed: true);
            int iw = w - D(36), x = D(18), y = D(14);
            Put(secFixed, new Lbl("Sua mira fixa", Theme.H2, Theme.Text), x, y);
            y += D(30);
            var sub = Put(secFixed, new Lbl("Escolha como a mira fica. Os abates não mudam essa mira; o preview acima mostra o resultado.", Theme.Small, Theme.Muted, iw), x, y);
            y += sub.Height + D(12);

            Put(secFixed, new Lbl("Formato", Theme.Small, Theme.Muted), x, y);
            y += D(20);
            int bx = x;
            foreach (var (id, label) in new[] { ("cruz", "Cruz"), ("cruz_ponto", "Cruz + ponto"), ("ponto", "Ponto"), ("t", "T"), ("circulo", "Círculo"), ("circulo_ponto", "Círculo + ponto") })
            {
                var b = new Btn(label);
                b.Width = TextRenderer.MeasureText(label, Theme.BodyBold).Width + D(28);
                if (bx + b.Width > x + iw) { bx = x; y += D(42); }
                Put(secFixed, b, bx, y);
                bx += b.Width + D(8);
                b.Click += (s, e) => { app.S.FixedShape = id; app.FixedChanged(); };
                shapeBtns[id] = b;
            }
            y += D(34) + D(16);

            Put(secFixed, new Lbl("Cor", Theme.Small, Theme.Muted), x, y);
            y += D(20);
            var rainbow = new Swatch(Color.White, rainbow: true) { Text = "Arco-íris" };
            rainbow.Width = D(120);
            Put(secFixed, rainbow, x - D(4), y);
            rainbow.Click += (s, e) => { app.S.FixedRainbow = true; app.FixedChanged(); };
            swatches.Add(rainbow);
            int sx = rainbow.Right + D(6);
            foreach (var (hex, name) in new[] { ("#3bff6b", "Verde"), ("#00ffff", "Ciano"), ("#ffff00", "Amarelo"), ("#ff2d2d", "Vermelho"), ("#ff4fd8", "Rosa"), ("#ffffff", "Branco"), ("#a020f0", "Roxo"), ("#ff8800", "Laranja") })
            {
                var sw = new Swatch(ColorTranslator.FromHtml(hex));
                Put(secFixed, sw, sx, y);
                sx += sw.Width + D(2);
                tips.SetToolTip(sw, name);
                sw.Click += (s, e) => { app.S.FixedColor = hex; app.S.FixedRainbow = false; app.FixedChanged(); };
                swatches.Add(sw);
            }
            var other = Put(secFixed, new Btn("Outra cor…"), sx + D(8), y);
            other.Click += (s, e) =>
            {
                Color start;
                try { start = ColorTranslator.FromHtml(app.S.FixedColor); } catch { start = Color.Lime; }
                using (var dlg = new ColorDialog { FullOpen = true, Color = start })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    app.S.FixedColor = Hex(dlg.Color);
                    app.S.FixedRainbow = false;
                    app.FixedChanged();
                }
            };
            y += D(34) + D(16);

            int colW = (iw - D(24)) / 2, xr = x + colW + D(24), yl = y, yr = y;
            fSize = SliderRow(secFixed, "Tamanho", 1, 40, 1, S.FixedSize, Num, x, ref yl, colW, out fSizeVal);
            fThick = SliderRow(secFixed, "Espessura", 0.5, 10, 0.5, S.FixedThick, Num, x, ref yl, colW, out fThickVal);
            fGap = SliderRow(secFixed, "Espaço no meio", -5, 20, 1, S.FixedGap, Num, xr, ref yr, colW, out fGapVal);
            fOutline = Put(secFixed, new Toggle("Contorno preto") { Checked = S.FixedOutline }, xr, yr + D(4));
            yr += D(40);
            secFixed.Height = Math.Max(yl, yr) + D(6);

            fSize.ValueChanged += (s, e) => { app.S.FixedSize = fSize.Value; fSizeVal.Text = Num(fSize.Value); app.FixedChanged(); };
            fThick.ValueChanged += (s, e) => { app.S.FixedThick = fThick.Value; fThickVal.Text = Num(fThick.Value); app.FixedChanged(); };
            fGap.ValueChanged += (s, e) => { app.S.FixedGap = fGap.Value; fGapVal.Text = Num(fGap.Value); app.FixedChanged(); };
            fOutline.CheckedChanged += (s, e) => { app.S.FixedOutline = fOutline.Checked; app.FixedChanged(); };
        }

        void BuildConsole()
        {
            secConsole = Section(boxed: true);
            int iw = w - D(36), iy = D(14);
            Put(secConsole, new Lbl("Tirar a mira normal do CS2", Theme.H2, Theme.Text), D(18), iy);
            iy += D(30);
            var how = Put(secConsole, new Lbl("Abra o console do CS2 (tecla ' ao lado do 1), cole o comando e aperte Enter:", Theme.Body, Theme.Muted, iw), D(18), iy);
            iy += how.Height + D(8);
            foreach (var (cmd, what) in new[] { ("crosshair 0", "tira a mira normal do jogo"), ("crosshair 1", "volta a mira normal") })
            {
                var row = Put(secConsole, new Panel { BackColor = Theme.Panel2, Size = new Size(iw, D(46)) }, D(18), iy);
                row.Controls.Add(new Lbl(cmd, Theme.MonoBig, Theme.Accent) { Location = new Point(D(12), D(12)) });
                row.Controls.Add(new Lbl(what, Theme.Small, Theme.Muted) { Location = new Point(D(140), D(14)) });
                var copy = new Btn("Copiar") { Width = D(96) };
                copy.Location = new Point(iw - copy.Width - D(6), D(6));
                copy.Click += (s, e) => CopyCommand(copy, cmd);
                row.Controls.Add(copy);
                iy += row.Height + D(8);
            }
            var noConsole = Put(secConsole, new Lbl("O console não abre? No CS2: Configurações → Jogo → Ativar console do desenvolvedor → Sim.", Theme.Small, Theme.Muted, iw), D(18), iy + D(2));
            secConsole.Height = noConsole.Bottom + D(14);
        }

        void BuildMiras()
        {
            secMiras = Section();
            var title = Put(secMiras, new Lbl("Miras da roleta", Theme.H2, Theme.Text), 0, 0);
            countLbl = Put(secMiras, new Lbl("", Theme.Body, Theme.Muted), title.PreferredWidth + D(4), D(3));
            randomToggle = new Toggle("Incluir miras aleatórias geradas (estilo CS2)") { Checked = app.S.IncludeRandom };
            Put(secMiras, randomToggle, w - randomToggle.Width, 0);
            int y = D(32);
            var listHint = Put(secMiras, new Lbl("Todas as miras do vídeo. Clique numa mira para usar agora; desmarque para tirar da roleta.", Theme.Small, Theme.Muted, w), 0, y);
            y += listHint.Height + D(10);
            int cols = 5, gap = D(10);
            int cw = (w - gap * (cols - 1)) / cols, ch = (int)(cw * 0.75) + D(48);
            for (int i = 0; i < Crosshairs.Presets.Count; i++)
            {
                var p = Crosshairs.Presets[i];
                var card = new Card(Crosshairs.Resolve(p, new Random(1))) { Size = new Size(cw, ch) };
                card.UseClicked += (s, e) => { app.Equip(p.Id); Toast("Usando: " + p.Name); };
                card.ToggleClicked += (s, e) => app.SetInRoulette(p.Id, app.S.Disabled.Contains(p.Id));
                Put(secMiras, card, (i % cols) * (cw + gap), y + (i / cols) * (ch + gap));
                cards.Add(card);
            }
            secMiras.Height = y + (Crosshairs.Presets.Count + cols - 1) / cols * (ch + gap) - gap;
            randomToggle.CheckedChanged += (s, e) => { app.S.IncludeRandom = randomToggle.Checked; app.SettingsChanged(); };
        }

        void BuildBottom()
        {
            var S = app.S;
            secBottom = Section();
            int colW = (w - D(32)) / 2, xl = 0, xr = colW + D(32), yl = 0, yr = 0;
            Put(secBottom, new Lbl("Configurações", Theme.H2, Theme.Text), xl, yl);
            yl += D(36);
            scale = SliderRow(secBottom, "Tamanho na tela", 0.5, 2.5, 0.05, S.Scale, Pct, xl, ref yl, colW, out scaleVal);
            opacity = SliderRow(secBottom, "Opacidade", 0.2, 1, 0.05, S.Opacity, Pct, xl, ref yl, colW, out opacityVal);
            Put(secBottom, new Lbl("Monitor", Theme.Small, Theme.Muted), xl, yl);
            // a button + menu instead of a ComboBox, which Windows refuses to paint dark
            display = Put(secBottom, new Btn("") { TextAlign = ContentAlignment.MiddleLeft }, xl, yl + D(20));
            display.SetText(MonitorLabel(SelectedScreen()) + "   ▾");
            display.Width = colW;
            display.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip();
                foreach (var sc in Screen.AllScreens)
                {
                    var item = new ToolStripMenuItem(MonitorLabel(sc)) { Checked = sc.DeviceName == SelectedScreen().DeviceName };
                    item.Click += (s2, e2) =>
                    {
                        app.S.Display = sc.Primary ? null : sc.DeviceName;
                        display.Text = MonitorLabel(sc) + "   ▾";
                        app.SettingsChanged();
                    };
                    menu.Items.Add(item);
                }
                menu.Show(display, new Point(0, display.Height));
            };
            yl += D(60);
            soundToggle = Put(secBottom, new Toggle("Som da roleta") { Checked = S.Sound }, xl, yl);
            yl += D(34);
            volume = SliderRow(secBottom, "Volume", 0, 1, 0.05, S.Volume, Pct, xl, ref yl, colW, out volumeVal);
            hkRoll = HotkeyRow("Atalho: sortear mira", S.HotkeyRoll, xl, ref yl, colW);
            hkToggle = HotkeyRow("Atalho: mostrar / esconder", S.HotkeyToggle, xl, ref yl, colW);
            hkWarn = Put(secBottom, new Lbl("", Theme.Small, Theme.Warn, colW) { Visible = false }, xl, yl);
            yl += D(38);
            startupToggle = Put(secBottom, new Toggle("Iniciar com o Windows (fica na bandeja)") { Checked = S.StartWithWindows }, xl, yl);
            yl += D(38);
            var shortcutBtn = Put(secBottom, new Btn("Criar atalhos (área de trabalho e menu Iniciar)"), xl, yl);
            shortcutBtn.Click += (s, e) => Toast(app.CreateShortcuts(out var err) ? "Atalhos criados na área de trabalho e no menu Iniciar." : "Não consegui criar os atalhos: " + err);
            yl += shortcutBtn.Height;

            Put(secBottom, new Lbl("Conexão com o CS2", Theme.H2, Theme.Text), xr, yr);
            yr += D(36);
            gsiText = Put(secBottom, new Lbl("", Theme.Body, Theme.Text, colW) { Height = D(62) }, xr, yr);
            yr += D(66);
            gsiPath = Put(secBottom, new Lbl("", Theme.Mono, Theme.Muted, colW) { BackColor = Theme.Panel2, Height = D(56), Padding = new Padding(D(8), D(6), D(8), D(6)) }, xr, yr);
            yr += D(66);
            installBtn = new Btn("Reinstalar");
            pickBtn = new Btn("Escolher pasta…");
            openDirBtn = new Btn("Abrir pasta");
            int bx = xr;
            foreach (var b in new[] { installBtn, pickBtn, openDirBtn }) { Put(secBottom, b, bx, yr); bx += b.Width + D(8); }
            installBtn.Click += (s, e) => { Toast(app.TryInstall(out var err) ? "Integração instalada. Se o CS2 estava aberto, reinicie o jogo." : err); RefreshState(); };
            pickBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Escolha a pasta cfg do CS2 (…\\game\\csgo\\cfg)" })
                    if (dlg.ShowDialog(this) == DialogResult.OK) app.SetCfgDir(dlg.SelectedPath);
            };
            openDirBtn.Click += (s, e) => app.OpenCfgDir();
            yr += D(46);
            foreach (var tip in new[]
            {
                "1. No CS2 use Vídeo → Modo de exibição: Tela cheia em janela. Em tela cheia exclusiva nenhuma mira externa aparece.",
                "2. Joga esticado (ex. 1440×1080)? Coloque essa resolução no Windows e ative \"Esticar\" no painel da placa de vídeo.",
                "3. Se o CS2 já estava aberto quando você instalou, reinicie o jogo uma vez.",
            })
            {
                var t = Put(secBottom, new Lbl(tip, Theme.Small, Theme.Muted, colW), xr, yr);
                yr += t.Height + D(6);
            }
            yr += D(8);
            var simBtn = Put(secBottom, new Btn("Testar: simular um abate"), xr, yr);
            var quitBtn = Put(secBottom, new Btn("Fechar o programa") { ForeColor = Theme.Bad }, simBtn.Right + D(8), yr);
            simBtn.Click += (s, e) => app.SimulateKill();
            quitBtn.Click += (s, e) => app.Quit();
            yr += D(44);
            Put(secBottom, new Lbl($"Versão 1.2 (nativa, leve) · porta {S.GsiPort}", Theme.Small, Theme.Muted), xr, yr);
            yr += D(24);
            secBottom.Height = Math.Max(yl, yr) + D(10);

            // wire the settings (after the initial values are in place)
            scale.ValueChanged += (s, e) => { app.S.Scale = scale.Value; scaleVal.Text = Pct(scale.Value); preview.Zoom = scale.Value; preview.Invalidate(); app.SettingsChanged(); };
            opacity.ValueChanged += (s, e) => { app.S.Opacity = opacity.Value; opacityVal.Text = Pct(opacity.Value); app.SettingsChanged(); };
            volume.ValueChanged += (s, e) => { app.S.Volume = volume.Value; volumeVal.Text = Pct(volume.Value); app.SettingsChanged(); };
            soundToggle.CheckedChanged += (s, e) => { app.S.Sound = soundToggle.Checked; app.SettingsChanged(); };
            startupToggle.CheckedChanged += (s, e) => { app.S.StartWithWindows = startupToggle.Checked; app.SettingsChanged(); };
            hkRoll.Captured += (s, e) => { app.S.HotkeyRoll = hkRoll.Value; hkRoll.SetValue(hkRoll.Value); app.SettingsChanged(hotkeys: true); };
            hkToggle.Captured += (s, e) => { app.S.HotkeyToggle = hkToggle.Value; hkToggle.SetValue(hkToggle.Value); app.SettingsChanged(hotkeys: true); };
            preview.Zoom = S.Scale;
        }

        Screen SelectedScreen() => Screen.AllScreens.FirstOrDefault(s => s.DeviceName == app.S.Display) ?? Screen.PrimaryScreen;

        static string MonitorLabel(Screen s)
        {
            int n = Array.IndexOf(Screen.AllScreens, s) + 1;
            return $"{(s.Primary ? "Monitor principal" : $"Monitor {n}")} — {s.Bounds.Width}×{s.Bounds.Height}";
        }

        Slider SliderRow(Control parent, string label, double min, double max, double step, double value, Func<double, string> fmt, int x, ref int y, int width, out Lbl valueLbl)
        {
            Put(parent, new Lbl(label, Theme.Small, Theme.Muted), x, y);
            valueLbl = new Lbl(fmt(value), Theme.Small, Theme.Text) { AutoSize = false, TextAlign = ContentAlignment.TopRight, Size = new Size(D(60), D(18)) };
            Put(parent, valueLbl, x + width - valueLbl.Width, y);
            var slider = new Slider { Min = min, Max = max, Step = step, Width = width };
            slider.Value = value;
            Put(parent, slider, x, y + D(20));
            y += D(54);
            return slider;
        }

        HotkeyBox HotkeyRow(string label, string value, int x, ref int y, int width)
        {
            Put(secBottom, new Lbl(label, Theme.Small, Theme.Muted), x, y);
            var box = new HotkeyBox { Width = width };
            box.SetValue(value);
            Put(secBottom, box, x, y + D(20));
            y += D(60);
            return box;
        }

        readonly Dictionary<Btn, Timer> copyTimers = new Dictionary<Btn, Timer>();

        void CopyCommand(Btn btn, string cmd)
        {
            try { Clipboard.SetText(cmd); }
            catch { Toast("Não consegui copiar. Digite: " + cmd); return; }
            Toast($"Copiado: {cmd}. Cole no console do CS2.");
            btn.Text = "Copiado!";
            btn.ForeColor = Theme.Ok;
            btn.FlatAppearance.BorderColor = Theme.Ok;
            if (!copyTimers.TryGetValue(btn, out var t))
            {
                t = new Timer { Interval = 1600 };
                t.Tick += (s, e) => { t.Stop(); btn.Text = "Copiar"; btn.ForeColor = Theme.Text; btn.FlatAppearance.BorderColor = Theme.Line; };
                copyTimers[btn] = t;
            }
            t.Stop();
            t.Start();
        }

        void RefreshState()
        {
            var S = app.S;
            bool fx = app.FixedMode;
            modeRoulette.SetOn(!fx);
            modeFixed.SetOn(fx);
            if (laidOutFixed != fx)
            {
                laidOutFixed = fx;
                Relayout();
            }

            curName.Text = fx ? app.Display.Name : app.Rolling ? "Sorteando…" : app.Current?.Name ?? "—";
            nowHint.Text = fx
                ? "Mira fixa: os abates não mudam a mira. O preview mostra o tamanho real no centro da tela."
                : "Cada abate no CS2 sorteia uma mira nova. O preview mostra o tamanho real no centro da tela.";
            preview.Spec = app.Display;
            preview.Spread = PreviewSpread();
            preview.Invalidate();
            rollBtn.Visible = !fx;
            rollBtn.SetText($"Sortear mira  ({App.KeyLabel(S.HotkeyRoll)})");
            hideBtn.SetText($"{(S.OverlayVisible ? "Esconder" : "Mostrar")}  ({App.KeyLabel(S.HotkeyToggle)})");
            hideBtn.Left = w - hideBtn.Width;
            rollBtn.Left = hideBtn.Left - D(8) - rollBtn.Width;

            // fixed-crosshair editor
            foreach (var kv in shapeBtns) kv.Value.SetOn(kv.Key == S.FixedShape);
            foreach (var sw in swatches)
            {
                sw.Selected = sw.Rainbow ? S.FixedRainbow : !S.FixedRainbow && string.Equals(Hex(sw.Value), S.FixedColor, StringComparison.OrdinalIgnoreCase);
                sw.Invalidate();
            }
            bool circle = S.FixedShape.StartsWith("circulo"), dotOnly = S.FixedShape == "ponto";
            fThick.Enabled = !dotOnly;
            fGap.Enabled = !circle && !dotOnly;

            // roulette list
            int active = Crosshairs.Presets.Count(p => !S.Disabled.Contains(p.Id));
            countLbl.Text = $"({active} de {Crosshairs.Presets.Count} na roleta)";
            foreach (var c in cards)
            {
                c.InRoulette = !S.Disabled.Contains(c.Spec.Id);
                c.IsCurrent = !app.Rolling && app.Current != null && app.Current.Id == c.Spec.Id;
                c.Invalidate();
            }

            var names = new Dictionary<string, string> { ["roll"] = "sortear mira", ["toggle"] = "mostrar / esconder" };
            hkWarn.Visible = app.HotkeyErrors.Count > 0;
            hkWarn.Text = $"Outro programa já usa o atalho de {string.Join(" e ", app.HotkeyErrors.Select(n => names[n]))}. Escolha outra tecla.";

            var st = app.GsiStatus();
            string label;
            Color dot, fore;
            if (app.GsiError != null) { label = "Erro na conexão"; dot = fore = Theme.Bad; }
            else if (app.Connected && app.Observing == "self") { label = "CS2 conectado" + (app.Map != null ? " · " + app.Map : ""); dot = fore = Theme.Ok; }
            else if (app.Connected && app.Observing == "other") { label = "CS2 conectado (espectando)"; dot = fore = Theme.Warn; }
            else if (app.Connected) { label = "CS2 no menu"; dot = fore = Theme.Warn; }
            else if (!st.UpToDate) { label = "Integração não instalada"; dot = fore = Theme.Bad; }
            else { label = "Aguardando o CS2"; dot = Color.FromArgb(91, 96, 109); fore = Theme.Muted; }
            status.Set(label, dot, fore);
            if (status.Parent != null) PlaceStatus(status.Parent);

            if (app.GsiError != null) gsiText.Text = app.GsiError;
            else if (!st.Found) gsiText.Text = "Não encontrei o CS2 automaticamente. Clique em \"Escolher pasta…\" e selecione …\\Counter-Strike Global Offensive\\game\\csgo\\cfg.";
            else if (st.UpToDate) gsiText.Text = "Integração instalada. O CS2 avisa o programa a cada abate (recurso oficial da Valve, seguro para VAC).";
            else if (st.Installed) gsiText.Text = "Existe uma versão antiga da integração. Clique em Reinstalar.";
            else gsiText.Text = "CS2 encontrado. Clique em Instalar integração.";
            string dir = st.CfgDir ?? "";
            int steamapps = dir.IndexOf(@"\steamapps\", StringComparison.OrdinalIgnoreCase);
            string shown = steamapps > 0 ? "…" + dir.Substring(steamapps) : dir;
            gsiPath.Text = shown.Replace(@"\game\", "\n" + @"\game\"); // GDI can't wrap inside a path
            gsiPath.Visible = gsiPath.Text.Length > 0;
            installBtn.SetText(st.Installed ? "Reinstalar" : "Instalar integração");
            pickBtn.Left = installBtn.Right + D(8);
            openDirBtn.Left = pickBtn.Right + D(8);
            installBtn.Enabled = st.Found;
            openDirBtn.Enabled = st.Found;
        }
    }
}
