using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Deskmon.Capture;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 문양 인식 자가 점검. 문양을 추가하거나 변형을 손봤을 때 돌린다.
    ///
    /// 왜 필요한가: 문양끼리의 충돌은 조용히 생긴다. 번개에 2꺾임 변형을 넣었더니
    /// 꺾쇠의 37%를 가져간 적이 있는데, 컴파일도 되고 번개 자체는 잘 되니까
    /// 직접 꺾쇠를 여러 번 그려보기 전에는 알 수가 없었다.
    ///
    /// 사용: 메뉴 [Deskmon/각인 인식 자가 점검]
    /// </summary>
    public static class SigilSelfCheck
    {
        /// <summary>이 비율 아래면 실패로 본다.</summary>
        const float PASS_RATE = 0.9f;

        [MenuItem("Deskmon/각인 인식 자가 점검")]
        public static void Run()
        {
            var names = new List<string>(SigilRecognizer.Names);
            if (names.Count == 0)
            {
                Debug.LogError("[각인 점검] 등록된 문양이 없습니다.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[각인 점검] 문양 {names.Count}종");

            int failed = 0;

            // ── 1. 자기 분류 ──
            // 각 문양을 손떨림을 섞어 그렸을 때 자기 자신으로 분류되는가.
            foreach (var name in names)
            {
                int hit = 0;
                const int TRIES = 60;

                for (int s = 0; s < TRIES; s++)
                {
                    var pts = Trace(SigilRecognizer.Raw(name), 34f, 0.15f, s * 17);
                    if (pts.Count < 8) continue;
                    if (SigilRecognizer.Recognize(pts).name == name) hit++;
                }

                float rate = hit / (float)TRIES;
                bool ok = rate >= PASS_RATE;
                if (!ok) failed++;

                sb.AppendLine($"  {(ok ? "OK  " : "실패")} {name,-10} {rate * 100:F0}%");

                // 실패했다면 무엇에 밀렸는지도 알려준다 - 원인을 바로 좁힐 수 있게.
                if (!ok) sb.AppendLine($"        -> {Confusions(name)}");
            }

            // ── 2. 거울상 ──
            // 좌우/상하로 뒤집어 그려도 같은 문양으로 인식되는가.
            // 여기서 실패하면 AddMirrorVariants가 그 문양에 적용되지 않은 것이다.
            sb.AppendLine();
            sb.AppendLine("  거울상 (뒤집어 그려도 인식되는가)");

            foreach (var name in names)
            {
                var raw = SigilRecognizer.Raw(name);
                bool allOk = true;

                foreach (var m in new[] { (true, false), (false, true), (true, true) })
                {
                    var mirrored = Mirror(raw, m.Item1, m.Item2);
                    int hit = 0;
                    const int TRIES = 30;

                    for (int s = 0; s < TRIES; s++)
                    {
                        var pts = Trace(mirrored, 34f, 0.15f, s * 13);
                        if (pts.Count < 8) continue;
                        if (SigilRecognizer.Recognize(pts).name == name) hit++;
                    }

                    if (hit / (float)TRIES < PASS_RATE) allOk = false;
                }

                if (!allOk) { failed++; sb.AppendLine($"    실패 {name}"); }
            }
            if (failed == 0) sb.AppendLine("    전부 OK");

            // ── 3. 출제 목록과의 정합 ──
            // 인식기에는 있는데 밸런스에 없으면 게임에 안 나오고,
            // 밸런스에만 있으면 그릴 수 없는 문양이 출제된다.
            sb.AppendLine();
            CheckBalance(names, sb, ref failed);

            sb.AppendLine();
            sb.AppendLine(failed == 0 ? "  결과: 전부 통과" : $"  결과: 실패 {failed}건");

            if (failed == 0) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }

        /// <summary>무엇으로 잘못 분류되는지 상위 항목.</summary>
        static string Confusions(string name)
        {
            var lose = new Dictionary<string, int>();
            for (int s = 0; s < 60; s++)
            {
                var pts = Trace(SigilRecognizer.Raw(name), 34f, 0.15f, s * 17);
                if (pts.Count < 8) continue;

                var got = SigilRecognizer.Recognize(pts).name;
                if (got == name || got == null) continue;

                lose.TryGetValue(got, out int n);
                lose[got] = n + 1;
            }

            if (lose.Count == 0) return "(점수 미달 - 형태가 너무 흔들림)";

            var sb = new StringBuilder("밀린 상대: ");
            foreach (var kv in lose) sb.Append($"{kv.Key}({kv.Value})  ");
            return sb.ToString();
        }

        static void CheckBalance(List<string> names, StringBuilder sb, ref int failed)
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceData>("Assets/Data/Balance.asset");
            if (balance == null)
            {
                sb.AppendLine("  출제 목록: Balance.asset이 없어 건너뜀");
                return;
            }

            var listed = new HashSet<string>();
            if (balance.sigilEasy != null) foreach (var g in balance.sigilEasy) listed.Add(g);
            if (balance.sigilHard != null) foreach (var g in balance.sigilHard) listed.Add(g);

            sb.AppendLine("  출제 목록 정합");

            foreach (var n in names)
                if (!listed.Contains(n))
                    sb.AppendLine($"    주의 {n} - 인식기에 있지만 출제되지 않는다 (Balance의 easy/hard에 없음)");

            foreach (var g in listed)
                if (!names.Contains(g))
                {
                    failed++;
                    sb.AppendLine($"    실패 {g} - 출제되지만 인식기에 템플릿이 없다 (절대 성공할 수 없음)");
                }
        }

        /// <summary>마우스로 따라 그린 획을 흉내낸다. AddPoint의 2px 필터까지 재현한다.</summary>
        static List<Vector2> Trace(Vector2[] tpl, float radius, float noise, int seed)
        {
            var rng = new System.Random(seed);
            var frames = new List<Vector2>();
            int segments = tpl.Length - 1;

            // 1.3초를 60fps로 그린 정도
            for (int f = 0; f < 80; f++)
            {
                float p = f / 79f * segments;
                int i = Mathf.Min((int)p, segments - 1);
                float t = p - i;

                float nx = (float)(rng.NextDouble() - 0.5) * noise;
                float ny = (float)(rng.NextDouble() - 0.5) * noise;

                frames.Add(new Vector2(
                    (tpl[i].x + (tpl[i + 1].x - tpl[i].x) * t + nx) * radius + 500f,
                    (tpl[i].y + (tpl[i + 1].y - tpl[i].y) * t + ny) * radius + 400f));
            }

            var kept = new List<Vector2> { frames[0] };
            foreach (var p in frames)
                if (Vector2.Distance(kept[kept.Count - 1], p) >= 2f) kept.Add(p);

            return kept;
        }

        static Vector2[] Mirror(Vector2[] pts, bool flipX, bool flipY)
        {
            var a = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                a[i] = new Vector2(flipX ? -pts[i].x : pts[i].x, flipY ? -pts[i].y : pts[i].y);
            return a;
        }
    }
}
