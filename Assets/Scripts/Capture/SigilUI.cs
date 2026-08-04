using System.Collections.Generic;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인 UI 렌더링. overlay.html drawSigilUI() + drawGlyphGhost() + 획 궤적 이식.
    ///
    /// 왜 UGUI가 아니라 GL 즉시 모드인가:
    ///   그려야 하는 것이 매 프레임 바뀌는 폴리라인(점선 고스트, 손 궤적)이다. UGUI로 하면
    ///   선 하나에 메시를 만들고 매 프레임 재생성해야 하는데, 여기서 얻는 게 없다.
    ///   GL은 투명 오버레이 위에 알파를 그대로 남기며 그릴 수 있고 배칭 걱정도 없다.
    ///
    /// 좌표계: 전부 스크린 픽셀 (좌하단 원점). SigilCapture가 넘기는 획 좌표와 같다.
    /// </summary>
    [RequireComponent(typeof(SigilCapture))]
    public class SigilUI : MonoBehaviour
    {
        [Header("배치 (overlay.html drawSigilUI 수치)")]
        [Tooltip("고스트 문양 반지름 px")]
        public float radius = 34f;
        [Tooltip("야생 머리 위로 띄우는 거리 px")]
        public float offsetY = 76f;
        [Tooltip("화면 가장자리에서 이 거리 안쪽으로 고정한다")]
        public float edgeMargin = 58f;

        [Header("색")]
        public Color normalColor = new Color(1f, 0.941f, 0.745f, 0.95f);   // rgba(255,240,190,.95)
        public Color failColor = new Color(1f, 0.561f, 0.561f, 1f);        // #ff8f8f
        public Color successColor = new Color(0.624f, 0.941f, 0.659f, 1f); // #9ff0a8
        public Color panelColor = new Color(0.071f, 0.102f, 0.078f, 0.55f);// rgba(18,26,20,.55)
        public Color strokeColor = new Color(1f, 0.878f, 0.533f, 1f);      // #ffe088
        public Color guideColor = new Color(1f, 0.961f, 0.784f, 1f);       // #fff5c8

        [Header("대상")]
        [Tooltip("각인 UI를 띄울 야생의 트랜스폼. 비면 이 오브젝트를 쓴다.")]
        public Transform wildTarget;

        SigilCapture _capture;
        Camera _cam;
        Material _lineMat;

        void Awake()
        {
            _capture = GetComponent<SigilCapture>();
            if (wildTarget == null) wildTarget = transform;
        }

        void OnDestroy()
        {
            if (_lineMat != null) DestroyImmediate(_lineMat);
        }

        /// <summary>
        /// GL 드로잉용 머티리얼. 셰이더를 에셋으로 두지 않고 내장 것을 쓴다 -
        /// 이 용도로는 Hidden/Internal-Colored가 정확히 맞고 빌드에도 항상 포함된다.
        /// </summary>
        Material LineMat
        {
            get
            {
                if (_lineMat != null) return _lineMat;

                var shader = Shader.Find("Hidden/Internal-Colored");
                _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMat.SetInt("_ZWrite", 0);
                _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                return _lineMat;
            }
        }

        /// <summary>
        /// OnRenderObject는 카메라가 씬을 그린 뒤 호출된다 - 크리처 위에 UI가 얹힌다.
        /// OnGUI를 쓰지 않는 이유는 IMGUI가 매 이벤트마다 레이아웃을 다시 도는 비용 때문이다.
        /// </summary>
        void OnRenderObject()
        {
            if (_capture == null || !_capture.Engaged || _capture.CurrentGlyph == null) return;

            // 여러 카메라가 있으면 각각에서 호출된다. 메인에서만 그린다.
            if (_cam == null) _cam = Camera.main;
            if (Camera.current != _cam) return;

            Vector2 anchor = AnchorPos();

            LineMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();   // 스크린 픽셀 좌표계 (좌하단 원점)

            DrawPanel(anchor);
            DrawGhost(anchor);
            DrawGuideDot(anchor);
            DrawProgressDots(anchor);
            DrawStroke();

            GL.PopMatrix();
        }

        /// <summary>
        /// UI를 띄울 위치. 야생 머리 위지만 화면 밖으로 나가지 않게 가둔다.
        /// overlay.html: clamp(w.x,58,W-58), max(64, w.y-76)
        /// </summary>
        Vector2 AnchorPos()
        {
            if (_cam == null) _cam = Camera.main;

            Vector2 screen = _cam != null
                ? (Vector2)_cam.WorldToScreenPoint(wildTarget.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            float x = Mathf.Clamp(screen.x, edgeMargin, Screen.width - edgeMargin);

            // 원본은 Y가 아래로 증가해 y-76이 "위"였다. Unity는 위로 증가하므로 더한다.
            float y = Mathf.Min(screen.y + offsetY, Screen.height - 64f);

            return new Vector2(x, y);
        }

        /// <summary>현재 상태 색. 실패=빨강, 성공 직후=초록, 평소=크림.</summary>
        Color CurrentColor()
        {
            if (_capture.ShakeT > 0f) return failColor;
            if (_capture.OkFlash > 0f) return successColor;
            return normalColor;
        }

        /// <summary>실패 시 좌우 흔들림. overlay.html: sin(shakeT*60)*4</summary>
        float ShakeOffset()
            => _capture.ShakeT > 0f ? Mathf.Sin(_capture.ShakeT * 60f) * 4f : 0f;

        /// <summary>문양 뒤 어두운 원판 - 바탕화면이 밝아도 문양이 보이게 한다.</summary>
        void DrawPanel(Vector2 c)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(panelColor);

            float r = radius + 15f;
            const int SEG = 32;
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

        /// <summary>
        /// 목표 문양을 점선으로. overlay.html drawGlyphGhost().
        /// 시작점에 원을 찍어 "여기서부터 그린다"를 알린다.
        /// </summary>
        void DrawGhost(Vector2 c)
        {
            var raw = SigilRecognizer.Raw(_capture.CurrentGlyph);
            if (raw == null || raw.Length < 2) return;

            float jx = ShakeOffset();
            Color col = CurrentColor();

            GL.Begin(GL.LINES);
            GL.Color(col);

            // 점선: 각 구간을 4px 그리고 4px 띄운다
            for (int i = 0; i < raw.Length - 1; i++)
            {
                Vector2 a = ToScreen(raw[i], c, jx);
                Vector2 b = ToScreen(raw[i + 1], c, jx);
                DrawDashed(a, b, 4f, 4f);
            }
            GL.End();

            // 시작점 표식
            DrawDisc(ToScreen(raw[0], c, jx), 3.5f, col);
        }

        /// <summary>
        /// 문양 경로를 도는 안내 빛점. 그리는 중이거나 흔들릴 때는 숨긴다
        /// (손으로 그리는 동안 점이 돌아다니면 시선을 뺏는다).
        /// </summary>
        void DrawGuideDot(Vector2 c)
        {
            if (_capture.Drawing || _capture.ShakeT > 0f) return;

            var raw = SigilRecognizer.Raw(_capture.CurrentGlyph);
            if (raw == null || raw.Length < 2) return;

            // overlay.html: tp=(t*0.5)%1 - 2초에 한 바퀴
            float tp = (_capture.Time01 * 0.5f) % 1f;
            float fi = tp * (raw.Length - 1);
            int i0 = Mathf.FloorToInt(fi);
            float fr = fi - i0;

            Vector2 a = raw[i0];
            Vector2 b = raw[Mathf.Min(raw.Length - 1, i0 + 1)];
            Vector2 p = ToScreen(Vector2.Lerp(a, b, fr), c, ShakeOffset());

            var halo = guideColor; halo.a = 0.4f;
            DrawDisc(p, 7f, halo);
            DrawDisc(p, 3.5f, guideColor);
        }

        /// <summary>진행 점 - 맞춘 것 초록, 지금 것 노랑, 남은 것 회색.</summary>
        void DrawProgressDots(Vector2 c)
        {
            int n = _capture.TotalGlyphs;
            if (n <= 0) return;

            float y = c.y - radius - 15f;   // 원본은 +였다 (Y 아래로 증가) - 뒤집는다
            for (int i = 0; i < n; i++)
            {
                Color col = i < _capture.Index ? successColor
                          : i == _capture.Index ? new Color(1f, 0.914f, 0.639f, 1f)
                          : new Color(1f, 1f, 1f, 0.35f);

                DrawDisc(new Vector2(c.x - (n - 1) * 5f + i * 10f, y), 3f, col);
            }
        }

        /// <summary>
        /// 그리는 중인 손 궤적. 굵은 반투명 + 얇은 불투명 두 겹으로 그려
        /// 바탕화면 위에서도 선이 보이게 한다 (원본과 같은 방식).
        /// </summary>
        void DrawStroke()
        {
            var s = _capture.Stroke;
            if (s == null || s.Count < 2) return;

            var glow = strokeColor; glow.a = 0.22f;
            DrawPolylineThick(s, 9f, glow);
            DrawPolylineThick(s, 3.5f, strokeColor);
        }

        // ── 그리기 도구 ──

        Vector2 ToScreen(Vector2 p, Vector2 c, float jx)
        {
            // 문양 좌표는 -1..1, Y가 아래로 증가한다. Unity 스크린은 위로 증가하므로 뒤집는다.
            return new Vector2(c.x + jx + p.x * radius, c.y - p.y * radius);
        }

        /// <summary>GL.LINES 안에서 호출. a-b를 점선으로 나눠 그린다.</summary>
        static void DrawDashed(Vector2 a, Vector2 b, float on, float off)
        {
            float len = Vector2.Distance(a, b);
            if (len <= 0.001f) return;

            Vector2 dir = (b - a) / len;
            float pos = 0f;
            while (pos < len)
            {
                float end = Mathf.Min(pos + on, len);
                GL.Vertex3(a.x + dir.x * pos, a.y + dir.y * pos, 0f);
                GL.Vertex3(a.x + dir.x * end, a.y + dir.y * end, 0f);
                pos = end + off;
            }
        }

        /// <summary>
        /// 두께 있는 폴리라인. GL.LINES의 선 굵기는 플랫폼마다 다르게 처리되거나
        /// 무시되므로(D3D11에서 항상 1px), 사각형 두 개로 직접 만든다.
        /// </summary>
        static void DrawPolylineThick(IReadOnlyList<Vector2> pts, float width, Color col)
        {
            float h = width * 0.5f;

            GL.Begin(GL.QUADS);
            GL.Color(col);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len <= 0.0001f) continue;

                // 선분에 수직인 법선으로 폭을 준다
                Vector2 nrm = new Vector2(-d.y, d.x) / len * h;

                GL.Vertex3(a.x + nrm.x, a.y + nrm.y, 0f);
                GL.Vertex3(b.x + nrm.x, b.y + nrm.y, 0f);
                GL.Vertex3(b.x - nrm.x, b.y - nrm.y, 0f);
                GL.Vertex3(a.x - nrm.x, a.y - nrm.y, 0f);
            }
            GL.End();

            // 꺾이는 지점의 빈틈을 원으로 메운다 (lineJoin='round' 대응)
            for (int i = 1; i < pts.Count - 1; i++) DrawDisc(pts[i], h, col);
        }

        static void DrawDisc(Vector2 c, float r, Color col)
        {
            const int SEG = 16;
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
