using System.Text;
using UnityEditor;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// S3 로직(방목·친밀도·진화) 자가 점검. 임시 세이브로 규칙을 검사한다 - 실제 세이브는 건드리지 않는다.
    ///
    /// 사용: 메뉴 [Deskmon/S3 로직 자가 점검]
    /// </summary>
    public static class S3SelfCheck
    {
        static int _fail;
        static StringBuilder _sb;

        [MenuItem("Deskmon/S3 로직 자가 점검")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<DeskmonDatabase>("Assets/Data/DeskmonDB.asset");
            if (db == null || db.balance == null)
            {
                Debug.LogError("[S3 점검] DeskmonDB/Balance가 없습니다 - 데이터 임포트를 먼저 실행하세요.");
                return;
            }

            _fail = 0;
            _sb = new StringBuilder("[S3 점검]\n");

            var save = SaveSystem.Fresh(db);   // 임시 세이브 - 파일에 저장하지 않는다

            // ── 방목 키 ──
            Check("키 형식", RoamSystem.Key("mongle", 1, true) == "mongle:1:1", RoamSystem.Key("mongle", 1, true));
            Check("키 해석", RoamSystem.TryParse("owl:0:0", out var pid, out var pst, out var psh)
                             && pid == "owl" && pst == 0 && !psh, "owl:0:0");
            Check("깨진 키 거부", !RoamSystem.TryParse("mongle:x:1", out _, out _, out _), "");

            // ── 슬롯 - 기본 2, 필드당 +1, 4필드 전부 해금 시 5 ──
            Check("슬롯 기본", RoamSystem.Slots(save, db.balance) == 2, $"{RoamSystem.Slots(save, db.balance)}");
            save.habitats.Add("forest"); save.habitats.Add("lake"); save.habitats.Add("office");
            Check("슬롯 전체 해금 = 5", RoamSystem.Slots(save, db.balance) == 5, $"{RoamSystem.Slots(save, db.balance)}");

            // ── 방목 토글 ──
            Check("미보유 방목 거부",
                RoamSystem.Toggle(save, db.balance, "mongle", 0, false) == RoamSystem.ToggleResult.NotOwned, "");

            save.Creature("mongle").s[0] = 3;
            Check("방목 켜기",
                RoamSystem.Toggle(save, db.balance, "mongle", 0, false) == RoamSystem.ToggleResult.Added, "");
            Check("방목 끄기(회수)",
                RoamSystem.Toggle(save, db.balance, "mongle", 0, false) == RoamSystem.ToggleResult.Removed, "");

            // 슬롯 가득
            RoamSystem.Toggle(save, db.balance, "mongle", 0, false);
            save.Creature("dewdrop").s[0] = 1; RoamSystem.Toggle(save, db.balance, "dewdrop", 0, false);
            save.Creature("dotori").s[0] = 1; RoamSystem.Toggle(save, db.balance, "dotori", 0, false);
            save.Creature("mossy").s[0] = 1; RoamSystem.Toggle(save, db.balance, "mossy", 0, false);
            save.Creature("mush").s[0] = 1; RoamSystem.Toggle(save, db.balance, "mush", 0, false);
            save.Creature("owl").s[0] = 1;
            Check("슬롯 가득 거부",
                RoamSystem.Toggle(save, db.balance, "owl", 0, false) == RoamSystem.ToggleResult.Full,
                $"{save.roam.Count}/5");

            // ── 유령 방목 정리 ──
            save.Creature("dewdrop").s[0] = 0;
            RoamSystem.Validate(save);
            Check("보유 0 정리", !save.roam.Contains("dewdrop:0:0"), "");

            // ── 친밀도 ──
            save.berry = 100;
            int lv0 = FriendshipSystem.Level(save, db.balance, "mongle", 0);
            var pet = FriendshipSystem.Pet(save, db.balance, "mongle", 0);
            Check("쓰다듬기 베리 2~5", pet.ok && pet.berry >= 2 && pet.berry <= 5, $"+{pet.berry}");
            Check("쓰다듬기 친밀 +2", FriendshipSystem.Points(save, "mongle", 0) == 2, $"{FriendshipSystem.Points(save, "mongle", 0)}pt");

            var snack = FriendshipSystem.Snack(save, db.balance, "mongle", 0);
            Check("간식 친밀 +6 · 만복 기록", snack.ok
                && FriendshipSystem.Points(save, "mongle", 0) == 8
                && save.Fed(SaveData.Key("mongle", 0)), $"{FriendshipSystem.Points(save, "mongle", 0)}pt");
            Check("8pt = Lv1 (문턱 8)", FriendshipSystem.Level(save, db.balance, "mongle", 0) == 1, "");

            save.berry = 1;
            Check("베리 부족 간식 거부", FriendshipSystem.Snack(save, db.balance, "mongle", 0).notEnoughBerry, "");

            // ── 진화 ──
            var day = new WorldConditions { isNight = false };
            var night = new WorldConditions { isNight = true };

            // 친밀 게이트: Lv1로는 부족 (기본형은 Lv2 = 20pt)
            Check("친밀 부족 차단",
                EvolutionSystem.Check(save, db, "mongle", 0, false, day) == EvolutionSystem.BlockReason.Friendship, "");

            save.SetFriend(SaveData.Key("mongle", 0), 20);   // Lv2
            Check("친밀 충족 시 통과",
                EvolutionSystem.Check(save, db, "mongle", 0, false, day) == EvolutionSystem.BlockReason.None, "");

            int before = save.Creature("mongle").s[0];
            Check("진화 실행 (1 소모 -> 상위 1)",
                EvolutionSystem.TryEvolve(save, db, "mongle", 0, false, day) == EvolutionSystem.BlockReason.None
                && save.Creature("mongle").s[0] == before - 1
                && save.Creature("mongle").s[1] == 1
                && save.Dex("mongle").forms[1], "");

            // 야행 게이트 (부엉)
            save.SetFriend(SaveData.Key("owl", 0), 20);
            Check("야행 - 낮 차단",
                EvolutionSystem.Check(save, db, "owl", 0, false, day) == EvolutionSystem.BlockReason.NeedNight, "");
            Check("야행 - 밤 통과",
                EvolutionSystem.Check(save, db, "owl", 0, false, night) == EvolutionSystem.BlockReason.None, "");

            // 만복 게이트 (버섯쫑)
            save.SetFriend(SaveData.Key("mush", 0), 20);
            Check("만복 - 간식 전 차단",
                EvolutionSystem.Check(save, db, "mush", 0, false, day) == EvolutionSystem.BlockReason.NeedFed, "");
            save.SetFed(SaveData.Key("mush", 0), true);
            Check("만복 - 간식 후 통과",
                EvolutionSystem.Check(save, db, "mush", 0, false, day) == EvolutionSystem.BlockReason.None, "");

            // 연쇄: 기본형 여럿 + 전 단계 친밀 충족 -> 일괄에서 최종형까지
            var chain = SaveSystem.Fresh(db);
            chain.Creature("mongle").s[0] = 2;
            chain.SetFriend(SaveData.Key("mongle", 0), 20);   // Lv2
            chain.SetFriend(SaveData.Key("mongle", 1), 40);   // Lv3 (1차형 문턱)
            // 기본형 2마리 -> 1차형 2마리 -> 최종형 2마리. 조건이 유지되는 한 전부 연쇄한다.
            int evolved = EvolutionSystem.EvolveAll(chain, db, day);
            Check("일괄 연쇄 진화", evolved == 4
                && chain.Creature("mongle").s[0] == 0
                && chain.Creature("mongle").s[1] == 0
                && chain.Creature("mongle").s[2] == 2,
                $"{evolved}회, 폼 분포 {chain.Creature("mongle").s[0]}/{chain.Creature("mongle").s[1]}/{chain.Creature("mongle").s[2]}");

            // 마지막 개체 진화 시 유령 방목 정리 (2폼 무조건 진화 라인이면 어느 종이든 된다)
            var ghost = SaveSystem.Fresh(db);
            ghost.Creature("dewdrop").s[0] = 1;
            ghost.SetFriend(SaveData.Key("dewdrop", 0), 20);
            RoamSystem.Toggle(ghost, db.balance, "dewdrop", 0, false);
            EvolutionSystem.TryEvolve(ghost, db, "dewdrop", 0, false, day);
            Check("진화 후 유령 방목 정리", !ghost.roam.Contains("dewdrop:0:0"), "");

            // 돌려보내기 - 최소 3
            var rel = SaveSystem.Fresh(db);
            rel.Creature("mongle").s[0] = 1;
            double b0 = rel.berry;
            int gained = EvolutionSystem.ReleaseOne(rel, db, "mongle", 0, false);
            Check("돌려보내기 (최소 3베리)", gained >= 3 && rel.berry == b0 + gained
                && rel.Creature("mongle").s[0] == 0, $"+{gained}");

            _sb.AppendLine(_fail == 0 ? "  결과: 전부 통과" : $"  결과: 실패 {_fail}건");
            if (_fail == 0) Debug.Log(_sb.ToString());
            else Debug.LogError(_sb.ToString());
        }

        static void Check(string label, bool ok, string detail)
        {
            if (!ok) _fail++;
            _sb.AppendLine($"  {(ok ? "OK  " : "실패")} {label}  {detail}");
        }
    }
}
