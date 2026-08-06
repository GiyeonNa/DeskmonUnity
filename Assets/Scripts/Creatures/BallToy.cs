using UnityEngine;

namespace Deskmon.Creatures
{
    /// <summary>
    /// 공놀이 공. overlay.html의 ball 이식 - 화면에 동시에 하나만 존재한다.
    ///
    /// 궤적 (overlay.html:621-626 수치 그대로):
    ///   0.5초 동안 시작점 -> 목표점 직선 보간 + sin(p*pi)*90px 위로 아크
    ///   착지하면 Ground - 이때부터 방목 개체가 쫓아온다 (나는 동안은 기다린다)
    ///
    /// 스프라이트는 런타임 생성 원 (원본 캔버스 원 그리기의 대응).
    /// 도트 이미지(fx_ball)가 승인되면 UITheme를 통해 교체한다.
    /// </summary>
    public class BallToy : MonoBehaviour
    {
        public enum Phase { Fly, Ground }

        /// <summary>현재 공. 하나뿐이라는 규칙의 구현이다.</summary>
        public static BallToy Current { get; private set; }

        public Phase phase = Phase.Fly;

        /// <summary>스크린 좌표 (좌하단 원점). 추격 판정이 이걸 읽는다.</summary>
        public Vector2 ScreenPos { get; private set; }

        Vector2 _from, _to;
        float _t;
        Camera _cam;

        const float FLY_DURATION = 0.5f;
        const float ARC_HEIGHT = 90f;

        /// <summary>던지기. 이미 공이 있으면 무시된다.</summary>
        public static BallToy Throw(Vector2 fromScreen, Vector2 toScreen)
        {
            if (Current != null) return Current;

            var go = new GameObject("BallToy");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite();
            sr.sortingOrder = 8;   // 크리처(9~10)보다 뒤 - 물러 가는 대상이지 주인공이 아니다
            go.transform.localScale = Vector3.one * 2f;   // 크리처와 같은 픽셀 배율

            var ball = go.AddComponent<BallToy>();
            ball._from = fromScreen;
            ball._to = toScreen;
            ball.ScreenPos = fromScreen;
            ball._cam = Camera.main;
            ball.Apply();

            Current = ball;
            return ball;
        }

        /// <summary>공 제거 - 물었을 때, 또는 던진 개체가 사라졌을 때.</summary>
        public static void Clear()
        {
            if (Current == null) return;
            Destroy(Current.gameObject);
            Current = null;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (phase != Phase.Fly) return;

            _t += Time.unscaledDeltaTime;
            float p = Mathf.Min(1f, _t / FLY_DURATION);

            ScreenPos = Vector2.Lerp(_from, _to, p)
                      + new Vector2(0f, Mathf.Sin(p * Mathf.PI) * ARC_HEIGHT);

            if (p >= 1f)
            {
                phase = Phase.Ground;
                ScreenPos = _to;
            }
            Apply();
        }

        void Apply()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            transform.position = _cam.ScreenToWorldPoint(
                new Vector3(ScreenPos.x, ScreenPos.y, -_cam.transform.position.z));
        }

        // ── 스프라이트 (런타임 생성) ──

        static Sprite _sprite;

        /// <summary>16px 라임색 공. overlay.html의 #c8e86a 원 + 외곽선.</summary>
        static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;

            const int S = 16;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point };

            var body = new Color32(200, 232, 106, 255);    // #c8e86a
            var line = new Color32(143, 174, 63, 255);     // #8fae3f
            var hi = new Color32(240, 250, 200, 255);
            float c = (S - 1) * 0.5f, rOut = 7f, rIn = 5.8f;

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color32 px = new Color32(0, 0, 0, 0);
                    if (d <= rOut) px = d > rIn ? line : body;
                    tex.SetPixel(x, y, px);
                }
            tex.SetPixel(5, 10, hi);
            tex.SetPixel(6, 10, hi);
            tex.SetPixel(5, 9, hi);
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, S, S),
                                    new Vector2(0.5f, 0.5f), 100f);
            _sprite.name = "BallToy (generated)";
            return _sprite;
        }
    }
}
