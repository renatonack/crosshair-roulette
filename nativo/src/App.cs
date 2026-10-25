using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace CrosshairRoulette
{
    // Owns the state: settings, the CS2 feed, the roulette, the overlay and the tray.
    // The window only shows it and calls back in.
    sealed class App : ApplicationContext
    {
        public static readonly int ShowMessage = Native.RegisterWindowMessage("CrosshairRoulette.Show");
        const double RollSeconds = 1.0;
        const int RollTicks = 12;
        const int HkRoll = 1, HkToggle = 2;

        public readonly Settings S;
        public Spec Current;
        public double Spread;
        public int Kills;
        public string Observing = "none", Map, GsiError;
        public DateTime LastPacket = DateTime.MinValue;
        public readonly List<string> HotkeyErrors = new List<string>();
        public event Action Changed; // state the window shows
        public event Action Frame;   // the preview should repaint

        readonly Overlay overlay = new Overlay();
        readonly Sound sound = new Sound();
        readonly KillDetector detector = new KillDetector();
        readonly GsiServer gsi = new GsiServer();
        readonly Random rnd = new Random();
        readonly Timer anim = new Timer { Interval = 15 };
        readonly Timer slow = new Timer { Interval = 2000 };
        readonly SynchronizationContext ui;
        readonly MessageWindow messages;
        readonly NotifyIcon tray;
        readonly MainForm window;
        string cfgDirCache;
        bool cfgDirLooked, rolling, quitting, wasConnected;
        Spec flicker, result;
        DateTime rollStart, lastFrame;
        int rollN;

        public bool Rolling => rolling;
        public Spec Display => rolling ? flicker : Current; // what is on screen right now
        public bool Connected => (DateTime.UtcNow - LastPacket).TotalSeconds < 25; // CS2 heartbeats every 10s

        public App(bool startHidden)
        {
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            S = Settings.Load();
            Current = S.Current ?? Crosshairs.Resolve(Crosshairs.ById("verde_modesto"), rnd);
            if (!string.IsNullOrEmpty(S.CfgDir))
            {
                // fix a folder picked one level too high; drop it if it's what Steam would find anyway
                string fixedDir = Cs2.NormalizeCfgDir(S.CfgDir);
                if (string.Equals(fixedDir, Cs2.FindCfgDir(), StringComparison.OrdinalIgnoreCase)) fixedDir = null;
                if (fixedDir != S.CfgDir) { S.CfgDir = fixedDir; S.Save(); }
            }
            sound.Enabled = S.Sound;
            sound.SetVolume(S.Volume);
            messages = new MessageWindow(this);

            detector.Kill += n => { Kills += n; Roll(); };
            detector.Shot += OnShot;
            detector.Status += (obs, map) =>
            {
                bool changed = obs != Observing || map != Map;
                Observing = obs;
                Map = map;
                if (changed) Changed?.Invoke();
            };
            gsi.Payload += p => ui.Post(_ =>
            {
                bool was = Connected;
                LastPacket = DateTime.UtcNow;
                detector.Update(p);
                if (!was) Changed?.Invoke();
            }, null);
            if (!gsi.Start(S.GsiPort, S.GsiToken)) GsiError = gsi.Error;
            var st = GsiStatus();
            if (st.Found && !st.UpToDate) TryInstall(out _); // first launch: connect to CS2 on its own

            RegisterHotkeys();
            ApplyLoginItem();

            tray = new NotifyIcon { Icon = AppIcon(16), Text = "Crosshair Roulette", Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            BuildTrayMenu();

            anim.Tick += (s, e) => Animate();
            slow.Tick += (s, e) =>
            {
                overlay.KeepOnTop();
                bool c = Connected;
                if (c != wasConnected) { wasConnected = c; Changed?.Invoke(); }
            };
            slow.Start();

            window = new MainForm(this);
            RenderOverlay();
            if (!startHidden) window.Show();
        }

        // ------------------------------------------------------------ the roulette
        List<Spec> Pool() => Crosshairs.Presets.Where(p => !S.Disabled.Contains(p.Id)).ToList();

        Spec PickNext()
        {
            var pool = Pool();
            if (pool.Count == 0 || (S.IncludeRandom && rnd.NextDouble() < 0.5)) return Crosshairs.RandomSpec(rnd);
            var fresh = pool.Where(p => Current == null || p.Id != Current.Id).ToList();
            var list = fresh.Count > 0 ? fresh : pool;
            return Crosshairs.Resolve(list[rnd.Next(list.Count)], rnd);
        }

        // something random to flash on screen while the roulette spins
        Spec Flicker()
        {
            var pool = Pool();
            if (pool.Count == 0 || (S.IncludeRandom && rnd.NextDouble() < 0.4)) return Crosshairs.RandomSpec(rnd);
            return Crosshairs.Resolve(pool[rnd.Next(pool.Count)], rnd);
        }

        public void Roll()
        {
            result = PickNext();
            rolling = true;
            rollStart = DateTime.UtcNow;
            rollN = 0;
            flicker = Flicker();
            Spread = 0;
            sound.PlayTick(0);
            StartAnim();
            RenderOverlay();
            Changed?.Invoke();
        }

        void StartAnim()
        {
            if (anim.Enabled) return;
            lastFrame = DateTime.UtcNow;
            anim.Start();
        }

        void Animate()
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            bool dirty = false;
            if (rolling)
            {
                double p = Math.Min(1, (now - rollStart).TotalSeconds / RollSeconds);
                int target = (int)Math.Floor(RollTicks * (1 - Math.Pow(1 - p, 3)));
                while (rollN < target)
                {
                    rollN++;
                    flicker = Flicker();
                    dirty = true;
                    sound.PlayTick(p);
                }
                if (p >= 1)
                {
                    rolling = false;
                    Current = result;
                    S.Current = Current;
                    S.Save();
                    sound.PlayLand();
                    dirty = true;
                    BuildTrayMenu();
                    Changed?.Invoke();
                }
            }
            if (Spread > 0)
            {
                Spread *= Math.Pow(0.5, dt / 0.25); // CS2-style recovery after shooting
                if (Spread < 0.5) Spread = 0;
                dirty = true;
            }
            if (dirty)
            {
                RenderOverlay();
                Frame?.Invoke();
            }
            if (!rolling && Spread == 0) anim.Stop();
        }

        void OnShot(int shots)
        {
            var s = Display;
            if (s == null || s.Dynamic <= 0) return;
            double max = s.DynamicMax > 0 ? s.DynamicMax : s.Dynamic * 3;
            Spread = Math.Min(max, Spread + s.Dynamic * shots);
            RenderOverlay();
            Frame?.Invoke();
            StartAnim();
        }

        public void RenderOverlay() => overlay.Render(Display, Spread, S);

        public void Equip(string id)
        {
            var p = Crosshairs.ById(id);
            if (p == null) return;
            rolling = false;
            Spread = 0;
            Current = Crosshairs.Resolve(p, rnd);
            S.Current = Current;
            S.Save();
            sound.PlayLand();
            RenderOverlay();
            BuildTrayMenu();
            Changed?.Invoke();
        }

        public void SetInRoulette(string id, bool on)
        {
            S.Disabled.Remove(id);
            if (!on) S.Disabled.Add(id);
            S.Save();
            Changed?.Invoke();
        }

        public void ToggleOverlay()
        {
            S.OverlayVisible = !S.OverlayVisible;
            S.Save();
            RenderOverlay();
            BuildTrayMenu();
            Changed?.Invoke();
        }

        public void SimulateKill()
        {
            Kills++;
            Roll();
        }

        public void SettingsChanged(bool hotkeys = false)
        {
            sound.Enabled = S.Sound;
            sound.SetVolume(S.Volume);
            if (hotkeys) RegisterHotkeys();
            ApplyLoginItem();
            S.Save();
            RenderOverlay();
            BuildTrayMenu();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ hotkeys
        void RegisterHotkeys()
        {
            Native.UnregisterHotKey(messages.Handle, HkRoll);
            Native.UnregisterHotKey(messages.Handle, HkToggle);
            HotkeyErrors.Clear();
            Register(HkRoll, S.HotkeyRoll, "roll");
            Register(HkToggle, S.HotkeyToggle, "toggle");
        }

        void Register(int id, string acc, string name)
        {
            if (string.IsNullOrEmpty(acc)) return;
            if (!Hotkeys.TryParse(acc, out var mods, out var key) || !Native.RegisterHotKey(messages.Handle, id, mods | Native.MOD_NOREPEAT, (uint)key))
                HotkeyErrors.Add(name);
        }

        public void OnHotkey(int id)
        {
            if (id == HkRoll) Roll();
            else if (id == HkToggle) ToggleOverlay();
        }

        // ------------------------------------------------------------ CS2
        public string CfgDir
        {
            get
            {
                if (!string.IsNullOrEmpty(S.CfgDir)) return S.CfgDir;
                if (!cfgDirLooked) { cfgDirCache = Cs2.FindCfgDir(); cfgDirLooked = true; }
                return cfgDirCache;
            }
        }

        public Cs2.Status GsiStatus() => Cs2.Check(CfgDir, S.GsiPort, S.GsiToken);

        public bool TryInstall(out string error)
        {
            error = null;
            string dir = CfgDir;
            if (dir == null) { error = "Não encontrei a pasta cfg do CS2. Escolha a pasta manualmente."; return false; }
            try { Cs2.Install(dir, S.GsiPort, S.GsiToken); return true; }
            catch (Exception e) { error = "Não consegui gravar o arquivo (" + e.Message + ")."; return false; }
        }

        public void SetCfgDir(string dir)
        {
            S.CfgDir = Cs2.NormalizeCfgDir(dir);
            cfgDirLooked = false;
            S.Save();
            Changed?.Invoke();
        }

        public void OpenCfgDir()
        {
            string d = CfgDir;
            if (d != null) Process.Start("explorer.exe", "\"" + d + "\"");
        }

        // ------------------------------------------------------------ Windows integration
        void ApplyLoginItem()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (S.StartWithWindows) k.SetValue("Crosshair Roulette", "\"" + Application.ExecutablePath + "\" --hidden");
                    else if (k.GetValue("Crosshair Roulette") != null) k.DeleteValue("Crosshair Roulette", false);
                }
            }
            catch { }
        }

        public bool CreateShortcuts(out string error)
        {
            error = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                string exe = Application.ExecutablePath;
                foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs) })
                {
                    object lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(dir, "Crosshair Roulette.lnk") });
                    var t = lnk.GetType();
                    t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { exe });
                    t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(exe) });
                    t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { exe + ",0" });
                    t.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Cada abate no CS2 sorteia uma mira nova" });
                    t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static Icon AppIcon(int size)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))
                return s != null ? new Icon(s, size, size) : SystemIcons.Application;
        }

        public static string KeyLabel(string acc) => string.IsNullOrEmpty(acc) ? "sem atalho" : acc;

        void BuildTrayMenu()
        {
            if (tray == null) return;
            var m = new ContextMenuStrip();
            m.Items.Add("Abrir Crosshair Roulette", null, (s, e) => ShowWindow());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add($"Sortear mira  ({KeyLabel(S.HotkeyRoll)})", null, (s, e) => Roll());
            m.Items.Add($"{(S.OverlayVisible ? "Esconder" : "Mostrar")} mira  ({KeyLabel(S.HotkeyToggle)})", null, (s, e) => ToggleOverlay());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Sair", null, (s, e) => Quit());
            var old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = m;
            old?.Dispose();
            string tip = "Crosshair Roulette" + (Current != null ? " — " + Current.Name : "");
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        public void ShowWindow()
        {
            if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
            window.Show();
            window.Activate();
        }

        public void WindowClosing(FormClosingEventArgs e)
        {
            if (quitting || e.CloseReason == CloseReason.WindowsShutDown) return;
            e.Cancel = true;
            window.Hide();
            if (!S.TrayHintShown)
            {
                tray.ShowBalloonTip(4000, "Crosshair Roulette continua rodando", "A mira segue ativa. Clique com o botão direito no ícone da bandeja para sair.", ToolTipIcon.Info);
                S.TrayHintShown = true;
                S.Save();
            }
        }

        public void Quit()
        {
            quitting = true;
            anim.Stop();
            slow.Stop();
            gsi.Dispose();
            Native.UnregisterHotKey(messages.Handle, HkRoll);
            Native.UnregisterHotKey(messages.Handle, HkToggle);
            tray.Visible = false;
            tray.Dispose();
            S.Save();
            overlay.Close();
            window.Close();
            messages.DestroyHandle();
            ExitThread();
        }

        // an invisible top-level window for hotkeys and "show yourself" from a second launch
        sealed class MessageWindow : NativeWindow
        {
            readonly App app;

            public MessageWindow(App app)
            {
                this.app = app;
                CreateHandle(new CreateParams { Caption = "CrosshairRoulette.Messages" });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Native.WM_HOTKEY) { app.OnHotkey(m.WParam.ToInt32()); return; }
                if (m.Msg == ShowMessage && ShowMessage != 0) { app.ShowWindow(); return; }
                base.WndProc(ref m);
            }
        }
    }
}
