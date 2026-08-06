namespace Deskmon.Core
{
    /// <summary>
    /// 진화. index.html의 evoBlockReason()/evolve()/evolveAll()/releaseOne() 이식.
    /// 기획서 v4 §4.1 - 개체 조건 진화 (3마리 합체는 2026-08-03 폐기).
    ///
    /// 규칙 요약:
    ///   1마리 소모 -> 상위 폼 1마리 (머지 아님)
    ///   공통 조건 = 폼 친밀 레벨 (기본형 Lv2, 1차 진화형 Lv3)
    ///   라인별 추가 조건 = 야행(밤에만) / 만복(간식 기록 필요)
    ///   샤이니는 샤이니 트랙에서 따로 진화한다
    /// </summary>
    public static class EvolutionSystem
    {
        /// <summary>진화가 막힌 이유. index.html evoBlockReason()의 반환값과 1:1.</summary>
        public enum BlockReason
        {
            /// <summary>진화 가능.</summary>
            None,
            /// <summary>그 폼의 개체가 없다.</summary>
            NoCreature,
            /// <summary>최종 폼이라 더 진화할 수 없다.</summary>
            FinalStage,
            /// <summary>친밀 레벨 부족.</summary>
            Friendship,
            /// <summary>야행 진화 - 밤(18~06시)이 아니다.</summary>
            NeedNight,
            /// <summary>만복 진화 - 간식을 먹인 기록이 없다.</summary>
            NeedFed,
        }

        /// <summary>
        /// 진화 가능 여부와 이유. 검사 순서도 원본과 같다 -
        /// 개체 -> 친밀 -> 밤 -> 만복. UI가 이유를 그대로 안내한다.
        /// </summary>
        public static BlockReason Check(SaveData save, DeskmonDatabase db,
                                        string speciesId, int stage, bool shiny, in WorldConditions w)
        {
            var sp = db?.Get(speciesId);
            if (sp == null || save == null || db.balance == null) return BlockReason.NoCreature;
            if (stage >= sp.forms - 1) return BlockReason.FinalStage;

            var c = save.Creature(speciesId);
            int count = shiny ? c.shiny[stage] : c.s[stage];
            if (count < 1) return BlockReason.NoCreature;

            if (FriendshipSystem.Level(save, db.balance, speciesId, stage) < db.balance.EvolveLevelFor(stage))
                return BlockReason.Friendship;

            if (sp.evolveCondition == EvolveCondition.Night && !w.isNight) return BlockReason.NeedNight;
            if (sp.evolveCondition == EvolveCondition.Fed
                && !save.Fed(SaveData.Key(speciesId, stage))) return BlockReason.NeedFed;

            return BlockReason.None;
        }

        /// <summary>
        /// 진화 실행. index.html evolve() 이식.
        /// 성공하면 도감 등록과 마일스톤까지 처리된다 (CreatureRegistry.RegisterForm).
        /// 방목 목록 정리도 여기서 한다 - 마지막 개체가 진화하면 유령 방목이 남는다.
        /// </summary>
        public static BlockReason TryEvolve(SaveData save, DeskmonDatabase db,
                                            string speciesId, int stage, bool shiny, in WorldConditions w)
        {
            var reason = Check(save, db, speciesId, stage, shiny, w);
            if (reason != BlockReason.None) return reason;

            var c = save.Creature(speciesId);
            var arr = shiny ? c.shiny : c.s;
            arr[stage] -= 1;
            arr[stage + 1] += 1;

            CreatureRegistry.RegisterForm(save, db, db.Get(speciesId), stage + 1, shiny);
            RoamSystem.Validate(save);

            return BlockReason.None;
        }

        /// <summary>
        /// 일괄 진화. index.html evolveAll() - 가능한 것을 전부, 연쇄 포함.
        /// 낮은 폼부터 돌아야 진화로 생긴 상위 폼이 같은 호출에서 또 진화할 수 있다.
        /// 반환은 진화한 횟수.
        /// </summary>
        public static int EvolveAll(SaveData save, DeskmonDatabase db, in WorldConditions w)
        {
            if (save == null || db == null) return 0;

            int total = 0;
            foreach (var sp in db.species)
            {
                if (sp == null) continue;
                for (int stage = 0; stage < sp.forms - 1; stage++)
                {
                    for (int track = 0; track < 2; track++)
                    {
                        bool shiny = track == 1;
                        // 같은 폼에 여러 마리가 있으면 조건이 유지되는 한 전부 진화한다
                        while (TryEvolve(save, db, sp.id, stage, shiny, w) == BlockReason.None)
                            total++;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// 돌려보내기 - 중복 개체의 출구. index.html releaseOne()/releaseValue() 이식.
        /// 해당 폼 RELEASE_SEC(30초)치 생산량을 베리로 환산한다 (최소 3).
        /// </summary>
        public static int ReleaseOne(SaveData save, DeskmonDatabase db,
                                     string speciesId, int stage, bool shiny)
        {
            var sp = db?.Get(speciesId);
            if (sp == null || save == null || db.balance == null) return 0;

            var c = save.Creature(speciesId);
            var arr = shiny ? c.shiny : c.s;
            if (arr[stage] < 1) return 0;

            arr[stage] -= 1;

            float perSec = sp.ProductionAt(stage, db.balance)
                         * (shiny ? db.balance.shinyMultiplier : 1f);
            int value = UnityEngine.Mathf.Max(3,
                UnityEngine.Mathf.RoundToInt(perSec * db.balance.releaseSec));

            save.berry += value;
            RoamSystem.Validate(save);
            return value;
        }
    }
}
