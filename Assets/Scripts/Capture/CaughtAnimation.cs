using UnityEngine;

namespace Deskmon.Capture
{
    /// <summary>
    /// 포획된 개체가 베이스캠프로 날아가는 연출. overlay.html updateWild()의 caught 분기 이식.
    ///
    /// 타이밍 (원본 수치):
    ///   0.00~0.22s  부풀었다 가라앉는다 (scale 1.42 -> 1.0)
    ///   0.45s       날아가기 시작
    ///   0.45~1.00s  ease-in(p*p)으로 화면 가장자리를 향해 가속하며 궤적을 남긴다
    ///
    /// 왜 0.45초를 기다리나: 잡자마자 날아가면 "잡았다"는 순간을 볼 틈이 없다.
    /// 부푸는 연출이 끝나고 잠깐 멈췄다 출발해야 획득감이 생긴다.
    /// </summary>
    public class CaughtAnimation : MonoBehaviour
    {
        [Header("타이밍 (overlay.html)")]
        [Tooltip("날아가기 시작까지 대기")]
        public float flyDelay = 0.45f;
        [Tooltip("날아가는 시간")]
        public float flyDuration = 0.55f;
        [Tooltip("부푸는 연출 시간")]
        public float popDuration = 0.22f;
        [Tooltip("최대 배율 - 1 + 이 값")]
        public float popScale = 0.42f;

        [Header("참조")]
        public CaptureEffects effects;

        /// <summary>연출이 끝났을 때. 여기서 개체를 없앤다.</summary>
        public event System.Action OnFinished;

        Camera _cam;
        Vector3 _baseScale;
        Vector2 _from, _to;
        float _t;
        bool _flying, _done;

        /// <summary>포획 확정 시 호출. 이 시점부터 개체는 조작 불가다.</summary>
        public void Begin()
        {
            _cam = Camera.main;
            _baseScale = transform.localScale;
            _t = 0f;
            _flying = false;
            _done = false;

            if (effects != null) effects.PlayCatch(ScreenPos());

            // 목적지는 가까운 쪽 화면 가장자리. 베이스캠프 카드가 어느 쪽에 있든
            // "화면 밖으로 사라진다"는 인상은 같으므로 가까운 쪽을 쓴다.
            Vector2 p = ScreenPos();
            _from = p;
            _to = new Vector2(p.x < Screen.width * 0.5f ? 14f : Screen.width - 14f,
                              Screen.height * 0.5f);
        }

        void Update()
        {
            if (_done || _cam == null) return;

            // 히트스톱 동안은 시간이 멈춘다 - 타격감의 핵심이다.
            if (effects != null && effects.HitStop > 0f) return;

            float dt = Time.unscaledDeltaTime;
            _t += dt;

            // 부풀었다 가라앉기
            float pop = 1f + popScale * Mathf.Max(0f, 1f - _t / popDuration);
            transform.localScale = _baseScale * pop;

            if (!_flying)
            {
                if (_t >= flyDelay) { _flying = true; _t = 0f; }
                return;
            }

            // ease-in - 처음엔 느리다가 빨아들이듯 가속한다
            float prog = Mathf.Min(1f, _t / flyDuration);
            float eased = prog * prog;

            Vector2 pos = Vector2.Lerp(_from, _to, eased);
            transform.position = ScreenToWorld(pos);

            if (effects != null) effects.AddTrail(pos);

            if (prog >= 1f)
            {
                _done = true;
                OnFinished?.Invoke();
            }
        }

        Vector2 ScreenPos()
        {
            if (_cam == null) _cam = Camera.main;
            return _cam != null ? (Vector2)_cam.WorldToScreenPoint(transform.position) : Vector2.zero;
        }

        Vector3 ScreenToWorld(Vector2 screen)
            => _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
    }
}
