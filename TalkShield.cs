// ============================================================================
//  TalkShield (톡실드) — PC 카카오톡 광고 제거
//  개발: 김영삼 (영삼넷 https://youngsam.net · info@youngsam.net)
//  라이선스: MIT © 2026 김영삼
// ----------------------------------------------------------------------------
//  동작 원리
//    100ms 주기로 카카오톡 창을 검사한다.
//    [기존 카카오톡 — KakaoTalk.exe]
//      1) 메인 창의 광고 컨테이너(빈 제목의 자식 창)를 WM_CLOSE 로 닫고
//      2) 친구목록/잠금화면 영역을 리사이즈해 배너 자리를 메우고
//      3) 별도 광고 팝업(웹뷰 광고)을 숨긴다.
//    [카카오톡 베타 — KakaoTalkUI.exe, Qt Quick]
//      1) 하단 광고 창(WebView2)을 빈 창 영역으로 만들어 보이지 않게 하고
//      2) 메인 창의 보이는 영역을 광고 자리 위까지로 잘라 빈자리를 없앤다.
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
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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
        [DllImport("user32.dll")]                            public static extern bool   GetCursorInfo(ref CURSORINFO ci);
        [DllImport("user32.dll")]                            public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")]                            public static extern int    SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
        [DllImport("user32.dll")]                            public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")]                            public static extern int    GetWindowRgnBox(IntPtr h, out RECT r);
        [DllImport("gdi32.dll")]                             public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")]                             public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("gdi32.dll")]                             public static extern bool   DeleteObject(IntPtr obj);
        [DllImport("dwmapi.dll")]                            public static extern int    DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
        [DllImport("user32.dll")]                            public static extern uint   GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")]                            public static extern int    GetSystemMetrics(int index);
        [DllImport("user32.dll")]                            public static extern int    GetSystemMetricsForDpi(int index, uint dpi);
        [DllImport("user32.dll")]                            public static extern bool   IsZoomed(IntPtr h);
        [DllImport("user32.dll")]                            public static extern int    GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")]                            public static extern bool   IsIconic(IntPtr h);
        [DllImport("user32.dll")]                            public static extern IntPtr SetCapture(IntPtr h);
        [DllImport("user32.dll")]                            public static extern bool   ReleaseCapture();
        [DllImport("user32.dll")]                            public static extern bool   SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")]                            public static extern bool   GetCursorPos(out POINT p);
        [DllImport("user32.dll")]                            public static extern IntPtr SetCursor(IntPtr cursor);
        [DllImport("user32.dll")]                            public static extern IntPtr LoadCursor(IntPtr inst, IntPtr name);
        [DllImport("user32.dll")]                            public static extern bool   SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")]                            public static extern bool   GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
        [DllImport("user32.dll")]                            public static extern int    SetWindowLong(IntPtr h, int index, int value);
        [DllImport("gdi32.dll")]                             public static extern int    CombineRgn(IntPtr dest, IntPtr a, IntPtr b, int mode);
        [DllImport("user32.dll")]                            public static extern bool   PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")]                            public static extern bool   InvalidateRect(IntPtr h, IntPtr rect, bool erase);
        [DllImport("user32.dll")]                            public static extern IntPtr WindowFromPoint(POINT p);

        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc cb, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool   UnhookWinEvent(IntPtr hook);

        public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")]   public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr mod, uint tid);
        [DllImport("user32.dll")]   public static extern bool   UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]   public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")]   public static extern int    GetMessage(out MSG msg, IntPtr h, uint min, uint max);
        [DllImport("user32.dll")]   public static extern bool   PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")]   public static extern short  GetAsyncKeyState(int key);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint   GetCurrentThreadId();
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] public static extern bool   SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        public const int  WH_MOUSE_LL    = 14;
        public const int  WM_LBUTTONDOWN = 0x0201;
        public const uint WM_QUIT        = 0x0012;
        public const int  VK_LBUTTON     = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT pt; }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT { public int length, flags, showCmd; public POINT ptMinPosition, ptMaxPosition; public RECT rcNormalPosition; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern bool   GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool LockWindowUpdate(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);

        public const int  GWL_STYLE        = -16;
        public const int  WS_DLGFRAME      = 0x00400000;
        public const uint SWP_FRAMECHANGED = 0x0020;

        public const uint WM_CLOSE   = 0x0010;
        public const uint SWP_NOMOVE = 0x0002;
        public const int  SW_HIDE    = 0;
        public const uint GW_OWNER   = 4;
        public const int  NULLREGION = 1;
        public const int  GWL_EXSTYLE      = -20;
        public const int  WS_EX_NOACTIVATE = 0x08000000;
        public const int  SM_CXSIZEFRAME    = 32;
        public const int  SM_CXPADDEDBORDER = 92;
        public const uint SWP_NOSIZE       = 0x0001;
        public const uint SWP_NOZORDER     = 0x0004;
        public const uint SWP_NOACTIVATE   = 0x0010;
        public const uint SWP_SHOWWINDOW   = 0x0040;
        public const uint SWP_HIDEWINDOW   = 0x0080;
        public const uint WM_ENTERSIZEMOVE = 0x0231;
        public const uint WM_EXITSIZEMOVE  = 0x0232;
        public const int  RGN_OR           = 2;
        public const uint EVENT_SYSTEM_MOVESIZESTART  = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND    = 0x000B;
        public const uint EVENT_OBJECT_SHOW           = 0x8002;
        public const uint EVENT_OBJECT_HIDE           = 0x8003;
        public const uint GW_HWNDNEXT       = 2;
        public const uint GW_HWNDPREV       = 3;
        public const int  WS_EX_TOPMOST     = 0x00000008;
        public const uint PW_RENDERFULLCONTENT = 2;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const int  WS_EX_LAYERED     = 0x00080000;
        public const int  WS_EX_TRANSPARENT = 0x00000020;
        public const uint LWA_ALPHA         = 2;
        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const int  OBJID_WINDOW     = 0;

        public const int DWMWA_NCRENDERING_POLICY       = 2;  // 0: 기본, 1: 끔(그림자/테두리 없음)
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33; // 0: 기본, 1: 각진 모서리
        public const int DWMWA_BORDER_COLOR             = 34; // -1: 기본, -2: 테두리 없음

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        public static void SetRegion(IntPtr h, IntPtr rgn)
        {
            if (SetWindowRgn(h, rgn, true) == 0 && rgn != IntPtr.Zero) DeleteObject(rgn); // 성공 시 소유권은 시스템으로 넘어간다
        }

        private static readonly IntPtr[] SizeCursors =
        {
            LoadCursor(IntPtr.Zero, new IntPtr(32642)), // IDC_SIZENWSE
            LoadCursor(IntPtr.Zero, new IntPtr(32643)), // IDC_SIZENESW
            LoadCursor(IntPtr.Zero, new IntPtr(32644)), // IDC_SIZEWE
            LoadCursor(IntPtr.Zero, new IntPtr(32645)), // IDC_SIZENS
        };

        // 지금 화면의 커서가 크기 조절 커서(↔ ↕ ⤡ ⤢)인지
        public static bool IsSizeCursor()
        {
            var ci = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
            if (!GetCursorInfo(ref ci)) return false;
            return Array.IndexOf(SizeCursors, ci.hCursor) >= 0;
        }

        public static void SetDwm(IntPtr h, int attr, int value)
        {
            try { DwmSetWindowAttribute(h, attr, ref value, 4); } catch { } // Windows 10 일부 속성 미지원
        }

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

        // ----- 문자열을 만들지 않는 비교 (매 틱 호출되므로 GC 힙 할당 없이 고정 버퍼로 비교한다) -----

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassNameChars(IntPtr h, [Out] char[] buf, int max);
        [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetWindowTextChars(IntPtr h, [Out] char[] buf, int max);

        private static readonly char[] Chars = new char[512];

        private static bool CharsStartWith(int len, string s)
        {
            if (len < s.Length) return false;
            for (int i = 0; i < s.Length; i++) if (Chars[i] != s[i]) return false;
            return true;
        }

        public static bool ClassIs(IntPtr h, string name)
        {
            int len = GetClassNameChars(h, Chars, Chars.Length);
            return len == name.Length && CharsStartWith(len, name);
        }

        public static bool ClassStartsWith(IntPtr h, string prefix)
        {
            return CharsStartWith(GetClassNameChars(h, Chars, Chars.Length), prefix);
        }

        // 클래스 이름이 prefix 로 시작하고 그 뒤 어딘가에 part 를 포함하는지 (예: "Qt6103QWindowIcon")
        public static bool ClassStartsWithAndContains(IntPtr h, string prefix, string part)
        {
            int len = GetClassNameChars(h, Chars, Chars.Length);
            if (!CharsStartWith(len, prefix)) return false;
            for (int i = prefix.Length; i + part.Length <= len; i++)
            {
                int j = 0;
                while (j < part.Length && Chars[i + j] == part[j]) j++;
                if (j == part.Length) return true;
            }
            return false;
        }

        public static bool HasText(IntPtr h)
        {
            return GetWindowTextLength(h) > 0;
        }

        public static bool TextStartsWith(IntPtr h, string prefix)
        {
            if (GetWindowTextLength(h) < prefix.Length) return false;
            return CharsStartWith(GetWindowTextChars(h, Chars, Chars.Length), prefix);
        }

        public static bool TextIs(IntPtr h, string text)
        {
            if (GetWindowTextLength(h) != text.Length) return false;
            int len = GetWindowTextChars(h, Chars, Chars.Length);
            return len == text.Length && CharsStartWith(len, text);
        }

        public static List<IntPtr> Descendants(IntPtr parent)
        {
            var list = new List<IntPtr>();
            EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }
    }

    // 카카오톡 프로세스 찾기. Process.GetProcessesByName 은 모든 프로세스의 스레드 정보까지 만들어 호출당 수 ms 가 들므로,
    // 최상위 창의 소유 프로세스만 모아 처음 보는 PID 의 실행 파일 이름만 한 번 확인한다. (창이 없는 카카오톡은 처리할 것도 없다)
    // UI 스레드 전용.
    internal static class ProcessFinder
    {
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder buf, ref int size);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int  ScanReuseMs = 50; // 두 엔진이 같은 틱에 부르면 한 번만 검사

        private static readonly Dictionary<uint, string> Names = new Dictionary<uint, string>(); // PID → 실행 파일 이름
        private static readonly HashSet<uint> Seen = new HashSet<uint>();
        private static readonly List<uint> Dead = new List<uint>();
        private static readonly StringBuilder PathBuf = new StringBuilder(1024);
        private static readonly Native.EnumWindowsProc Collect = delegate(IntPtr h, IntPtr l)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            Seen.Add(pid);
            return true;
        };
        private static int _scannedAt;
        private static bool _scanned;

        // exeName(예: "KakaoTalk.exe") 프로세스의 PID 를 result 에 채운다.
        public static void Find(string exeName, HashSet<uint> result)
        {
            Scan();
            result.Clear();
            foreach (var kv in Names)
                if (string.Equals(kv.Value, exeName, StringComparison.OrdinalIgnoreCase)) result.Add(kv.Key);
        }

        private static void Scan()
        {
            int now = Environment.TickCount;
            if (_scanned && now - _scannedAt < ScanReuseMs) return;
            _scanned = true;
            _scannedAt = now;

            Seen.Clear();
            Native.EnumWindows(Collect, IntPtr.Zero);

            // 창이 모두 사라진(종료된) PID 는 잊는다. PID 가 재사용되어도 새로 확인하게 된다.
            Dead.Clear();
            foreach (var pid in Names.Keys) if (!Seen.Contains(pid)) Dead.Add(pid);
            foreach (var pid in Dead) Names.Remove(pid);

            foreach (var pid in Seen)
                if (!Names.ContainsKey(pid)) Names[pid] = ImageName(pid);
        }

        private static string ImageName(uint pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return string.Empty;
            try
            {
                PathBuf.Length = 0;
                int size = PathBuf.Capacity;
                if (!QueryFullProcessImageName(h, 0, PathBuf, ref size)) return string.Empty;
                string path = PathBuf.ToString();
                int slash = path.LastIndexOf('\\');
                return slash >= 0 ? path.Substring(slash + 1) : path;
            }
            finally
            {
                CloseHandle(h);
            }
        }
    }

    #endregion

    #region 광고 제거 엔진

    internal sealed class AdBlocker
    {
        private const int LayoutShadowPadding = 2;    // 좌우 그림자 여백(px)
        private const int MainViewPadding     = 31;   // 하단 배너 높이(px)
        private const int PidScanIntervalMs   = 1000; // 프로세스 스캔 주기
        private const int LegacyBorder        = 6;    // 카카오톡 자체 크기 조절 테두리 두께(px, 실측)
        private const int LegacyCorner        = 12;   // 두 방향을 함께 조절하는 모서리 범위

        // 틱마다 재사용하는 버퍼들 (틱당 힙 할당 최소화)
        private readonly List<IntPtr>    _topWindows = new List<IntPtr>(16);
        private readonly List<IntPtr>    _mains      = new List<IntPtr>(4);
        private readonly HashSet<IntPtr> _mainSet    = new HashSet<IntPtr>();
        private readonly List<IntPtr>    _candidates = new List<IntPtr>(8);
        private readonly List<IntPtr>    _children   = new List<IntPtr>(64);
        private readonly List<IntPtr>    _descendants = new List<IntPtr>(32);
        private readonly HashSet<uint>   _pids       = new HashSet<uint>();

        // EnumWindows 콜백 델리게이트 캐시 (틱당 델리게이트 할당 방지)
        private readonly Native.EnumWindowsProc _collectTop;
        private readonly Native.EnumWindowsProc _collectChild;
        private readonly Native.EnumWindowsProc _collectDescendant;

        // 검사 결과 캐시
        private readonly Dictionary<IntPtr, bool> _customScrollCache = new Dictionary<IntPtr, bool>();
        private readonly HashSet<IntPtr> _countedClose = new HashSet<IntPtr>();

        private int _lastPidScan = -PidScanIntervalMs;

        // 창 크기 변경 즉시 반영 (주기 검사만으로는 크기를 바꾸는 동안 다음 검사까지 배너 자리가 잠깐 보인다)
        private readonly Native.WinEventProc _onLocationChange; // GC 되지 않도록 필드로 보관
        private readonly List<IntPtr>  _hooks     = new List<IntPtr>();
        private readonly HashSet<uint> _foundPids = new HashSet<uint>();
        private bool _inEvent;
        private int  _carryBlocked; // 이벤트 중에 닫은 광고 수 (다음 틱에 합산)
        private readonly Dictionary<IntPtr, EdgeGrip> _edgeGrips = new Dictionary<IntPtr, EdgeGrip>(); // 메인 창 → 테두리 손잡이
        private readonly List<IntPtr> _gripScratch = new List<IntPtr>(4);

        public int  BlockedThisTick { get; private set; }
        public bool KakaoRunning    { get; private set; }

        public AdBlocker()
        {
            _onLocationChange = OnLocationChange;
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
            _collectDescendant = delegate(IntPtr h, IntPtr l)
            {
                _descendants.Add(h);
                return true;
            };
        }

        public void Tick()
        {
            BlockedThisTick = _carryBlocked;
            _carryBlocked = 0;

            RefreshPids();
            if (!KakaoRunning)
            {
                if (_customScrollCache.Count > 0) _customScrollCache.Clear();
                if (_countedClose.Count > 0) _countedClose.Clear();
                if (_edgeGrips.Count > 0) DestroyEdgeGrips();
                return;
            }

            CollectWindows();
            foreach (var main in _mains) ProcessMainWindow(main);
            HideAdPopups();
            PruneCaches();
            PruneEdgeGrips();
        }

        // 프로세스 스캔은 비싸므로 1초에 한 번만 수행
        private void RefreshPids()
        {
            int now = Environment.TickCount;
            if (now - _lastPidScan < PidScanIntervalMs) return;
            _lastPidScan = now;

            ProcessFinder.Find("KakaoTalk.exe", _foundPids);
            if (!_foundPids.SetEquals(_pids))
            {
                _pids.Clear();
                _pids.UnionWith(_foundPids);
                Rehook();
            }
            KakaoRunning = _pids.Count > 0;
        }

        // ----- 테두리 손잡이 (크기 조절을 대신 수행) -----

        private void UpdateEdgeGrip(IntPtr main, Native.RECT rect)
        {
            EdgeGrip grip;
            _edgeGrips.TryGetValue(main, out grip);
            if (!Native.IsWindowVisible(main) || Native.IsIconic(main) || Native.IsZoomed(main))
            {
                if (grip != null) grip.Hide();
                return;
            }
            if (grip == null)
            {
                grip = new EdgeGrip(main, EdgeGrip.EdgeAll, ResizeFromGrip, null);
                _edgeGrips[main] = grip;
            }
            var area = new Native.RECT { Left = 0, Top = 0, Right = rect.Right - rect.Left, Bottom = rect.Bottom - rect.Top };
            grip.Update(rect, area, LegacyBorder, LegacyCorner);
        }

        // 창 크기를 바꾸고 곧바로 콘텐츠 뷰를 늘린다. 카카오톡은 크기가 바뀌는 처리 안에서 배너 자리를 비운 채
        // 바로 그려 버리므로, 그동안 창 그리기를 잠가(LockWindowUpdate) 두고 뷰까지 맞춘 뒤 풀어 완성된 모습만 그리게 한다.
        // (WM_SETREDRAW 는 오히려 더 깜빡이고, 창을 크게 잡고 가리는 방법은 카카오톡이 크기 변경마다 자체 창 영역을
        //  다시 설정해 쓸 수 없다 — 실측)
        private void ResizeFromGrip(IntPtr main, Native.RECT r)
        {
            bool wasInEvent = _inEvent;
            _inEvent = true;
            int saved = BlockedThisTick;
            BlockedThisTick = 0;
            bool locked = Native.LockWindowUpdate(main); // 다른 프로그램이 잠가 두었으면 실패하고 예전처럼 동작한다
            try
            {
                Native.SetWindowPos(main, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                ProcessMainWindow(main);
            }
            catch { }
            finally
            {
                if (locked) Native.LockWindowUpdate(IntPtr.Zero);
                _carryBlocked += BlockedThisTick;
                BlockedThisTick = saved;
                _inEvent = wasInEvent;
            }
        }

        private void PruneEdgeGrips()
        {
            if (_edgeGrips.Count == 0) return;
            _gripScratch.Clear();
            foreach (var kv in _edgeGrips)
                if (!_mainSet.Contains(kv.Key) && !kv.Value.Dragging) _gripScratch.Add(kv.Key);
            foreach (var h in _gripScratch)
            {
                _edgeGrips[h].DestroyHandle();
                _edgeGrips.Remove(h);
            }
        }

        private void DestroyEdgeGrips()
        {
            foreach (var g in _edgeGrips.Values) g.DestroyHandle();
            _edgeGrips.Clear();
        }

        // 차단을 끄거나 종료할 때: 이벤트 구독을 멈추고 손잡이를 없앤다. 다시 켜면 다음 틱에 새로 만든다.
        public void Stop()
        {
            DestroyEdgeGrips();
            Unhook();
            _pids.Clear();
            _lastPidScan = Environment.TickCount - PidScanIntervalMs;
        }

        private void Rehook()
        {
            Unhook();
            foreach (uint pid in _pids)
            {
                IntPtr hook = Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE,
                                                     IntPtr.Zero, _onLocationChange, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
                if (hook != IntPtr.Zero) _hooks.Add(hook);
            }
        }

        private void Unhook()
        {
            foreach (var hook in _hooks) Native.UnhookWinEvent(hook);
            _hooks.Clear();
        }

        // 메인 창이나 그 콘텐츠 뷰의 위치·크기가 바뀌면 다음 틱을 기다리지 않고 바로 배너 자리를 메운다.
        private void OnLocationChange(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
        {
            if (_inEvent || idObject != Native.OBJID_WINDOW) return;
            IntPtr main = h;
            if (!_mainSet.Contains(main))
            {
                main = Native.GetParent(h);
                if (main == IntPtr.Zero || !_mainSet.Contains(main)) return;
            }
            _inEvent = true;
            int saved = BlockedThisTick;
            BlockedThisTick = 0;
            try { ProcessMainWindow(main); }
            catch { }
            finally
            {
                _carryBlocked += BlockedThisTick;
                BlockedThisTick = saved;
                _inEvent = false;
            }
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
                if (Native.ClassIs(h, "EVA_Window_Dblclk") &&
                    Native.GetParent(h) == IntPtr.Zero &&
                    Native.HasText(h))
                {
                    _mains.Add(h);
                    _mainSet.Add(h);
                }
            }
            foreach (var h in _topWindows)
            {
                if (Native.HasText(h)) continue;
                IntPtr parent = Native.GetParent(h);

                bool isAdPopup =
                    (parent != IntPtr.Zero && _mainSet.Contains(parent) && Native.ClassIs(h, "EVA_Window_Dblclk")) ||
                    (parent == IntPtr.Zero && Native.ClassIs(h, "EVA_Window"));
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
            UpdateEdgeGrip(main, rect);

            for (int i = 0; i < _children.Count; i++)
            {
                IntPtr child = _children[i];
                if (Native.GetParent(child) != main) continue; // 직속 자식만

                if (!Native.HasText(child))
                {
                    if (Native.ClassIs(child, "EVA_ChildWindow") && !HasCustomScroll(child))
                    {
                        Native.SendMessage(child, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        if (_countedClose.Add(child)) BlockedThisTick++;
                    }
                    continue; // 제목 없는 창은 콘텐츠 뷰가 아니다
                }

                ResizeContentView(rect, child);
            }
        }

        // 친구목록(OnlineMainView)은 배너 높이만큼, 잠금화면(LockModeView)은 전체로 늘려
        // 광고가 있던 자리를 콘텐츠로 메운다.
        private static void ResizeContentView(Native.RECT r, IntPtr h)
        {
            int w = r.Right - r.Left - LayoutShadowPadding;
            int height;

            if (Native.TextStartsWith(h, "OnlineMainView"))
                height = r.Bottom - r.Top - MainViewPadding;
            else if (Native.TextStartsWith(h, "LockModeView"))
                height = r.Bottom - r.Top;
            else
                return;

            if (height < 1) return;

            // 이미 원하는 크기면 건드리지 않는다 (매 틱 카카오톡에 창 메시지를 보내지 않도록)
            Native.RECT cur;
            if (Native.GetWindowRect(h, out cur) && cur.Right - cur.Left == w && cur.Bottom - cur.Top == height) return;

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
                if (!Native.ClassIs(h, "EVA_ChildWindow")) continue;
                if (Native.TextStartsWith(h, "OnlineMainView") ||
                    Native.TextStartsWith(h, "LockModeView")) return true;
            }
            return false;
        }

        // 실제 콘텐츠 창(커스텀 스크롤 _EVA_* 보유)은 광고가 아니므로 닫으면 안 된다. 결과 캐시.
        private bool HasCustomScroll(IntPtr h)
        {
            bool cached;
            if (_customScrollCache.TryGetValue(h, out cached) && Native.IsWindow(h)) return cached;

            bool result = Native.ClassStartsWith(h, "_EVA_");
            if (!result)
            {
                CollectDescendants(h);
                foreach (var d in _descendants)
                    if (Native.ClassStartsWith(d, "_EVA_")) { result = true; break; }
            }
            _customScrollCache[h] = result;
            return result;
        }

        // 광고가 아닌 팝업은 매 틱 다시 검사되므로 목록·델리게이트를 재사용한다.
        private bool HasChromeLegacyWindow(IntPtr h)
        {
            if (Native.TextIs(h, "Chrome Legacy Window")) return true;
            CollectDescendants(h);
            foreach (var d in _descendants)
                if (Native.TextIs(d, "Chrome Legacy Window")) return true;
            return false;
        }

        private void CollectDescendants(IntPtr h)
        {
            _descendants.Clear();
            Native.EnumChildWindows(h, _collectDescendant, IntPtr.Zero);
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

    #region 광고 제거 엔진 — 카카오톡 베타 (KakaoTalkUI.exe, Qt Quick)

    // 베타는 UI 전체를 Qt Quick 이 창 하나에 그리므로 자식 창 리사이즈가 불가능하다.
    // 대신 광고(WebView2)는 메인 창이 소유한 별도 팝업 창으로 하단 빈자리 위에 얹혀 있다.
    //   1) 광고 창: 완전 투명(레이어드 알파 0) + 클릭 통과로 만들어 보이지도, 클릭되지도 않게 한다.
    //      숨기거나 닫으면 카카오톡이 광고 상태를 잃고 빈 껍데기 창이 남으므로 절대 닫지 않는다.
    //      창 영역(region)으로 가리면 카카오톡이 크기 변경 때마다 자체 마스크로 덮어쓰므로 쓰지 않는다.
    //   2) 메인 창: 광고가 표시 중인 동안 창 영역을 '광고 자리 위'까지로 잘라 빈자리를 없앤다.
    //      DWM 그림자/테두리는 원래 창 크기로 그려지므로 끄고, 둥근 모서리는 영역으로 대신 만든다.
    //      잘린 좌·우·하단에는 크기 조절 손잡이(ResizeGrip)를 깔아 테두리 드래그를 되살린다.
    //   3) 크기가 바뀔 때마다 Windows 테마 엔진이 메인 창 영역을 초기화하므로, 크기 조절 중과 최대화 중에는
    //      빈자리 위에 카카오톡 배경색 덮개(BottomCover)를 올려 흰 빈자리가 한 프레임도 비치지 않게 한다.
    //   광고가 사라지면(탭 전환·최소화 등) 메인 창은 원래 상태로 되돌린다.
    internal sealed class BetaAdBlocker
    {
        private const string ProcessName       = "KakaoTalkUI.exe";
        private const int    PidScanIntervalMs = 1000;
        private const int    PassIntervalMs    = 250; // 정기 점검 주기 (변화는 이벤트로 즉시 처리)
        private const int    MinAdHeight       = 40;  // 하단 배너로 인정할 높이 범위(px, 96dpi 기준)
        private const int    MaxAdHeight       = 200;
        private const int    CornerDiameter    = 16;  // Windows 11 둥근 모서리(반경 8px)
        private const int    UnclipGraceMs     = 400; // 광고가 사라진 뒤 원복까지 기다리는 시간
        private const int    CoverGraceMs      = 350; // 크기 변경이 멈춘 뒤 덮개를 유지하는 시간
        private const int    AppTopResizeZone  = 7;   // 카카오톡 자체 상단 크기 조절 범위(px, 96dpi 기준, 실측: 0~6px 크기 조절, 7px~ 이동)
        private const int    CoverOverlap      = 3;   // 크기 조절 중 덮개를 위로 겹치는 높이(px, 96dpi 기준)
        private const int    CutHistoryMs      = 250; // 덮개 윗변을 정할 때 돌아볼 시간
        private const int    PendingAdMs       = 3000; // 잠정 감춘 배너가 광고로 확인되기를 기다리는 시간
        private const int    RegionLagMs       = 120; // 덮개 없이 크기 조절할 때 자를 위치를 정하며 돌아볼 시간 (카카오톡이 늦게 그리는 만큼)
        private const int    ColorRefreshMs    = 5000;  // 덮개 색을 다시 뽑는 주기 (테마 변경 대응)
        private const int    SidebarWidth      = 68;   // 색을 뽑기 전 기본 사이드바 폭(px, 96dpi 기준)

        private readonly HashSet<uint>   _pids       = new HashSet<uint>();
        private readonly HashSet<uint>   _foundPids  = new HashSet<uint>();
        private readonly List<IntPtr>    _scratch    = new List<IntPtr>(8); // 정리할 핸들 임시 목록 (매 점검 재사용)
        private readonly List<IntPtr>    _topWindows = new List<IntPtr>(32);
        private readonly List<IntPtr>    _children   = new List<IntPtr>(16);

        private readonly Dictionary<IntPtr, int> _adWindows = new Dictionary<IntPtr, int>(); // 투명 처리한 광고 창 → 원래 확장 스타일
        private readonly HashSet<IntPtr> _adOwners   = new HashSet<IntPtr>(); // 광고가 확인된 메인 창
        private readonly Dictionary<IntPtr, int> _pendingAds = new Dictionary<IntPtr, int>(); // 잠정 감춘 배너 → 감춘 시각
        private readonly HashSet<IntPtr> _notAds     = new HashSet<IntPtr>(); // 확인 결과 광고가 아니었던 창
        private readonly Dictionary<IntPtr, int>         _clipped = new Dictionary<IntPtr, int>();         // 메인 창 → 잘라낸 높이
        private readonly Dictionary<IntPtr, int>         _wanted  = new Dictionary<IntPtr, int>();         // 이번 틱: 메인 창 → 광고 높이
        private readonly Dictionary<IntPtr, int>         _lastWanted = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막으로 광고가 보인 시각
        private readonly Dictionary<IntPtr, int>         _adHeight   = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막 광고 높이
        private readonly Dictionary<IntPtr, IntPtr>      _adOf       = new Dictionary<IntPtr, IntPtr>();   // 메인 창 → 광고 창
        private readonly Dictionary<IntPtr, ResizeGrip>  _grips      = new Dictionary<IntPtr, ResizeGrip>();
        private readonly Dictionary<IntPtr, EdgeGrip>    _topGrips   = new Dictionary<IntPtr, EdgeGrip>();     // 위쪽 테두리 손잡이
        private readonly Dictionary<IntPtr, BottomCover> _covers     = new Dictionary<IntPtr, BottomCover>();
        private readonly Dictionary<IntPtr, long>        _lastSize   = new Dictionary<IntPtr, long>();     // 메인 창 → 마지막 크기(w,h)
        private readonly Dictionary<IntPtr, int>         _sizedAt    = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막 크기 변경 시각
        private readonly HashSet<IntPtr>                 _resizing   = new HashSet<IntPtr>();              // 크기 조절 중인 메인 창
        private readonly Dictionary<IntPtr, int>         _frameShift = new Dictionary<IntPtr, int>();      // WS_DLGFRAME 을 뺀 메인 창 → 내용이 내려간 px
        private readonly Dictionary<IntPtr, int>         _placementShift = new Dictionary<IntPtr, int>();  // 최대화·최소화 중 '보통 크기' 위치에 남은 보정 px
        private readonly Dictionary<IntPtr, int>         _clientTop  = new Dictionary<IntPtr, int>();      // 스타일을 뺀 메인 창 → 내용이 시작하는 창 기준 y
        private readonly Dictionary<IntPtr, List<CutSample>> _recentCuts = new Dictionary<IntPtr, List<CutSample>>(); // 메인 창 → 최근 자를 위치
        private readonly HashSet<IntPtr>                 _lagged     = new HashSet<IntPtr>();              // 최근 경계 때문에 덜 보이게 잘라 둔 메인 창
        private readonly HashSet<IntPtr>                 _appSizing  = new HashSet<IntPtr>();              // 카카오톡 자체 테두리로 크기 조절 중
        private readonly Dictionary<IntPtr, int>         _appSizedAt = new Dictionary<IntPtr, int>();      // 카카오톡 자체 크기 조절이 끝난 시각
        private readonly Dictionary<IntPtr, List<CutSample>> _cutHistory = new Dictionary<IntPtr, List<CutSample>>(); // 메인 창 → 최근 경계
        private readonly Dictionary<IntPtr, CoverColors> _colors     = new Dictionary<IntPtr, CoverColors>();  // 메인 창 → 덮개 색
        private readonly List<IntPtr>                    _hooks      = new List<IntPtr>();
        private readonly MouseDownWatcher                _mouse      = new MouseDownWatcher();             // 끌기 시작 위치(이동/크기 조절 구분)

        private struct CutSample   { public int Time, ScreenY, WindowY; }
        private struct CoverColors { public Color Left, Right; public int Split, Time; }

        private readonly Native.EnumWindowsProc _collectTop;
        private readonly Native.EnumWindowsProc _collectChild;
        private readonly Native.WinEventProc    _onLocationChange; // GC 되지 않도록 필드로 보관
        private readonly Native.WinEventProc    _onShow;
        private readonly Native.WinEventProc    _onMoveSize;

        private int  _lastPidScan = -PidScanIntervalMs;
        private int  _lastPass    = -PassIntervalMs;
        private bool _dpiApiMissing;
        private bool _busy;

        public int  BlockedThisTick { get; private set; }
        public bool KakaoRunning    { get; private set; }

        public BetaAdBlocker()
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
            _onLocationChange = OnLocationChange;
            _onShow = OnShow;
            _onMoveSize = OnMoveSize;
        }

        // 카카오톡 자체 테두리(상단 등)로 크기 조절을 시작하는 순간, 첫 크기 변경 전에 덮개를 올린다.
        private void OnMoveSize(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != Native.OBJID_WINDOW || !_clipped.ContainsKey(h)) return;
            if (ev == Native.EVENT_SYSTEM_MOVESIZEEND)
            {
                if (_appSizing.Remove(h)) _appSizedAt[h] = Environment.TickCount;
                SetResizing(h, false);
                return;
            }
            // 이동인지 크기 조절인지는 마우스를 누른 위치로 구분한다. 카카오톡은 위 7px(96dpi) 안에서 누르면 크기 조절,
            // 그 아래는 이동으로 처리한다. (좌·우·하단 테두리는 잘려 있어 손잡이 창이 직접 처리한다)
            // 시작 이벤트 시점의 커서는 이미 움직인 뒤라 위치도 모양도 믿을 수 없다. 이동은 창 영역을 초기화하지 않으므로 덮개가 필요 없다.
            Native.POINT press;
            bool sizing;
            if (_mouse.TryGetPress(out press))
            {
                IntPtr oldCtx = EnterDpiAware();
                try
                {
                    Native.RECT win;
                    if (!Native.GetWindowRect(h, out win)) return;
                    int zone = (int)Math.Round(AppTopResizeZone * Scale(h));
                    int top  = win.Top + VisibleTop(h);
                    sizing = press.Y >= top && press.Y - top < zone && press.X >= win.Left && press.X < win.Right;
                }
                finally
                {
                    LeaveDpiAware(oldCtx);
                }
            }
            else
            {
                sizing = Native.IsSizeCursor(); // 훅을 쓸 수 없을 때의 대안
            }
            if (sizing)
            {
                // 카카오톡이 직접 크기를 바꾸면 미리 잘라 둘 수 없어 경계가 한 박자 늦는다 → 이때는 테두리 스타일과 상관없이 덮개를 쓴다.
                _appSizing.Add(h);
                SetResizing(h, true);
            }
        }

        private void SetResizing(IntPtr main, bool on)
        {
            if (on) _resizing.Add(main);
            else
            {
                // 크기 조절이 끝난 뒤에만 잠시 덮개를 유지해 마지막 초기화까지 가린다. 단순 이동은 해당 없음.
                if (!_resizing.Remove(main)) return;
                _sizedAt[main] = Environment.TickCount;
            }
            int height;
            if (_busy || !_adHeight.TryGetValue(main, out height)) return;
            _busy = true;
            IntPtr oldCtx = EnterDpiAware();
            try { ClipMain(main, height); }
            catch { }
            finally { LeaveDpiAware(oldCtx); _busy = false; }
        }

        // 카카오톡이 광고 창을 새로 띄우거나 숨기면(탭 전환 등) 정기 점검을 기다리지 않고 바로 처리한다.
        private void OnShow(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
        {
            if (_busy || idObject != Native.OBJID_WINDOW || Native.GetWindow(h, Native.GW_OWNER) == IntPtr.Zero) return;
            try { RunPass(); } catch { }
        }

        // 크기 조절 중에도 잘라낸 영역이 즉시 따라가도록 메인 창의 위치/크기 변경을 바로 반영한다.
        private void OnLocationChange(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
        {
            if (_busy || idObject != Native.OBJID_WINDOW) return;
            if (_adWindows.ContainsKey(h)) h = Native.GetWindow(h, Native.GW_OWNER); // 광고 창이 움직였으면 = 카카오톡이 다시 배치함
            int height;
            if (!_clipped.ContainsKey(h) || !_adHeight.TryGetValue(h, out height)) return;

            _busy = true;
            IntPtr oldCtx = EnterDpiAware();
            try { ClipMain(h, height); }
            catch { }
            finally { LeaveDpiAware(oldCtx); _busy = false; }
        }

        public void Tick()
        {
            BlockedThisTick = 0;

            RefreshPids();
            if (!KakaoRunning)
            {
                ForgetAll();
                return;
            }
            // 창 표시/숨김/이동/크기 조절은 이벤트로 즉시 처리하므로, 정기 점검은 조금 느슨하게 해 CPU 를 아낀다.
            int now = Environment.TickCount;
            if (now - _lastPass < PassIntervalMs)
            {
                if (!_busy) SettleRecentCuts();
                return;
            }
            RunPass();
        }

        private void RunPass()
        {
            // 카카오톡 베타는 DPI 인식(Per-Monitor V2) 앱이므로 같은 좌표계로 계산해야 한다.
            // 카카오톡 창에 대한 호출은 응답을 기다리는 동안 위치 변경 이벤트가 끼어들 수 있어 재진입을 막는다.
            _busy = true;
            _lastPass = Environment.TickCount;
            IntPtr oldCtx = EnterDpiAware();
            try
            {
                _topWindows.Clear();
                Native.EnumWindows(_collectTop, IntPtr.Zero);

                _wanted.Clear();
                foreach (var h in _topWindows) ProcessCandidate(h);
                ApplyMainClips();
                Prune();
            }
            finally
            {
                LeaveDpiAware(oldCtx);
                _busy = false;
            }
        }

        // 차단을 끄거나 종료할 때 모든 창을 원래대로 되돌린다.
        public void Restore()
        {
            IntPtr oldCtx = EnterDpiAware();
            try
            {
                foreach (var kv in _adWindows) if (Native.IsWindow(kv.Key)) Native.SetWindowLong(kv.Key, Native.GWL_EXSTYLE, kv.Value);
                foreach (var h in new List<IntPtr>(_clipped.Keys)) if (Native.IsWindow(h)) UnclipMain(h);
            }
            finally
            {
                LeaveDpiAware(oldCtx);
            }
            Unhook();
            _pids.Clear(); // 다시 켜면 프로세스를 바로 다시 찾고 훅을 새로 건다
            _lastPidScan = Environment.TickCount - PidScanIntervalMs;
            ForgetAll();
        }

        private void RefreshPids()
        {
            int now = Environment.TickCount;
            if (now - _lastPidScan < PidScanIntervalMs) return;
            _lastPidScan = now;

            var found = _foundPids;
            ProcessFinder.Find(ProcessName, found);
            if (!found.SetEquals(_pids))
            {
                _pids.Clear();
                _pids.UnionWith(found);
                Rehook();
            }
            KakaoRunning = _pids.Count > 0;
        }

        private void Rehook()
        {
            Unhook();
            foreach (uint pid in _pids)
            {
                IntPtr hook = Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE,
                                                     IntPtr.Zero, _onLocationChange, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
                if (hook != IntPtr.Zero) _hooks.Add(hook);
                hook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_HIDE, // 표시 + 숨김
                                              IntPtr.Zero, _onShow, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
                if (hook != IntPtr.Zero) _hooks.Add(hook);
                hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_MOVESIZESTART, Native.EVENT_SYSTEM_MOVESIZEEND,
                                              IntPtr.Zero, _onMoveSize, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
                if (hook != IntPtr.Zero) _hooks.Add(hook);
            }
            if (_pids.Count > 0) _mouse.Start();
        }

        private void Unhook()
        {
            foreach (var hook in _hooks) Native.UnhookWinEvent(hook);
            _hooks.Clear();
            _mouse.Stop();
        }

        private void ProcessCandidate(IntPtr h)
        {
            IntPtr owner = Native.GetWindow(h, Native.GW_OWNER);
            if (owner == IntPtr.Zero || !IsKakaoWindow(owner)) return;
            if (!IsQtWindow(h)) return;

            int since;
            if (_pendingAds.TryGetValue(h, out since))
            {
                // 잠정 처리한 창: 광고 웹뷰가 확인되면 확정, 끝내 확인되지 않으면 원래대로 되돌린다.
                if (HasAdWebView(h))
                {
                    _pendingAds.Remove(h);
                    _adOwners.Add(owner);
                    BlockedThisTick++;
                }
                else if (Environment.TickCount - since > PendingAdMs)
                {
                    _pendingAds.Remove(h);
                    int ex;
                    if (_adWindows.TryGetValue(h, out ex)) Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex);
                    _adWindows.Remove(h);
                    _notAds.Add(h);
                    return;
                }
            }
            else if (!_adWindows.ContainsKey(h))
            {
                if (_notAds.Contains(h) || !LooksLikeBanner(h, owner)) return;
                // 처음 보는 메인 창이면 WebView2 의 광고 제목으로 확인. 이후 재생성된 광고 창은 모양만으로 판별.
                // 로그인 직후에는 광고 창이 먼저 뜨고 웹뷰가 조금 늦게 생기므로, 메인 창 맨 아래에 붙은 배너 모양이면
                // 일단 바로 감춰 두고(잠정) 확인을 기다린다. 확인되지 않으면 PendingAdMs 뒤 원래대로 되돌린다.
                bool confirmed = _adOwners.Contains(owner) || HasAdWebView(h);
                if (!confirmed && !AtOwnerBottom(h, owner)) return;

                // 원래 스타일 저장. 이전 실행이 비정상 종료돼 남긴 투명 비트는 빼고 저장한다.
                _adWindows[h] = Native.GetWindowLong(h, Native.GWL_EXSTYLE) & ~(Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT);
                if (confirmed)
                {
                    _adOwners.Add(owner);
                    BlockedThisTick++;
                }
                else
                {
                    _pendingAds[h] = Environment.TickCount;
                }
            }
            MakeInvisible(h); // 카카오톡이 스타일을 되돌렸다면 다시 적용된다

            if (!Native.IsWindowVisible(h) || !Native.IsWindowVisible(owner)) return;

            Native.RECT ad;
            if (!Native.GetWindowRect(h, out ad)) return;
            int height = ad.Bottom - ad.Top;
            int prev;
            if (!_wanted.TryGetValue(owner, out prev) || height > prev)
            {
                _wanted[owner] = height;
                _adOf[owner]   = h;
            }
        }

        private bool IsKakaoWindow(IntPtr h)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            return _pids.Contains(pid);
        }

        private static bool IsQtWindow(IntPtr h)
        {
            return Native.ClassStartsWithAndContains(h, "Qt", "QWindow");
        }

        // 하단 배너 모양: 활성화되지 않는 팝업, 폭이 메인 창의 보이는 폭과 같고 높이가 배너 범위
        private bool LooksLikeBanner(IntPtr h, IntPtr owner)
        {
            if ((Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_NOACTIVATE) == 0) return false;

            Native.RECT ad, win;
            if (!Native.GetWindowRect(h, out ad) || !Native.GetWindowRect(owner, out win)) return false;

            float scale = Scale(owner);
            int height   = ad.Bottom - ad.Top;
            int width    = ad.Right - ad.Left;
            int contentW = win.Right - win.Left - 2 * FrameThickness(owner);
            return height >= MinAdHeight * scale && height <= MaxAdHeight * scale && Math.Abs(width - contentW) <= 2;
        }

        // 메인 창의 맨 아래(보이지 않는 테두리 바로 위)에 붙어 있는지
        private bool AtOwnerBottom(IntPtr h, IntPtr owner)
        {
            Native.RECT ad, win;
            if (!Native.GetWindowRect(h, out ad) || !Native.GetWindowRect(owner, out win)) return false;
            int f = FrameThickness(owner);
            return Math.Abs(ad.Bottom - (win.Bottom - f)) <= 2 && Math.Abs(ad.Left - (win.Left + f)) <= 2;
        }

        private bool HasAdWebView(IntPtr h)
        {
            _children.Clear();
            Native.EnumChildWindows(h, _collectChild, IntPtr.Zero);
            for (int i = 0; i < _children.Count; i++)
            {
                IntPtr c = _children[i];
                if (Native.ClassName(c) == "Chrome_WidgetWin_1" && Native.WindowText(c).IndexOf("광고", StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        // 원하는 상태(_wanted)와 실제 상태를 맞춘다.
        private void ApplyMainClips()
        {
            int now = Environment.TickCount;
            if (_clipped.Count > 0)
            {
                var stale = _scratch;
                stale.Clear();
                foreach (var h in _clipped.Keys)
                {
                    if (_wanted.ContainsKey(h)) continue;
                    int seen;
                    // 광고 창이 교체되는 짧은 순간에는 원복하지 않아 깜빡임을 막는다.
                    if (Native.IsWindow(h) && Native.IsWindowVisible(h) &&
                        _lastWanted.TryGetValue(h, out seen) && now - seen < UnclipGraceMs) continue;
                    stale.Add(h);
                }
                foreach (var h in stale)
                {
                    if (Native.IsWindow(h)) UnclipMain(h);
                    else
                    {
                        DestroyGrip(h); DestroyCover(h);
                        _adHeight.Remove(h); _lastSize.Remove(h); _sizedAt.Remove(h); _resizing.Remove(h); _cutHistory.Remove(h);
                        _frameShift.Remove(h); _placementShift.Remove(h); _clientTop.Remove(h); _recentCuts.Remove(h); _lagged.Remove(h);
                        _appSizing.Remove(h); _appSizedAt.Remove(h);
                    }
                    _clipped.Remove(h);
                    _lastWanted.Remove(h);
                }
            }

            foreach (var kv in _wanted)
            {
                _lastWanted[kv.Key] = now;
                ClipMain(kv.Key, kv.Value);
            }
        }

        private void ClipMain(IntPtr main, int adHeight)
        {
            Native.RECT win;
            if (Native.GetWindowRect(main, out win)) ClipMain(main, win, adHeight);
        }

        // 손잡이로 크기를 조절할 때: 새 크기 기준으로 먼저 잘라 두고 창 크기를 바꿔야 흰 배너 자리가 비치지 않는다.
        private void ResizeFromGrip(IntPtr main, Native.RECT r)
        {
            int height;
            if (_busy || !_adHeight.TryGetValue(main, out height)) return;

            _busy = true;
            IntPtr oldCtx = EnterDpiAware();
            try
            {
                ClipMain(main, r, height);
                Native.SetWindowPos(main, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                ClipMain(main, height); // 카카오톡이 최소 크기 등으로 조정했을 수 있으므로 실제 크기로 다시 맞춘다
            }
            finally
            {
                LeaveDpiAware(oldCtx);
                _busy = false;
            }
        }

        private void ClipMain(IntPtr main, Native.RECT win, int adHeight)
        {
            // 창 사각형 = 보이는 내용 + 좌·우·하단의 보이지 않는 크기 조절 테두리. 상단은 내용이 끝까지 차 있다.
            // (DWM 프레임 경계값은 1px 테두리를 포함하고, DWM 을 끈 뒤에는 값 자체가 달라지므로 쓰지 않는다.)
            UpdateFrameStyle(main, ref win);

            int f      = FrameThickness(main);
            int w      = win.Right - win.Left;
            int left   = f;
            int top    = VisibleTop(main); // 테두리 스타일을 뺀 동안은 위쪽 테두리 띠만큼 아래부터 보인다
            int right  = w - f;
            int bottom = (win.Bottom - win.Top) - f - adHeight;

            // 크기가 바뀐 직후 카카오톡은 한두 프레임 늦게 다시 배치하므로, 그동안 흰 배너는 예전 자리에 그려져 있다.
            // 광고 창은 카카오톡이 실제로 배치한 위치를 따라가므로, 그 위치가 더 위라면 그곳에서 자른다.
            IntPtr adWnd;
            Native.RECT ad;
            if (_adOf.TryGetValue(main, out adWnd) && Native.GetWindowRect(adWnd, out ad) &&
                ad.Left >= win.Left && ad.Right <= win.Right && ad.Top > win.Top && ad.Bottom <= win.Bottom)
            {
                bottom = Math.Min(bottom, ad.Top - win.Top);
            }
            // 덮개 없이 크기를 바꾸는 동안(테두리 스타일을 뺀 상태)에는, 카카오톡이 늦게 그린 예전 화면의 흰 빈자리가
            // 새 경계 바로 위에 잠깐 남는다. 최근 경계 중 가장 위쪽에서 잘라 두고, 잠시 뒤 다시 맞춘다(Tick).
            if (_frameShift.ContainsKey(main)) bottom = Math.Min(bottom, RecentCut(main, bottom));
            if (bottom - top < adHeight || right - left < 100) return; // 비정상적인 값이면 건드리지 않는다

            _adHeight[main] = adHeight;
            UpdateGrip(main, win, f, top, bottom);
            UpdateCover(main, win, f, bottom);

            int key = (right << 16) ^ (bottom << 4) ^ top;
            int cur;
            bool same = _clipped.TryGetValue(main, out cur) && cur == key;
            Native.RECT box;
            if (same && Native.GetWindowRgnBox(main, out box) > Native.NULLREGION &&
                Math.Abs(box.Right - right) <= 1 && Math.Abs(box.Bottom - bottom) <= 1 && Math.Abs(box.Top - top) <= 1) return;

            if (!_clipped.ContainsKey(main))
            {
                Native.SetDwm(main, Native.DWMWA_NCRENDERING_POLICY, 1);
                Native.SetDwm(main, Native.DWMWA_BORDER_COLOR, -2);
                Native.SetDwm(main, Native.DWMWA_WINDOW_CORNER_PREFERENCE, 1);
            }

            IntPtr rgn;
            if (Native.IsZoomed(main))
            {
                rgn = Native.CreateRectRgn(left, top, right, bottom);
            }
            else
            {
                int d = (int)Math.Round(CornerDiameter * Scale(main));
                rgn = Native.CreateRoundRectRgn(left, top, right + 1, bottom + 1, d, d);
            }
            Native.SetRegion(main, rgn);
            _clipped[main] = key;
        }

        // ----- 창 테두리 스타일(WS_DLGFRAME) -----
        // 크기가 바뀔 때마다 Windows 테마 엔진이 창 영역을 원래대로 되돌리는 것은 WS_CAPTION(= WS_BORDER | WS_DLGFRAME)이
        // 있는 창에만 일어난다. 되돌려질 때마다 좌·우·아래 크기 조절 테두리가 하늘색 줄로, 광고 자리가 흰색으로 비치므로
        // 잘라 두는 동안에는 WS_DLGFRAME 을 뺀다.
        // 빼면 카카오톡 내용이 위쪽 테두리 두께만큼(100% 배율 7px) 내려가므로, 창을 그만큼 위로 올리고 늘려
        // 내용이 화면의 같은 자리·같은 크기에 있게 한다. (카카오톡은 테두리를 뺀 내용 영역 기준으로 위치를 저장하므로
        // 이 상태로 종료돼도 다음 실행 위치가 밀리지 않는다 — 실측 확인)
        // 최대화 중에는 스타일이 없으면 작업 표시줄까지 덮으므로 원래 스타일로 둔다.
        private void UpdateFrameStyle(IntPtr main, ref Native.RECT win)
        {
            if (Native.IsZoomed(main))
            {
                if (!_frameShift.ContainsKey(main)) return;
                // 스타일이 빠진 채 최대화되면 Windows 가 작업 표시줄까지 덮는 크기로 잡으므로, 되돌린 뒤 표준 최대화 크기로 맞춘다.
                RestoreFrame(main, false);
                FitMaximized(main);
                Native.GetWindowRect(main, out win);
                return;
            }
            if (Native.IsIconic(main) || _frameShift.ContainsKey(main)) return;

            // 최대화 전에 보정해 둔 '보통 크기' 위치로 돌아온 경우는 이미 보정돼 있으므로 스타일만 뺀다.
            bool compensated = _placementShift.Remove(main);
            StripFrame(main, !compensated);
            Native.GetWindowRect(main, out win);
        }

        private void StripFrame(IntPtr main, bool compensate)
        {
            int style = Native.GetWindowLong(main, Native.GWL_STYLE);
            int shift;
            if ((style & Native.WS_DLGFRAME) != 0)
            {
                int before = ClientTop(main);
                SetStyle(main, style & ~Native.WS_DLGFRAME);
                shift = ClientTop(main) - before;
            }
            else
            {
                // 이전 실행이 비정상 종료돼 스타일이 빠진 채 남았다. 위치는 그때 이미 보정돼 있으므로 차이만 잰다.
                int without = ClientTop(main);
                SetStyle(main, style | Native.WS_DLGFRAME);
                shift = without - ClientTop(main);
                SetStyle(main, style);
                compensate = false;
            }
            if (shift <= 0 || shift > 30 * Scale(main))
            {
                SetStyle(main, style | Native.WS_DLGFRAME); // 예상과 다르게 동작하면 쓰지 않는다
                return;
            }
            _frameShift[main] = shift;
            if (compensate) MoveTopEdge(main, -shift);
            _clientTop[main] = ClientTop(main); // 위쪽 테두리 띠 전체(마지막 1px 줄 포함)를 가리고 내용부터 보인다
        }

        // moveBack: 보정했던 위치도 되돌린다. 최대화 중이면 '보통 크기' 위치에 보정이 남으므로 기억해 둔다.
        private void RestoreFrame(IntPtr main, bool moveBack)
        {
            int shift;
            if (!_frameShift.TryGetValue(main, out shift)) return;
            _frameShift.Remove(main);
            _clientTop.Remove(main);
            SetStyle(main, Native.GetWindowLong(main, Native.GWL_STYLE) | Native.WS_DLGFRAME);
            if (Native.IsZoomed(main) || Native.IsIconic(main)) _placementShift[main] = shift;
            else if (moveBack) MoveTopEdge(main, shift);
        }

        // 표준 최대화 크기 = 모니터 작업 영역 + 사방 크기 조절 테두리 두께
        private void FitMaximized(IntPtr main)
        {
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
            IntPtr mon = Native.MonitorFromWindow(main, Native.MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero || !Native.GetMonitorInfo(mon, ref mi)) return;
            int f = FrameThickness(main);
            Native.RECT wa = mi.rcWork, cur;
            if (!Native.GetWindowRect(main, out cur)) return;
            int x = wa.Left - f, y = wa.Top - f, w = wa.Right - wa.Left + 2 * f, h = wa.Bottom - wa.Top + 2 * f;
            if (cur.Left == x && cur.Top == y && cur.Right - cur.Left == w && cur.Bottom - cur.Top == h) return;
            Native.SendMessage(main, Native.WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            Native.SetWindowPos(main, IntPtr.Zero, x, y, w, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Native.SendMessage(main, Native.WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        }

        // 최대화·최소화 중에 원복할 때: 복원될 '보통 크기' 위치에 남은 보정을 되돌린다.
        private void UndoPlacementShift(IntPtr main)
        {
            int shift;
            if (!_placementShift.TryGetValue(main, out shift)) return;
            _placementShift.Remove(main);
            var wp = new Native.WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(Native.WINDOWPLACEMENT)) };
            if (!Native.GetWindowPlacement(main, ref wp)) return;
            wp.rcNormalPosition.Top += shift;
            Native.SetWindowPlacement(main, ref wp);
        }

        // 최근 RegionLagMs 동안의 자를 위치(창 기준) 중 가장 위쪽
        private int RecentCut(IntPtr main, int cut)
        {
            int now = Environment.TickCount;
            List<CutSample> hist;
            if (!_recentCuts.TryGetValue(main, out hist)) _recentCuts[main] = hist = new List<CutSample>(8);
            if (hist.Count == 0 || hist[hist.Count - 1].WindowY != cut) hist.Add(new CutSample { Time = now, WindowY = cut });
            int min = cut;
            for (int i = hist.Count - 1; i >= 0; i--)
            {
                if (i < hist.Count - 1 && now - hist[i + 1].Time > RegionLagMs) { hist.RemoveRange(0, i + 1); break; } // 다음 경계로 바뀐 지 오래된 값
                if (hist[i].WindowY < min) min = hist[i].WindowY;
            }
            if (min < cut) _lagged.Add(main); else _lagged.Remove(main);
            return min;
        }

        // 크기 변경 직후 RecentCut 으로 줄여 둔 경계를 제자리로 되돌린다.
        private void SettleRecentCuts()
        {
            if (_lagged.Count == 0) return;
            _scratch.Clear();
            _scratch.AddRange(_lagged);
            foreach (var main in _scratch)
            {
                int height;
                if (!_clipped.ContainsKey(main) || !_adHeight.TryGetValue(main, out height)) { _lagged.Remove(main); continue; }
                _busy = true;
                IntPtr oldCtx = EnterDpiAware();
                try { ClipMain(main, height); }
                catch { }
                finally { LeaveDpiAware(oldCtx); _busy = false; }
            }
        }

        private int VisibleTop(IntPtr main)
        {
            int top;
            return _clientTop.TryGetValue(main, out top) ? top : 0;
        }

        private static int ClientTop(IntPtr main)
        {
            var p = new Native.POINT();
            Native.RECT win;
            if (!Native.ClientToScreen(main, ref p) || !Native.GetWindowRect(main, out win)) return 0;
            return p.Y - win.Top;
        }

        private static void SetStyle(IntPtr main, int style)
        {
            Native.SetWindowLong(main, Native.GWL_STYLE, style);
            Native.SetWindowPos(main, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        }

        // 위쪽 변만 dy 만큼 옮긴다(아래쪽 변은 그대로). 외부에서 크기를 바꿀 때는 반드시 크기 조절 시작/끝으로 감싼다.
        private static void MoveTopEdge(IntPtr main, int dy)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(main, out r)) return;
            Native.SendMessage(main, Native.WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            Native.SetWindowPos(main, IntPtr.Zero, r.Left, r.Top + dy, r.Right - r.Left, r.Bottom - r.Top - dy,
                                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Native.SendMessage(main, Native.WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        }

        private void UnclipMain(IntPtr main)
        {
            RestoreFrame(main, true);
            UndoPlacementShift(main);
            _recentCuts.Remove(main);
            _lagged.Remove(main);
            _appSizing.Remove(main);
            _appSizedAt.Remove(main);
            Native.SetRegion(main, IntPtr.Zero);
            Native.SetDwm(main, Native.DWMWA_NCRENDERING_POLICY, 0);
            Native.SetDwm(main, Native.DWMWA_BORDER_COLOR, -1);
            Native.SetDwm(main, Native.DWMWA_WINDOW_CORNER_PREFERENCE, 0);
            DestroyGrip(main);
            DestroyCover(main);
            _adHeight.Remove(main);
            _adOf.Remove(main);
            _lastSize.Remove(main);
            _sizedAt.Remove(main);
            _resizing.Remove(main);
            _cutHistory.Remove(main);
            _colors.Remove(main);
        }

        // 최대화·최소화 상태에서는 크기 조절 테두리가 필요 없으므로 손잡이를 숨긴다.
        private void UpdateGrip(IntPtr main, Native.RECT win, int frame, int top, int cut)
        {
            ResizeGrip grip;
            EdgeGrip topGrip;
            _topGrips.TryGetValue(main, out topGrip);
            if (Native.IsZoomed(main) || Native.IsIconic(main))
            {
                if (_grips.TryGetValue(main, out grip)) grip.Hide();
                if (topGrip != null) topGrip.Hide();
                return;
            }
            int corner = (int)Math.Round(CornerDiameter * Scale(main));
            if (!_grips.TryGetValue(main, out grip))
            {
                grip = new ResizeGrip(main, ResizeFromGrip, SetResizing); // DPI 인식 컨텍스트에서 생성해 카카오톡과 같은 좌표계를 쓴다
                _grips[main] = grip;
            }
            grip.Update(win, frame, top, cut, corner);

            // 위쪽 테두리도 카카오톡 대신 직접 조절한다 (카카오톡이 직접 바꾸면 잘라 둔 경계가 한 박자 늦어 덮개가 필요해진다)
            if (topGrip == null)
            {
                topGrip = new EdgeGrip(main, EdgeGrip.EdgeTop, ResizeFromGrip, SetResizing);
                _topGrips[main] = topGrip;
            }
            var area = new Native.RECT { Left = frame, Top = top, Right = (win.Right - win.Left) - frame, Bottom = cut };
            topGrip.Update(win, area, (int)Math.Round(AppTopResizeZone * Scale(main)), corner);
        }

        // 덮개가 필요한 때: 최대화(아래쪽 틈 메우기), 크기 조절 중, 크기가 바뀐 직후(마지막 영역 초기화까지)
        private void UpdateCover(IntPtr main, Native.RECT win, int frame, int cut)
        {
            int now = Environment.TickCount;
            long size = ((long)(win.Right - win.Left) << 32) | (uint)(win.Bottom - win.Top);
            long last;
            if (_lastSize.TryGetValue(main, out last) && last != size) _sizedAt[main] = now;
            _lastSize[main] = size;

            // 테두리 스타일을 뺀 동안에는 크기가 바뀌어도 창 영역이 초기화되지 않으므로 크기 조절 중 덮개가 필요 없다.
            int sizedAt, appSizedAt;
            bool zoomed = Native.IsZoomed(main);
            bool resetsOnSize = !_frameShift.ContainsKey(main) || _appSizing.Contains(main) ||
                                (_appSizedAt.TryGetValue(main, out appSizedAt) && now - appSizedAt < CoverGraceMs);
            bool need = !Native.IsIconic(main) &&
                        (zoomed || resetsOnSize && (_resizing.Contains(main) ||
                         (_sizedAt.TryGetValue(main, out sizedAt) && now - sizedAt < CoverGraceMs)));

            BottomCover cover;
            _covers.TryGetValue(main, out cover);
            if (!need)
            {
                if (cover != null) cover.Hide();
                RefreshColors(main, win, frame, cut);
                return;
            }
            if (cover == null)
            {
                cover = new BottomCover(main);
                _covers[main] = cover;
            }

            // 크기 조절 중 카카오톡은 몇 프레임 늦게 그리므로, 흰 빈자리가 예전 자리에 남아 보일 수 있다.
            //  - 아래쪽으로 키울 때: 예전 화면이 그대로 → 흰 빈자리는 예전 '화면 위치'에 있다.
            //  - 위쪽으로 키울 때: 예전 화면이 새 창 위치에 붙어 보인다 → 예전 '창 기준 위치'에 있다.
            // 최근 경계를 두 기준으로 모두 기록해 더 위쪽부터 덮고, 1~2px 오차를 위해 위아래로 조금 더 덮는다.
            int top = win.Top + cut, slack = 0;
            if (!zoomed)
            {
                List<CutSample> hist;
                if (!_cutHistory.TryGetValue(main, out hist)) _cutHistory[main] = hist = new List<CutSample>();
                hist.Add(new CutSample { Time = now, ScreenY = win.Top + cut, WindowY = cut });
                hist.RemoveAll(e => now - e.Time > CutHistoryMs);
                foreach (var e in hist) top = Math.Min(top, Math.Min(e.ScreenY, win.Top + e.WindowY));
                slack = (int)Math.Round(CoverOverlap * Scale(main));
            }
            Native.RECT r;
            r.Left = win.Left + frame;  r.Right = win.Right - frame;
            r.Top = top - slack;        r.Bottom = win.Bottom - frame + slack;

            // 덮개는 즉시 떠야 하므로 미리 뽑아 둔 색을 쓴다. (처음 한 번만 그 자리에서 뽑는다)
            CoverColors colors;
            if (!_colors.TryGetValue(main, out colors)) colors = SampleColors(main, win, frame, cut, true);
            cover.SetColors(colors.Left, colors.Right, colors.Split);
            cover.Show(r, zoomed ? 0 : (int)Math.Round(CornerDiameter * Scale(main)));
        }

        // 평소(덮개가 필요 없을 때) 몇 초마다 카카오톡 배경색을 다시 뽑아 둔다. 테마 변경에도 따라간다.
        // 이때는 화면에서 한 줄만 복사하는 가벼운 방법만 쓴다 (창 전체 캡처는 CPU 를 많이 쓴다).
        private void RefreshColors(IntPtr main, Native.RECT win, int frame, int cut)
        {
            CoverColors colors;
            if (_colors.TryGetValue(main, out colors) && Environment.TickCount - colors.Time < ColorRefreshMs) return;
            SampleColors(main, win, frame, cut, false);
        }

        // 잘린 경계 바로 위 한 줄에서 사이드바 색과 목록 배경색을 뽑는다 (다크/라이트 모드 자동 대응).
        // 카카오톡이 화면에 보이면 그 한 줄만 화면에서 복사하고, 가려져 있으면 fullCapture 일 때만 창 전체를 캡처한다.
        // 실패하면 이전 값(처음이면 기본 다크 모드 색)을 돌려준다.
        private CoverColors SampleColors(IntPtr main, Native.RECT win, int frame, int cut, bool fullCapture)
        {
            var result = new CoverColors
            {
                Left  = Color.FromArgb(49, 49, 49),
                Right = Color.FromArgb(34, 34, 34),
                Split = (int)Math.Round(SidebarWidth * Scale(main)),
                Time  = Environment.TickCount
            };
            CoverColors previous;
            if (_colors.TryGetValue(main, out previous)) result = previous; // 실패 시 이전 값을 유지
            result.Time = Environment.TickCount;
            _colors[main] = result;

            int w = win.Right - win.Left, h = win.Bottom - win.Top;
            int y = cut - 3;
            int x0 = frame + 2, x1 = w - frame - 3;
            if (x1 - x0 < 10 || y <= 0 || y >= h) return result;
            try
            {
                if (IsOnScreen(main, win.Left + x0, win.Left + (x0 + x1) / 2, win.Left + x1, win.Top + y))
                {
                    using (var row = new Bitmap(w, 1))
                    {
                        using (var g = Graphics.FromImage(row))
                            g.CopyFromScreen(win.Left, win.Top + y, 0, 0, new Size(w, 1));
                        AnalyzeRow(x => row.GetPixel(x, 0), x0, x1, frame, ref result);
                    }
                }
                else if (fullCapture)
                {
                    using (var bmp = new Bitmap(w, h))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            IntPtr hdc = g.GetHdc();
                            bool ok = Native.PrintWindow(main, hdc, Native.PW_RENDERFULLCONTENT);
                            g.ReleaseHdc(hdc);
                            if (!ok) return result;
                        }
                        AnalyzeRow(x => bmp.GetPixel(x, y), x0, x1, frame, ref result);
                    }
                }
                _colors[main] = result;
            }
            catch { } // 캡처 실패 시 이전 값(또는 기본 다크 모드 색)을 쓴다
            return result;
        }

        // 그 줄이 다른 창에 가려지지 않고 카카오톡 메인 창이 보이는지
        private static bool IsOnScreen(IntPtr main, int xa, int xb, int xc, int y)
        {
            return Native.WindowFromPoint(new Native.POINT { X = xa, Y = y }) == main &&
                   Native.WindowFromPoint(new Native.POINT { X = xb, Y = y }) == main &&
                   Native.WindowFromPoint(new Native.POINT { X = xc, Y = y }) == main;
        }

        // 왼쪽(사이드바) 색, 그 색이 끝나는 x, 오른쪽 끝 근처의 가장 흔한 색(목록 배경)을 구한다.
        private static void AnalyzeRow(Func<int, Color> pixel, int x0, int x1, int frame, ref CoverColors result)
        {
            Color left = Opaque(pixel(x0));
            int split = x0;
            while (split < x1 && Close(pixel(split), left)) split++;

            var counts = new Dictionary<int, int>(); // 아이콘/글자 픽셀을 피하려고 가장 흔한 색을 쓴다
            for (int x = Math.Max(split, x1 - 60); x <= x1; x++)
            {
                int c = Opaque(pixel(x)).ToArgb(), n;
                counts.TryGetValue(c, out n);
                counts[c] = n + 1;
            }
            int best = left.ToArgb(), bestN = -1;
            foreach (var kv in counts) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            if (split >= x1) split = frame; // 한 가지 색뿐이면 전체를 오른쪽 색으로

            result.Left  = left;
            result.Right = Color.FromArgb(best);
            result.Split = split - frame; // 덮개 기준 x
        }

        // PrintWindow(PW_RENDERFULLCONTENT) 결과는 알파가 0 이라 그대로 칠하면 투명(검정)이 된다.
        private static Color Opaque(Color c)
        {
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        private static bool Close(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) <= 3 && Math.Abs(a.G - b.G) <= 3 && Math.Abs(a.B - b.B) <= 3;
        }

        private void DestroyCover(IntPtr main)
        {
            BottomCover cover;
            if (!_covers.TryGetValue(main, out cover)) return;
            cover.DestroyHandle();
            _covers.Remove(main);
        }

        private void DestroyGrip(IntPtr main)
        {
            EdgeGrip topGrip;
            if (_topGrips.TryGetValue(main, out topGrip))
            {
                topGrip.DestroyHandle();
                _topGrips.Remove(main);
            }
            ResizeGrip grip;
            if (!_grips.TryGetValue(main, out grip)) return;
            grip.DestroyHandle();
            _grips.Remove(main);
        }

        // 보이지 않는 크기 조절 테두리 두께 (Windows 표준: SIZEFRAME + PADDEDBORDER, 100% 배율에서 8px)
        private int FrameThickness(IntPtr h)
        {
            if (!_dpiApiMissing)
            {
                try
                {
                    uint dpi = Native.GetDpiForWindow(h);
                    if (dpi > 0)
                        return Native.GetSystemMetricsForDpi(Native.SM_CXSIZEFRAME, dpi) +
                               Native.GetSystemMetricsForDpi(Native.SM_CXPADDEDBORDER, dpi);
                }
                catch (EntryPointNotFoundException)
                {
                    _dpiApiMissing = true;
                }
            }
            return Native.GetSystemMetrics(Native.SM_CXSIZEFRAME) + Native.GetSystemMetrics(Native.SM_CXPADDEDBORDER);
        }

        private float Scale(IntPtr h)
        {
            if (_dpiApiMissing) return 1f;
            try
            {
                uint dpi = Native.GetDpiForWindow(h);
                return dpi > 0 ? dpi / 96f : 1f;
            }
            catch (EntryPointNotFoundException)
            {
                _dpiApiMissing = true;
                return 1f;
            }
        }

        private IntPtr EnterDpiAware()
        {
            if (_dpiApiMissing) return IntPtr.Zero;
            try { return Native.SetThreadDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch (EntryPointNotFoundException) { _dpiApiMissing = true; return IntPtr.Zero; }
        }

        private void LeaveDpiAware(IntPtr old)
        {
            if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old);
        }

        // 완전 투명 + 클릭 통과. 이미 적용되어 있으면 아무것도 하지 않는다.
        private static void MakeInvisible(IntPtr h)
        {
            const int bits = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            uint key, flags;
            byte alpha;
            if ((ex & bits) == bits && Native.GetLayeredWindowAttributes(h, out key, out alpha, out flags) &&
                (flags & Native.LWA_ALPHA) != 0 && alpha == 0) return;

            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | bits);
            Native.SetLayeredWindowAttributes(h, 0, 0, Native.LWA_ALPHA);
        }

        private void Prune()
        {
            var dead = _scratch;
            dead.Clear();
            foreach (var h in _adWindows.Keys) if (!Native.IsWindow(h)) dead.Add(h);
            foreach (var h in dead) _adWindows.Remove(h);

            dead.Clear();
            foreach (var h in _adOwners) if (!Native.IsWindow(h)) dead.Add(h);
            foreach (var h in dead) _adOwners.Remove(h);
        }

        private void ForgetAll()
        {
            foreach (var grip in _grips.Values) grip.DestroyHandle();
            _grips.Clear();
            foreach (var grip in _topGrips.Values) grip.DestroyHandle();
            _topGrips.Clear();
            foreach (var cover in _covers.Values) cover.DestroyHandle();
            _covers.Clear();
            _lastSize.Clear();
            _sizedAt.Clear();
            _resizing.Clear();
            _frameShift.Clear();
            _placementShift.Clear();
            _clientTop.Clear();
            _recentCuts.Clear();
            _lagged.Clear();
            _appSizing.Clear();
            _appSizedAt.Clear();
            _cutHistory.Clear();
            _colors.Clear();
            _adHeight.Clear();
            _adOf.Clear();
            _adWindows.Clear();
            _adOwners.Clear();
            _clipped.Clear();
            _wanted.Clear();
            _lastWanted.Clear();
        }
    }

    // 마우스 왼쪽 버튼을 누른 위치를 기록한다. (카카오톡 창 끌기가 이동인지 크기 조절인지 판단하는 데 쓴다)
    // 끌기 시작 이벤트 시점의 커서 위치·모양은 이미 움직인 뒤의 값이라 믿을 수 없으므로 누른 순간을 직접 본다.
    // 저수준 훅은 모든 마우스 입력마다 호출되므로, UI 스레드가 바빠도 입력이 지연되지 않게 전용 스레드에서 돌린다.
    internal sealed class MouseDownWatcher
    {
        private readonly object _lock = new object();
        private Native.HookProc _proc; // GC 방지
        private Thread _thread;
        private uint   _threadId;
        private const int MaxPressAgeMs = 10000; // Windows 가 응답 지연으로 훅을 몰래 해제했을 때 오래된 값을 쓰지 않도록
        private Native.POINT _down;
        private int    _downAt;
        private bool   _hasDown;

        public void Start()
        {
            if (_thread != null) return;
            _proc = HookCallback;
            var ready = new ManualResetEvent(false);
            _thread = new Thread(delegate()
            {
                _threadId = Native.GetCurrentThreadId();
                IntPtr hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
                ready.Set();
                if (hook == IntPtr.Zero) return;
                Native.MSG msg;
                while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }
                Native.UnhookWindowsHookEx(hook);
            });
            _thread.IsBackground = true;
            _thread.Start();
            ready.WaitOne(1000);
        }

        public void Stop()
        {
            if (_thread == null) return;
            Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(500);
            _thread = null;
            lock (_lock) _hasDown = false;
        }

        // 지금 눌려 있는 왼쪽 버튼을 누른 위치 (화면 좌표, 물리 픽셀)
        public bool TryGetPress(out Native.POINT p)
        {
            int at;
            lock (_lock) { p = _down; at = _downAt; if (!_hasDown) return false; }
            if (Environment.TickCount - at > MaxPressAgeMs) return false;
            return (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0;
        }

        private IntPtr HookCallback(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0 && w.ToInt32() == Native.WM_LBUTTONDOWN)
            {
                var pt = (Native.POINT)Marshal.PtrToStructure(l, typeof(Native.POINT)); // MSLLHOOKSTRUCT 의 첫 필드
                lock (_lock) { _down = pt; _downAt = Environment.TickCount; _hasDown = true; }
            }
            return Native.CallNextHookEx(IntPtr.Zero, code, w, l);
        }
    }

    // 창 영역(region) 밖은 마우스도 통과하므로, 잘라낸 메인 창은 좌·우·하단의 크기 조절 테두리를 잃는다.
    // 그 자리에 보이지 않는 창을 메인 창 바로 아래에 깔아 두고, 끌면 대신 크기를 조절한다.
    // (ENTER/EXITSIZEMOVE 없이 외부에서 크기를 바꾸면 카카오톡 베타가 높이를 65535로 폭주시키므로 반드시 감싼다.)
    internal sealed class ResizeGrip : NativeWindow
    {
        private const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;
        private const int WM_SETCURSOR = 0x0020, WM_MOUSEACTIVATE = 0x0021, WM_NCHITTEST = 0x0084;
        private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_CAPTURECHANGED = 0x0215;
        private const int MA_NOACTIVATE = 3, HTCLIENT = 1;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_LAYERED = 0x00080000, WS_EX_TOOLWINDOW = 0x00000080;
        private const uint LWA_ALPHA = 2;

        private static readonly IntPtr CursorWE   = Native.LoadCursor(IntPtr.Zero, new IntPtr(32644)); // IDC_SIZEWE
        private static readonly IntPtr CursorNS   = Native.LoadCursor(IntPtr.Zero, new IntPtr(32645)); // IDC_SIZENS
        private static readonly IntPtr CursorNWSE = Native.LoadCursor(IntPtr.Zero, new IntPtr(32642)); // IDC_SIZENWSE
        private static readonly IntPtr CursorNESW = Native.LoadCursor(IntPtr.Zero, new IntPtr(32643)); // IDC_SIZENESW

        private readonly IntPtr _target;
        private readonly Action<IntPtr, Native.RECT> _resize; // 새 창 사각형을 적용하는 콜백 (잘라내기 → 크기 변경 순서)
        private readonly Action<IntPtr, bool>        _dragState; // 드래그 시작/끝 알림 (덮개 표시용)
        private int _x, _y, _w, _top, _cut, _frame, _corner;  // 메인 창 기준 좌표
        private bool _visible;

        // 드래그 상태
        private bool         _dragging;
        private int          _dragZone;
        private IntPtr       _dragCursor;
        private Native.POINT _dragStart;
        private Native.RECT  _dragRect;
        private int          _minW, _minH;

        public ResizeGrip(IntPtr target, Action<IntPtr, Native.RECT> resize, Action<IntPtr, bool> dragState)
        {
            _target    = target;
            _resize    = resize;
            _dragState = dragState;
            CreateHandle(new CreateParams
            {
                Caption = "TalkShield Resize Grip",
                Style   = WS_POPUP,
                ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE
            });
            Native.SetLayeredWindowAttributes(Handle, 0, 1, LWA_ALPHA); // 사실상 투명하지만 마우스는 받는다
        }

        // win: 메인 창 사각형, frame: 테두리 두께, cut: 잘라낸 아래 경계(메인 창 기준 y)
        public void Update(Native.RECT win, int frame, int top, int cut, int corner)
        {
            int w = win.Right - win.Left;
            bool geometryChanged = w != _w || cut != _cut || frame != _frame || corner != _corner || top != _top;
            bool placed = _visible && win.Left == _x && win.Top == _y && !geometryChanged &&
                          Native.GetWindow(_target, Native.GW_HWNDNEXT) == Handle;
            if (placed) return;

            _x = win.Left; _y = win.Top; _w = w; _cut = cut; _frame = frame; _corner = corner; _top = top;

            if (geometryChanged || !_visible)
            {
                // 좌측 띠 + 우측 띠 + 하단 띠 (모두 카카오톡의 보이는 영역 바깥, 보이는 윗변부터)
                IntPtr rgn = Native.CreateRectRgn(0, top, frame, cut + frame);
                IntPtr r2  = Native.CreateRectRgn(w - frame, top, w, cut + frame);
                IntPtr r3  = Native.CreateRectRgn(0, cut, w, cut + frame);
                Native.CombineRgn(rgn, rgn, r2, Native.RGN_OR);
                Native.CombineRgn(rgn, rgn, r3, Native.RGN_OR);
                Native.DeleteObject(r2);
                Native.DeleteObject(r3);
                Native.SetRegion(Handle, rgn);
            }

            // 메인 창 바로 아래에 둔다. 메인 창의 잘린 부분은 마우스가 통과하므로 이 창이 받는다.
            Native.SetWindowPos(Handle, _target, _x, _y, w, cut + frame, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            _visible = true;
        }

        public void Hide()
        {
            if (!_visible) return;
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
            _visible = false;
        }

        private int ZoneAt(int x, int y)
        {
            if (y >= _cut)
            {
                if (x < _frame + _corner) return WMSZ_BOTTOMLEFT;
                if (x >= _w - _frame - _corner) return WMSZ_BOTTOMRIGHT;
                return WMSZ_BOTTOM;
            }
            if (x < _frame) return y >= _cut - _corner ? WMSZ_BOTTOMLEFT : WMSZ_LEFT;
            return y >= _cut - _corner ? WMSZ_BOTTOMRIGHT : WMSZ_RIGHT;
        }

        private static IntPtr CursorFor(int zone)
        {
            switch (zone)
            {
                case WMSZ_LEFT: case WMSZ_RIGHT: return CursorWE;
                case WMSZ_BOTTOM:                return CursorNS;
                case WMSZ_BOTTOMRIGHT:           return CursorNWSE;
                default:                         return CursorNESW;
            }
        }

        // 크기 조절은 이 창이 마우스를 잡고 직접 수행한다.
        // (카카오톡에 SC_SIZE 를 넘기면 마우스 버튼 상태가 프로세스 간에 공유되지 않아 엉뚱하게 동작한다.)
        // 카카오톡에는 실제 드래그와 같이 ENTER/EXITSIZEMOVE 로 감싸 알린다.
        private void BeginDrag()
        {
            if (!Native.GetWindowRect(_target, out _dragRect)) return;
            Native.GetCursorPos(out _dragStart);
            _dragZone   = ZoneAt(_dragStart.X - _x, _dragStart.Y - _y);
            _dragCursor = CursorFor(_dragZone);
            _minW = _minH = 0;
            _dragging = true;

            Native.SetForegroundWindow(_target);
            Native.SetCapture(Handle);
            _dragState(_target, true); // 첫 크기 변경 전에 덮개를 올린다
            Native.SendMessage(_target, Native.WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        }

        private void DragTo()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;

            Native.RECT r = _dragRect;
            if (_dragZone == WMSZ_LEFT || _dragZone == WMSZ_BOTTOMLEFT) r.Left  += dx;
            if (_dragZone == WMSZ_RIGHT || _dragZone == WMSZ_BOTTOMRIGHT) r.Right += dx;
            if (_dragZone >= WMSZ_BOTTOM) r.Bottom += dy;

            // 카카오톡의 최소 크기보다 줄이면 카카오톡이 되돌리므로, 확인된 최소 크기에서 멈춘다.
            if (r.Right - r.Left < _minW)
            {
                if (_dragZone == WMSZ_LEFT || _dragZone == WMSZ_BOTTOMLEFT) r.Left = r.Right - _minW;
                else r.Right = r.Left + _minW;
            }
            if (r.Bottom - r.Top < _minH) r.Bottom = r.Top + _minH;

            _resize(_target, r);

            Native.RECT actual;
            if (Native.GetWindowRect(_target, out actual))
            {
                if (actual.Right - actual.Left > r.Right - r.Left) _minW = actual.Right - actual.Left;
                if (actual.Bottom - actual.Top > r.Bottom - r.Top) _minH = actual.Bottom - actual.Top;
            }
        }

        private void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            Native.ReleaseCapture();
            Native.SendMessage(_target, Native.WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            _dragState(_target, false);
        }

        public override void DestroyHandle()
        {
            EndDrag();
            base.DestroyHandle();
        }

        private int ZoneAtCursor()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            return ZoneAt(p.X - _x, p.Y - _y);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = new IntPtr(HTCLIENT);
                    return;
                case WM_MOUSEACTIVATE:
                    m.Result = new IntPtr(MA_NOACTIVATE);
                    return;
                case WM_SETCURSOR:
                    Native.SetCursor(_dragging ? _dragCursor : CursorFor(ZoneAtCursor()));
                    m.Result = new IntPtr(1);
                    return;
                case WM_LBUTTONDOWN:
                    BeginDrag();
                    return;
                case WM_MOUSEMOVE:
                    if (_dragging) { Native.SetCursor(_dragCursor); DragTo(); }
                    return;
                case WM_LBUTTONUP:
                    EndDrag();
                    return;
                case WM_CAPTURECHANGED:
                    EndDrag(); // 다른 창이 마우스를 가져가도 반드시 크기 조절을 끝낸다
                    break;
            }
            base.WndProc(ref m);
        }
    }

    // 카카오톡 자체 크기 조절 테두리 위에 얹는 손잡이. 카카오톡이 직접 크기를 바꾸면 먼저 그려 버리므로
    // (기존 카카오톡: 배너 자리를 비운 채, 베타: 잘라 둔 경계가 한 박자 늦게) 보이지 않는 창으로 크기 조절을 대신 수행한다.
    //  - 기존 카카오톡: 사방 테두리(6px)
    //  - 베타: 위쪽 테두리(카카오톡 자체 7px). 좌·우·아래는 잘라낸 바깥이라 ResizeGrip 이 맡는다.
    internal sealed class EdgeGrip : NativeWindow
    {
        public const int EdgeLeft = 1, EdgeTop = 2, EdgeRight = 4, EdgeBottom = 8, EdgeAll = 15;

        private const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5,
                          WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;
        private const int WM_SETCURSOR = 0x0020, WM_MOUSEACTIVATE = 0x0021, WM_NCHITTEST = 0x0084;
        private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_CAPTURECHANGED = 0x0215;
        private const int MA_NOACTIVATE = 3, HTCLIENT = 1;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_LAYERED = 0x00080000, WS_EX_TOOLWINDOW = 0x00000080;
        private static readonly IntPtr CursorWE   = Native.LoadCursor(IntPtr.Zero, new IntPtr(32644));
        private static readonly IntPtr CursorNS   = Native.LoadCursor(IntPtr.Zero, new IntPtr(32645));
        private static readonly IntPtr CursorNWSE = Native.LoadCursor(IntPtr.Zero, new IntPtr(32642));
        private static readonly IntPtr CursorNESW = Native.LoadCursor(IntPtr.Zero, new IntPtr(32643));

        private readonly IntPtr _target;
        private readonly int    _edges;
        private readonly Action<IntPtr, Native.RECT> _resize;
        private readonly Action<IntPtr, bool>        _dragState; // 없어도 된다
        private Native.RECT _rect;     // 메인 창 사각형 (화면)
        private Native.RECT _area;     // 손잡이를 두를 영역 (메인 창 기준) — 보이는 부분
        private int  _border, _corner;
        private bool _visible;

        private bool         _dragging;
        private int          _dragZone;
        private Native.POINT _dragStart;
        private Native.RECT  _dragRect;
        private int          _minW, _minH;

        public EdgeGrip(IntPtr target, int edges, Action<IntPtr, Native.RECT> resize, Action<IntPtr, bool> dragState)
        {
            _target = target;
            _edges = edges;
            _resize = resize;
            _dragState = dragState;
            CreateHandle(new CreateParams
            {
                Caption = "TalkShield Edge Grip",
                Style   = WS_POPUP,
                ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE
            });
            Native.SetLayeredWindowAttributes(Handle, 0, 1, Native.LWA_ALPHA); // 사실상 투명하지만 마우스는 받는다
        }

        public bool Dragging { get { return _dragging; } }

        // 메인 창 바로 위에, area(메인 창 기준 보이는 영역)의 안쪽 가장자리를 따라 border 두께의 띠로 둔다.
        public void Update(Native.RECT win, Native.RECT area, int border, int corner)
        {
            if (_dragging) return;
            int w = win.Right - win.Left, h = win.Bottom - win.Top;
            bool shaped = w != _rect.Right - _rect.Left || h != _rect.Bottom - _rect.Top || border != _border || corner != _corner ||
                          area.Left != _area.Left || area.Top != _area.Top || area.Right != _area.Right || area.Bottom != _area.Bottom;
            bool moved = shaped || win.Left != _rect.Left || win.Top != _rect.Top;
            if (_visible && !moved && IsJustAboveTarget()) return;

            if (shaped || !_visible)
            {
                IntPtr rgn = Native.CreateRectRgn(0, 0, 0, 0);
                if ((_edges & EdgeLeft) != 0)   AddRect(rgn, area.Left, area.Top, area.Left + border, area.Bottom);
                if ((_edges & EdgeRight) != 0)  AddRect(rgn, area.Right - border, area.Top, area.Right, area.Bottom);
                if ((_edges & EdgeTop) != 0)    AddRect(rgn, area.Left, area.Top, area.Right, area.Top + border);
                if ((_edges & EdgeBottom) != 0) AddRect(rgn, area.Left, area.Bottom - border, area.Right, area.Bottom);
                Native.SetRegion(Handle, rgn);
            }
            _rect = win; _area = area; _border = border; _corner = corner;

            IntPtr above = Native.GetWindow(_target, Native.GW_HWNDPREV);
            if (above == Handle) above = Native.GetWindow(Handle, Native.GW_HWNDPREV);
            bool targetTopmost = (Native.GetWindowLong(_target, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;
            IntPtr after = above != IntPtr.Zero ? above : targetTopmost ? HWND_TOPMOST : IntPtr.Zero /* HWND_TOP */;
            if (!targetTopmost && above != IntPtr.Zero && (Native.GetWindowLong(above, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0)
                after = IntPtr.Zero; // 위가 '항상 위' 창이면 일반 창 중 맨 위
            Native.SetWindowPos(Handle, after, win.Left, win.Top, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            _visible = true;
        }

        public void Hide()
        {
            if (!_visible || _dragging) return;
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
            _visible = false;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        // 이 창 아래가 곧바로 메인 창인지 (사이에 메인 창이 소유한 팝업만 있으면 괜찮다)
        private bool IsJustAboveTarget()
        {
            IntPtr next = Native.GetWindow(Handle, Native.GW_HWNDNEXT);
            while (next != IntPtr.Zero && next != _target && Native.GetWindow(next, Native.GW_OWNER) == _target)
                next = Native.GetWindow(next, Native.GW_HWNDNEXT);
            return next == _target;
        }

        private static void AddRect(IntPtr rgn, int l, int t, int r, int b)
        {
            if (r <= l || b <= t) return;
            IntPtr part = Native.CreateRectRgn(l, t, r, b);
            Native.CombineRgn(rgn, rgn, part, Native.RGN_OR);
            Native.DeleteObject(part);
        }

        // x, y: 메인 창 기준. 모서리 근처(corner)는 두 방향을 함께 조절한다.
        private int ZoneAt(int x, int y)
        {
            Native.RECT a = _area;
            bool onTop = (_edges & EdgeTop) != 0 && y < a.Top + _border;
            bool onBottom = (_edges & EdgeBottom) != 0 && y >= a.Bottom - _border;
            bool onLeft = (_edges & EdgeLeft) != 0 && x < a.Left + _border;
            bool onRight = (_edges & EdgeRight) != 0 && x >= a.Right - _border;
            bool nearL = x < a.Left + _corner, nearR = x >= a.Right - _corner;
            bool nearT = y < a.Top + _corner, nearB = y >= a.Bottom - _corner;
            bool left = onLeft || (onTop || onBottom) && nearL;
            bool right = onRight || (onTop || onBottom) && nearR;
            bool top = onTop || (onLeft || onRight) && nearT && (_edges & EdgeTop) != 0;
            bool bottom = onBottom || (onLeft || onRight) && nearB && (_edges & EdgeBottom) != 0;
            if (top) return left ? WMSZ_TOPLEFT : right ? WMSZ_TOPRIGHT : WMSZ_TOP;
            if (bottom) return left ? WMSZ_BOTTOMLEFT : right ? WMSZ_BOTTOMRIGHT : WMSZ_BOTTOM;
            return left ? WMSZ_LEFT : WMSZ_RIGHT;
        }

        private static IntPtr CursorFor(int zone)
        {
            switch (zone)
            {
                case WMSZ_LEFT: case WMSZ_RIGHT:              return CursorWE;
                case WMSZ_TOP: case WMSZ_BOTTOM:              return CursorNS;
                case WMSZ_TOPLEFT: case WMSZ_BOTTOMRIGHT:     return CursorNWSE;
                default:                                      return CursorNESW;
            }
        }

        private void BeginDrag()
        {
            if (!Native.GetWindowRect(_target, out _dragRect)) return;
            Native.GetCursorPos(out _dragStart);
            _dragZone = ZoneAt(_dragStart.X - _rect.Left, _dragStart.Y - _rect.Top);
            _minW = _minH = 0;
            _dragging = true;
            Native.SetForegroundWindow(_target);
            Native.SetCapture(Handle);
            if (_dragState != null) _dragState(_target, true);
            Native.SendMessage(_target, Native.WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        }

        private void DragTo()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
            bool l = _dragZone == WMSZ_LEFT || _dragZone == WMSZ_TOPLEFT || _dragZone == WMSZ_BOTTOMLEFT;
            bool r = _dragZone == WMSZ_RIGHT || _dragZone == WMSZ_TOPRIGHT || _dragZone == WMSZ_BOTTOMRIGHT;
            bool t = _dragZone == WMSZ_TOP || _dragZone == WMSZ_TOPLEFT || _dragZone == WMSZ_TOPRIGHT;
            bool b = _dragZone >= WMSZ_BOTTOM;

            Native.RECT n = _dragRect;
            if (l) n.Left += dx;
            if (r) n.Right += dx;
            if (t) n.Top += dy;
            if (b) n.Bottom += dy;
            // 카카오톡 최소 크기보다 작게 하면 카카오톡이 되돌리므로 확인된 최소 크기에서 멈춘다 (반대편 변은 고정)
            if (n.Right - n.Left < _minW) { if (l) n.Left = n.Right - _minW; else n.Right = n.Left + _minW; }
            if (n.Bottom - n.Top < _minH) { if (t) n.Top = n.Bottom - _minH; else n.Bottom = n.Top + _minH; }

            _resize(_target, n);

            Native.RECT actual;
            if (Native.GetWindowRect(_target, out actual))
            {
                if (actual.Right - actual.Left > n.Right - n.Left) _minW = actual.Right - actual.Left;
                if (actual.Bottom - actual.Top > n.Bottom - n.Top) _minH = actual.Bottom - actual.Top;
                if (l && actual.Left != n.Left || t && actual.Top != n.Top)
                {
                    // 최소 크기에 걸려 반대편 변이 밀렸으면 제자리로
                    Native.SetWindowPos(_target, IntPtr.Zero, l ? n.Right - (actual.Right - actual.Left) : actual.Left,
                        t ? n.Bottom - (actual.Bottom - actual.Top) : actual.Top, 0, 0,
                        Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                }
            }
            if (Native.GetWindowRect(_target, out actual))
            {
                _rect = actual;
                Native.SetWindowPos(Handle, IntPtr.Zero, actual.Left, actual.Top, actual.Right - actual.Left, actual.Bottom - actual.Top,
                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            }
        }

        private void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            Native.ReleaseCapture();
            Native.SendMessage(_target, Native.WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            if (_dragState != null) _dragState(_target, false);
            _rect = new Native.RECT(); // 다음 Update 에서 영역을 새 크기로 다시 만든다
            _visible = false;
        }

        public override void DestroyHandle()
        {
            EndDrag();
            base.DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = new IntPtr(HTCLIENT);
                    return;
                case WM_MOUSEACTIVATE:
                    m.Result = new IntPtr(MA_NOACTIVATE);
                    return;
                case WM_SETCURSOR:
                {
                    Native.POINT p;
                    Native.GetCursorPos(out p);
                    Native.SetCursor(CursorFor(_dragging ? _dragZone : ZoneAt(p.X - _rect.Left, p.Y - _rect.Top)));
                    m.Result = new IntPtr(1);
                    return;
                }
                case WM_LBUTTONDOWN:
                    BeginDrag();
                    return;
                case WM_MOUSEMOVE:
                    if (_dragging) DragTo();
                    return;
                case WM_LBUTTONUP:
                    EndDrag();
                    return;
                case WM_CAPTURECHANGED:
                    EndDrag();
                    break;
            }
            base.WndProc(ref m);
        }
    }

    // 크기가 바뀔 때마다 Windows 테마 엔진(카카오톡 프로세스 내부)이 메인 창 영역을 강제로 초기화한다.
    // 다시 자르기까지 수~수십 ms 동안 흰 빈자리가 비치므로, 크기 조절 중과 최대화 중에는
    // 빈자리 위에 카카오톡 배경색으로 칠한 덮개를 메인 창 바로 위에 올려 둔다.
    internal sealed class BottomCover : NativeWindow
    {
        private const int WM_ERASEBKGND = 0x0014, WM_MOUSEACTIVATE = 0x0021, MA_NOACTIVATE = 3;
        private const int WS_POPUP = unchecked((int)0x80000000), WS_EX_TOOLWINDOW = 0x00000080;

        private readonly IntPtr _target;
        private Color _left = Color.FromArgb(49, 49, 49), _right = Color.FromArgb(34, 34, 34);
        private int   _split; // 왼쪽 색(사이드바)이 끝나는 x
        private Native.RECT _rect;
        private int   _corner = -1;
        private bool  _visible;

        public bool Visible { get { return _visible; } }

        public BottomCover(IntPtr target)
        {
            _target = target;
            CreateHandle(new CreateParams
            {
                Caption = "TalkShield Cover",
                Style   = WS_POPUP,
                ExStyle = WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE
            });
        }

        public void SetColors(Color left, Color right, int split)
        {
            if (left == _left && right == _right && split == _split) return;
            _left = left; _right = right; _split = split;
            Native.InvalidateRect(Handle, IntPtr.Zero, true);
        }

        // r: 덮을 화면 사각형, corner: 아래 모서리 둥글기(0 이면 각짐)
        public void Show(Native.RECT r, int corner)
        {
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) { Hide(); return; }

            if (corner != _corner || w != _rect.Right - _rect.Left || h != _rect.Bottom - _rect.Top)
            {
                // 위쪽은 각지게(카카오톡 내용과 이어짐), 아래쪽은 카카오톡 창 모서리와 같게 둥글게
                IntPtr rgn = corner > 0 ? Native.CreateRoundRectRgn(0, -corner, w + 1, h + 1, corner, corner)
                                        : Native.CreateRectRgn(0, 0, w, h);
                Native.SetRegion(Handle, rgn);
                _corner = corner;
            }

            bool moved = !_visible || r.Left != _rect.Left || r.Top != _rect.Top || r.Right != _rect.Right || r.Bottom != _rect.Bottom;
            _rect = r;
            if (!moved && Native.GetWindow(Handle, Native.GW_HWNDNEXT) == _target) return;

            // 메인 창 바로 위에 둔다. (메인 창 위가 '항상 위' 창이면 일반 창 중 맨 위로)
            IntPtr above = Native.GetWindow(_target, Native.GW_HWNDPREV);
            if (above == Handle) above = Native.GetWindow(Handle, Native.GW_HWNDPREV);
            IntPtr after = above == IntPtr.Zero || (Native.GetWindowLong(above, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0
                         ? IntPtr.Zero /* HWND_TOP */ : above;
            Native.SetWindowPos(Handle, after, r.Left, r.Top, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            _visible = true;
        }

        public void Hide()
        {
            if (!_visible) return;
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
            _visible = false;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEACTIVATE:
                    m.Result = new IntPtr(MA_NOACTIVATE);
                    return;
                case WM_ERASEBKGND:
                    using (var g = Graphics.FromHdc(m.WParam))
                    {
                        int w = _rect.Right - _rect.Left, h = _rect.Bottom - _rect.Top;
                        int split = Math.Max(0, Math.Min(_split, w));
                        using (var b = new SolidBrush(_left))  g.FillRectangle(b, 0, 0, split, h);
                        using (var b = new SolidBrush(_right)) g.FillRectangle(b, split, 0, w - split, h);
                    }
                    m.Result = new IntPtr(1);
                    return;
            }
            base.WndProc(ref m);
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

    #region 트레이 아이콘·메뉴 디자인 (Windows 다크/라이트 테마 자동)

    // Windows 테마: 작업 표시줄(시스템)과 앱 각각 다크/라이트를 따로 고를 수 있다.
    internal static class WinTheme
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool TaskbarLight { get { return Read("SystemUsesLightTheme", false); } } // 트레이 아이콘이 놓이는 곳
        public static bool AppsLight    { get { return Read("AppsUseLightTheme", true); } }     // 메뉴

        private static bool Read(string name, bool fallback)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    object v = k != null ? k.GetValue(name) : null;
                    return v is int ? (int)v != 0 : fallback;
                }
            }
            catch { return fallback; }
        }
    }

    // 트레이 아이콘: 작업 표시줄 색에 맞춘 단색 말풍선 (다크 → 흰색, 라이트 → 검은색).
    // 켜짐은 빨간 차단 배지, 꺼짐은 회색 말풍선. 화면 크기에 맞춰 그때그때 그린다.
    internal static class TrayIconPainter
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        public static Icon Create(bool on, bool lightTaskbar)
        {
            Size size = SystemInformation.SmallIconSize;
            const int Ss = 4; // 크게 그린 뒤 줄여 가장자리를 부드럽게
            int w = size.Width * Ss, h = size.Height * Ss;
            float u = w / 16f; // 16x16 격자 기준 1칸

            using (var big = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Color bubble = !on ? Color.FromArgb(140, 140, 140)
                                 : lightTaskbar ? Color.FromArgb(31, 31, 31) : Color.White;
                    using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                    using (var brush = new SolidBrush(bubble))
                    {
                        AddRoundRect(path, 1f * u, 2f * u, 14f * u, 10f * u, 3.6f * u);
                        g.FillPath(brush, path);
                        g.FillPolygon(brush, new[] { new PointF(3.6f * u, 11.2f * u), new PointF(3.6f * u, 15f * u), new PointF(8f * u, 11.2f * u) });
                    }

                    if (on)
                    {
                        // 배지 둘레를 투명하게 비워 말풍선과 분리해 보이게 한다
                        float cx = 12.2f * u, cy = 11.6f * u, r = 3.7f * u, gap = 1.1f * u;
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        using (var clear = new SolidBrush(Color.Transparent))
                            g.FillEllipse(clear, cx - r - gap, cy - r - gap, 2 * (r + gap), 2 * (r + gap));
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        using (var red = new SolidBrush(Color.FromArgb(229, 57, 53)))
                            g.FillEllipse(red, cx - r, cy - r, 2 * r, 2 * r);
                        using (var bar = new Pen(Color.White, 1.3f * u))
                        {
                            bar.StartCap = bar.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                            g.DrawLine(bar, cx - r * 0.5f, cy, cx + r * 0.5f, cy);
                        }
                    }
                }

                using (var small = new Bitmap(size.Width, size.Height))
                {
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(big, 0, 0, size.Width, size.Height);
                    }
                    IntPtr hIcon = small.GetHicon();
                    try
                    {
                        using (var temp = Icon.FromHandle(hIcon))
                            return (Icon)temp.Clone(); // 복사본이 자기 핸들을 가진다
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
        }

        internal static void AddRoundRect(System.Drawing.Drawing2D.GraphicsPath p, float x, float y, float w, float h, float r)
        {
            float d = 2 * r;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
        }
    }

    // 트레이 메뉴: Windows 11 메뉴처럼 둥근 모서리, 넉넉한 여백, 둥근 강조 표시. 앱 테마(다크/라이트)를 따른다.
    internal sealed class ModernMenuRenderer : ToolStripRenderer
    {
        public const string HeaderTag = "header";

        private Color _back, _text, _subText, _disabled, _hover, _separator, _check;

        public ModernMenuRenderer(bool light) { SetTheme(light); }

        public Color Back { get { return _back; } }

        public void SetTheme(bool light)
        {
            if (light)
            {
                _back = Color.FromArgb(249, 249, 249); _text = Color.FromArgb(26, 26, 26); _subText = Color.FromArgb(96, 96, 96);
                _disabled = Color.FromArgb(150, 150, 150); _hover = Color.FromArgb(233, 233, 233);
                _separator = Color.FromArgb(224, 224, 224); _check = Color.FromArgb(26, 26, 26);
            }
            else
            {
                _back = Color.FromArgb(44, 44, 44); _text = Color.FromArgb(242, 242, 242); _subText = Color.FromArgb(170, 170, 170);
                _disabled = Color.FromArgb(120, 120, 120); _hover = Color.FromArgb(61, 61, 61);
                _separator = Color.FromArgb(64, 64, 64); _check = Color.FromArgb(255, 212, 59); // 카카오 옐로
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(_back)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { } // 테두리는 Windows(DWM)가 둥글게 그린다

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled || HeaderTag.Equals(e.Item.Tag)) return;
            var r = new RectangleF(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            using (var b = new SolidBrush(_hover))
            {
                TrayIconPainter.AddRoundRect(path, r.X, r.Y, r.Width, r.Height, 4);
                e.Graphics.FillPath(b, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (HeaderTag.Equals(e.Item.Tag))
            {
                // 머리글: 1줄 제목(굵게) + 2줄 상태
                string[] lines = e.Text.Split('\n');
                using (var bold = new Font(e.TextFont, FontStyle.Bold))
                {
                    var r = e.TextRectangle;
                    int lineH = TextRenderer.MeasureText("가", bold).Height;
                    int top = (e.Item.Height - lineH * lines.Length) / 2;
                    TextRenderer.DrawText(e.Graphics, lines[0], bold, new Point(r.Left, top), _text, TextFormatFlags.NoPrefix);
                    if (lines.Length > 1)
                        TextRenderer.DrawText(e.Graphics, lines[1], e.TextFont, new Point(r.Left, top + lineH), _subText, TextFormatFlags.NoPrefix);
                }
                return;
            }
            e.TextColor = e.Item.Enabled ? _text : _disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float s = Math.Min(r.Width, r.Height);
            float x = r.Left + (r.Width - s) / 2, y = r.Top + (r.Height - s) / 2;
            using (var pen = new Pen(_check, Math.Max(1.6f, s / 9f)))
            {
                pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                e.Graphics.DrawLines(pen, new[] { new PointF(x + s * 0.22f, y + s * 0.52f), new PointF(x + s * 0.42f, y + s * 0.72f), new PointF(x + s * 0.78f, y + s * 0.30f) });
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(_separator)) e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { }
    }

    #endregion

    #region 트레이 앱

    internal sealed class TrayApp : ApplicationContext
    {
        public const string Version        = "1.2.0";
        public const string InfoUrl        = "https://youngsam.net/programs";
        public const string ShowSignalName = "TalkShield_ShowIcon";

        private const string GithubOwner = "openr03";
        private const string GithubRepo  = "TalkShield";

        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly AdBlocker _blocker = new AdBlocker();         // 기존 카카오톡 (KakaoTalk.exe)
        private readonly BetaAdBlocker _beta = new BetaAdBlocker();    // 카카오톡 베타 (KakaoTalkUI.exe)
        private readonly ToolStripMenuItem _miEnabled, _miHeader;
        private readonly ContextMenuStrip _menu;
        private readonly ModernMenuRenderer _renderer;
        private Icon _iconOn, _iconOff;
        private bool _taskbarLight, _appsLight;
        private readonly EventWaitHandle _showSignal;
        private readonly SynchronizationContext _ui;

        private const int FirstTrimMs    = 10 * 1000;
        private const int TrimIntervalMs = 5 * 60 * 1000;

        private bool _enabled;
        private long _total;
        private int  _lastTrim = Environment.TickCount;
        private bool _trimmedOnce;
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
            _taskbarLight = WinTheme.TaskbarLight;
            _appsLight    = WinTheme.AppsLight;
            CreateIcons();

            _miHeader  = new ToolStripMenuItem { Tag = ModernMenuRenderer.HeaderTag, Enabled = false };
            _miEnabled = new ToolStripMenuItem("광고 차단", null, OnToggleEnabled) { Checked = _enabled };
            var miAutostart = new ToolStripMenuItem("시작 시 자동 실행", null, OnToggleAutostart) { Checked = Settings.Autostart };

            _renderer = new ModernMenuRenderer(_appsLight);
            _menu = new ContextMenuStrip
            {
                Renderer = _renderer,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 9.5f),
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Padding = new Padding(0, 6, 0, 6),
                BackColor = _renderer.Back
            };
            _menu.Items.Add(_miHeader);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miEnabled);
            _menu.Items.Add(miAutostart);
            _menu.Items.Add(new ToolStripMenuItem("트레이 아이콘 숨기기", null, OnHideIcon));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("정보", null, OnInfo));
            _menu.Items.Add(new ToolStripMenuItem("종료", null, OnExit));
            foreach (ToolStripItem item in _menu.Items)
                if (item is ToolStripMenuItem) item.Padding = new Padding(4, 3, 18, 3);
            _menu.Opening += delegate { ApplyTheme(); UpdateHeader(); };
            _menu.HandleCreated += delegate { Native.SetDwm(_menu.Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, 2); }; // Windows 11 둥근 모서리
            UpdateHeader();

            _tray = new NotifyIcon
            {
                Icon = _enabled ? _iconOn : _iconOff,
                Visible = !Settings.IconHidden, // 저장된 숨김 상태를 반영
                ContextMenuStrip = _menu
            };
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
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
                // 예기치 못한 창 상태에도 앱은 죽지 않는다. 두 엔진은 서로 독립적으로 동작한다.
                try { _blocker.Tick(); } catch { }
                try { _beta.Tick(); } catch { }
                int blocked = _blocker.BlockedThisTick + _beta.BlockedThisTick;
                if (blocked > 0)
                {
                    _total += blocked;
                    UpdateTooltip();
                    if ((DateTime.UtcNow - _lastSave).TotalSeconds > 10) SaveTotal();
                }
            }

            // CPU 절약: 카카오톡이 없으면 검사 주기를 늦춘다
            int want = _blocker.KakaoRunning || _beta.KakaoRunning ? 100 : 1500;
            if (_timer.Interval != want) _timer.Interval = want;

            TrimMemoryIfDue();
        }

        // 메모리 절약: 시작 직후(초기화·업데이트 확인에만 쓰인 메모리)와 이후 주기적으로 작업 집합을 비워
        // 실제로 계속 쓰는 페이지만 남긴다. 다시 필요한 페이지는 Windows 가 곧바로 되돌려 준다.
        private void TrimMemoryIfDue()
        {
            int now = Environment.TickCount;
            int due = _trimmedOnce ? TrimIntervalMs : FirstTrimMs;
            if (now - _lastTrim < due) return;
            _lastTrim = now;
            _trimmedOnce = true;
            try { Native.SetProcessWorkingSetSize(Native.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1)); } catch { }
        }

        // ----- 메뉴/트레이 이벤트 -----

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _enabled = !_enabled;
            Settings.Enabled = _enabled;
            if (!_enabled)
            {
                RestoreBeta(); // 베타는 창 모양을 바꿔두므로 끌 때 원래대로 되돌린다
                try { _blocker.Stop(); } catch { }
            }
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
            OpenUrl(InfoUrl);
        }

        // Windows 테마(다크/라이트)를 바꾸면 아이콘과 메뉴가 바로 따라간다.
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle) ApplyTheme();
        }

        private void ApplyTheme()
        {
            bool taskbarLight = WinTheme.TaskbarLight, appsLight = WinTheme.AppsLight;
            if (taskbarLight != _taskbarLight)
            {
                _taskbarLight = taskbarLight;
                Icon oldOn = _iconOn, oldOff = _iconOff;
                CreateIcons();
                _tray.Icon = _enabled ? _iconOn : _iconOff;
                oldOn.Dispose();
                oldOff.Dispose();
            }
            if (appsLight != _appsLight)
            {
                _appsLight = appsLight;
                _renderer.SetTheme(appsLight);
                _menu.BackColor = _renderer.Back;
                _menu.Invalidate();
            }
        }

        private void CreateIcons()
        {
            _iconOn  = TrayIconPainter.Create(true, _taskbarLight);
            _iconOff = TrayIconPainter.Create(false, _taskbarLight);
        }

        private void UpdateHeader()
        {
            _miHeader.Text = string.Format("TalkShield {0}\n{1} · 차단 {2:N0}건", Version, _enabled ? "켜짐" : "꺼짐", _total);
        }

        private void OnBalloonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateUrl)) OpenUrl(_updateUrl);
        }

        private void OnExit(object sender, EventArgs e)
        {
            _timer.Stop();
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            RestoreBeta();
            try { _blocker.Stop(); } catch { }
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

        internal void RestoreBeta()
        {
            try { _beta.Restore(); } catch { }
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

                // 예기치 못한 오류에도 오류 창을 띄우거나 종료하지 않는다. (창 프로시저·이벤트 콜백 포함)
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate { };

                var app = new TrayApp();
                // 그래도 프로세스가 죽게 되면(다른 스레드의 치명적 오류) 카카오톡 창만은 원래대로 되돌려 둔다.
                AppDomain.CurrentDomain.UnhandledException += delegate { app.RestoreBeta(); };
                Application.Run(app);
            }
        }
    }
}
