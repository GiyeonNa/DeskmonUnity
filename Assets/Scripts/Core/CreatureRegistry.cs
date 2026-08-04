using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 포획 결과를 세이브에 반영한다. index.html addCreature() + checkMilestone() 이식.
    ///
    /// 각인을 다 그린 뒤 실제로 "잡았다"가 성립하는 지점이 여기다. S2의 DoD가
    /// "한 종을 잡아 도감에 등록"이므로 이 클래스가 그 마지막 연결부다.
    ///
    /// 순수 로직으로 둔다(MonoBehaviour 아님) - 세이브만 다루므로 씬에 붙을 이유가 없고,
    /// 테스트에서 그냥 호출할 수 있어야 한다.
    /// </summary>
    public static class CreatureRegistry
    {
        /// <summary>보상/알림을 호출부에 알린다. UI가 없어도 로직은 돌아야 하므로 이벤트로 뺀다.</summary>
        public struct CatchResult
        {
            public SpeciesData species;
            public bool shiny;
            /// <summary>이 종을 처음 잡았는가 - 도감 카드를 띄울 조건.</summary>
            public bool firstCatch;
            /// <summary>첫 샤이니 보상을 받았는가.</summary>
            public bool firstShinyBonus;
            /// <summary>이 종의 라인을 완성했는가.</summary>
            public bool lineCompleted;
            /// <summary>필드 도감을 완성했는가. 완성한 필드 id, 아니면 null.</summary>
            public string fieldCompleted;
            /// <summary>이번 포획으로 얻은 베리 총합 (마일스톤 보상).</summary>
            public int berryGained;
        }

        /// <summary>첫 샤이니 보상. index.html:373</summary>
        public const int FIRST_SHINY_BERRY = 200;
        /// <summary>필드 도감 완성 보상. index.html:467</summary>
        public const int FIELD_COMPLETE_BERRY = 500;

        /// <summary>
        /// 포획 확정. index.html addCreature() 이식.
        /// 항상 0단계(기본형)로 들어온다 - 진화는 친밀도로만 일어난다.
        /// </summary>
        public static CatchResult Add(SaveData save, DeskmonDatabase db, SpeciesData species, bool shiny)
        {
            var result = new CatchResult { species = species, shiny = shiny };
            if (save == null || species == null || string.IsNullOrEmpty(species.id)) return result;

            var c = save.Creature(species.id);
            var dx = save.Dex(species.id);

            result.firstCatch = dx.caught == 0;

            if (shiny) c.shiny[0]++;
            else c.s[0]++;

            dx.caught++;
            if (shiny) dx.shinyForms[0] = true;
            else dx.forms[0] = true;

            // 첫 샤이니는 한 번뿐인 보상이다
            if (shiny && save.milestones != null && !save.milestones.firstShiny)
            {
                save.milestones.firstShiny = true;
                save.berry += FIRST_SHINY_BERRY;
                result.firstShinyBonus = true;
                result.berryGained += FIRST_SHINY_BERRY;
            }

            CheckMilestone(save, db, species, ref result);
            return result;
        }

        /// <summary>
        /// 진화 등록. index.html:421 - 진화하면 다음 폼이 도감에 열린다.
        /// 개체 수 이동은 호출부(진화 로직)가 하고, 여기서는 도감만 본다.
        /// </summary>
        public static void RegisterForm(SaveData save, DeskmonDatabase db, SpeciesData species,
                                        int newStage, bool shiny)
        {
            if (save == null || species == null) return;
            if (newStage < 0 || newStage >= 3) return;

            var dx = save.Dex(species.id);
            if (shiny) dx.shinyForms[newStage] = true;
            else dx.forms[newStage] = true;

            var dummy = new CatchResult { species = species, shiny = shiny };
            CheckMilestone(save, db, species, ref dummy);
        }

        /// <summary>
        /// 마일스톤. index.html checkMilestone() 이식.
        ///
        /// 두 단계다 - 종 라인 완성(MILESTONE_BERRY), 필드 전체 완성(500).
        /// 샤이니 칸은 세지 않는다. 원본이 forms만 보고 shinyForms는 보지 않는데,
        /// 샤이니가 1% 확률이라 완성 조건에 넣으면 사실상 달성 불가가 된다.
        /// </summary>
        static void CheckMilestone(SaveData save, DeskmonDatabase db, SpeciesData species,
                                   ref CatchResult result)
        {
            var dx = save.Dex(species.id);

            // ── 종 라인 완성 ──
            bool lineDone = true;
            for (int i = 0; i < species.forms; i++)
                if (!dx.forms[i]) { lineDone = false; break; }

            if (!dx.milestone && lineDone)
            {
                dx.milestone = true;
                int reward = db?.balance != null ? db.balance.milestoneBerry : 100;
                save.berry += reward;
                result.lineCompleted = true;
                result.berryGained += reward;
            }

            // ── 필드 완성 ──
            // special(루미)/event(크로노)는 해금 대상 필드가 아니라 제외한다.
            if (db == null) return;
            if (species.field == Field.Special || species.field == Field.Event) return;

            string fieldId = SpawnScheduler.FieldId(species.field);
            if (save.milestones == null || save.milestones.habitats.Contains(fieldId)) return;

            foreach (var sp in db.species)
            {
                if (sp == null || sp.field != species.field) continue;

                var d = save.Dex(sp.id);
                for (int i = 0; i < sp.forms; i++)
                    if (!d.forms[i]) return;   // 하나라도 비면 아직
            }

            save.milestones.habitats.Add(fieldId);
            save.berry += FIELD_COMPLETE_BERRY;
            result.fieldCompleted = fieldId;
            result.berryGained += FIELD_COMPLETE_BERRY;
        }

        /// <summary>
        /// 도감 진행도. index.html:852 - 필드별 "획득/전체" 표시에 쓴다.
        /// 샤이니는 별도 칸이라 여기 세지 않는다.
        /// </summary>
        public static (int got, int total) DexProgress(SaveData save, DeskmonDatabase db, Field field)
        {
            int got = 0, total = 0;
            if (save == null || db == null) return (0, 0);

            foreach (var sp in db.species)
            {
                if (sp == null || sp.field != field) continue;

                total += sp.forms;
                var dx = save.Dex(sp.id);
                for (int i = 0; i < sp.forms; i++)
                    if (dx.forms[i]) got++;
            }
            return (got, total);
        }

        /// <summary>
        /// 총 생산량 (베리/초). index.html prodPerSec() 이식.
        /// 샤이니는 SHINY_MUL배, 작업 중이면 BOOST.prod배.
        /// </summary>
        public static float ProductionPerSecond(SaveData save, DeskmonDatabase db, bool working)
        {
            if (save == null || db?.balance == null) return 0f;
            var b = db.balance;

            float p = 0f;
            foreach (var sp in db.species)
            {
                if (sp == null) continue;
                var c = save.Creature(sp.id);

                for (int st = 0; st < sp.forms; st++)
                {
                    int count = c.s[st] + Mathf.RoundToInt(c.shiny[st] * b.shinyMultiplier);
                    if (count == 0) continue;

                    // 친밀도 보정 - index.html prodOf()의 (1 + prodPerLv*friendLv)
                    int lv = b.FriendLevel(save.Friend(SaveData.Key(sp.id, st)));
                    p += count * sp.ProductionAt(st, b) * (1f + b.friendProdPerLevel * lv);
                }
            }

            return p * (working ? b.boostProd : 1f);
        }
    }
}
