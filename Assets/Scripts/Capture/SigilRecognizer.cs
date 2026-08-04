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

        /// <summary>문양 이름 -> 표시용 원본 좌표 (대표 변형 하나).</summary>
        static readonly Dictionary<string, Vector2[]> RawTemplates = new Dictionary<string, Vector2[]>();

        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "circle", "원" }, { "triangle", "삼각형" }, { "square", "사각형" },
            { "star", "별" }, { "spiral", "나선" }, { "wave", "물결" },
            { "caret", "꺾쇠" }, { "zigzag", "번개" },
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

            AddDrawingVariants();
        }

        /// <summary>
        /// 사람이 실제로 그리는 방식의 변형들.
        ///
        /// 왜 필요한가: 단일 템플릿은 "정확히 이 형태"만 인정한다. 그런데 화면의 고스트를
        /// 보고 따라 그릴 때 사람은 바퀴 수를 세지 않고, 꺾임 수도 정확히 맞추지 않는다.
        /// 나선을 2바퀴 기준으로만 두면 2.5바퀴를 그린 사람은 0.2점을 받는다 - 분명히
        /// 나선을 그렸는데도 실패한다. 변형을 함께 등록해 그 폭을 인정한다.
        ///
        /// 반대 방향(예: 나선을 다른 문양으로 오분류)은 아래 검증에서 확인했다.
        /// </summary>
        static void AddDrawingVariants()
        {
            // ── 나선: 바퀴 수 ──
            // 가장 취약했다. 사람은 바퀴를 세면서 그리지 않는다.
            foreach (float turns in new[] { 1.25f, 1.5f, 2.5f, 3f })
                AddVariant("spiral", $"spiral@{turns}", MakeSpiral(turns));

            // ── 물결: 진동 횟수 ──
            // 기본은 한 주기(위아래 한 번)다. 두 번 꺾어 그리는 사람이 많다.
            AddVariant("wave", "wave@1.5", MakeWave(1.5f));
            AddVariant("wave", "wave@2", MakeWave(2f));

            // ── 번개: 꺾임 수 ──
            // 기본은 3번 꺾인다. 2번이나 4번으로 그려도 번개로 인정한다.
            AddVariant("zigzag", "zigzag@2", new[]
            {
                new Vector2(-1f, -1f), new Vector2(1f, -0.2f), new Vector2(-1f, 1f),
            });
            AddVariant("zigzag", "zigzag@4", new[]
            {
                new Vector2(-1f, -1f), new Vector2(1f, -0.6f), new Vector2(-1f, -0.1f),
                new Vector2(1f, 0.5f), new Vector2(-1f, 1f),
            });
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
