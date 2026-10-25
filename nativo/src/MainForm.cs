using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    // The control window: current crosshair, console commands, the list,
    // settings and the CS2 connection. One scrolling page.
    sealed class MainForm : Form
    {
        readonly App app;
        readonly Panel body;
        readonly StatusLabel status = new StatusLabel();
        readonly Preview preview = new Preview();
        readonly List<Card> cards = new List<Card>();
        readonly Timer toastTimer = new Timer { Interval = 2400 };
        Lbl curName, countLbl, hkWarn, gsiText, gsiPath, scaleVal, opacityVal, volumeVal;
        Btn rollBtn, hideBtn, installBtn, pickBtn, openDirBtn;
        Toggle randomToggle, soundToggle, startupToggle;
        Slider scale, opacity, volume;
        Btn display;
        HotkeyBox hkRoll, hkToggle;
        Label toast;

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

        T Add<T>(T c, int x, int y) where T : Control
        {
            c.Location = new Point(x, y);
            body.Controls.Add(c);
            return c;
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

        void Build()
        {
            int x0 = D(24);
            int w = ClientSize.Width - D(48) - SystemInformation.VerticalScrollBarWidth;
            int y = D(20);

            // ---- current crosshair
            preview.Size = new Size(w, D(260));
            Add(preview, x0, y);
            y += preview.Height + D(12);
            Add(new Lbl("Mira atual", Theme.Small, Theme.Muted), x0, y);
            curName = Add(new Lbl("", Theme.Big, Theme.Text) { AutoSize = false, Size = new Size(w - D(330), D(30)) }, x0, y + D(16));
            hideBtn = new Btn("Esconder  (F8)");
            rollBtn = new Btn("Sortear mira  (F7)", accent: true);
            Add(hideBtn, x0 + w - hideBtn.Width, y + D(6));
            Add(rollBtn, hideBtn.Left - D(8) - rollBtn.Width, y + D(6));
            rollBtn.Click += (s, e) => app.Roll();
            hideBtn.Click += (s, e) => app.ToggleOverlay();
            y += D(52);
            var hint = Add(new Lbl("Cada abate no CS2 sorteia uma mira nova. O preview mostra o tamanho real no centro da tela.", Theme.Small, Theme.Muted, w), x0, y);
            y += hint.Height + D(22);

            // ---- console commands
            var box = Add(new Box(Theme.Panel) { Width = w }, x0, y);
            int iw = w - D(36), iy = D(14);
            box.Controls.Add(new Lbl("Tirar a mira normal do CS2", Theme.H2, Theme.Text) { Location = new Point(D(18), iy) });
            iy += D(30);
            var how = new Lbl("Abra o console do CS2 (tecla ' ao lado do 1), cole o comando e aperte Enter:", Theme.Body, Theme.Muted, iw) { Location = new Point(D(18), iy) };
            box.Controls.Add(how);
            iy += how.Height + D(8);
            foreach (var (cmd, what) in new[] { ("crosshair 0", "tira a mira normal do jogo"), ("crosshair 1", "volta a mira normal") })
            {
                var row = new Panel { BackColor = Theme.Panel2, Size = new Size(iw, D(46)), Location = new Point(D(18), iy) };
                row.Controls.Add(new Lbl(cmd, Theme.MonoBig, Theme.Accent) { Location = new Point(D(12), D(12)) });
                row.Controls.Add(new Lbl(what, Theme.Small, Theme.Muted) { Location = new Point(D(140), D(14)) });
                var copy = new Btn("Copiar") { Width = D(96) };
                copy.Location = new Point(iw - copy.Width - D(6), D(6));
                copy.Click += (s, e) => CopyCommand(copy, cmd);
                row.Controls.Add(copy);
                box.Controls.Add(row);
                iy += row.Height + D(8);
            }
            var noConsole = new Lbl("O console não abre? No CS2: Configurações → Jogo → Ativar console do desenvolvedor → Sim.", Theme.Small, Theme.Muted, iw) { Location = new Point(D(18), iy + D(2)) };
            box.Controls.Add(noConsole);
            box.Height = noConsole.Bottom + D(14);
            y += box.Height + D(28);

            // ---- the crosshairs
            var title = Add(new Lbl("Miras", Theme.H2, Theme.Text), x0, y);
            countLbl = Add(new Lbl("", Theme.Body, Theme.Muted), x0 + title.PreferredWidth + D(4), y + D(3));
            randomToggle = new Toggle("Incluir miras aleatórias geradas (estilo CS2)");
            Add(randomToggle, x0 + w - randomToggle.Width, y - D(1));
            y += D(32);
            var listHint = Add(new Lbl("Todas as miras do vídeo. Clique numa mira para usar agora; desmarque para tirar da roleta.", Theme.Small, Theme.Muted, w), x0, y);
            y += listHint.Height + D(10);
            int cols = 5, gap = D(10);
            int cw = (w - gap * (cols - 1)) / cols, ch = (int)(cw * 0.75) + D(48);
            for (int i = 0; i < Crosshairs.Presets.Count; i++)
            {
                var p = Crosshairs.Presets[i];
                var card = new Card(Crosshairs.Resolve(p, new Random(1))) { Size = new Size(cw, ch) };
                card.UseClicked += (s, e) => { app.Equip(p.Id); Toast("Usando: " + p.Name); };
                card.ToggleClicked += (s, e) => app.SetInRoulette(p.Id, app.S.Disabled.Contains(p.Id));
                Add(card, x0 + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap));
                cards.Add(card);
            }
            y += (Crosshairs.Presets.Count + cols - 1) / cols * (ch + gap) + D(20);

            // ---- settings (left) and CS2 (right)
            int colW = (w - D(32)) / 2, xl = x0, xr = x0 + colW + D(32), yl = y, yr = y;
            Add(new Lbl("Configurações", Theme.H2, Theme.Text), xl, yl);
            yl += D(36);
            scale = SliderRow("Tamanho", 0.5, 2.5, app.S.Scale, xl, ref yl, colW, out scaleVal);
            opacity = SliderRow("Opacidade", 0.2, 1, app.S.Opacity, xl, ref yl, colW, out opacityVal);
            Add(new Lbl("Monitor", Theme.Small, Theme.Muted), xl, yl);
            // a button + menu instead of a ComboBox, which Windows refuses to paint dark
            display = Add(new Btn("") { TextAlign = ContentAlignment.MiddleLeft }, xl, yl + D(20));
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
            soundToggle = Add(new Toggle("Som da roleta") { Checked = app.S.Sound }, xl, yl);
            yl += D(34);
            volume = SliderRow("Volume", 0, 1, app.S.Volume, xl, ref yl, colW, out volumeVal);
            hkRoll = HotkeyRow("Atalho: sortear mira", app.S.HotkeyRoll, xl, ref yl, colW);
            hkToggle = HotkeyRow("Atalho: mostrar / esconder", app.S.HotkeyToggle, xl, ref yl, colW);
            hkWarn = Add(new Lbl("", Theme.Small, Theme.Warn, colW) { Visible = false }, xl, yl);
            yl += D(38);
            startupToggle = Add(new Toggle("Iniciar com o Windows (fica na bandeja)") { Checked = app.S.StartWithWindows }, xl, yl);
            yl += D(38);
            var shortcutBtn = Add(new Btn("Criar atalhos (área de trabalho e menu Iniciar)"), xl, yl);
            shortcutBtn.Click += (s, e) => Toast(app.CreateShortcuts(out var err) ? "Atalhos criados na área de trabalho e no menu Iniciar." : "Não consegui criar os atalhos: " + err);
            yl += shortcutBtn.Height;

            Add(new Lbl("Conexão com o CS2", Theme.H2, Theme.Text), xr, yr);
            yr += D(36);
            gsiText = Add(new Lbl("", Theme.Body, Theme.Text, colW) { Height = D(62) }, xr, yr);
            yr += D(66);
            gsiPath = Add(new Lbl("", Theme.Mono, Theme.Muted, colW) { BackColor = Theme.Panel2, Height = D(56), Padding = new Padding(D(8), D(6), D(8), D(6)) }, xr, yr);
            yr += D(66);
            installBtn = new Btn("Reinstalar");
            pickBtn = new Btn("Escolher pasta…");
            openDirBtn = new Btn("Abrir pasta");
            int bx = xr;
            foreach (var b in new[] { installBtn, pickBtn, openDirBtn }) { Add(b, bx, yr); bx += b.Width + D(8); }
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
                var t = Add(new Lbl(tip, Theme.Small, Theme.Muted, colW), xr, yr);
                yr += t.Height + D(6);
            }
            yr += D(8);
            var simBtn = Add(new Btn("Testar: simular um abate"), xr, yr);
            var quitBtn = Add(new Btn("Fechar o programa") { ForeColor = Theme.Bad }, simBtn.Right + D(8), yr);
            simBtn.Click += (s, e) => app.SimulateKill();
            quitBtn.Click += (s, e) => app.Quit();
            yr += D(44);
            Add(new Lbl($"Versão 1.1 (nativa, leve) · porta {app.S.GsiPort}", Theme.Small, Theme.Muted), xr, yr);
            yr += D(24);

            Add(new Label { Size = new Size(1, D(24)), BackColor = Theme.Bg }, x0, Math.Max(yl, yr) + D(4)); // bottom margin for the scroll area

            // ---- wire the settings (after the initial values are in place)
            scale.ValueChanged += (s, e) => { app.S.Scale = scale.Value; scaleVal.Text = Pct(scale.Value); preview.Zoom = scale.Value; preview.Invalidate(); app.SettingsChanged(); };
            opacity.ValueChanged += (s, e) => { app.S.Opacity = opacity.Value; opacityVal.Text = Pct(opacity.Value); app.SettingsChanged(); };
            volume.ValueChanged += (s, e) => { app.S.Volume = volume.Value; volumeVal.Text = Pct(volume.Value); app.SettingsChanged(); };
            soundToggle.CheckedChanged += (s, e) => { app.S.Sound = soundToggle.Checked; app.SettingsChanged(); };
            startupToggle.CheckedChanged += (s, e) => { app.S.StartWithWindows = startupToggle.Checked; app.SettingsChanged(); };
            randomToggle.CheckedChanged += (s, e) => { app.S.IncludeRandom = randomToggle.Checked; app.SettingsChanged(); };
            hkRoll.Captured += (s, e) => { app.S.HotkeyRoll = hkRoll.Value; hkRoll.SetValue(hkRoll.Value); app.SettingsChanged(hotkeys: true); };
            hkToggle.Captured += (s, e) => { app.S.HotkeyToggle = hkToggle.Value; hkToggle.SetValue(hkToggle.Value); app.SettingsChanged(hotkeys: true); };
            randomToggle.Checked = app.S.IncludeRandom;
            preview.Zoom = app.S.Scale;
        }

        static string Pct(double v) => Math.Round(v * 100) + "%";

        Screen SelectedScreen() => Screen.AllScreens.FirstOrDefault(s => s.DeviceName == app.S.Display) ?? Screen.PrimaryScreen;

        static string MonitorLabel(Screen s)
        {
            int n = Array.IndexOf(Screen.AllScreens, s) + 1;
            return $"{(s.Primary ? "Monitor principal" : $"Monitor {n}")} — {s.Bounds.Width}×{s.Bounds.Height}";
        }

        Slider SliderRow(string label, double min, double max, double value, int x, ref int y, int width, out Lbl valueLbl)
        {
            Add(new Lbl(label, Theme.Small, Theme.Muted), x, y);
            valueLbl = new Lbl(Pct(value), Theme.Small, Theme.Text) { AutoSize = false, TextAlign = ContentAlignment.TopRight, Size = new Size(D(60), D(18)) };
            Add(valueLbl, x + width - valueLbl.Width, y);
            var slider = new Slider { Min = min, Max = max, Width = width };
            slider.Value = value;
            Add(slider, x, y + D(20));
            y += D(54);
            return slider;
        }

        HotkeyBox HotkeyRow(string label, string value, int x, ref int y, int width)
        {
            Add(new Lbl(label, Theme.Small, Theme.Muted), x, y);
            var box = new HotkeyBox { Width = width };
            box.SetValue(value);
            Add(box, x, y + D(20));
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
            curName.Text = app.Rolling ? "Sorteando…" : app.Current?.Name ?? "—";
            preview.Spec = app.Display;
            preview.Spread = PreviewSpread();
            preview.Invalidate();
            rollBtn.SetText($"Sortear mira  ({App.KeyLabel(S.HotkeyRoll)})");
            hideBtn.SetText($"{(S.OverlayVisible ? "Esconder" : "Mostrar")}  ({App.KeyLabel(S.HotkeyToggle)})");
            hideBtn.Left = preview.Right - hideBtn.Width;
            rollBtn.Left = hideBtn.Left - D(8) - rollBtn.Width;

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
