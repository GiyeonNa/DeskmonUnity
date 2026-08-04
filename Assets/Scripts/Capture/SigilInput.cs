using System.Collections.Generic;
using UnityEngine;
using Deskmon.Native;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인 입력. overlay.html의 mousedown/mousemove/mouseup 처리 이식.
    ///
    /// 클릭통과와의 관계가 핵심이다:
    ///   평소 이 창은 클릭통과 상태라 마우스를 받지 못한다. 야생 근처에 커서가 가야
    ///   DesktopOverlay가 통과를 풀고, 그때 비로소 클릭이 들어온다.
    ///   각인이 시작되면(Engaged) 화면 어디서든 그릴 수 있어야 하므로
    ///   DesktopOverlay.captureAll을 켜서 전체 화면 입력을 받는다.
    ///   overlay.html:252 "if(wild.engaged||wild.drawing) return true"와 같은 규칙이다.
    /// </summary>
    [RequireComponent(typeof(SigilCapture))]
    public class SigilInput : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("야생의 트랜스폼. 비면 이 오브젝트를 쓴다.")]
        public Transform wildTarget;

        [Tooltip("이 반경 안을 눌러야 각인이 시작된다. overlay.html: 40px")]
        public float engageRadius = 40f;

        SigilCapture _capture;
        Camera _cam;
        bool _appliedCaptureAll;

        /// <summary>
        /// 입력을 삼킬 화면 영역 (스크린 좌표, 좌하단 원점).
        ///
        /// 각인 중에는 화면 어디를 눌러도 획이 시작되는데, 그러면 그 위에 얹힌 버튼을
        /// 누를 때도 획이 그어진다. 테스트 하네스처럼 UI를 겹쳐 두는 쪽이 여기에
        /// 자기 영역을 등록해 두면 그 안의 클릭은 무시한다.
        /// </summary>
        static readonly List<Rect> BlockedRects = new List<Rect>();

        public static void BlockArea(Rect screenRect)
        {
            if (!BlockedRects.Contains(screenRect)) BlockedRects.Add(screenRect);
        }

        public static void UnblockArea(Rect screenRect) => BlockedRects.Remove(screenRect);

        static bool IsBlocked(Vector2 screenPos)
        {
            for (int i = 0; i < BlockedRects.Count; i++)
                if (BlockedRects[i].Contains(screenPos)) return true;
            return false;
        }

        void Awake()
        {
            _capture = GetComponent<SigilCapture>();
            if (wildTarget == null) wildTarget = transform;
        }

        void OnDisable() => ReleaseCaptureAll();
        void OnDestroy() => ReleaseCaptureAll();

        void Update()
        {
            SyncCaptureAll();

            Vector2 cursor = NativeCursor.GetPositionInWindow();

            if (Input.GetMouseButtonDown(0)) OnPress(cursor);
            else if (Input.GetMouseButton(0)) OnDrag(cursor);
            else if (Input.GetMouseButtonUp(0)) OnRelease();
        }

        /// <summary>
        /// 각인 중에는 화면 전체가 입력을 받아야 한다.
        /// 켠 사람이 끄는 책임도 진다 - 다른 곳에서 captureAll을 쓰더라도
        /// 우리가 켠 경우에만 되돌린다.
        /// </summary>
        void SyncCaptureAll()
        {
            var overlay = DesktopOverlay.Instance;
            if (overlay == null) return;

            bool want = _capture.Engaged || _capture.Drawing;
            if (want == _appliedCaptureAll) return;

            overlay.captureAll = want;
            _appliedCaptureAll = want;
        }

        void ReleaseCaptureAll()
        {
            if (!_appliedCaptureAll) return;
            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.captureAll = false;
            _appliedCaptureAll = false;
        }

        void OnPress(Vector2 cursor)
        {
            // UI가 덮고 있는 자리면 획을 시작하지 않는다 (버튼 클릭이 선으로 남는 것 방지)
            if (IsBlocked(cursor)) return;

            // 이미 각인 중이면 화면 어디서 눌러도 획이 시작된다. overlay.html:291-293
            if (_capture.Engaged)
            {
                _capture.BeginStroke(cursor);
                return;
            }

            // 아직이면 야생 근처를 눌러야 시작된다
            if (Vector2.Distance(cursor, WildScreenPos()) <= engageRadius)
            {
                _capture.Engage();
                _capture.BeginStroke(cursor);
            }
        }

        void OnDrag(Vector2 cursor)
        {
            if (_capture.Drawing) _capture.AddPoint(cursor);
        }

        void OnRelease()
        {
            if (_capture.Drawing) _capture.EndStroke();
        }

        Vector2 WildScreenPos()
        {
            if (_cam == null) _cam = Camera.main;
            return _cam != null ? (Vector2)_cam.WorldToScreenPoint(wildTarget.position) : Vector2.zero;
        }
    }
}
