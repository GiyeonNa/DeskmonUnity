using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 151 도감 원장(기획서 §17 "도감 번호 기준 원장 (정본)") 파서.
    ///
    /// 왜 md를 직접 읽는가: 원장 표가 곧 정본이다. 표를 C#으로 옮겨 적으면 그 순간부터
    /// 두 벌이 되고, 기획서를 고친 사람은 코드 쪽을 잊는다. 임포터가 문서를 직접 읽으면
    /// "기획서 §17 수정 -> [Deskmon/데이터 임포트]"만으로 게임 데이터가 따라온다.
    ///
    /// 진화군 묶기: 원장은 진화체마다 개별 도감 번호를 갖는 평평한 151행이다 (§8.2).
    /// 게임 모델(SpeciesData)은 라인 단위이므로, "진화 1/N" 행이 새 라인을 열고
    /// 뒤따르는 2/N..N/N 행이 같은 라인에 붙는 규칙으로 복원한다. 원장이 진화군을
    /// 연속 번호로 배치한다는 §17의 전제에 기대며, 어긋나면 예외로 즉시 알린다.
    /// </summary>
    public static class DexLedger
    {
        public const string DOC_PATH = "Docs/151종_몬스터_스프라이트_생성_기획서.md";

        /// <summary>원장 한 행 = 도감 엔트리 하나 (폼 하나).</summary>
        public class Entry
        {
            public int no;
            public string id, koName, enName;
            public int stage, totalStages;
            public Field field;
            public string subfield;
            public Rarity rarity;
            public string prestige;
            public string desc;
        }

        /// <summary>진화군 하나 = SpeciesData 하나.</summary>
        public class Line
        {
            public List<Entry> forms = new List<Entry>();
            public Entry Base => forms[0];
        }

        // | 001 | mongle | 몽글이 | Mongle | 1/3 | Grass/Meadow | Common | RegionalIcon | 설명... |
        static readonly Regex RowRx = new Regex(
            @"^\|\s*(\d{3})\s*\|([^|]*)\|([^|]*)\|([^|]*)\|\s*(\d)\s*/\s*(\d)\s*\|([^|]*)\|([^|]*)\|([^|]*)\|(.*)\|\s*$");

        public static List<Line> ParseLines()
        {
            var entries = ParseEntries();
            var lines = new List<Line>();
            Line current = null;

            foreach (var e in entries)
            {
                if (e.stage == 1)
                {
                    current = new Line();
                    lines.Add(current);
                }
                else
                {
                    if (current == null || e.stage != current.forms.Count + 1)
                        throw new InvalidDataException(
                            $"원장 No.{e.no:000} {e.id}: 진화 {e.stage}/{e.totalStages}인데 앞선 단계가 없다. §17 표의 진화군 연속 배치가 깨졌다.");
                }
                current.forms.Add(e);

                if (e.totalStages != current.Base.totalStages)
                    throw new InvalidDataException(
                        $"원장 No.{e.no:000} {e.id}: 진화 총 단계({e.totalStages})가 라인 시작({current.Base.totalStages})과 다르다.");
            }

            foreach (var line in lines)
                if (line.forms.Count != line.Base.totalStages)
                    throw new InvalidDataException(
                        $"원장 {line.Base.id} 라인: {line.Base.totalStages}단계여야 하는데 {line.forms.Count}행만 있다.");

            return lines;
        }

        public static List<Entry> ParseEntries()
        {
            if (!File.Exists(DOC_PATH))
                throw new FileNotFoundException($"151 원장 문서를 찾지 못했다: {DOC_PATH}");

            var result = new List<Entry>();
            bool inSection = false;

            foreach (var raw in File.ReadAllLines(DOC_PATH))
            {
                if (raw.StartsWith("## "))
                {
                    inSection = raw.StartsWith("## 17.");
                    continue;
                }
                if (!inSection) continue;

                var m = RowRx.Match(raw);
                if (!m.Success) continue;

                var fieldPair = m.Groups[7].Value.Trim().Split('/');
                var e = new Entry
                {
                    no = int.Parse(m.Groups[1].Value),
                    id = m.Groups[2].Value.Trim(),
                    koName = m.Groups[3].Value.Trim(),
                    enName = m.Groups[4].Value.Trim(),
                    stage = int.Parse(m.Groups[5].Value),
                    totalStages = int.Parse(m.Groups[6].Value),
                    subfield = fieldPair.Length > 1 ? fieldPair[1].Trim() : "",
                    prestige = m.Groups[9].Value.Trim(),
                    desc = m.Groups[10].Value.Trim(),
                };

                if (!Enum.TryParse(fieldPair[0].Trim(), out e.field))
                    throw new InvalidDataException($"원장 No.{e.no:000} {e.id}: 알 수 없는 필드 '{fieldPair[0].Trim()}'. Field 열거형에 추가해야 한다.");
                if (!Enum.TryParse(m.Groups[8].Value.Trim(), true, out e.rarity))
                    throw new InvalidDataException($"원장 No.{e.no:000} {e.id}: 알 수 없는 희귀도 '{m.Groups[8].Value.Trim()}'.");

                result.Add(e);
            }

            if (result.Count == 0)
                throw new InvalidDataException($"§17 표에서 행을 하나도 읽지 못했다: {DOC_PATH}");
            return result;
        }
    }
}
