using UnityEngine;
using Deskmon.Native;

namespace Deskmon.Creatures
{
    /// <summary>
    /// 크리처 1마리 — 산책 AI + 코드 모션.
    /// 포팅계획서 §3.1 "정지 스프라이트 1장 + 모션은 코드로" 채택안의 구현부.
    ///
    /// 이식 원본:
    ///   creature.js:252   sy = 1 + sin(t*6 + phase) * 0.08,  sx = 1/sy   ← 부피 보존 스쿼시
    ///   overlay.html:106  updateRoamers 상태머신 (idle / walk / sleep)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CreatureView : MonoBehaviour, IInteractive
    {
        public enum State { Idle, Walk, Sleep }

        [Header("이동 (overlay.html updateRoamers 수치)")]
        public float walkSpeed = 55f;          // px/s
        public float arriveDist = 12f;
        public Vector2 pauseRange = new Vector2(1.5f, 4f);
        public Vector2 sleepRange = new Vector2(5f, 10f);
        [Range(0f, 1f)] public float sleepChance = 0.18f;

        [Header("모션 (creature.js)")]
        public float squashFreq = 6f;
        public float squashAmp = 0.08f;
        public bool hop;                        // 깡총이류 — sin 바운스
        public float hopHeight = 6f;

        [Header("상호작용")]
        public float interactRadius = 60f;

        public State state = State.Idle;

        SpriteRenderer _sr;
        Camera _cam;
        Vector2 _target;
        float _pauseT, _sleepT, _phase, _t;
        int _dir = 1;
        Vector3 _baseScale;

        // ── IInteractive ──
        public Vector2 ScreenPosition => _cam != null ? (Vector2)_cam.WorldToScreenPoint(transform.position) : Vector2.zero;
        public float InteractRadius => interactRadius;
        public bool IsActive => isActiveAndEnabled;

        /// <summary>커서가 반경 안에 있는지 — 하이라이트/버튼 표시 판정용.</summary>
        public bool CursorNear { get; private set; }

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Start()
        {
            _cam = Camera.main;
            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.Register(this);
            PickTarget();
        }

        void OnDestroy()
        {
            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.Unregister(this);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);   // overlay.html:645 dt 클램프
            _t += dt;

            Vector2 cursor = NativeCursor.GetPositionInWindow();
            CursorNear = (ScreenPosition - cursor).sqrMagnitude <= interactRadius * interactRadius;

            Tick(dt);
            ApplyMotion();
        }

        /// <summary>overlay.html:105-120 상태 전이.</summary>
        void Tick(float dt)
        {
            if (state == State.Sleep)
            {
                _sleepT -= dt;
                if (_sleepT <= 0f) { state = State.Idle; _pauseT = 0f; }
                return;
            }

            if (_pauseT > 0f)
            {
                _pauseT -= dt;
                if (_pauseT <= 0f)
                {
                    if (Random.value < sleepChance)
                    {
                        state = State.Sleep;
                        _sleepT = Random.Range(sleepRange.x, sleepRange.y);
                    }
                    else
                    {
                        PickTarget();
                        state = State.Walk;
                    }
                }
                return;
            }

            if (state == State.Walk)
            {
                Vector2 pos = transform.position;
                Vector2 d = _target - pos;
                float dist = d.magnitude;

                if (dist <= arriveDist * PixelsToUnits(1f))
                {
                    state = State.Idle;
                    _pauseT = Random.Range(pauseRange.x, pauseRange.y);
                }
                else
                {
                    _dir = d.x > 0 ? 1 : -1;
                    transform.position += (Vector3)(d.normalized * PixelsToUnits(walkSpeed) * dt);
                }
            }
        }

        /// <summary>creature.js:252 — 부피 보존 스쿼시 & 스트레치 + 좌우 플립 + 홉.</summary>
        void ApplyMotion()
        {
            float sy = 1f + Mathf.Sin(_t * squashFreq + _phase) * squashAmp;
            if (state == State.Sleep) sy = 1f + Mathf.Sin(_t * 1.5f + _phase) * 0.03f;   // 잠잘 땐 느린 호흡
            float sx = 1f / sy;

            transform.localScale = new Vector3(
                _baseScale.x * sx * _dir,   // 음수 x = 좌우 반전 (스프라이트 미러)
                _baseScale.y * sy,
                _baseScale.z);

            if (hop && state == State.Walk)
            {
                float bounce = Mathf.Abs(Mathf.Sin(_t * 8f)) * PixelsToUnits(hopHeight);
                var p = transform.position;
                transform.position = new Vector3(p.x, _groundY + bounce, p.z);
            }
            else
            {
                _groundY = transform.position.y;
            }
        }

        float _groundY;

        void PickTarget()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // overlay.html:47 roamY() — 화면 상단 30%는 제외, 하단 90px 여백
            float h = _cam.orthographicSize * 2f;
            float w = h * _cam.aspect;
            float x = Random.Range(-w * 0.5f + PixelsToUnits(40f), w * 0.5f - PixelsToUnits(40f));
            float yMin = -h * 0.5f + PixelsToUnits(90f);
            float yMax = h * 0.5f - h * 0.3f;
            float y = Random.Range(yMin, Mathf.Max(yMin, yMax));
            _target = new Vector2(x, y);
        }

        /// <summary>px 수치를 월드 유닛으로. 카메라를 "1유닛 = 100px"로 세팅한 전제.</summary>
        float PixelsToUnits(float px) => px / 100f;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = CursorNear ? Color.green : new Color(1, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactRadius / 100f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _target);
        }
    }
}
