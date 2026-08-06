using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Capture
{
    /// <summary>
    /// 포획 연출. overlay.html의 rings/hearts/sparks/trail + doCatch() 이식.
    /// 포팅계획 §4 S2의 "포획 연출"에 해당한다.
    ///
    /// 파티클 시스템 대신 직접 그리는 이유:
    ///   원본이 캔버스에 도형을 찍는 방식이고, 개수가 스무 개 남짓이라 파티클을 쓸 이유가 없다.
    ///   같은 GL 경로로 그리면 각인 UI와 겹치는 순서도 예측 가능하다.
    ///
    /// 각 이펙트는 수명이 끝나면 목록에서 빠진다. 오브젝트 풀을 두지 않는 이유는
    /// 포획이 몇 분에 한 번 일어나는 일이라 할당이 문제가 되지 않기 때문이다.
    /// </summary>
    public class CaptureEffects : MonoBehaviour
    {
        struct Ring { public Vector2 pos; public float t, max; }
        struct Heart { public Vector2 pos; public float t; }
        struct Spark { public Vector2 pos, vel; public float t; }
        struct Trail { public Vector2 pos; public float t; }

        readonly List<Ring> _rings = new List<Ring>();
        readonly List<Heart> _hearts = new List<Heart>();
        readonly List<Spark> _sparks = new List<Spark>();
        readonly List<Trail> _trails = new List<Trail>();

        [Header("색 (overlay.html)")]
        public Color ringColor = new Color(1f, 0.863f, 0.471f, 1f);    // rgba(255,220,120)
        public Color heartColor = new Color(1f, 0.490f, 0.620f, 1f);   // #ff7d9e
        public Color sparkColor = new Color(1f, 0.914f, 0.639f, 1f);   // #ffe9a3

        Camera _cam;
        Material _mat;

        /// <summary>
        /// 히트스톱 남은 시간. 포획 순간 화면을 잠깐 멈춰 타격감을 만든다.
        /// overlay.html: hitstop=0.09
        /// </summary>
        public float HitStop { get; private set; }

        void OnDestroy()
        {
            if (_mat != null) DestroyImmediate(_mat);
            if (_texMat != null) DestroyImmediate(_texMat);
        }

        Material Mat
        {
            get
            {
                if (_mat != null) return _mat;
                _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
                { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _mat.SetInt("_ZWrite", 0);
                _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                return _mat;
            }
        }

        /// <summary>
        /// 포획 성공 연출. overlay.html doCatch() 이식.
        /// 링 1개 + 하트 6개(시차를 두고 뜬다) + 반짝임 12개.
        /// </summary>
        public void PlayCatch(Vector2 screenPos)
        {
            HitStop = 0.09f;

            _rings.Add(new Ring { pos = screenPos, t = 0f, max = 74f });

            // t를 음수로 시작해 차례로 뜨게 한다 - 한꺼번에 터지면 덩어리로 보인다.
            for (int i = 0; i < 6; i++)
                _hearts.Add(new Heart
                {
                    pos = screenPos + new Vector2(Random.Range(-18f, 18f), Random.Range(-6f, 10f)),
                    t = -i * 0.06f,
                });

            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                _sparks.Add(new Spark
                {
                    pos = screenPos,
                    // 위쪽으로 치우치게 (원본의 -40) - 아래로만 흩어지면 가라앉아 보인다
                    vel = new Vector2(Mathf.Cos(a) * Random.Range(50f, 180f),
                                      Mathf.Sin(a) * Random.Range(50f, 180f) + 40f),
                    t = 0f,
                });
            }
        }

        /// <summary>문양 하나 성공 - 작은 펄스. overlay.html:242</summary>
        public void PlayGlyphSuccess(Vector2 screenPos)
            => _rings.Add(new Ring { pos = screenPos, t = 0f, max = 46f });

        /// <summary>쓰다듬기 - 하트 3개가 시차를 두고 떠오른다. overlay.html:311</summary>
        public void PlayPet(Vector2 screenPos)
        {
            for (int i = 0; i < 3; i++)
                _hearts.Add(new Heart
                {
                    pos = screenPos + new Vector2(Random.Range(-12f, 12f), 28f),
                    t = -i * 0.1f,
                });
        }

        /// <summary>포획된 개체가 날아가는 동안의 궤적.</summary>
        public void AddTrail(Vector2 screenPos)
            => _trails.Add(new Trail
            {
                pos = screenPos + new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f)),
                t = 0f,
            });

        public void Clear()
        {
            _rings.Clear();
            _hearts.Clear();
            _sparks.Clear();
            _trails.Clear();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (HitStop > 0f) HitStop = Mathf.Max(0f, HitStop - dt);

            // 뒤에서부터 지운다 - 앞에서 지우면 인덱스가 밀린다.
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i]; r.t += dt;
                if (r.t > 0.5f) _rings.RemoveAt(i); else _rings[i] = r;
            }
            for (int i = _hearts.Count - 1; i >= 0; i--)
            {
                var h = _hearts[i]; h.t += dt;
                if (h.t > 1f) _hearts.RemoveAt(i); else _hearts[i] = h;
            }
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.t += dt;
                s.pos += s.vel * dt;
                s.vel += new Vector2(0f, -220f * dt);   // 중력
                if (s.t > 0.8f) _sparks.RemoveAt(i); else _sparks[i] = s;
            }
            for (int i = _trails.Count - 1; i >= 0; i--)
            {
                var t = _trails[i]; t.t += dt;
                if (t.t > 0.4f) _trails.RemoveAt(i); else _trails[i] = t;
            }
        }

        // ── 테마 스프라이트 드로잉 ──
        // UI 이미지(fx_heart/fx_spark/fx_ring)가 승인되면 벡터 드로잉 대신 도트를 쓴다.
        // 없는 것은 각자 벡터로 폴백한다 - 셋이 따로따로 승인될 수 있다.

        Material _texMat;

        Material TexMat
        {
            get
            {
                if (_texMat != null) return _texMat;
                // UI/Default는 씬에 Canvas가 있으면 항상 빌드에 포함된다.
                var shader = Shader.Find("UI/Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                _texMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return _texMat;
            }
        }

        /// <summary>텍스처를 지정하고 SetPass. 이후 DrawSpriteQuad가 그 텍스처로 그린다.</summary>
        bool BeginSprite(Sprite s)
        {
            var mat = TexMat;
            if (mat == null || s == null) return false;
            mat.mainTexture = s.texture;
            mat.SetPass(0);
            return true;
        }

        static void DrawSpriteQuad(Sprite s, Vector2 c, float size, Color col)
        {
            var tex = s.texture;
            Rect r = s.textureRect;
            float u0 = r.x / tex.width, v0 = r.y / tex.height;
            float u1 = (r.x + r.width) / tex.width, v1 = (r.y + r.height) / tex.height;
            float h = size * 0.5f;

            GL.Begin(GL.QUADS);
            GL.Color(col);
            GL.TexCoord2(u0, v0); GL.Vertex3(c.x - h, c.y - h, 0f);
            GL.TexCoord2(u1, v0); GL.Vertex3(c.x + h, c.y - h, 0f);
            GL.TexCoord2(u1, v1); GL.Vertex3(c.x + h, c.y + h, 0f);
            GL.TexCoord2(u0, v1); GL.Vertex3(c.x - h, c.y + h, 0f);
            GL.End();
        }

        void OnRenderObject()
        {
            if (_rings.Count == 0 && _hearts.Count == 0
                && _sparks.Count == 0 && _trails.Count == 0) return;

            if (_cam == null) _cam = Camera.main;
            if (Camera.current != _cam) return;

            var theme = Deskmon.UI.UIKit.Theme;
            var sparkSprite = theme != null ? theme.fxSpark : null;
            var ringSprite = theme != null ? theme.fxRing : null;
            var heartSprite = theme != null ? theme.fxHeart : null;

            GL.PushMatrix();
            GL.LoadPixelMatrix();

            // 같은 텍스처끼리 묶어 SetPass 횟수를 줄인다 (spark은 궤적과 반짝임이 공유)
            if (sparkSprite != null && BeginSprite(sparkSprite))
            {
                foreach (var t in _trails)
                {
                    var c = Color.white; c.a = (1f - t.t / 0.4f) * 0.8f;
                    DrawSpriteQuad(sparkSprite, t.pos, 12f, c);
                }
                foreach (var s in _sparks)
                {
                    var c = Color.white; c.a = 1f - s.t / 0.8f;
                    DrawSpriteQuad(sparkSprite, s.pos, 16f, c);
                }
            }

            if (ringSprite != null && BeginSprite(ringSprite))
            {
                foreach (var r in _rings)
                {
                    float p = r.t / 0.5f;
                    var c = Color.white; c.a = 1f - p;
                    // 도트 링을 지름에 맞춰 늘린다 - 확대 앨리어싱은 0.5초 연출이라 허용
                    DrawSpriteQuad(ringSprite, r.pos, (10f + (r.max - 10f) * p) * 2f, c);
                }
            }

            if (heartSprite != null && BeginSprite(heartSprite))
            {
                foreach (var h in _hearts)
                {
                    if (h.t < 0f) continue;
                    var c = Color.white; c.a = 1f - h.t;
                    DrawSpriteQuad(heartSprite, h.pos + new Vector2(0f, h.t * 28f), 20f, c);
                }
            }

            // ── 벡터 폴백 (이미지가 없는 것만) ──
            bool needVector = (sparkSprite == null && (_trails.Count > 0 || _sparks.Count > 0))
                           || (ringSprite == null && _rings.Count > 0)
                           || (heartSprite == null && _hearts.Count > 0);
            if (needVector)
            {
                Mat.SetPass(0);

                if (sparkSprite == null)
                {
                    foreach (var t in _trails)
                    {
                        var c = sparkColor; c.a = (1f - t.t / 0.4f) * 0.8f;
                        DrawStar(t.pos, 3f, c);
                    }
                    foreach (var s in _sparks)
                    {
                        var c = sparkColor; c.a = 1f - s.t / 0.8f;
                        DrawStar(s.pos, 3.5f, c);
                    }
                }

                if (ringSprite == null)
                    foreach (var r in _rings)
                    {
                        float p = r.t / 0.5f;
                        var c = ringColor; c.a = 1f - p;
                        DrawRing(r.pos, 10f + (r.max - 10f) * p, 2.5f, c);
                    }

                if (heartSprite == null)
                    foreach (var h in _hearts)
                    {
                        if (h.t < 0f) continue;
                        var c = heartColor; c.a = 1f - h.t;
                        DrawHeart(h.pos + new Vector2(0f, h.t * 28f), 6f, c);
                    }
            }

            GL.PopMatrix();
        }

        // ── 도형 ──

        static void DrawRing(Vector2 c, float radius, float width, Color col)
        {
            const int SEG = 40;
            float inner = radius - width * 0.5f, outer = radius + width * 0.5f;

            GL.Begin(GL.QUADS);
            GL.Color(col);
            for (int i = 0; i < SEG; i++)
            {
                float a0 = i / (float)SEG * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)SEG * Mathf.PI * 2f;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

                GL.Vertex3(c.x + c0 * inner, c.y + s0 * inner, 0f);
                GL.Vertex3(c.x + c1 * inner, c.y + s1 * inner, 0f);
                GL.Vertex3(c.x + c1 * outer, c.y + s1 * outer, 0f);
                GL.Vertex3(c.x + c0 * outer, c.y + s0 * outer, 0f);
            }
            GL.End();
        }

        /// <summary>4각 별 - creature.js star()에 대응. 뾰족한 십자 형태.</summary>
        static void DrawStar(Vector2 c, float r, Color col)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(col);

            float inner = r * 0.38f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                float na = a + Mathf.PI * 0.25f;
                float pa = a - Mathf.PI * 0.25f;

                GL.Vertex3(c.x, c.y, 0f);
                GL.Vertex3(c.x + Mathf.Cos(pa) * inner, c.y + Mathf.Sin(pa) * inner, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);

                GL.Vertex3(c.x, c.y, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
                GL.Vertex3(c.x + Mathf.Cos(na) * inner, c.y + Mathf.Sin(na) * inner, 0f);
            }
            GL.End();
        }

        /// <summary>하트 - 원 두 개 + 아래로 모이는 삼각형.</summary>
        static void DrawHeart(Vector2 c, float r, Color col)
        {
            DrawDisc(new Vector2(c.x - r * 0.5f, c.y + r * 0.3f), r * 0.55f, col);
            DrawDisc(new Vector2(c.x + r * 0.5f, c.y + r * 0.3f), r * 0.55f, col);

            GL.Begin(GL.TRIANGLES);
            GL.Color(col);
            GL.Vertex3(c.x - r, c.y + r * 0.35f, 0f);
            GL.Vertex3(c.x + r, c.y + r * 0.35f, 0f);
            GL.Vertex3(c.x, c.y - r, 0f);
            GL.End();
        }

        static void DrawDisc(Vector2 c, float r, Color col)
        {
            const int SEG = 14;
            GL.Begin(GL.TRIANGLES);
            GL.Color(col);
            for (int i = 0; i < SEG; i++)
            {
                float a0 = i / (float)SEG * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)SEG * Mathf.PI * 2f;
                GL.Vertex3(c.x, c.y, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, 0f);
            }
            GL.End();
        }
    }
}
