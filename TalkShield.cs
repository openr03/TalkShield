// ============================================================================
//  TalkShield (톡실드) — PC 카카오톡 광고 제거
//  개발: 김영삼 (영삼넷 https://youngsam.net · info@youngsam.net)
//  라이선스: MIT © 2026 김영삼
// ----------------------------------------------------------------------------
//  동작 원리
//    100ms 주기로 카카오톡(kakaotalk.exe) 창을 검사하여
//      1) 메인 창의 광고 컨테이너(빈 제목의 자식 창)를 WM_CLOSE 로 닫고
//      2) 친구목록/잠금화면 영역을 리사이즈해 배너 자리를 메우고
//      3) 별도 광고 팝업(웹뷰 광고)을 숨긴다.
//    카카오톡이 꺼져 있으면 검사 주기를 자동으로 늦춰 자원을 아낀다.
//
//  빌드: build.bat (Windows 내장 csc.exe 사용, 추가 설치 불필요)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("TalkShield")]
[assembly: AssemblyDescription("PC 카카오톡 광고 제거")]
[assembly: AssemblyCompany("영삼넷 (youngsam.net)")]
[assembly: AssemblyProduct("TalkShield")]
[assembly: AssemblyCopyright("© 2026 김영삼")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace TalkShield
{
    #region Win32 API

    internal static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]                            public static extern bool   EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")]                            public static extern bool   EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int    GetClassName(IntPtr h, StringBuilder buf, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int    GetWindowText(IntPtr h, StringBuilder buf, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int    GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")]                            public static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")]                            public static extern uint   GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")]                            public static extern bool   GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")]                            public static extern bool   SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]                            public static extern bool   ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]                            public static extern bool   UpdateWindow(IntPtr h);
        [DllImport("user32.dll")]                            public static extern bool   IsWindow(IntPtr h);
        [DllImport("user32.dll")]                            public static extern bool   IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public const uint WM_CLOSE   = 0x0010;
        public const uint SWP_NOMOVE = 0x0002;
        public const int  SW_HIDE    = 0;

        // UI 스레드 전용 호출이므로 버퍼를 재사용해 GC 부담을 없앤다.
        private static readonly StringBuilder Buf = new StringBuilder(512);

        public static string ClassName(IntPtr h)
        {
            Buf.Length = 0;
            GetClassName(h, Buf, 256);
            return Buf.ToString();
        }

        public static string WindowText(IntPtr h)
        {
            int len = GetWindowTextLength(h);
            if (len <= 0) return string.Empty;
            Buf.Length = 0;
            Buf.EnsureCapacity(len + 1);
            GetWindowText(h, Buf, len + 1);
            return Buf.ToString();
        }

        public static List<IntPtr> Descendants(IntPtr parent)
        {
            var list = new List<IntPtr>();
            EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }
    }

    #endregion

    #region 광고 제거 엔진

    internal sealed class AdBlocker
    {
        private const int LayoutShadowPadding = 2;    // 좌우 그림자 여백(px)
        private const int MainViewPadding     = 31;   // 하단 배너 높이(px)
        private const int PidScanIntervalMs   = 1000; // 프로세스 스캔 주기

        // 틱마다 재사용하는 버퍼들 (틱당 힙 할당 최소화)
        private readonly List<IntPtr>    _topWindows = new List<IntPtr>(16);
        private readonly List<IntPtr>    _mains      = new List<IntPtr>(4);
        private readonly HashSet<IntPtr> _mainSet    = new HashSet<IntPtr>();
        private readonly List<IntPtr>    _candidates = new List<IntPtr>(8);
        private readonly List<IntPtr>    _children   = new List<IntPtr>(64);
        private readonly HashSet<uint>   _pids       = new HashSet<uint>();

        // EnumWindows 콜백 델리게이트 캐시 (틱당 델리게이트 할당 방지)
        private readonly Native.EnumWindowsProc _collectTop;
        private readonly Native.EnumWindowsProc _collectChild;

        // 검사 결과 캐시
        private readonly Dictionary<IntPtr, bool> _customScrollCache = new Dictionary<IntPtr, bool>();
        private readonly HashSet<IntPtr> _countedClose = new HashSet<IntPtr>();

        private int _lastPidScan = -PidScanIntervalMs;

        public int  BlockedThisTick { get; private set; }
        public bool KakaoRunning    { get; private set; }

        public AdBlocker()
        {
            _collectTop = delegate(IntPtr h, IntPtr l)
            {
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (_pids.Contains(pid)) _topWindows.Add(h);
                return true;
            };
            _collectChild = delegate(IntPtr h, IntPtr l)
            {
                _children.Add(h);
                return true;
            };
        }

        public void Tick()
        {
            BlockedThisTick = 0;

            RefreshPids();
            if (!KakaoRunning)
            {
                if (_customScrollCache.Count > 0) _customScrollCache.Clear();
                if (_countedClose.Count > 0) _countedClose.Clear();
                return;
            }

            CollectWindows();
            foreach (var main in _mains) ProcessMainWindow(main);
            HideAdPopups();
            PruneCaches();
        }

        // 프로세스 스캔은 비싸므로 1초에 한 번만 수행
        private void RefreshPids()
        {
            int now = Environment.TickCount;
            if (now - _lastPidScan < PidScanIntervalMs) return;
            _lastPidScan = now;

            _pids.Clear();
            foreach (var p in Process.GetProcessesByName("KakaoTalk"))
            {
                try { _pids.Add((uint)p.Id); } catch { }
                p.Dispose();
            }
            KakaoRunning = _pids.Count > 0;
        }

        // 카카오톡 최상위 창을 모아 메인 창 / 광고 팝업 후보로 분류
        private void CollectWindows()
        {
            _topWindows.Clear();
            _mains.Clear();
            _mainSet.Clear();
            _candidates.Clear();

            Native.EnumWindows(_collectTop, IntPtr.Zero);

            foreach (var h in _topWindows)
            {
                if (Native.ClassName(h) == "EVA_Window_Dblclk" &&
                    Native.GetParent(h) == IntPtr.Zero &&
                    Native.WindowText(h).Length > 0)
                {
                    _mains.Add(h);
                    _mainSet.Add(h);
                }
            }
            foreach (var h in _topWindows)
            {
                string cls = Native.ClassName(h);
                if (Native.WindowText(h).Length > 0) continue;
                IntPtr parent = Native.GetParent(h);

                bool isAdPopup =
                    (cls == "EVA_Window_Dblclk" && parent != IntPtr.Zero && _mainSet.Contains(parent)) ||
                    (cls == "EVA_Window" && parent == IntPtr.Zero);
                if (isAdPopup) _candidates.Add(h);
            }
        }

        // 메인 창: 광고 컨테이너 닫기 + 콘텐츠 영역 리사이즈
        private void ProcessMainWindow(IntPtr main)
        {
            if (!Native.IsWindow(main)) return;

            _children.Clear();
            Native.EnumChildWindows(main, _collectChild, IntPtr.Zero);
            if (!IsMainWindowReady()) return; // UI 초기화 전이면 건드리지 않음

            Native.RECT rect;
            if (!Native.GetWindowRect(main, out rect)) return;

            for (int i = 0; i < _children.Count; i++)
            {
                IntPtr child = _children[i];
                if (Native.GetParent(child) != main) continue; // 직속 자식만

                string cls  = Native.ClassName(child);
                string text = Native.WindowText(child);

                if (cls == "EVA_ChildWindow" && text.Length == 0 && !HasCustomScroll(child))
                {
                    Native.SendMessage(child, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    if (_countedClose.Add(child)) BlockedThisTick++;
                }

                ResizeContentView(text, rect, child);
            }
        }

        // 친구목록(OnlineMainView)은 배너 높이만큼, 잠금화면(LockModeView)은 전체로 늘려
        // 광고가 있던 자리를 콘텐츠로 메운다.
        private static void ResizeContentView(string text, Native.RECT r, IntPtr h)
        {
            int w = r.Right - r.Left - LayoutShadowPadding;
            int height;

            if (text.StartsWith("OnlineMainView", StringComparison.Ordinal))
                height = r.Bottom - r.Top - MainViewPadding;
            else if (text.StartsWith("LockModeView", StringComparison.Ordinal))
                height = r.Bottom - r.Top;
            else
                return;

            if (height < 1) return;
            Native.UpdateWindow(h);
            Native.SetWindowPos(h, IntPtr.Zero, 0, 0, w, height, Native.SWP_NOMOVE);
        }

        // 별도 광고 팝업(내부에 웹뷰 'Chrome Legacy Window' 보유)을 숨긴다.
        private void HideAdPopups()
        {
            foreach (var c in _candidates)
            {
                if (Native.IsWindow(c) && Native.IsWindowVisible(c) && HasChromeLegacyWindow(c))
                {
                    Native.ShowWindow(c, Native.SW_HIDE);
                    BlockedThisTick++;
                }
            }
        }

        // 친구목록/잠금화면 뷰가 보여야 정상 초기화된 메인 창으로 간주
        private bool IsMainWindowReady()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                IntPtr h = _children[i];
                if (Native.ClassName(h) != "EVA_ChildWindow") continue;
                string t = Native.WindowText(h);
                if (t.StartsWith("OnlineMainView", StringComparison.Ordinal) ||
                    t.StartsWith("LockModeView", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // 실제 콘텐츠 창(커스텀 스크롤 _EVA_* 보유)은 광고가 아니므로 닫으면 안 된다. 결과 캐시.
        private bool HasCustomScroll(IntPtr h)
        {
            bool cached;
            if (_customScrollCache.TryGetValue(h, out cached) && Native.IsWindow(h)) return cached;

            bool result = Native.ClassName(h).StartsWith("_EVA_", StringComparison.Ordinal);
            if (!result)
            {
                foreach (var d in Native.Descendants(h))
                    if (Native.ClassName(d).StartsWith("_EVA_", StringComparison.Ordinal)) { result = true; break; }
            }
            _customScrollCache[h] = result;
            return result;
        }

        private static bool HasChromeLegacyWindow(IntPtr h)
        {
            if (Native.WindowText(h) == "Chrome Legacy Window") return true;
            foreach (var d in Native.Descendants(h))
                if (Native.WindowText(d) == "Chrome Legacy Window") return true;
            return false;
        }

        // 죽은 창 핸들이 캐시에 쌓이지 않도록 주기적으로 정리
        private void PruneCaches()
        {
            if (_customScrollCache.Count >= 128)
            {
                var dead = new List<IntPtr>();
                foreach (var kv in _customScrollCache) if (!Native.IsWindow(kv.Key)) dead.Add(kv.Key);
                foreach (var d in dead) _customScrollCache.Remove(d);
            }
            if (_countedClose.Count >= 256)
            {
                var dead = new List<IntPtr>();
                foreach (var h in _countedClose) if (!Native.IsWindow(h)) dead.Add(h);
                foreach (var d in dead) _countedClose.Remove(d);
            }
        }
    }

    #endregion

    #region 설정 (레지스트리)

    internal static class Settings
    {
        private const string RegPath = @"Software\TalkShield";
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TalkShield";

        private static RegistryKey Open(bool write)
        {
            return Registry.CurrentUser.CreateSubKey(RegPath, write);
        }

        public static bool Enabled
        {
            get { using (var k = Open(false)) return Convert.ToInt32(k.GetValue("Enabled", 1)) != 0; }
            set { using (var k = Open(true)) k.SetValue("Enabled", value ? 1 : 0, RegistryValueKind.DWord); }
        }

        // 시작 시 새 버전 확인 (끄려면 레지스트리 HKCU\Software\TalkShield\AutoUpdate = 0)
        public static bool AutoUpdate
        {
            get { using (var k = Open(false)) return Convert.ToInt32(k.GetValue("AutoUpdate", 1)) != 0; }
        }

        public static long TotalBlocked
        {
            get
            {
                using (var k = Open(false))
                {
                    long v;
                    return long.TryParse(Convert.ToString(k.GetValue("TotalBlocked", "0")), out v) ? v : 0;
                }
            }
            set { using (var k = Open(true)) k.SetValue("TotalBlocked", value.ToString(), RegistryValueKind.String); }
        }

        // 트레이 아이콘 숨김 상태를 저장해 재부팅/재시작 후에도 유지한다.
        public static bool IconHidden
        {
            get { using (var k = Open(false)) return Convert.ToInt32(k.GetValue("IconHidden", 0)) != 0; }
            set { using (var k = Open(true)) k.SetValue("IconHidden", value ? 1 : 0, RegistryValueKind.DWord); }
        }

        public static bool Autostart
        {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunPath)) return k != null && k.GetValue(AppName) != null; }
            set
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunPath, true))
                {
                    if (k == null) return;
                    if (value) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(AppName, false);
                }
            }
        }
    }

    #endregion

    #region 트레이 앱

    internal sealed class TrayApp : ApplicationContext
    {
        public const string Version        = "1.1.1";
        public const string BlogUrl        = "https://youngsam.net";
        public const string ShowSignalName = "TalkShield_ShowIcon";

        private const string GithubOwner = "openr03";
        private const string GithubRepo  = "TalkShield";

        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly AdBlocker _blocker = new AdBlocker();
        private readonly ToolStripMenuItem _miEnabled;
        private readonly Icon _iconOn, _iconOff;
        private readonly EventWaitHandle _showSignal;
        private readonly SynchronizationContext _ui;

        private bool _enabled;
        private long _total;
        private DateTime _lastSave = DateTime.UtcNow;
        private string _updateUrl;

        public TrayApp()
        {
            _ui = SynchronizationContext.Current;
            if (_ui == null)
            {
                _ui = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(_ui);
            }

            _enabled = Settings.Enabled;
            _total   = Settings.TotalBlocked;
            _iconOn  = LoadIcon("on.ico");
            _iconOff = LoadIcon("off.ico");

            _miEnabled = new ToolStripMenuItem("광고 차단 사용", null, OnToggleEnabled) { Checked = _enabled };
            var miAutostart = new ToolStripMenuItem("Windows 시작 시 자동 실행", null, OnToggleAutostart) { Checked = Settings.Autostart };

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("TalkShield v" + Version) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miEnabled);
            menu.Items.Add(miAutostart);
            menu.Items.Add(new ToolStripMenuItem("트레이 아이콘 숨기기", null, OnHideIcon));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("정보", null, OnInfo));
            menu.Items.Add(new ToolStripMenuItem("종료", null, OnExit));

            _tray = new NotifyIcon
            {
                Icon = _enabled ? _iconOn : _iconOff,
                Visible = !Settings.IconHidden, // 저장된 숨김 상태를 반영
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += OnToggleEnabled;
            _tray.BalloonTipClicked += OnBalloonClicked;
            UpdateTooltip();

            // 숨긴 트레이 아이콘은 TalkShield.exe 를 한 번 더 실행하면 복귀한다.
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
            var wake = new Thread(WaitShowSignal) { IsBackground = true };
            wake.Start();

            _timer = new System.Windows.Forms.Timer { Interval = 100 };
            _timer.Tick += OnTick;
            _timer.Start();

            // 시작 알림 없이 조용히 동작. 새 버전이 있을 때만 알림이 뜬다.
            if (Settings.AutoUpdate) CheckUpdateAsync();
        }

        // ----- 메인 루프 -----

        private void OnTick(object sender, EventArgs e)
        {
            if (_enabled)
            {
                try { _blocker.Tick(); } catch { } // 예기치 못한 창 상태에도 앱은 죽지 않는다
                if (_blocker.BlockedThisTick > 0)
                {
                    _total += _blocker.BlockedThisTick;
                    UpdateTooltip();
                    if ((DateTime.UtcNow - _lastSave).TotalSeconds > 10) SaveTotal();
                }
            }

            // CPU 절약: 카카오톡이 없으면 검사 주기를 늦춘다
            int want = _blocker.KakaoRunning ? 100 : 1500;
            if (_timer.Interval != want) _timer.Interval = want;
        }

        // ----- 메뉴/트레이 이벤트 -----

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _enabled = !_enabled;
            Settings.Enabled = _enabled;
            _miEnabled.Checked = _enabled;
            _tray.Icon = _enabled ? _iconOn : _iconOff;
            UpdateTooltip();
        }

        private void OnToggleAutostart(object sender, EventArgs e)
        {
            Settings.Autostart = !Settings.Autostart;
            ((ToolStripMenuItem)sender).Checked = Settings.Autostart;
        }

        private void OnHideIcon(object sender, EventArgs e)
        {
            Settings.IconHidden = true; // 재부팅 후에도 숨김 유지
            _tray.Visible = false;      // 차단은 계속 동작. 아이콘 복귀는 exe 재실행.
        }

        private void OnInfo(object sender, EventArgs e)
        {
            OpenUrl(BlogUrl);
        }

        private void OnBalloonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateUrl)) OpenUrl(_updateUrl);
        }

        private void OnExit(object sender, EventArgs e)
        {
            _timer.Stop();
            SaveTotal();
            _tray.Visible = false;
            _tray.Dispose();
            _showSignal.Dispose();
            _iconOn.Dispose();
            _iconOff.Dispose();
            ExitThread();
        }

        // ----- 보조 -----

        private void WaitShowSignal()
        {
            try
            {
                while (_showSignal.WaitOne())
                    _ui.Post(delegate { Settings.IconHidden = false; _tray.Visible = true; }, null);
            }
            catch { } // 종료 시 핸들 Dispose 로 빠져나온다
        }

        private void UpdateTooltip()
        {
            string t = string.Format("TalkShield ({0}) · 차단 {1:N0}건", _enabled ? "켜짐" : "꺼짐", _total);
            if (t.Length > 63) t = t.Substring(0, 63); // NotifyIcon.Text 길이 제한
            _tray.Text = t;
        }

        private void SaveTotal()
        {
            Settings.TotalBlocked = _total;
            _lastSave = DateTime.UtcNow;
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch { }
        }

        private static Icon LoadIcon(string res)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
            {
                if (s != null) return new Icon(s, SystemInformation.SmallIconSize);
            }
            return SystemIcons.Application;
        }

        // ----- 업데이트 확인 (GitHub Releases, 백그라운드) -----

        private void CheckUpdateAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                    string api = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest", GithubOwner, GithubRepo);
                    var req = (HttpWebRequest)WebRequest.Create(api);
                    req.UserAgent = "TalkShield";
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = 8000;

                    string json;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                        json = sr.ReadToEnd();

                    string tag  = Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    string page = Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+)\"");
                    if (tag == null || CompareVersion(tag, Version) <= 0) return;

                    _ui.Post(delegate
                    {
                        _updateUrl = page;
                        _tray.ShowBalloonTip(5000, "TalkShield 새 버전 " + tag,
                            "클릭하면 다운로드 페이지로 이동합니다.", ToolTipIcon.Info);
                    }, null);
                }
                catch { } // 네트워크 실패는 조용히 무시
            });
        }

        private static string Match(string s, string pattern)
        {
            var m = Regex.Match(s, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static int CompareVersion(string a, string b)
        {
            int[] pa = ParseVer(a), pb = ParseVer(b);
            for (int i = 0; i < 4; i++)
                if (pa[i] != pb[i]) return pa[i] > pb[i] ? 1 : -1;
            return 0;
        }

        private static int[] ParseVer(string v)
        {
            var nums = Regex.Matches(v, "\\d+");
            var r = new int[4];
            for (int i = 0; i < nums.Count && i < 4; i++) int.TryParse(nums[i].Value, out r[i]);
            return r;
        }
    }

    #endregion

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (new Mutex(true, "TalkShield_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // 이미 실행 중 → (숨겨둔) 트레이 아이콘만 다시 보이게 하고 조용히 종료
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(TrayApp.ShowSignalName))
                            ev.Set();
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
