using System.Collections.Generic;
using UnityEngine;
using Deskmon.Native;

namespace Deskmon
{
    /// <summary>
    /// 데스크탑 오버레이 무대. overlay.html의 창 제어 부분에 대응.
    ///
    /// 책임:
    ///   - 창을 투명 클릭통과 오버레이로 전환 (WindowController.Apply)
    ///   - 커서 근접 판정으로 클릭통과를 동적 토글 (overlay.html interactiveNow)
    ///   - 전체화면 앱 감지 시 자동 숨김 (기획서 §6.1)
    ///   - 항상위 주기적 재확보
    ///
    /// 클릭통과 규칙 (overlay.html:250-258 이식):
    ///   야생이 살아있고 각인 중이면      → 전체 화면 입력 수신
    ///   그 외에는 등록된 인터랙티브 대상 반경 안일 때만 수신
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DesktopOverlay : MonoBehaviour
    {
        public static DesktopOverlay Instance { get; private set; }

        [Header("클릭통과")]
        [Tooltip("커서가 이 반경 안에 들어오면 입력을 받는다 (overlay.html: roamer 60px)")]
        public float defaultInteractRadius = 60f;

        [Tooltip("true면 화면 전체에서 입력을 받는다 (각인 미니게임 중)")]
        public bool captureAll;

        [Header("항상위 / 전체화면 감지")]
        public float topmostInterval = 2f;
        public bool autoHideOnFullscreen = true;

        [Header("디버그")]
        public bool logStateChanges;

        // 커서 근접 시 입력을 받아야 하는 대상들
        readonly List<IInteractive> _targets = new List<IInteractive>();

        bool _hiddenByFullscreen;
        float _topmostT;
        int _lastW, _lastH;
        Killswitch _killswitch;

        public bool ClickThrough => WindowController.IsClickThrough;

        void Awake()
        {
            Instance = this;
            _killswitch = FindFirstObjectByType<Killswitch>();
            if (_killswitch == null)
                Debug.LogWarning("[DesktopOverlay] Killswitch가 없습니다 — 전역 종료 핫키가 동작하지 않습니다.");

            // 투명 창의 필수 조건: 카메라가 알파 0으로 클리어해야 한다.
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.allowHDR = false;   // HDR 버퍼는 알파를 보존하지 않는 경우가 있다
            }
            else
            {
                Debug.LogError("[DesktopOverlay] Camera.main 없음 — 투명 배경을 설정할 수 없습니다.");
            }

            Application.runInBackground = true;
        }

        void Start()
        {
            WindowController.Apply();
            FitToScreen();
        }

        void FitToScreen()
        {
            var res = Screen.currentResolution;
            _lastW = res.width;
            _lastH = res.height;
            WindowController.FitTo(0, 0, res.width, res.height);
        }

        void Update()
        {
            // 워치독에게 "렌더 루프 정상"을 알린다. 이게 끊기면 Killswitch가 앱을 종료한다.
            if (_killswitch != null) _killswitch.Heartbeat();

            // 해상도/모니터 구성이 바뀌면 창을 다시 맞춘다 (DPI 변경 대응)
            var res = Screen.currentResolution;
            if (res.width != _lastW || res.height != _lastH) FitToScreen();

            // 전체화면 앱 감지 → 자동 숨김
            if (autoHideOnFullscreen)
            {
                bool fs = WindowController.IsForegroundFullscreen();
                if (fs != _hiddenByFullscreen)
                {
                    _hiddenByFullscreen = fs;
                    SetVisible(!fs);
                    if (logStateChanges) Debug.Log($"[DesktopOverlay] 전체화면 앱 {(fs ? "감지 → 숨김" : "해제 → 복귀")}");
                }
            }
            if (_hiddenByFullscreen)
            {
                WindowController.SetClickThrough(true);
                return;
            }

            UpdateClickThrough();

            // 항상위 재확보
            _topmostT += Time.unscaledDeltaTime;
            if (_topmostT >= topmostInterval)
            {
                _topmostT = 0f;
                WindowController.RestoreTopmost();
            }
        }

        /// <summary>overlay.html의 interactiveNow(x,y) + mousemove 디바운스에 대응.</summary>
        void UpdateClickThrough()
        {
            bool interactive = captureAll || IsCursorNearAnyTarget();
            bool want = !interactive;

            if (want != WindowController.IsClickThrough)
            {
                WindowController.SetClickThrough(want);
                if (logStateChanges) Debug.Log($"[DesktopOverlay] 클릭통과 → {want}");
            }
        }

        bool IsCursorNearAnyTarget()
        {
            // 클릭통과 상태에서는 Unity가 마우스 위치를 못 받으므로 OS 커서를 직접 읽는다.
            Vector2 cursor = NativeCursor.GetPositionInWindow();

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var t = _targets[i];
                if (t == null || !t.IsActive) { _targets.RemoveAt(i); continue; }
                float r = t.InteractRadius > 0 ? t.InteractRadius : defaultInteractRadius;
                if ((t.ScreenPosition - cursor).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        void SetVisible(bool v)
        {
            var cam = Camera.main;
            if (cam != null) cam.enabled = v;
        }

        public void Register(IInteractive t)
        {
            if (!_targets.Contains(t)) _targets.Add(t);
        }

        public void Unregister(IInteractive t) => _targets.Remove(t);
    }

    /// <summary>커서가 가까이 오면 클릭통과를 해제해야 하는 대상.</summary>
    public interface IInteractive
    {
        /// <summary>스크린 좌표 (좌하단 원점, Unity Input 좌표계).</summary>
        Vector2 ScreenPosition { get; }
        float InteractRadius { get; }
        bool IsActive { get; }
    }
}
