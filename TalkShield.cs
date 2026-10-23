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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

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
        private const string ProcessName       = "KakaoTalkUI";
        private const int    PidScanIntervalMs = 1000;
        private const int    PassIntervalMs    = 250; // 정기 점검 주기 (변화는 이벤트로 즉시 처리)
        private const int    MinAdHeight       = 40;  // 하단 배너로 인정할 높이 범위(px, 96dpi 기준)
        private const int    MaxAdHeight       = 200;
        private const int    CornerDiameter    = 16;  // Windows 11 둥근 모서리(반경 8px)
        private const int    UnclipGraceMs     = 400; // 광고가 사라진 뒤 원복까지 기다리는 시간
        private const int    CoverGraceMs      = 350; // 크기 변경이 멈춘 뒤 덮개를 유지하는 시간
        private const int    CoverOverlap      = 3;   // 크기 조절 중 덮개를 위로 겹치는 높이(px, 96dpi 기준)
        private const int    ResizeEdgeSlop    = 6;   // 크기 조절로 볼 가장자리 범위(테두리 두께 + px, 96dpi 기준)
        private const int    CutHistoryMs      = 250; // 덮개 윗변을 정할 때 돌아볼 시간
        private const int    ColorRefreshMs    = 5000;  // 덮개 색을 다시 뽑는 주기 (테마 변경 대응)
        private const int    SidebarWidth      = 68;   // 색을 뽑기 전 기본 사이드바 폭(px, 96dpi 기준)

        private readonly HashSet<uint>   _pids       = new HashSet<uint>();
        private readonly List<IntPtr>    _topWindows = new List<IntPtr>(32);
        private readonly List<IntPtr>    _children   = new List<IntPtr>(16);

        private readonly Dictionary<IntPtr, int> _adWindows = new Dictionary<IntPtr, int>(); // 투명 처리한 광고 창 → 원래 확장 스타일
        private readonly HashSet<IntPtr> _adOwners   = new HashSet<IntPtr>(); // 광고가 확인된 메인 창
        private readonly Dictionary<IntPtr, int>         _clipped = new Dictionary<IntPtr, int>();         // 메인 창 → 잘라낸 높이
        private readonly Dictionary<IntPtr, int>         _wanted  = new Dictionary<IntPtr, int>();         // 이번 틱: 메인 창 → 광고 높이
        private readonly Dictionary<IntPtr, int>         _lastWanted = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막으로 광고가 보인 시각
        private readonly Dictionary<IntPtr, int>         _adHeight   = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막 광고 높이
        private readonly Dictionary<IntPtr, IntPtr>      _adOf       = new Dictionary<IntPtr, IntPtr>();   // 메인 창 → 광고 창
        private readonly Dictionary<IntPtr, ResizeGrip>  _grips      = new Dictionary<IntPtr, ResizeGrip>();
        private readonly Dictionary<IntPtr, BottomCover> _covers     = new Dictionary<IntPtr, BottomCover>();
        private readonly Dictionary<IntPtr, long>        _lastSize   = new Dictionary<IntPtr, long>();     // 메인 창 → 마지막 크기(w,h)
        private readonly Dictionary<IntPtr, int>         _sizedAt    = new Dictionary<IntPtr, int>();      // 메인 창 → 마지막 크기 변경 시각
        private readonly HashSet<IntPtr>                 _resizing   = new HashSet<IntPtr>();              // 크기 조절 중인 메인 창
        private readonly Dictionary<IntPtr, List<CutSample>> _cutHistory = new Dictionary<IntPtr, List<CutSample>>(); // 메인 창 → 최근 경계
        private readonly Dictionary<IntPtr, CoverColors> _colors     = new Dictionary<IntPtr, CoverColors>();  // 메인 창 → 덮개 색
        private readonly List<IntPtr>                    _hooks      = new List<IntPtr>();

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
                SetResizing(h, false);
                return;
            }
            // 이동인지 크기 조절인지는 커서가 창 가장자리 근처인지로 구분한다.
            // (제목줄 드래그는 가장자리에서 떨어진 곳에서 시작한다. 잘못 판단해도 덮개가 잠시 보일 뿐이다.)
            IntPtr oldCtx = EnterDpiAware();
            try
            {
                Native.POINT p;
                Native.RECT win;
                if (!Native.GetCursorPos(out p) || !Native.GetWindowRect(h, out win)) return;
                int edge = FrameThickness(h) + (int)Math.Round(ResizeEdgeSlop * Scale(h));
                bool nearEdge = p.Y - win.Top < edge || p.X - win.Left < edge || win.Right - p.X < edge || win.Bottom - p.Y < edge;
                if (nearEdge) SetResizing(h, true);
            }
            finally
            {
                LeaveDpiAware(oldCtx);
            }
        }

        private void SetResizing(IntPtr main, bool on)
        {
            if (on) _resizing.Add(main);
            else
            {
                _resizing.Remove(main);
                _sizedAt[main] = Environment.TickCount; // 끝난 뒤에도 잠시 덮개를 유지해 마지막 초기화까지 가린다
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
            if (now - _lastPass < PassIntervalMs) return;
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

            var found = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try { found.Add((uint)p.Id); } catch { }
                p.Dispose();
            }
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
        }

        private void Unhook()
        {
            foreach (var hook in _hooks) Native.UnhookWinEvent(hook);
            _hooks.Clear();
        }

        private void ProcessCandidate(IntPtr h)
        {
            IntPtr owner = Native.GetWindow(h, Native.GW_OWNER);
            if (owner == IntPtr.Zero || !IsKakaoWindow(owner)) return;
            if (!IsQtWindow(h)) return;

            if (!_adWindows.ContainsKey(h))
            {
                if (!LooksLikeBanner(h, owner)) return;
                // 처음 보는 메인 창이면 WebView2 의 광고 제목으로 확인. 이후 재생성된 광고 창은 모양만으로 판별.
                if (!_adOwners.Contains(owner) && !HasAdWebView(h)) return;

                // 원래 스타일 저장. 이전 실행이 비정상 종료돼 남긴 투명 비트는 빼고 저장한다.
                _adWindows[h] = Native.GetWindowLong(h, Native.GWL_EXSTYLE) & ~(Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT);
                _adOwners.Add(owner);
                BlockedThisTick++;
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
            string cls = Native.ClassName(h);
            return cls.StartsWith("Qt", StringComparison.Ordinal) && cls.IndexOf("QWindow", StringComparison.Ordinal) > 0;
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
                var stale = new List<IntPtr>();
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
            int f      = FrameThickness(main);
            int w      = win.Right - win.Left;
            int left   = f;
            int top    = 0;
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
            if (bottom - top < adHeight || right - left < 100) return; // 비정상적인 값이면 건드리지 않는다

            _adHeight[main] = adHeight;
            UpdateGrip(main, win, f, bottom);
            UpdateCover(main, win, f, bottom);

            int key = (right << 16) ^ bottom;
            int cur;
            bool same = _clipped.TryGetValue(main, out cur) && cur == key;
            Native.RECT box;
            if (same && Native.GetWindowRgnBox(main, out box) > Native.NULLREGION &&
                Math.Abs(box.Right - right) <= 1 && Math.Abs(box.Bottom - bottom) <= 1) return;

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

        private void UnclipMain(IntPtr main)
        {
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
        private void UpdateGrip(IntPtr main, Native.RECT win, int frame, int cut)
        {
            ResizeGrip grip;
            if (Native.IsZoomed(main) || Native.IsIconic(main))
            {
                if (_grips.TryGetValue(main, out grip)) grip.Hide();
                return;
            }
            if (!_grips.TryGetValue(main, out grip))
            {
                grip = new ResizeGrip(main, ResizeFromGrip, SetResizing); // DPI 인식 컨텍스트에서 생성해 카카오톡과 같은 좌표계를 쓴다
                _grips[main] = grip;
            }
            grip.Update(win, frame, cut, (int)Math.Round(CornerDiameter * Scale(main)));
        }

        // 덮개가 필요한 때: 최대화(아래쪽 틈 메우기), 크기 조절 중, 크기가 바뀐 직후(마지막 영역 초기화까지)
        private void UpdateCover(IntPtr main, Native.RECT win, int frame, int cut)
        {
            int now = Environment.TickCount;
            long size = ((long)(win.Right - win.Left) << 32) | (uint)(win.Bottom - win.Top);
            long last;
            if (_lastSize.TryGetValue(main, out last) && last != size) _sizedAt[main] = now;
            _lastSize[main] = size;

            int sizedAt;
            bool zoomed = Native.IsZoomed(main);
            bool need = !Native.IsIconic(main) &&
                        (zoomed || _resizing.Contains(main) ||
                         (_sizedAt.TryGetValue(main, out sizedAt) && now - sizedAt < CoverGraceMs));

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
            if (_adWindows.Count > 0)
            {
                var dead = new List<IntPtr>();
                foreach (var h in _adWindows.Keys) if (!Native.IsWindow(h)) dead.Add(h);
                foreach (var h in dead) _adWindows.Remove(h);
            }
            _adOwners.RemoveWhere(h => !Native.IsWindow(h));
        }

        private void ForgetAll()
        {
            foreach (var grip in _grips.Values) grip.DestroyHandle();
            _grips.Clear();
            foreach (var cover in _covers.Values) cover.DestroyHandle();
            _covers.Clear();
            _lastSize.Clear();
            _sizedAt.Clear();
            _resizing.Clear();
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
        private int _x, _y, _w, _cut, _frame, _corner;        // 메인 창 기준 좌표
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
        public void Update(Native.RECT win, int frame, int cut, int corner)
        {
            int w = win.Right - win.Left;
            bool geometryChanged = w != _w || cut != _cut || frame != _frame || corner != _corner;
            bool placed = _visible && win.Left == _x && win.Top == _y && !geometryChanged &&
                          Native.GetWindow(_target, Native.GW_HWNDNEXT) == Handle;
            if (placed) return;

            _x = win.Left; _y = win.Top; _w = w; _cut = cut; _frame = frame; _corner = corner;

            if (geometryChanged || !_visible)
            {
                // 좌측 띠 + 우측 띠 + 하단 띠 (모두 카카오톡의 보이는 영역 바깥)
                IntPtr rgn = Native.CreateRectRgn(0, 0, frame, cut + frame);
                IntPtr r2  = Native.CreateRectRgn(w - frame, 0, w, cut + frame);
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

    #region 트레이 앱

    internal sealed class TrayApp : ApplicationContext
    {
        public const string Version        = "1.2.0";
        public const string BlogUrl        = "https://youngsam.net";
        public const string ShowSignalName = "TalkShield_ShowIcon";

        private const string GithubOwner = "openr03";
        private const string GithubRepo  = "TalkShield";

        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly AdBlocker _blocker = new AdBlocker();         // 기존 카카오톡 (KakaoTalk.exe)
        private readonly BetaAdBlocker _beta = new BetaAdBlocker();    // 카카오톡 베타 (KakaoTalkUI.exe)
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
        }

        // ----- 메뉴/트레이 이벤트 -----

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _enabled = !_enabled;
            Settings.Enabled = _enabled;
            if (!_enabled) RestoreBeta(); // 베타는 창 모양을 바꿔두므로 끌 때 원래대로 되돌린다
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
            RestoreBeta();
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

        private void RestoreBeta()
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
