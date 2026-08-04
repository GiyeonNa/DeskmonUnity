using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Kirurobo;

namespace Deskmon.Native
{
    /// <summary>
    /// 투명·항상위·클릭통과 오버레이 창.
    ///
    /// 창 제어 자체는 UniWindowController(Kirurobo)에 위임한다.
    /// 직접 Win32(SetWindowLong + DwmExtendFrameIntoClientArea)를 때리는 방식은
    /// 아래 이유로 폐기했다:
    ///   - 플립 모델 스왑체인과 조합되면 알파가 버려져 창이 새까맣게 나온다
    ///   - WS_EX_NOACTIVATE를 직접 걸면 창이 포커스를 못 받아 키 입력으로 종료할 수 없다
    ///   - Unity가 창 스타일을 임의 시점에 재적용해 설정이 되돌아간다
    /// UniWinC는 이 문제들을 내부에서 처리하고, 같은 조합으로 이미 출시된 사례가 있다.
    ///
    /// 이 클래스는 기존 호출부를 그대로 두기 위한 얇은 파사드다.
    ///
    /// 전제: 씬에 UniWindowController 컴포넌트가 배치돼 있어야 한다.
    ///       런타임 AddComponent는 부착 타이밍 문제로 빌드에서 투명이 안 먹는 사례가 있어
    ///       반드시 씬에 미리 배치한다 (UniWinC 공식 권장).
    /// </summary>
    public static class WindowController
    {
        static UniWindowController _uni;
        static bool _clickThrough;
        static bool _applied;

        public static bool IsClickThrough => _clickThrough;
        /// <summary>스파이크 판정용 — 투명 창 설정이 실제로 적용됐는지.</summary>
        public static bool IsApplied => _applied;
        public static UniWindowController Uni => _uni;

        /// <summary>
        /// 씬의 UniWindowController를 찾아 오버레이 모드로 설정한다. Start()에서 1회 호출.
        /// </summary>
        public static void Apply()
        {
            _uni = UnityEngine.Object.FindFirstObjectByType<UniWindowController>();
            if (_uni == null)
            {
                Debug.LogError("[WindowController] 씬에 UniWindowController가 없습니다 — 투명 창이 적용되지 않습니다. " +
                               "[Deskmon/S0 스파이크 씬 생성]으로 씬을 다시 만드세요.");
                _applied = false;
                return;
            }

            _uni.isTransparent = true;
            _uni.isTopmost = true;

            // 히트테스트는 UniWinC에게 맡기지 않는다.
            // 우리는 크리처 반경 기준으로 직접 판정하므로 (DesktopOverlay.IsCursorNearAnyTarget)
            // UniWinC의 자동 판정이 켜져 있으면 서로 덮어써서 클릭통과가 떨린다.
            _uni.isHitTestEnabled = false;

            // 에디터에서는 게임 뷰가 별도 창이 아니라 투명이 성립하지 않는다 (의도된 동작).
            _applied = !Application.isEditor;

            SetClickThrough(true);   // 기본은 통과
            if (_applied) Debug.Log("[WindowController] 투명 오버레이 적용 완료 (UniWinC).");
        }

        /// <summary>
        /// 클릭통과 on/off. Electron의 setIgnoreMouseEvents(ignore)와 1:1.
        /// 값이 바뀔 때만 실제로 적용한다.
        /// </summary>
        public static void SetClickThrough(bool on)
        {
            if (_clickThrough == on && _applied) return;
            _clickThrough = on;
            if (_uni != null) _uni.isClickThrough = on;
        }

        /// <summary>창을 지정 화면 영역에 맞춘다. 멀티모니터/DPI 변경 시 재호출.</summary>
        public static void FitTo(int x, int y, int w, int h)
        {
            if (_uni == null || Application.isEditor) return;

            // Screen.SetResolution은 쓰지 않는다 — Unity가 창 스타일을 표준 창으로 되돌려
            // UniWinC의 테두리 제거/항상위가 덮어써진다 (빌드에서 테두리가 다시 생기는 원인).
            _uni.windowSize = new Vector2(w, h);
            // UniWinC 좌표는 좌하단 원점. 좌상단 원점 (x,y)에서 변환한다.
            _uni.windowPosition = new Vector2(x, Screen.currentResolution.height - y - h);
        }

        /// <summary>항상위 재확보. 다른 topmost 창에 밀렸을 때 주기적으로 호출한다.</summary>
        public static void RestoreTopmost()
        {
            if (_uni == null) return;
            _uni.isTopmost = true;   // UniWinC setter는 같은 값도 다시 적용한다
        }

        // ── 전체화면 앱 감지 (UniWinC에 대응 기능이 없어 직접 구현) ──

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int left, top, right, bottom; }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();

        /// <summary>
        /// 현재 포그라운드 창이 화면 전체를 덮고 있는지 (전체화면 게임/발표 감지).
        /// 기획서 §6.1 "전체화면 앱 감지 시 자동 숨김"의 판정부.
        /// </summary>
        public static bool IsForegroundFullscreen()
        {
            if (Application.isEditor) return false;

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            // 바탕화면/셸 자체는 전체화면 앱이 아니다
            if (fg == GetDesktopWindow() || fg == GetShellWindow()) return false;
            if (!GetWindowRect(fg, out RECT r)) return false;

            int w = r.right - r.left, h = r.bottom - r.top;
            return w >= Screen.currentResolution.width && h >= Screen.currentResolution.height;
        }
    }
}
