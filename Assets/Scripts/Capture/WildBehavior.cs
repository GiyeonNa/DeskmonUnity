using UnityEngine;
using Deskmon.Core;
using Deskmon.Native;

namespace Deskmon.Capture
{
    /// <summary>
    /// 야생 개체의 접근 난이도. overlay.html updatePattern() 이식.
    ///
    /// 상세기획 §12에서 포획 자체는 각인 그리기로 통합됐지만, 각인을 시작하려면
    /// 먼저 야생 근처를 눌러야 한다. 겁쟁이는 도망가고 순간이동형은 사라지므로
    /// 여기가 "다가가는 미니게임"으로 남는다 (§3.3).
    ///
    /// 각인 중(Engaged)에는 움직이지 않는다 - 그리는 동안 도망가면 그릴 수가 없다.
    /// </summary>
    public class WildBehavior : MonoBehaviour
    {
        [Header("데이터")]
        public SpeciesData species;
        public SigilCapture capture;

        [Header("겁쟁이 (§3.3)")]
        [Tooltip("이 거리 안으로 커서가 들어오면 도주")]
        public float fleeRadius = 150f;
        [Tooltip("도주 속도 px/s")]
        public float fleeSpeed = 185f;
        [Tooltip("이 시간 이상 도망가면 헐떡인다")]
        public float fleeLimit = 4f;
        [Tooltip("헐떡이는 시간 - 무방비 상태, 여기가 노림수다")]
        public float pantDuration = 2.5f;

        [Header("순간이동")]
        public Vector2 blinkInterval = new Vector2(3f, 5f);
        public float fadeDuration = 0.3f;

        [Header("이동")]
        public float calmSpeed = 60f;
        public float hopSpeed = 85f;
        public float shySpeed = 70f;
        public float blinkSpeed = 55f;
        public float driftSpeed = 40f;
        public float arriveDist = 12f;

        [Header("체류")]
        [Tooltip("true면 잡히기 전까지 떠나지 않는다. 원본의 체류시간 퇴장을 대체하는 " +
                 "2026-08-07 결정 - 스카우트(방치 자동 포획)를 보류하는 대신, 놓친 출몰이 " +
                 "손실이 되지 않도록 야생이 화면에 남아 계속 돌아다닌다.")]
        public bool stayUntilCaught = true;

        [Tooltip("남은 체류 시간. stayUntilCaught가 꺼져 있을 때만 쓴다 (원본 동작).")]
        public float stayTime = 50f;

        /// <summary>헐떡이는 중 = 무방비. 연출에서 읽는다.</summary>
        public bool IsPanting => _pantT > 0f;

        /// <summary>페이드 알파. 순간이동형이 사라질 때 쓴다.</summary>
        public float Alpha { get; private set; } = 1f;

        /// <summary>체류시간 만료로 떠남.</summary>
        public event System.Action OnLeave;

        Camera _cam;
        Vector2 _target;
        float _fleeT, _pantT, _blinkT, _fadingT;
        int _dir = 1;

        void Start()
        {
            _cam = Camera.main;
            _blinkT = Random.Range(blinkInterval.x, blinkInterval.y);
            Alpha = species != null && species.pattern == BehaviorPattern.Blink ? 0f : 1f;
            PickTarget();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            // 체류시간 퇴장은 원본 동작이고, 기본은 "잡힐 때까지 남는다"(stayUntilCaught).
            // 떠나는 것은 각인 중이 아닐 때만 (그리는 도중 사라지면 조작이 허공을 친다).
            if (!stayUntilCaught)
            {
                stayTime -= dt;
                if (stayTime <= 0f && (capture == null || !capture.Engaged))
                {
                    OnLeave?.Invoke();
                    return;
                }
            }

            // 각인 중에는 정지. overlay.html:352
            if (capture != null && capture.Engaged) return;

            switch (species != null ? species.pattern : BehaviorPattern.Calm)
            {
                case BehaviorPattern.Shy:   UpdateShy(dt);   break;
                case BehaviorPattern.Blink: UpdateBlink(dt); break;
                case BehaviorPattern.Drift: WalkToTarget(dt, driftSpeed); break;
                default:
                    WalkToTarget(dt, species != null && species.hop ? hopSpeed : calmSpeed);
                    break;
            }
        }

        /// <summary>겁쟁이 - 커서가 가까우면 반대로 도망, 오래 도망가면 헐떡인다.</summary>
        void UpdateShy(float dt)
        {
            if (_pantT > 0f)
            {
                // 무방비 구간. 여기서 눌러야 각인이 시작된다.
                _pantT -= dt;
                if (_pantT <= 0f) _fleeT = 0f;
                return;
            }

            Vector2 cursor = NativeCursor.GetPositionInWindow();
            Vector2 self = ScreenPos();
            float cd = Vector2.Distance(cursor, self);

            if (cd < fleeRadius)
            {
                Vector2 away = (self - cursor);
                float d = Mathf.Max(1f, cd);
                Move(away / d * fleeSpeed * dt);
                _dir = away.x > 0 ? 1 : -1;

                _fleeT += dt;
                if (_fleeT > fleeLimit) _pantT = pantDuration;
            }
            else WalkToTarget(dt, shySpeed);
        }

        /// <summary>순간이동 - 페이드아웃 후 랜덤 위치에 재등장.</summary>
        void UpdateBlink(float dt)
        {
            if (_fadingT > 0f)
            {
                _fadingT -= dt;
                Alpha = Mathf.Max(0f, _fadingT / fadeDuration);

                if (_fadingT <= 0f)
                {
                    Teleport();
                    Alpha = 0f;
                    _blinkT = Random.Range(blinkInterval.x, blinkInterval.y);
                }
                return;
            }

            // 등장 직후 1.2초는 알파가 차오르는 구간 = 무방비 (§3.3)
            Alpha = Mathf.Min(1f, Alpha + dt * 3f);
            WalkToTarget(dt, blinkSpeed);

            _blinkT -= dt;
            if (_blinkT <= 0f) _fadingT = fadeDuration;
        }

        void WalkToTarget(float dt, float speedPx)
        {
            Vector2 self = ScreenPos();
            Vector2 d = _target - self;

            if (d.magnitude <= arriveDist) { PickTarget(); return; }

            _dir = d.x > 0 ? 1 : -1;
            Move(d.normalized * speedPx * dt);
        }

        /// <summary>스크린 좌표 이동량을 월드로 옮긴다. 1유닛 = 100px 전제.</summary>
        void Move(Vector2 deltaPx)
        {
            transform.position += (Vector3)(deltaPx / 100f);
        }

        Vector2 ScreenPos()
        {
            if (_cam == null) _cam = Camera.main;
            return _cam != null ? (Vector2)_cam.WorldToScreenPoint(transform.position) : Vector2.zero;
        }

        void PickTarget()
        {
            _target = new Vector2(
                Random.Range(80f, Mathf.Max(81f, Screen.width - 80f)),
                Random.Range(60f, Mathf.Max(61f, Screen.height - 120f)));
        }

        void Teleport()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var screen = new Vector3(
                Random.Range(80f, Mathf.Max(81f, Screen.width - 80f)),
                Random.Range(60f, Mathf.Max(61f, Screen.height - 120f)),
                -_cam.transform.position.z);

            transform.position = _cam.ScreenToWorldPoint(screen);
            PickTarget();
        }
    }
}
