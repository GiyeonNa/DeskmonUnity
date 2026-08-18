using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인(문양) 인식기. recognizer.js 이식 - $1 Unistroke Recognizer 기반.
    ///
    /// 원본이 $1에서 바꾼 두 가지를 그대로 유지한다. 이게 포획 손맛을 결정한다:
    ///
    ///   1. 회전 허용을 +-30도로 제한 (bestAngle의 탐색 구간).
    ///      $1 원본은 전 방향을 허용하지만, 그러면 삼각형을 거꾸로 그려도 통과한다.
    ///      "목표 문양을 따라 그린다"는 규칙이 성립하려면 방향이 맞아야 한다.
    ///
    ///   2. 획 방향 무관 (Candidates). 그린 획과 뒤집은 획 둘 다 비교해 가까운 쪽을 쓴다.
    ///      원을 시계/반시계 어느 쪽으로 그려도 같게 취급하기 위한 것 - 방향까지 맞추라고
    ///      하면 너무 빡빡해진다.
    ///
    /// 판정은 <see cref="Recognize"/>(전체 중 최근접 분류)를 쓴다. 목표 문양 하나와만
    /// 비교하는 <see cref="Match"/>보다 원/삼각형/사각형 구분에 강하다 - data.js 주석의
    /// "분류가 모양은 보장하므로 관대해도 안전"이 이 뜻이고, 그래서 tol이 0.66으로 낮다.
    /// </summary>
    public static class SigilRecognizer
    {
        const int N = 64;              // 리샘플 점 수
        const float SIZE = 250f;       // 정규화 후 바운딩 박스 변 길이
        static readonly float HALF = 0.5f * Mathf.Sqrt(SIZE * SIZE * 2f);   // 최대 거리 (점수 정규화용)

        /// <summary>
        /// 정규화된 템플릿. 키는 변형 이름("spiral@2.5" 등)이고, 값은 그것이 속한 문양이다.
        ///
        /// 한 문양에 변형을 여러 개 두는 것은 $1 원논문이 권장하는 방식이다.
        /// 나선처럼 "몇 바퀴를 그리느냐"가 사람마다 다른 문양은 단일 템플릿으로는
        /// 잡을 수 없다 - 2바퀴 기준일 때 2.5바퀴를 그리면 점수가 0.2까지 떨어진다.
        /// </summary>
        static readonly Dictionary<string, Vector2[]> Templates = new Dictionary<string, Vector2[]>();

        /// <summary>변형 이름 -> 문양 이름. "spiral@2.5" -> "spiral"</summary>
        static readonly Dictionary<string, string> VariantOwner = new Dictionary<string, string>();

        /// <summary>
        /// 변형의 정규화 전 좌표. 거울상을 만들려면 원본 좌표가 필요하다
        /// (정규화된 것은 이미 회전·스케일이 적용돼 있어 반전의 의미가 달라진다).
        /// </summary>
        static readonly Dictionary<string, Vector2[]> RawVariants = new Dictionary<string, Vector2[]>();

        /// <summary>문양 이름 -> 표시용 원본 좌표 (대표 변형 하나).</summary>
        static readonly Dictionary<string, Vector2[]> RawTemplates = new Dictionary<string, Vector2[]>();

        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "circle", "원" }, { "triangle", "삼각형" }, { "square", "사각형" },
            { "star", "별" }, { "spiral", "나선" }, { "wave", "물결" },
            { "caret", "꺾쇠" }, { "zigzag", "번개" },
            { "hline", "가로선" }, { "vline", "세로선" },
            { "slash", "대각선(/)" }, { "backslash", "대각선(\\)" },
            { "check", "체크" }, { "lshape", "ㄴ자" }, { "stairs", "계단" },
            { "mshape", "M자" }, { "heart", "하트" }, { "infinity", "무한대" },
            { "question", "물음표" }, { "loop", "고리" },
        };

        /// <summary>
        /// $1 파이프라인을 타지 않는 직선 문양. <see cref="TryMatchLine"/>이 따로 판정한다.
        ///
        /// 직선을 Templates에 넣을 수 없는 이유 두 가지:
        ///   1. ScaleToSquare가 바운딩 박스를 정사각으로 늘리므로, 직선의 미세한
        ///      손떨림(폭 몇 px)이 250px 크기의 아무 도형으로 뻥튀기된다.
        ///   2. 회전 정규화(IndicativeAngle)가 모든 직선을 같은 방향으로 눕히므로
        ///      세로선과 가로선이 정규화 후 동일해져 구분 자체가 사라진다.
        /// </summary>
        static readonly HashSet<string> LineGlyphs = new HashSet<string>
        {
            "hline", "vline", "slash", "backslash",
        };

        static SigilRecognizer() => BuildTemplates();

        /// <summary>문양의 한국어 이름. 없으면 키 그대로.</summary>
        public static string Label(string name)
            => Labels.TryGetValue(name, out var s) ? s : name;

        /// <summary>문양 원본 좌표 (-1..1). 화면에 목표를 그려 보여줄 때 쓴다.</summary>
        public static Vector2[] Raw(string name)
            => RawTemplates.TryGetValue(name, out var p) ? p : null;

        /// <summary>문양 이름 목록 (변형 제외).</summary>
        public static IEnumerable<string> Names => RawTemplates.Keys;

        /// <summary>
        /// 지정한 문양과의 일치도 (0~1). recognizer.js match().
        /// 변형이 여러 개면 그중 가장 잘 맞는 것을 쓴다.
        /// 점이 8개 미만이면 0 - 점 몇 개로는 형태를 판단할 수 없다.
        /// </summary>
        public static float Match(IList<Vector2> points, string name)
        {
            if (points == null || points.Count < 8) return 0f;

            if (LineGlyphs.Contains(name))
                return TryMatchLine(points, out var line, out var s) && line == name ? s : 0f;

            if (!RawTemplates.ContainsKey(name)) return 0f;

            var cands = Candidates(points);
            float best = float.MaxValue;

            foreach (var kv in Templates)
            {
                if (VariantOwner[kv.Key] != name) continue;
                float d = DistanceTo(cands, kv.Value);
                if (d < best) best = d;
            }

            if (best >= float.MaxValue) return 0f;
            return 1f - best / HALF;
        }

        /// <summary>
        /// 전체 문양 중 가장 가까운 것을 분류한다. recognizer.js recognize().
        /// 변형끼리 경쟁시킨 뒤 소속 문양 이름을 돌려준다.
        /// </summary>
        public static (string name, float score) Recognize(IList<Vector2> points)
        {
            if (points == null || points.Count < 8) return (null, 0f);

            // 직선이면 $1 분류를 건너뛴다. 직선 획은 $1 공간에서 무의미한 값이 되므로
            // (LineGlyphs 헤더 참고) 어느 쪽으로 분류되든 잘못된 답이다.
            if (TryMatchLine(points, out var lineName, out var lineScore))
                return (lineName, lineScore);

            var cands = Candidates(points);
            float best = float.MaxValue;
            string variant = null;

            foreach (var kv in Templates)
            {
                float d = DistanceTo(cands, kv.Value);
                if (d < best) { best = d; variant = kv.Key; }
            }

            if (variant == null) return (null, 0f);
            return (VariantOwner[variant], 1f - best / HALF);
        }

        /// <summary>
        /// 포획 성공 판정. 분류 결과가 목표와 같고 점수가 임계 이상이어야 한다.
        /// 임계는 BalanceData.sigilTolerance (data.js SIGIL.tol = 0.66).
        /// </summary>
        public static bool Passes(IList<Vector2> points, string targetName, float tolerance)
        {
            var (name, score) = Recognize(points);
            return name == targetName && score >= tolerance;
        }

        // ── 직선 판정 ($1 밖) ──

        /// <summary>
        /// 획이 직선이면 가로/세로/대각 4방향 중 하나로 분류하고 점수를 준다.
        /// LineGlyphs 헤더 참고.
        ///
        /// 판정 기준:
        ///   직선성 - 양 끝점을 잇는 현에서 가장 멀리 벗어난 점의 거리 / 현 길이.
        ///   방향   - 현의 각도. 4방향(0/45/90/135도) 중 가까운 쪽으로 분류.
        ///            방향 간격이 45도이므로 10도까지 무감점, 이후 감점해 약 19도부터
        ///            tol 0.66에서 탈락한다 - 이웃 방향과의 경계(22.5도) 부근은
        ///            어느 쪽으로도 통과하지 못하는 애매 구간으로 남긴다.
        /// 획 방향(위->아래 / 아래->위)은 각도를 0..180으로 접어 무시한다 -
        /// Candidates가 획 순서를 뒤집는 것과 같은 취지다.
        /// </summary>
        static bool TryMatchLine(IList<Vector2> points, out string name, out float score)
        {
            name = null;
            score = 0f;

            Vector2 a = points[0], b = points[points.Count - 1];
            Vector2 chord = b - a;
            float len = chord.magnitude;
            if (len < 30f) return false;   // 화면 px 기준 - 제자리 획은 방향을 논할 수 없다

            Vector2 dir = chord / len;
            float maxDev = 0f;
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 v = points[i] - a;
                float perp = Mathf.Abs(v.x * dir.y - v.y * dir.x);
                if (perp > maxDev) maxDev = perp;
            }

            // 0.15 = 현 길이의 15%까지 휘어도 직선으로 본다. 손으로 그은 선은 보통
            // 0.02~0.08이고, 기존 문양 중 가장 납작한 물결도 0.4를 넘는다.
            float devRatio = maxDev / len;
            if (devRatio > 0.15f) return false;

            // 화면 좌표는 y가 위로 증가한다 - 45도 = 우상향(/), 135도 = 우하향(\)
            float angle = Mathf.Repeat(Mathf.Atan2(chord.y, chord.x) * Mathf.Rad2Deg, 180f);

            name = "hline";
            float angleErr = Mathf.Min(angle, 180f - angle);
            float d = Mathf.Abs(angle - 45f);
            if (d < angleErr) { name = "slash"; angleErr = d; }
            d = Mathf.Abs(angle - 90f);
            if (d < angleErr) { name = "vline"; angleErr = d; }
            d = Mathf.Abs(angle - 135f);
            if (d < angleErr) { name = "backslash"; angleErr = d; }

            score = Mathf.Clamp01(1f - devRatio * 1.2f - Mathf.Max(0f, angleErr - 10f) / 30f);
            return true;
        }

        // ── $1 파이프라인 ──

        /// <summary>정방향/역방향 두 후보. 획 방향을 무시하기 위한 것.</summary>
        static Vector2[][] Candidates(IList<Vector2> points)
        {
            int n = points.Count;
            var fwd = new Vector2[n];
            var rev = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                fwd[i] = points[i];
                rev[i] = points[n - 1 - i];
            }
            return new[] { Normalize(fwd), Normalize(rev) };
        }

        static float DistanceTo(Vector2[][] cands, Vector2[] target)
            => Mathf.Min(BestAngle(cands[0], target), BestAngle(cands[1], target));

        static Vector2[] Normalize(Vector2[] p)
        {
            p = Resample(p, N);
            p = Rotate(p, -IndicativeAngle(p));
            p = ScaleToSquare(p, SIZE);
            return ToOrigin(p);
        }

        /// <summary>경로를 균등 간격 n개 점으로 다시 찍는다.</summary>
        static Vector2[] Resample(Vector2[] points, int n)
        {
            // 원본은 배열에 점을 삽입(splice)하며 진행한다. 리스트로 같은 동작을 재현한다.
            var pts = new List<Vector2>(points);
            float interval = PathLength(pts) / (n - 1);
            if (interval <= 0f) interval = 1f;

            float d = 0f;
            var outPts = new List<Vector2>(n) { pts[0] };

            for (int i = 1; i < pts.Count; i++)
            {
                float segment = Vector2.Distance(pts[i - 1], pts[i]);
                if (d + segment >= interval)
                {
                    float t = (interval - d) / (segment <= 0f ? 1f : segment);
                    var q = new Vector2(
                        pts[i - 1].x + t * (pts[i].x - pts[i - 1].x),
                        pts[i - 1].y + t * (pts[i].y - pts[i - 1].y));
                    outPts.Add(q);
                    pts.Insert(i, q);   // 삽입한 점에서 다음 구간을 이어 잰다
                    d = 0f;
                }
                else d += segment;
            }

            // 부동소수 누적 오차로 마지막 점이 모자랄 수 있다
            while (outPts.Count < n) outPts.Add(pts[pts.Count - 1]);

            var result = new Vector2[n];
            outPts.CopyTo(0, result, 0, n);
            return result;
        }

        static float PathLength(List<Vector2> p)
        {
            float d = 0f;
            for (int i = 1; i < p.Count; i++) d += Vector2.Distance(p[i - 1], p[i]);
            return d;
        }

        static Vector2 Centroid(Vector2[] p)
        {
            Vector2 c = Vector2.zero;
            for (int i = 0; i < p.Length; i++) c += p[i];
            return c / p.Length;
        }

        /// <summary>첫 점에서 무게중심으로 향하는 각. 회전 정규화의 기준.</summary>
        static float IndicativeAngle(Vector2[] p)
        {
            Vector2 c = Centroid(p);
            return Mathf.Atan2(c.y - p[0].y, c.x - p[0].x);
        }

        static Vector2[] Rotate(Vector2[] p, float angle)
        {
            Vector2 c = Centroid(p);
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            var outP = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float dx = p[i].x - c.x, dy = p[i].y - c.y;
                outP[i] = new Vector2(dx * cs - dy * sn + c.x, dx * sn + dy * cs + c.y);
            }
            return outP;
        }

        /// <summary>
        /// 바운딩 박스를 정사각으로 늘린다 (비균등 스케일).
        /// 종횡비를 버리므로 납작하게 그린 원도 원으로 인정된다 - $1의 의도된 동작이다.
        /// </summary>
        static Vector2[] ScaleToSquare(Vector2[] p, float size)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < p.Length; i++)
            {
                minX = Mathf.Min(minX, p[i].x); minY = Mathf.Min(minY, p[i].y);
                maxX = Mathf.Max(maxX, p[i].x); maxY = Mathf.Max(maxY, p[i].y);
            }
            float w = maxX - minX, h = maxY - minY;
            if (w <= 0f) w = 1f;
            if (h <= 0f) h = 1f;

            var outP = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
                outP[i] = new Vector2(p[i].x * size / w, p[i].y * size / h);
            return outP;
        }

        static Vector2[] ToOrigin(Vector2[] p)
        {
            Vector2 c = Centroid(p);
            var outP = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++) outP[i] = p[i] - c;
            return outP;
        }

        static float PathDistance(Vector2[] a, Vector2[] b)
        {
            float d = 0f;
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) d += Vector2.Distance(a[i], b[i]);
            return d / n;
        }

        /// <summary>
        /// 황금분할 탐색으로 최적 회전각을 찾는다. 구간은 +-30도로 제한 (원본의 의도적 변경).
        /// </summary>
        static float BestAngle(Vector2[] p, Vector2[] target)
        {
            const float Phi = 0.6180339887f;   // 0.5 * (-1 + sqrt(5))
            float a = -Mathf.PI / 6f, b = Mathf.PI / 6f;

            float x1 = Phi * a + (1f - Phi) * b;
            float f1 = PathDistance(Rotate(p, x1), target);
            float x2 = (1f - Phi) * a + Phi * b;
            float f2 = PathDistance(Rotate(p, x2), target);

            for (int i = 0; i < 8; i++)
            {
                if (f1 < f2)
                {
                    b = x2; x2 = x1; f2 = f1;
                    x1 = Phi * a + (1f - Phi) * b;
                    f1 = PathDistance(Rotate(p, x1), target);
                }
                else
                {
                    a = x1; x1 = x2; f1 = f2;
                    x2 = (1f - Phi) * a + Phi * b;
                    f2 = PathDistance(Rotate(p, x2), target);
                }
            }
            return Mathf.Min(f1, f2);
        }

        // ── 문양 정의 (recognizer.js 하단과 같은 좌표) ──

        // ─────────────────────────────────────────────────────────────────
        // 새 문양을 추가할 때
        //
        //   1. BuildTemplates()에 Add("이름", 좌표) 한 줄. 좌표는 -1..1 범위.
        //      거울상(좌우/상하/양쪽)은 AddMirrorVariants()가 자동으로 붙인다 -
        //      직접 넣지 말 것.
        //   2. "몇 바퀴 / 몇 번 꺾임"처럼 사람마다 횟수가 달라지는 문양이면
        //      AddDrawingVariants()에 그 폭을 AddVariant로 등록한다.
        //      이걸 빠뜨리면 정확히 그 횟수로 그린 사람만 통과한다(나선이 그랬다).
        //   3. Labels에 한국어 이름 추가. 없으면 키가 그대로 화면에 뜬다.
        //   4. BalanceData의 sigilEasy / sigilHard 중 하나에 넣어야 실제로 출제된다.
        //      여기까지 해야 게임에 등장한다.
        //   5. 기존 문양과 헷갈리지 않는지 확인한다. 특히 구간 수가 같은 것끼리
        //      충돌한다 - 정규화가 종횡비를 버리기 때문에 "2구간 꺾인 선"은
        //      방향이 달라도 비슷해진다(꺾쇠 vs 2꺾임 번개가 그랬다).
        //      테스트 씬의 "판정" 줄에서 분류가 빨간색으로 뜨면 그 충돌이다.
        //
        // 예외: 직선(hline/vline)은 이 절차를 따르지 않는다. $1이 직선을 다루지
        // 못해서(LineGlyphs 헤더) TryMatchLine이 파이프라인 밖에서 판정하고,
        // 등록도 RawTemplates에 표시용 좌표만 넣는다.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>문양 등록. 표시용 원본과 기본 변형을 함께 넣는다.</summary>
        static void Add(string name, Vector2[] pts)
        {
            RawTemplates[name] = pts;
            AddVariant(name, name, pts);
        }

        /// <summary>
        /// 같은 문양의 다른 그리기 방식을 추가 등록한다.
        /// 표시용 원본(<see cref="Raw"/>)은 바뀌지 않는다 - 화면에는 대표 모양만 보여준다.
        /// </summary>
        static void AddVariant(string owner, string variantKey, Vector2[] pts)
        {
            Templates[variantKey] = Normalize(pts);
            VariantOwner[variantKey] = owner;
            RawVariants[variantKey] = pts;
        }

        static void BuildTemplates()
        {
            // 원 - 12시에서 시작해 한 바퀴
            var circle = new Vector2[25];
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f * Mathf.PI * 2f - Mathf.PI / 2f;
                circle[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            }

            // 별 - 바깥/안쪽 반지름을 번갈아
            var star = new Vector2[11];
            for (int i = 0; i <= 10; i++)
            {
                float t = -Mathf.PI / 2f + i / 10f * Mathf.PI * 2f;
                float r = (i % 2) != 0 ? 0.42f : 1f;
                star[i] = new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            }

            // 나선 - 두 바퀴 돌며 반지름 증가
            var spiral = new Vector2[45];
            for (int i = 0; i <= 44; i++)
            {
                float t = i / 44f * Mathf.PI * 4f;
                float r = 0.12f + 0.88f * i / 44f;
                spiral[i] = new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            }

            // 물결 - 세로로 내려가며 좌우 진동
            var wave = new Vector2[21];
            for (int i = 0; i <= 20; i++)
            {
                float y = -1f + 2f * i / 20f;
                wave[i] = new Vector2(Mathf.Sin(i / 20f * Mathf.PI * 2f) * 0.9f, y);
            }

            Add("circle", circle);
            Add("triangle", Poly(3, -Mathf.PI / 2f));
            Add("square", Poly(4, -Mathf.PI / 4f));
            Add("star", star);
            Add("spiral", spiral);
            Add("wave", wave);
            Add("caret", new[] { new Vector2(-1f, 0.8f), new Vector2(0f, -1f), new Vector2(1f, 0.8f) });
            Add("zigzag", new[] { new Vector2(-1f, -1f), new Vector2(1f, -0.5f), new Vector2(-1f, 0.4f), new Vector2(1f, 1f) });

            // ── 확장 문양 (2026-08-18, 전체 20종 목표) ──

            // 체크 - 짧게 내려찍고 길게 올린다. 팔 길이 비대칭이 꺾쇠와의 구분점이다.
            Add("check", new[] { new Vector2(-1f, 0.3f), new Vector2(-0.4f, -1f), new Vector2(1f, 1f) });

            // ㄴ자 - 내려긋고 오른쪽으로. 직각 + 축 정렬.
            Add("lshape", new[] { new Vector2(-0.7f, 1f), new Vector2(-0.7f, -1f), new Vector2(1f, -1f) });

            // 계단 - 오른쪽/아래 번갈아 세 단.
            Add("stairs", new[]
            {
                new Vector2(-1f, 1f), new Vector2(-0.33f, 1f), new Vector2(-0.33f, 0.33f),
                new Vector2(0.33f, 0.33f), new Vector2(0.33f, -0.33f),
                new Vector2(1f, -0.33f), new Vector2(1f, -1f),
            });

            // M자 - 봉우리 두 개. 번개(대각 지그재그)와 달리 꼭짓점이 수직 방향이다.
            Add("mshape", new[]
            {
                new Vector2(-1f, -1f), new Vector2(-0.5f, 1f), new Vector2(0f, -0.6f),
                new Vector2(0.5f, 1f), new Vector2(1f, -1f),
            });

            Add("heart", MakeHeart());
            Add("infinity", MakeInfinity());
            Add("question", MakeQuestion());
            Add("loop", MakeLoop());

            // 직선 4종 - 표시용 원본만. Add()를 쓰지 않는 이유는 LineGlyphs 헤더 참고
            // ($1 템플릿 공간에 들어가면 안 된다). 시작점은 자연스러운 획 방향 기준.
            RawTemplates["hline"] = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            RawTemplates["vline"] = new[] { new Vector2(0f, 1f), new Vector2(0f, -1f) };
            RawTemplates["slash"] = new[] { new Vector2(1f, 1f), new Vector2(-1f, -1f) };
            RawTemplates["backslash"] = new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f) };

            AddDrawingVariants();
        }

        /// <summary>
        /// 사람이 실제로 그리는 방식의 변형들.
        ///
        /// 단일 템플릿은 "정확히 이 형태를 이 방향으로"만 인정한다. 화면의 고스트를 보고
        /// 따라 그릴 때 사람은 시작 위치도 회전 방향도 제각각인데, 그것들이 전부 실패한다.
        /// 실측한 실패 사례:
        ///   나선을 시계 방향으로 그림     -> 0.47, 번개로 분류
        ///   번개를 우상단에서 시작        -> 0.45, 물결로 분류
        ///   번개를 아래에서 위로 그림     -> 0.45, 물결로 분류
        /// 넷 중 셋이 실패하니 "20번 그려도 안 된다"가 나온다.
        ///
        /// 임계값을 낮춰도 해결되지 않는다 - 판정이 분류 일치를 요구하는데(SigilCapture),
        /// 이 경우 애초에 다른 문양이 이기기 때문이다.
        ///
        /// 획 방향 뒤집기(Candidates)로는 못 잡는다. 그것은 점 순서만 뒤집을 뿐
        /// 거울상을 만들지 않는데, 시계/반시계나 좌우 반전은 거울 대칭이기 때문이다.
        /// </summary>
        static void AddDrawingVariants()
        {
            // ── 나선: 바퀴 수 ──
            // 사람은 바퀴를 세면서 그리지 않는다. 2바퀴 고정이면 2.5바퀴가 0.2점이 된다.
            foreach (float turns in new[] { 1.25f, 1.5f, 2.5f, 3f })
                AddVariant("spiral", $"spiral@{turns}", MakeSpiral(turns));

            // ── 물결: 진동 횟수 ──
            AddVariant("wave", "wave@1.5", MakeWave(1.5f));
            AddVariant("wave", "wave@2", MakeWave(2f));

            // ── 번개: 꺾임 수 ──
            // 2꺾임 변형은 넣지 않는다. 꺾쇠(caret)도 2구간 꺾인 선이라 정규화 후 거의
            // 같아지고, 실측에서 꺾쇠의 37%를 번개가 가져갔다. 번개의 정체성은 3꺾임이므로
            // 2꺾임까지 인정할 이유가 없다 - 그렇게 그린 것은 실제로 꺾쇠에 가깝다.
            AddVariant("zigzag", "zigzag@4", new[]
            {
                new Vector2(-1f, -1f), new Vector2(1f, -0.6f), new Vector2(-1f, -0.1f),
                new Vector2(1f, 0.5f), new Vector2(-1f, 1f),
            });

            // ── 거울상 (시작 위치 / 회전 방향) ──
            // 지금까지 등록된 모든 변형에 좌우·상하 반전을 더한다. 마지막에 하는 이유는
            // 위에서 넣은 변형들까지 함께 반전시키기 위해서다.
            //
            // 대칭 도형(원·사각형 등)은 반전해도 같은 모양이라 후보만 늘고 판정은 그대로다.
            // 비대칭 도형(나선·번개·물결·꺾쇠)에서만 실질적인 효과가 있다.
            AddMirrorVariants();
        }

        /// <summary>
        /// 등록된 모든 변형의 좌우/상하/양쪽 반전을 추가한다.
        ///
        /// 반전을 문양별로 손으로 나열하지 않는 이유: 빠뜨리기 쉽고, 나중에 문양을
        /// 추가할 때 같은 실수를 반복한다. 규칙으로 두면 자동으로 적용된다.
        /// </summary>
        static void AddMirrorVariants()
        {
            // 순회 중에 컬렉션을 수정할 수 없으므로 먼저 복사한다.
            var seeds = new List<KeyValuePair<string, Vector2[]>>();
            foreach (var kv in RawVariants) seeds.Add(kv);

            foreach (var seed in seeds)
            {
                string owner = VariantOwner[seed.Key];
                AddVariant(owner, seed.Key + "|mx", Mirror(seed.Value, true, false));
                AddVariant(owner, seed.Key + "|my", Mirror(seed.Value, false, true));
                AddVariant(owner, seed.Key + "|mxy", Mirror(seed.Value, true, true));
            }
        }

        static Vector2[] Mirror(Vector2[] pts, bool flipX, bool flipY)
        {
            var a = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                a[i] = new Vector2(flipX ? -pts[i].x : pts[i].x,
                                   flipY ? -pts[i].y : pts[i].y);
            return a;
        }

        /// <summary>나선 - turns 바퀴.</summary>
        static Vector2[] MakeSpiral(float turns)
        {
            var a = new Vector2[45];
            for (int i = 0; i <= 44; i++)
            {
                float t = i / 44f * Mathf.PI * 2f * turns;
                float r = 0.12f + 0.88f * i / 44f;
                a[i] = new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r);
            }
            return a;
        }

        /// <summary>물결 - cycles 주기만큼 좌우 진동하며 세로로 내려간다.</summary>
        static Vector2[] MakeWave(float cycles)
        {
            var a = new Vector2[21];
            for (int i = 0; i <= 20; i++)
            {
                float y = -1f + 2f * i / 20f;
                a[i] = new Vector2(Mathf.Sin(i / 20f * Mathf.PI * 2f * cycles) * 0.9f, y);
            }
            return a;
        }

        /// <summary>하트 - 위 가운데 홈에서 시작해 한 바퀴. 매개변수 하트 곡선.</summary>
        static Vector2[] MakeHeart()
        {
            var a = new Vector2[33];
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                float s = Mathf.Sin(t);
                a[i] = new Vector2(s * s * s,
                    (13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t)
                     - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t)) / 17f);
            }
            return a;
        }

        /// <summary>무한대 - 오른쪽 끝에서 시작하는 옆으로 누운 8. 세로 8도 같은 문양으로
        /// 본다 (정규화가 종횡비를 버리므로 자동으로 그렇게 된다).
        ///
        /// 시작점을 가운데 교차점에 두면 안 된다 - 교차점이 무게중심과 같은 자리라
        /// 회전 정규화 기준각(첫 점 -> 무게중심)이 손떨림에 따라 널뛴다.</summary>
        static Vector2[] MakeInfinity()
        {
            var a = new Vector2[33];
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                a[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(2f * t) * 0.45f);
            }
            return a;
        }

        /// <summary>물음표 (점 없음) - 왼쪽에서 위를 돌아 내려온 뒤 꼬리가 수직 낙하.</summary>
        static Vector2[] MakeQuestion()
        {
            var a = new Vector2[20];
            for (int i = 0; i < 16; i++)
            {
                // 호: 중심 (0, 0.45), 반지름 0.55, 180도에서 -90도까지
                float ang = Mathf.PI - i / 15f * Mathf.PI * 1.5f;
                a[i] = new Vector2(Mathf.Cos(ang) * 0.55f, 0.45f + Mathf.Sin(ang) * 0.55f);
            }
            for (int i = 16; i < 20; i++)
                a[i] = new Vector2(0f, -0.1f - (i - 16) / 3f * 0.9f);
            return a;
        }

        /// <summary>고리 - 좌하단에서 올라가 반시계로 한 바퀴 감고 우하단으로 빠진다.
        /// 진입선과 탈출선이 교차하는 리본 매듭 모양.</summary>
        static Vector2[] MakeLoop()
        {
            var a = new Vector2[24];
            for (int i = 0; i < 5; i++)
                a[i] = new Vector2(-1f + i / 4f * 1.35f, -1f + i / 4f * 1.1f);
            for (int i = 0; i < 15; i++)
            {
                // 고리: 중심 (0, 0.35), 반지름 0.5, 진입각 -45도에서 반시계 300도
                float ang = -Mathf.PI / 4f + i / 14f * Mathf.PI * 2f * 0.83f;
                a[5 + i] = new Vector2(Mathf.Cos(ang) * 0.5f, 0.35f + Mathf.Sin(ang) * 0.5f);
            }
            Vector2 exit = a[19];
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 1) / 4f;
                a[20 + i] = new Vector2(exit.x + (1f - exit.x) * t, exit.y + (-1f - exit.y) * t);
            }
            return a;
        }

        /// <summary>정n각형. 마지막 점이 첫 점과 같아 닫힌 경로가 된다.</summary>
        static Vector2[] Poly(int n, float rot)
        {
            var a = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = rot + i / (float)n * Mathf.PI * 2f;
                a[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            }
            return a;
        }
    }
}
