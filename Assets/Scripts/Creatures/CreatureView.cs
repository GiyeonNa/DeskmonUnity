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

        [Header("모션 - 대기는 호흡, 걷기는 꿀렁 (WobbleMotion 참고)")]
        public float idleFreq = 2.2f;
        public float idleAmp = 0.035f;
        public float walkFreq = 11f;
        public float walkAmp = 0.14f;
        public float walkTiltDeg = 4f;
        [Tooltip("걷기 발걸음 바운스 (px). hop 종은 hopHeight를 대신 쓴다.")]
        public float walkBobPx = 2.5f;
        public bool hop;                        // 점프 이동 종 — 큰 sin 바운스
        public float hopHeight = 6f;

        [Header("상호작용")]
        public float interactRadius = 60f;

        public State state = State.Idle;

        /// <summary>
        /// 외부(공놀이 추격 등)가 이동을 대신하는 동안 산책 AI를 멈춘다.
        /// 모션(스쿼시·플립)은 계속 돈다 - 외부는 위치만 옮기고 살아있는 느낌은 여기 남는다.
        /// state를 Walk로 두면 걷기 스쿼시가, Idle이면 호흡만 나온다.
        /// </summary>
        public bool ExternallyDriven { get; set; }

        /// <summary>바라보는 방향을 외부에서 지정한다 (좌우 플립).</summary>
        public void FaceDir(int dir) => _dir = dir >= 0 ? 1 : -1;

        /// <summary>
        /// 잠깐 멈춘다 - 쓰다듬기 반응용 (overlay.html: r.pauseT=1).
        /// 자던 개체는 깨운다. 만지면 일어나는 것이 자연스럽다.
        /// </summary>
        public void Pause(float seconds)
        {
            state = State.Idle;
            _sleepT = 0f;
            _pauseT = Mathf.Max(_pauseT, seconds);
        }

        SpriteRenderer _sr;
        Camera _cam;
        Vector2 _target;
        float _pauseT, _sleepT, _t;
        int _dir = 1;
        Vector3 _baseScale;
        WobbleMotion _wobble;
        float _prevLift;   // 지난 프레임에 얹은 바운스 - 빼고 다시 얹어야 y 이동이 산다

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
            _wobble.Seed(Random.Range(0f, Mathf.PI * 2f));
        }

        void Start()
        {
            _cam = Camera.main;
            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.Register(this);
            PickTarget();

            // 산책 시동. Idle + _pauseT 0이면 Tick의 어느 분기에도 걸리지 않아
            // 쓰다듬기 전까지 영원히 서 있게 된다 - 잠깐 쉬었다 첫 걸음을 뗀다.
            _pauseT = Random.Range(pauseRange.x, pauseRange.y);
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
            ApplyMotion(dt);
        }

        /// <summary>overlay.html:105-120 상태 전이.</summary>
        void Tick(float dt)
        {
            // 외부가 몰고 있는 동안 산책 AI는 손을 뗀다 (위치를 서로 덮어쓰면 떨린다)
            if (ExternallyDriven) return;

            if (state == State.Sleep)
            {
                _sleepT -= dt;
                // 깨면 새 쉼을 준다. 0으로 두면 위 시동 문제와 같은 이유로 영영 멈춘다.
                if (_sleepT <= 0f) { state = State.Idle; _pauseT = Random.Range(pauseRange.x, pauseRange.y); }
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

        /// <summary>
        /// 상태별 모션 - 대기는 느린 호흡, 걷기는 빠른 꿀렁 + 기우뚱 + 발걸음 바운스.
        /// 프레임 걷기 애니메이션 대신 채택한 연출이다 (WobbleMotion 헤더 참고).
        /// </summary>
        void ApplyMotion(float dt)
        {
            bool walking = state == State.Walk;
            float freq, amp, tilt = 0f, bobPx = 0f;

            if (state == State.Sleep) { freq = 1.5f; amp = 0.03f; }          // 느린 호흡
            else if (walking)
            {
                freq = walkFreq; amp = walkAmp; tilt = walkTiltDeg;
                // 추격(공놀이) 중엔 외부가 y를 몰고, hop 종은 아래 홉이 y를 몬다
                if (!ExternallyDriven && !hop) bobPx = walkBobPx;
            }
            else { freq = idleFreq; amp = idleAmp; }

            _wobble.Step(dt, freq, amp, tilt);
            _wobble.Scale(out float sx, out float sy);

            transform.localScale = new Vector3(
                _baseScale.x * sx * _dir,   // 음수 x = 좌우 반전 (스프라이트 미러)
                _baseScale.y * sy,
                _baseScale.z);
            transform.rotation = Quaternion.Euler(0f, 0f, _wobble.TiltDeg());

            // 바운스는 "지난 프레임 몫을 빼고 새로 얹는" 순수 시각 오프셋이다.
            // 이전 구현은 걷기 시작 시점의 y를 붙들어서(_groundY), 걷는 동안
            // Tick의 y 이동이 통째로 지워졌다 - 홉 종이 세로로 이동하지 못했다.
            float lift = 0f;
            if (walking && !ExternallyDriven)
                lift = hop
                    ? Mathf.Abs(Mathf.Sin(_t * 8f)) * PixelsToUnits(hopHeight)
                    : _wobble.Bounce() * PixelsToUnits(bobPx);

            var p = transform.position;
            transform.position = new Vector3(p.x, p.y - _prevLift + lift, p.z);
            _prevLift = lift;
        }

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
