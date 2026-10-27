using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TomatoClock
{
    enum Mode { Focus, ShortBreak, LongBreak }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        internal static void EnableDpiAwareness()
        {
            try { SetProcessDPIAware(); }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--selftest")
            {
                SelfTest.Run(args.Length > 1 ? args[1] : null);
                return;
            }

            if (args.Length > 1 && args[0] == "--makeicon")
            {
                IconBuilder.Write(args[1]);
                return;
            }

            EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    static class Native
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref on, 4);
            }
            catch { }
        }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(21, 22, 26);
        public static readonly Color Surface = Color.FromArgb(33, 35, 41);
        public static readonly Color SurfaceHi = Color.FromArgb(45, 48, 57);
        public static readonly Color Track = Color.FromArgb(48, 51, 60);
        public static readonly Color Text = Color.FromArgb(244, 245, 247);
        public static readonly Color Muted = Color.FromArgb(148, 154, 166);
        public static readonly Color Ink = Color.FromArgb(20, 21, 26);
        public static readonly Color Focus = Color.FromArgb(255, 99, 82);
        public static readonly Color ShortBreak = Color.FromArgb(70, 209, 159);
        public static readonly Color LongBreak = Color.FromArgb(96, 165, 250);

        public static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A,
                (int)Math.Min(255, c.R + (255 - c.R) * amount),
                (int)Math.Min(255, c.G + (255 - c.G) * amount),
                (int)Math.Min(255, c.B + (255 - c.B) * amount));
        }

        public static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                (int)Math.Max(0, c.R * factor),
                (int)Math.Max(0, c.G * factor),
                (int)Math.Max(0, c.B * factor));
        }
    }

    // Single source for the per-mode facts (name, accent, duration).
    static class Modes
    {
        static readonly string[] Names = new string[] { "专注", "短休息", "长休息" };
        static readonly Color[] Accents = new Color[] { Theme.Focus, Theme.ShortBreak, Theme.LongBreak };

        public static string Name(Mode m) { return Names[(int)m]; }

        public static Color Accent(Mode m) { return Accents[(int)m]; }

        public static int Minutes(Mode m, Settings s)
        {
            if (m == Mode.Focus) return s.FocusMinutes;
            if (m == Mode.ShortBreak) return s.ShortBreakMinutes;
            return s.LongBreakMinutes;
        }
    }

    static class AppIcon
    {
        public static Icon Create()
        {
            using (Bitmap bmp = IconBuilder.Draw(32))
            {
                IntPtr h = bmp.GetHicon();
                return Icon.FromHandle(h);
            }
        }
    }

    static class IconBuilder
    {
        static readonly int[] Sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };

        public static void Write(string path)
        {
            List<byte[]> images = new List<byte[]>();
            foreach (int s in Sizes)
            {
                using (Bitmap bmp = Draw(s))
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }
            }

            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)Sizes.Length);

                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    int s = Sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)images[i].Length);
                    bw.Write((uint)offset);
                    offset += images[i].Length;
                }

                foreach (byte[] img in images) bw.Write(img);
            }
        }

        internal static Bitmap Draw(int s)
        {
            Bitmap bmp = new Bitmap(s, s);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float k = s;

                using (SolidBrush body = new SolidBrush(Theme.Focus))
                    g.FillEllipse(body, 0.10f * k, 0.26f * k, 0.80f * k, 0.70f * k);

                if (s >= 32)
                {
                    using (SolidBrush shine = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                        g.FillEllipse(shine, 0.24f * k, 0.38f * k, 0.22f * k, 0.16f * k);
                }

                using (SolidBrush leaf = new SolidBrush(Color.FromArgb(78, 200, 122)))
                {
                    g.FillEllipse(leaf, 0.42f * k, 0.08f * k, 0.26f * k, 0.24f * k);
                    g.FillEllipse(leaf, 0.34f * k, 0.16f * k, 0.24f * k, 0.16f * k);
                }
            }
            return bmp;
        }
    }

    class Settings
    {
        public int FocusMinutes = 25;
        public int ShortBreakMinutes = 5;
        public int LongBreakMinutes = 15;
        public int LongBreakEvery = 4;
        public bool AutoStart = true;
        public bool SoundOn = true;
        public bool AlwaysOnTop = false;
        public int CompletedToday = 0;
        public string CompletedDate = "";

        public static string FolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pomodoro"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.ini"); }
        }

        public static Settings Load()
        {
            Settings s;
            try
            {
                s = FromText(File.ReadAllText(FilePath));
            }
            catch
            {
                s = new Settings();
            }

            string today = TodayKey();
            if (s.CompletedDate != today)
            {
                s.CompletedDate = today;
                s.CompletedToday = 0;
            }
            s.Clamp();
            return s;
        }

        public static string TodayKey()
        {
            return DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static Settings FromText(string text)
        {
            Settings s = new Settings();
            if (text != null)
            {
                string[] lines = text.Replace("\r", "").Split('\n');
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "focus") s.FocusMinutes = ToInt(val, s.FocusMinutes);
                    else if (key == "short") s.ShortBreakMinutes = ToInt(val, s.ShortBreakMinutes);
                    else if (key == "long") s.LongBreakMinutes = ToInt(val, s.LongBreakMinutes);
                    else if (key == "every") s.LongBreakEvery = ToInt(val, s.LongBreakEvery);
                    else if (key == "autostart") s.AutoStart = ToBool(val, s.AutoStart);
                    else if (key == "sound") s.SoundOn = ToBool(val, s.SoundOn);
                    else if (key == "ontop") s.AlwaysOnTop = ToBool(val, s.AlwaysOnTop);
                    else if (key == "completed") s.CompletedToday = ToInt(val, s.CompletedToday);
                    else if (key == "date") s.CompletedDate = val;
                }
            }
            s.Clamp();
            return s;
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Pomodoro settings");
            sb.AppendLine("focus=" + FocusMinutes);
            sb.AppendLine("short=" + ShortBreakMinutes);
            sb.AppendLine("long=" + LongBreakMinutes);
            sb.AppendLine("every=" + LongBreakEvery);
            sb.AppendLine("autostart=" + (AutoStart ? "1" : "0"));
            sb.AppendLine("sound=" + (SoundOn ? "1" : "0"));
            sb.AppendLine("ontop=" + (AlwaysOnTop ? "1" : "0"));
            sb.AppendLine("completed=" + CompletedToday);
            sb.AppendLine("date=" + CompletedDate);
            return sb.ToString();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(FilePath, ToText(), Encoding.UTF8);
            }
            catch { }
        }

        public Settings Clone()
        {
            Settings s = new Settings();
            s.FocusMinutes = FocusMinutes;
            s.ShortBreakMinutes = ShortBreakMinutes;
            s.LongBreakMinutes = LongBreakMinutes;
            s.LongBreakEvery = LongBreakEvery;
            s.AutoStart = AutoStart;
            s.SoundOn = SoundOn;
            s.AlwaysOnTop = AlwaysOnTop;
            s.CompletedToday = CompletedToday;
            s.CompletedDate = CompletedDate;
            return s;
        }

        // Applies the user-editable fields; the daily counter stays with the live instance.
        public void CopyFrom(Settings o)
        {
            FocusMinutes = o.FocusMinutes;
            ShortBreakMinutes = o.ShortBreakMinutes;
            LongBreakMinutes = o.LongBreakMinutes;
            LongBreakEvery = o.LongBreakEvery;
            AutoStart = o.AutoStart;
            SoundOn = o.SoundOn;
            AlwaysOnTop = o.AlwaysOnTop;
            Clamp();
        }

        void Clamp()
        {
            FocusMinutes = Math.Min(180, Math.Max(1, FocusMinutes));
            ShortBreakMinutes = Math.Min(120, Math.Max(1, ShortBreakMinutes));
            LongBreakMinutes = Math.Min(180, Math.Max(1, LongBreakMinutes));
            LongBreakEvery = Math.Min(12, Math.Max(2, LongBreakEvery));
            CompletedToday = Math.Max(0, CompletedToday);
        }

        static int ToInt(string v, int fallback)
        {
            int n;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        static bool ToBool(string v, bool fallback)
        {
            if (v == "1") return true;
            if (v == "0") return false;
            bool b;
            return bool.TryParse(v, out b) ? b : fallback;
        }
    }

    class MainForm : Form
    {
        readonly Settings settings;
        readonly bool demo;

        Mode mode = Mode.Focus;
        int totalSeconds = 1500;
        double remaining = 1500;
        DateTime endAt = DateTime.MinValue;
        DateTime dayDate = DateTime.Today;
        bool running;
        bool phaseStarted;
        int completedInCycle;
        int shownSecond = -1;
        int lastTodayCount = -1;
        string lastTrayText = "";

        System.Windows.Forms.Timer ticker;
        NotifyIcon tray;
        Icon appIcon;

        RingControl ring;
        DotsBar dots;
        PillButton btnPrimary;
        PillButton btnReset;
        PillButton btnSkip;
        PillButton btnSettings;
        PillButton tabFocus;
        PillButton tabShort;
        PillButton tabLong;
        Label lblToday;

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        public MainForm() : this(false) { }

        public MainForm(bool demo)
        {
            this.demo = demo;
            settings = demo ? new Settings() : Settings.Load();
            if (demo) settings.SoundOn = false;
            dayDate = DateTime.Today;
            if (string.IsNullOrEmpty(settings.CompletedDate)) settings.CompletedDate = Settings.TodayKey();

            Text = "番茄钟";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(400, 548);
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUi();
            BuildTray();

            ticker = new System.Windows.Forms.Timer();
            ticker.Interval = 200;
            ticker.Tick += new EventHandler(Ticker_Tick);
            ticker.Start();

            mode = Mode.Focus;
            totalSeconds = Modes.Minutes(mode, settings) * 60;
            remaining = totalSeconds;
            TopMost = settings.AlwaysOnTop;

            // Must be set after all child controls exist so the whole tree scales together.
            AutoScaleMode = AutoScaleMode.Dpi;
            UpdateDisplay();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(Handle);
        }

        void BuildUi()
        {
            tabFocus = MakeTab(Mode.Focus, 60);
            tabShort = MakeTab(Mode.ShortBreak, 156);
            tabLong = MakeTab(Mode.LongBreak, 252);

            btnSettings = new PillButton();
            btnSettings.Text = "⚙";
            btnSettings.Font = new Font("Segoe UI Symbol", 13f);
            btnSettings.Filled = false;
            btnSettings.SetBounds(346, 15, 38, 38);
            btnSettings.Click += new EventHandler(delegate { OpenSettings(); });
            Controls.Add(btnSettings);

            ring = new RingControl();
            ring.SetBounds(50, 66, 300, 300);
            Controls.Add(ring);

            dots = new DotsBar();
            dots.SetBounds(150, 378, 100, 12);
            Controls.Add(dots);

            btnPrimary = new PillButton();
            btnPrimary.Font = new Font("Microsoft YaHei UI", 11f);
            btnPrimary.Filled = true;
            btnPrimary.SetBounds(120, 400, 160, 46);
            btnPrimary.Click += new EventHandler(delegate { TogglePlay(); });
            Controls.Add(btnPrimary);

            btnReset = new PillButton();
            btnReset.Text = "重置";
            btnReset.Filled = false;
            btnReset.SetBounds(52, 464, 100, 40);
            btnReset.Click += new EventHandler(delegate { ResetPhase(); });
            Controls.Add(btnReset);

            btnSkip = new PillButton();
            btnSkip.Text = "跳过";
            btnSkip.Filled = false;
            btnSkip.SetBounds(248, 464, 100, 40);
            btnSkip.Click += new EventHandler(delegate { SkipPhase(); });
            Controls.Add(btnSkip);

            lblToday = new Label();
            lblToday.SetBounds(0, 516, 400, 22);
            lblToday.TextAlign = ContentAlignment.MiddleCenter;
            lblToday.ForeColor = Theme.Muted;
            lblToday.BackColor = Theme.Bg;
            lblToday.Font = new Font("Microsoft YaHei UI", 9f);
            Controls.Add(lblToday);
        }

        PillButton MakeTab(Mode m, int x)
        {
            PillButton b = new PillButton();
            b.Text = Modes.Name(m);
            b.Tag = m;
            b.Filled = false;
            b.Font = new Font("Microsoft YaHei UI", 9f);
            b.SetBounds(x, 18, 88, 32);
            b.Click += new EventHandler(Tab_Click);
            Controls.Add(b);
            return b;
        }

        void Tab_Click(object sender, EventArgs e)
        {
            PillButton b = sender as PillButton;
            if (b != null && b.Tag is Mode) SwitchMode((Mode)b.Tag);
        }

        void BuildTray()
        {
            if (demo) return;
            appIcon = AppIcon.Create();
            Icon = appIcon;

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Visible = true;
            tray.Text = "番茄钟";

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem miShow = new ToolStripMenuItem("显示主窗口");
            miShow.Click += new EventHandler(delegate { ShowMain(); });
            ToolStripMenuItem miExit = new ToolStripMenuItem("退出");
            miExit.Click += new EventHandler(delegate { Close(); });
            menu.Items.Add(miShow);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += new EventHandler(delegate { ShowMain(); });
        }

        void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        public static string FormatTime(double seconds)
        {
            int total = (int)Math.Ceiling(seconds);
            if (total < 0) total = 0;
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0) return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", h, m, s);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", m, s);
        }

        void Ticker_Tick(object sender, EventArgs e)
        {
            CheckDayRollover();
            if (!running) return;

            double r = (endAt - DateTime.Now).TotalSeconds;
            if (r <= 0)
            {
                remaining = 0;
                CompletePhase();
                return;
            }
            remaining = r;
            UpdateClock(false);
        }

        void CheckDayRollover()
        {
            DateTime today = DateTime.Today;
            if (today == dayDate) return;
            dayDate = today;
            settings.CompletedDate = Settings.TodayKey();
            settings.CompletedToday = 0;
            if (!demo) settings.Save();
            UpdateDisplay();
        }

        // The single phase-transition entry point: sets up mode, duration, timer and UI.
        void EnterPhase(Mode m, bool autoStart)
        {
            mode = m;
            totalSeconds = Modes.Minutes(mode, settings) * 60;
            remaining = totalSeconds;
            phaseStarted = autoStart;
            running = autoStart;
            endAt = autoStart ? DateTime.Now.AddSeconds(remaining) : DateTime.MinValue;
            UpdateDisplay();
        }

        // Owns the cycle rule: count (or not) the finished focus and pick the next phase.
        Mode Advance(bool counted)
        {
            if (mode == Mode.Focus)
            {
                if (counted)
                {
                    settings.CompletedToday++;
                    completedInCycle++;
                }
                return completedInCycle >= settings.LongBreakEvery ? Mode.LongBreak : Mode.ShortBreak;
            }
            if (mode == Mode.LongBreak) completedInCycle = 0;
            return Mode.Focus;
        }

        void CompletePhase()
        {
            Mode next = Advance(true);
            string title = mode == Mode.Focus ? "专注完成" : "休息结束";

            string body;
            if (next == Mode.LongBreak)
                body = string.Format("已完成 {0} 个番茄，休息 {1} 分钟。", completedInCycle, settings.LongBreakMinutes);
            else if (next == Mode.ShortBreak)
                body = string.Format("休息 {0} 分钟，起来活动一下。", settings.ShortBreakMinutes);
            else
                body = string.Format("开始新的 {0} 分钟专注。", settings.FocusMinutes);

            if (!demo) settings.Save();
            Notify(title, body);
            EnterPhase(next, settings.AutoStart);
        }

        void Notify(string title, string body)
        {
            if (tray != null)
            {
                try { tray.ShowBalloonTip(4000, title, body, ToolTipIcon.None); }
                catch { }
            }

            if (settings.SoundOn)
            {
                try { SystemSounds.Asterisk.Play(); }
                catch { }
                try
                {
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        try
                        {
                            Console.Beep(880, 140);
                            Console.Beep(1175, 140);
                            Console.Beep(1568, 240);
                        }
                        catch { }
                    });
                }
                catch { }
            }

            try
            {
                FLASHWINFO fw = new FLASHWINFO();
                fw.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
                fw.hwnd = Handle;
                fw.dwFlags = 0x00000003 | 0x0000000C;
                fw.uCount = 4;
                fw.dwTimeout = 0;
                FlashWindowEx(ref fw);
            }
            catch { }
        }

        void TogglePlay()
        {
            if (running) Pause();
            else StartTimer();
        }

        void StartTimer()
        {
            if (remaining <= 0) remaining = totalSeconds;
            if (remaining <= 0) return;
            endAt = DateTime.Now.AddSeconds(remaining);
            running = true;
            phaseStarted = true;
            UpdateDisplay();
        }

        void Pause()
        {
            if (!running) return;
            double r = (endAt - DateTime.Now).TotalSeconds;
            remaining = r > 0 ? r : 0;
            running = false;
            endAt = DateTime.MinValue;
            UpdateDisplay();
        }

        void ResetPhase()
        {
            EnterPhase(mode, false);
        }

        void SkipPhase()
        {
            EnterPhase(Advance(false), false);
        }

        void SwitchMode(Mode m)
        {
            EnterPhase(m, false);
        }

        // A phase that never started can adopt new durations; a paused/elapsed one keeps them.
        void ApplySettings()
        {
            if (!running && !phaseStarted) EnterPhase(mode, false);
            if (completedInCycle > settings.LongBreakEvery) completedInCycle = settings.LongBreakEvery;
            TopMost = settings.AlwaysOnTop;
            if (!demo) settings.Save();
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            Color accent = Modes.Accent(mode);

            ring.Accent = accent;
            ring.SubText = SubTextForMode();

            btnPrimary.Text = running ? "暂停" : (phaseStarted ? "继续" : "开始");
            btnPrimary.Accent = accent;

            UpdateTab(tabFocus, Mode.Focus, accent);
            UpdateTab(tabShort, Mode.ShortBreak, accent);
            UpdateTab(tabLong, Mode.LongBreak, accent);

            dots.Total = settings.LongBreakEvery;
            dots.Filled = completedInCycle;
            dots.Accent = accent;

            if (settings.CompletedToday != lastTodayCount)
            {
                lastTodayCount = settings.CompletedToday;
                lblToday.Text = string.Format("今日完成 {0} 个番茄", lastTodayCount);
            }

            UpdateClock(true);
        }

        // Runs on every tick, but only touches the UI when the displayed second changed.
        void UpdateClock(bool force)
        {
            int sec = (int)Math.Ceiling(remaining);
            if (!force && sec == shownSecond) return;
            shownSecond = sec;

            string text = FormatTime(remaining);
            double progress = totalSeconds > 0 ? 1.0 - remaining / totalSeconds : 0;
            if (progress < 0) progress = 0;
            if (progress > 1) progress = 1;
            ring.Progress = progress;
            ring.TimeText = text;

            if (tray != null)
            {
                string t = string.Format("番茄钟 {0} · {1}", text, Modes.Name(mode));
                if (t != lastTrayText)
                {
                    lastTrayText = t;
                    try { tray.Text = t; } catch { }
                }
            }
        }

        string SubTextForMode()
        {
            if (mode == Mode.Focus)
            {
                int current = Math.Min(completedInCycle + 1, settings.LongBreakEvery);
                return string.Format("第 {0} / {1} 轮", current, settings.LongBreakEvery);
            }
            if (mode == Mode.LongBreak) return "长休息 · 一组已完成";
            return "短休息";
        }

        void UpdateTab(PillButton tab, Mode m, Color accent)
        {
            tab.Filled = mode == m;
            tab.Accent = accent;
        }

        void OpenSettings()
        {
            using (SettingsForm sf = new SettingsForm(settings))
            {
                if (sf.ShowDialog(this) == DialogResult.OK && sf.Result != null)
                {
                    settings.CopyFrom(sf.Result);
                    ApplySettings();
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space) { TogglePlay(); return true; }
            if (keyData == Keys.R) { ResetPhase(); return true; }
            if (keyData == Keys.S) { SkipPhase(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (ticker != null) ticker.Stop();
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            if (!demo) settings.Save();
            base.OnFormClosing(e);
        }

        // Used by the self test / preview renderer.
        public Mode DebugMode { get { return mode; } }
        public bool DebugRunning { get { return running; } }
        public int DebugCompletedToday { get { return settings.CompletedToday; } }
        public double DebugRemaining { get { return remaining; } }
        public string DebugClockText { get { return ring.TimeText; } }

        public void DebugSetState(Mode m, int totalSec, int remainingSec, int cycle, int today, bool isRunning)
        {
            mode = m;
            totalSeconds = totalSec;
            remaining = remainingSec;
            completedInCycle = cycle;
            settings.CompletedToday = today;
            settings.CompletedDate = Settings.TodayKey();
            running = isRunning;
            phaseStarted = isRunning || remainingSec < totalSec;
            endAt = isRunning ? DateTime.Now.AddSeconds(remainingSec) : DateTime.MinValue;
            UpdateDisplay();
        }
    }

    class RingControl : Control
    {
        double progress;
        string timeText = "25:00";
        string subText = "";
        Color accent = Theme.Focus;

        float cachedSize = -1f;
        Font timeFont;
        Font subFont;
        SolidBrush textBrush;
        SolidBrush subBrush;
        Pen trackPen;
        Pen accentPen;
        Pen glowPen;
        StringFormat centered;

        public RingControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public double Progress
        {
            get { return progress; }
            set
            {
                if (value != progress)
                {
                    progress = value;
                    Invalidate();
                }
            }
        }

        public string TimeText
        {
            get { return timeText; }
            set
            {
                if (value != timeText)
                {
                    timeText = value;
                    Invalidate();
                }
            }
        }

        public string SubText
        {
            get { return subText; }
            set
            {
                if (value != subText)
                {
                    subText = value;
                    Invalidate();
                }
            }
        }

        public Color Accent
        {
            get { return accent; }
            set
            {
                if (value != accent)
                {
                    accent = value;
                    cachedSize = -1f;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float size = Math.Min(Width, Height);
            EnsureResources(size);

            float thickness = Math.Max(6f, size * 0.044f);
            float pad = thickness / 2f + 4f;
            RectangleF rect = new RectangleF(
                (Width - size) / 2f + pad,
                (Height - size) / 2f + pad,
                size - pad * 2f,
                size - pad * 2f);

            g.DrawEllipse(trackPen, rect);

            if (Progress > 0.001)
            {
                float sweep = (float)(360.0 * Math.Min(1.0, Progress));
                g.DrawArc(glowPen, rect, -90f, sweep);
                g.DrawArc(accentPen, rect, -90f, sweep);
            }

            float timeSize = size * 0.235f;
            RectangleF tr = new RectangleF(0, Height * 0.5f - timeSize * 0.95f, Width, timeSize * 1.5f);
            g.DrawString(TimeText, timeFont, textBrush, tr, centered);

            if (!string.IsNullOrEmpty(SubText))
            {
                float subSize = Math.Max(11f, size * 0.045f);
                RectangleF sr = new RectangleF(0, Height * 0.5f + timeSize * 0.55f, Width, subSize * 2f);
                g.DrawString(SubText, subFont, subBrush, sr, centered);
            }
        }

        void EnsureResources(float size)
        {
            if (size == cachedSize) return;
            cachedSize = size;
            DisposeResources();

            float thickness = Math.Max(6f, size * 0.044f);
            timeFont = new Font("Segoe UI Semibold", size * 0.235f, FontStyle.Regular, GraphicsUnit.Pixel);
            subFont = new Font("Microsoft YaHei UI", Math.Max(11f, size * 0.045f), FontStyle.Regular, GraphicsUnit.Pixel);
            textBrush = new SolidBrush(Theme.Text);
            subBrush = new SolidBrush(Theme.Muted);
            trackPen = MakePen(Theme.Track, thickness);
            accentPen = MakePen(accent, thickness);
            glowPen = MakePen(Color.FromArgb(34, accent), thickness * 2.1f);

            centered = new StringFormat();
            centered.Alignment = StringAlignment.Center;
            centered.LineAlignment = StringAlignment.Center;
        }

        static Pen MakePen(Color color, float width)
        {
            Pen p = new Pen(color, width);
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            return p;
        }

        void DisposeResources()
        {
            if (timeFont != null) { timeFont.Dispose(); timeFont = null; }
            if (subFont != null) { subFont.Dispose(); subFont = null; }
            if (textBrush != null) { textBrush.Dispose(); textBrush = null; }
            if (subBrush != null) { subBrush.Dispose(); subBrush = null; }
            if (trackPen != null) { trackPen.Dispose(); trackPen = null; }
            if (accentPen != null) { accentPen.Dispose(); accentPen = null; }
            if (glowPen != null) { glowPen.Dispose(); glowPen = null; }
            if (centered != null) { centered.Dispose(); centered = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeResources();
            base.Dispose(disposing);
        }
    }

    class DotsBar : Control
    {
        int total = 4;
        int filled;
        Color accent = Theme.Focus;
        SolidBrush onBrush;
        SolidBrush offBrush;

        public DotsBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            onBrush = new SolidBrush(accent);
            offBrush = new SolidBrush(Theme.Track);
        }

        public int Total
        {
            get { return total; }
            set
            {
                if (value != total)
                {
                    total = value;
                    Invalidate();
                }
            }
        }

        public int Filled
        {
            get { return filled; }
            set
            {
                if (value != filled)
                {
                    filled = value;
                    Invalidate();
                }
            }
        }

        public Color Accent
        {
            get { return accent; }
            set
            {
                if (value != accent)
                {
                    accent = value;
                    if (onBrush != null) onBrush.Color = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Total <= 0) return;

            float d = Math.Min(Height, 9f);
            float gap = d * 0.95f;
            float totalW = Total * d + (Total - 1) * gap;
            float startX = (Width - totalW) / 2f;
            float y = (Height - d) / 2f;

            for (int i = 0; i < Total; i++)
            {
                SolidBrush b = i < Filled ? onBrush : offBrush;
                g.FillEllipse(b, startX + i * (d + gap), y, d, d);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (onBrush != null) { onBrush.Dispose(); onBrush = null; }
                if (offBrush != null) { offBrush.Dispose(); offBrush = null; }
            }
            base.Dispose(disposing);
        }
    }

    class PillButton : Control
    {
        Color accent = Theme.Focus;
        bool filled = true;
        bool hover;
        bool pressed;

        GraphicsPath path;
        Pen pen;
        SolidBrush brush;
        int pathWidth = -1;
        int pathHeight = -1;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }

        public Color Accent
        {
            get { return accent; }
            set
            {
                if (value != accent)
                {
                    accent = value;
                    Invalidate();
                }
            }
        }

        public bool Filled
        {
            get { return filled; }
            set
            {
                if (value != filled)
                {
                    filled = value;
                    Invalidate();
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (pressed)
            {
                pressed = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            EnsureGeometry();

            Color fill;
            Color border;
            Color txt;

            if (Filled)
            {
                fill = pressed ? Theme.Darken(Accent, 0.82f) : (hover ? Theme.Lighten(Accent, 0.12f) : Accent);
                border = fill;
                txt = Theme.Ink;
            }
            else
            {
                fill = hover ? Theme.SurfaceHi : Theme.Surface;
                border = pressed ? Theme.Muted : Theme.Track;
                txt = hover ? Theme.Text : Color.FromArgb(200, 205, 215);
            }

            brush.Color = fill;
            pen.Color = border;
            g.FillPath(brush, path);
            g.DrawPath(pen, path);

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, txt,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        void EnsureGeometry()
        {
            if (path != null && pathWidth == Width && pathHeight == Height) return;
            DisposeGeometry();

            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            path = RoundRect(r, Math.Max(2, r.Height / 2));
            pen = new Pen(Theme.Track);
            brush = new SolidBrush(Theme.Surface);
            pathWidth = Width;
            pathHeight = Height;
        }

        void DisposeGeometry()
        {
            if (path != null) { path.Dispose(); path = null; }
            if (pen != null) { pen.Dispose(); pen = null; }
            if (brush != null) { brush.Dispose(); brush = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeGeometry();
            base.Dispose(disposing);
        }

        internal static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class SettingsForm : Form
    {
        public Settings Result;

        readonly Settings draft;
        NumericUpDown numFocus;
        NumericUpDown numShort;
        NumericUpDown numLong;
        NumericUpDown numEvery;
        CheckBox chkAuto;
        CheckBox chkSound;
        CheckBox chkTop;

        public SettingsForm(Settings current)
        {
            draft = current.Clone();

            Text = "设置";
            ClientSize = new Size(360, 380);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);

            numFocus = MakeNum("专注时长（分钟）", 24, 22, draft.FocusMinutes, 1, 180);
            numShort = MakeNum("短休息（分钟）", 24, 64, draft.ShortBreakMinutes, 1, 120);
            numLong = MakeNum("长休息（分钟）", 24, 106, draft.LongBreakMinutes, 1, 180);
            numEvery = MakeNum("长休息间隔（轮）", 24, 148, draft.LongBreakEvery, 2, 12);

            chkAuto = MakeCheck(24, 196, "自动开始下一阶段", draft.AutoStart);
            chkSound = MakeCheck(24, 228, "阶段结束时播放提示音", draft.SoundOn);
            chkTop = MakeCheck(24, 260, "窗口总在最前", draft.AlwaysOnTop);

            PillButton save = new PillButton();
            save.Text = "保存";
            save.Filled = true;
            save.Font = new Font("Microsoft YaHei UI", 10f);
            save.SetBounds(92, 312, 144, 42);
            save.Click += new EventHandler(delegate { SaveAndClose(); });
            Controls.Add(save);

            PillButton cancel = new PillButton();
            cancel.Text = "取消";
            cancel.Filled = false;
            cancel.SetBounds(248, 312, 88, 42);
            cancel.Click += new EventHandler(delegate { DialogResult = DialogResult.Cancel; });
            Controls.Add(cancel);

            // Must be set after all child controls exist so the whole tree scales together.
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(Handle);
        }

        NumericUpDown MakeNum(string label, int x, int y, int value, int min, int max)
        {
            Label lbl = new Label();
            lbl.SetBounds(x + 8, y + 5, 190, 22);
            lbl.ForeColor = Theme.Text;
            lbl.BackColor = Theme.Bg;
            lbl.Text = label;
            Controls.Add(lbl);

            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Min(max, Math.Max(min, value));
            n.BackColor = Theme.Surface;
            n.ForeColor = Theme.Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.TextAlign = HorizontalAlignment.Right;
            n.Font = new Font("Segoe UI", 10f);
            n.SetBounds(238, y, 94, 28);
            Controls.Add(n);
            return n;
        }

        CheckBox MakeCheck(int x, int y, string text, bool value)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.ForeColor = Theme.Text;
            c.BackColor = Theme.Bg;
            c.FlatStyle = FlatStyle.Flat;
            c.SetBounds(x + 6, y, 300, 24);
            Controls.Add(c);
            return c;
        }

        void SaveAndClose()
        {
            draft.FocusMinutes = (int)numFocus.Value;
            draft.ShortBreakMinutes = (int)numShort.Value;
            draft.LongBreakMinutes = (int)numLong.Value;
            draft.LongBreakEvery = (int)numEvery.Value;
            draft.AutoStart = chkAuto.Checked;
            draft.SoundOn = chkSound.Checked;
            draft.AlwaysOnTop = chkTop.Checked;
            Result = draft;
            DialogResult = DialogResult.OK;
        }
    }

    static class SelfTest
    {
        public static void Run(string outDir)
        {
            if (string.IsNullOrEmpty(outDir)) outDir = Environment.CurrentDirectory;
            Directory.CreateDirectory(outDir);

            List<string> log = new List<string>();
            int fail = 0;

            Program.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Pure logic checks.
            Check(log, ref fail, "format 1500", MainForm.FormatTime(1500) == "25:00");
            Check(log, ref fail, "format 0", MainForm.FormatTime(0) == "00:00");
            Check(log, ref fail, "format 59.2", MainForm.FormatTime(59.2) == "01:00");
            Check(log, ref fail, "format 3661", MainForm.FormatTime(3661) == "1:01:01");

            Settings s = new Settings();
            s.FocusMinutes = 30;
            s.ShortBreakMinutes = 7;
            s.LongBreakMinutes = 20;
            s.LongBreakEvery = 3;
            s.AutoStart = false;
            s.SoundOn = false;
            s.AlwaysOnTop = true;
            s.CompletedToday = 5;
            s.CompletedDate = "2026-10-27";

            Settings rt = Settings.FromText(s.ToText());
            Check(log, ref fail, "settings roundtrip focus", rt.FocusMinutes == 30);
            Check(log, ref fail, "settings roundtrip every", rt.LongBreakEvery == 3);
            Check(log, ref fail, "settings roundtrip autostart", rt.AutoStart == false);
            Check(log, ref fail, "settings roundtrip ontop", rt.AlwaysOnTop == true);
            Check(log, ref fail, "settings roundtrip date", rt.CompletedDate == "2026-10-27");
            Check(log, ref fail, "settings clone keeps completion", s.Clone().CompletedToday == 5);

            Settings clamped = Settings.FromText("focus=999\nlong=0\nevery=0\ncompleted=-3");
            Check(log, ref fail, "settings clamp focus", clamped.FocusMinutes == 180);
            Check(log, ref fail, "settings clamp every", clamped.LongBreakEvery == 2);
            Check(log, ref fail, "settings clamp completed", clamped.CompletedToday == 0);

            // Render previews of the real UI.
            MainForm f = new MainForm(true);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(20, 20);
            f.Show();
            Application.DoEvents();

            f.DebugSetState(Mode.Focus, 1500, 1118, 2, 3, true);
            Application.DoEvents();
            SavePreview(f, Path.Combine(outDir, "preview-focus.png"));

            f.DebugSetState(Mode.LongBreak, 900, 617, 4, 3, true);
            Application.DoEvents();
            SavePreview(f, Path.Combine(outDir, "preview-longbreak.png"));

            f.DebugSetState(Mode.ShortBreak, 300, 300, 0, 3, false);
            Application.DoEvents();
            SavePreview(f, Path.Combine(outDir, "preview-idle.png"));

            SettingsForm sf = new SettingsForm(s);
            sf.Show();
            Application.DoEvents();
            SavePreview(sf, Path.Combine(outDir, "preview-settings.png"));
            sf.Close();

            // The ticker counts down and the displayed clock follows it.
            f.DebugSetState(Mode.Focus, 1500, 1500, 0, 3, true);
            Pump(1400);
            Check(log, ref fail, "ticker counts down", f.DebugRemaining < 1499.5);
            Check(log, ref fail, "clock text follows countdown",
                f.DebugClockText != "25:00" && f.DebugClockText == MainForm.FormatTime(f.DebugRemaining));

            // State machine: a focus session reaching zero advances to a short break,
            // counts a pomodoro and auto-starts the next phase (AutoStart defaults on).
            f.DebugSetState(Mode.Focus, 1500, 1, 0, 3, true);
            Pump(2500);
            Check(log, ref fail, "focus completes -> short break", f.DebugMode == Mode.ShortBreak);
            Check(log, ref fail, "completed count incremented", f.DebugCompletedToday == 4);
            Check(log, ref fail, "next phase auto-started", f.DebugRunning);

            f.Close();

            log.Add(fail == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + fail + ")");
            File.WriteAllLines(Path.Combine(outDir, "selftest.txt"), log.ToArray());
            Environment.Exit(fail == 0 ? 0 : 1);
        }

        static void Check(List<string> log, ref int fail, string name, bool ok)
        {
            if (!ok) fail++;
            log.Add((ok ? "PASS  " : "FAIL  ") + name);
        }

        static void Pump(int ms)
        {
            DateTime until = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < until)
            {
                Application.DoEvents();
                Thread.Sleep(30);
            }
        }

        static void SavePreview(Form f, string path)
        {
            using (Bitmap bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
