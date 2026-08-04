using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 종·필드·밸런스 에셋을 한곳에서 조회한다. data.js 전역 객체 D의 대응물.
    ///
    /// 출몰 판정(<see cref="SpawnEligible"/>, <see cref="SpawnWeightOf"/>)을 여기 둔 이유:
    /// 이 규칙은 종 하나로 결정되지 않고 시간·요일·작업 상태·진영·해금 같은
    /// 바깥 상태를 함께 봐야 한다. SpeciesData에 두면 에셋이 게임 상태를 알아야 해서
    /// 데이터와 규칙이 섞인다.
    /// </summary>
    [CreateAssetMenu(fileName = "DeskmonDB", menuName = "Deskmon/Database", order = 3)]
    public class DeskmonDatabase : ScriptableObject
    {
        public BalanceData balance;
        public List<FieldData> fields = new List<FieldData>();
        public List<SpeciesData> species = new List<SpeciesData>();

        Dictionary<string, SpeciesData> _byId;

        /// <summary>종 id로 조회. 없으면 null.</summary>
        public SpeciesData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null || _byId.Count != species.Count) BuildIndex();
            return _byId.TryGetValue(id, out var sp) ? sp : null;
        }

        public FieldData GetField(Field f)
        {
            for (int i = 0; i < fields.Count; i++)
                if (fields[i] != null && fields[i].id == f) return fields[i];
            return null;
        }

        void BuildIndex()
        {
            _byId = new Dictionary<string, SpeciesData>(species.Count);
            for (int i = 0; i < species.Count; i++)
            {
                var sp = species[i];
                if (sp == null || string.IsNullOrEmpty(sp.id)) continue;
                _byId[sp.id] = sp;
            }
        }

        /// <summary>
        /// 출몰 조건 판정. index.html:580-585 spawnEligible() 이식.
        /// 진영 배타 + 시간·요일·작업 게이트만 본다 (해금·이벤트 검사는 호출부).
        /// </summary>
        public static bool SpawnEligible(SpeciesData sp, in WorldConditions w, Faction playerFaction)
        {
            if (sp == null) return false;
            if (sp.faction != Faction.None && sp.faction != playerFaction) return false;

            switch (sp.gate)
            {
                case SpawnGate.WeekdayWork: return w.isWeekday && w.isWorking;
                case SpawnGate.LateNight:   return w.isLateNight;
                default:                    return true;
            }
        }

        /// <summary>
        /// 가중치. index.html:598-600 — 밤 종은 밤에 2배.
        /// </summary>
        public float SpawnWeightOf(SpeciesData sp, in WorldConditions w)
        {
            if (sp == null || balance == null) return 0f;
            float weight = balance.SpawnWeight(sp.rarity);
            if (sp.nightOnly && w.isNight) weight *= 2f;
            return weight;
        }

        /// <summary>
        /// 가중 랜덤 추첨. index.html:212 pickW() 이식.
        /// 후보가 비면 null.
        /// </summary>
        public SpeciesData PickWeighted(IList<SpeciesData> pool, in WorldConditions w)
        {
            if (pool == null || pool.Count == 0) return null;

            float sum = 0f;
            for (int i = 0; i < pool.Count; i++) sum += SpawnWeightOf(pool[i], w);
            if (sum <= 0f) return pool[pool.Count - 1];

            float r = Random.value * sum;
            for (int i = 0; i < pool.Count; i++)
            {
                r -= SpawnWeightOf(pool[i], w);
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 세이브 키가 겹치면 두 종의 보유 수가 한 칸을 공유해 조용히 섞인다.
            var seen = new HashSet<string>();
            foreach (var sp in species)
            {
                if (sp == null || string.IsNullOrEmpty(sp.id)) continue;
                if (!seen.Add(sp.id))
                    Debug.LogError($"[DeskmonDB] 종 id 중복: '{sp.id}' — 세이브가 섞입니다.", this);
            }
            _byId = null;
        }
#endif
    }

    /// <summary>
    /// 출몰 판정에 쓰이는 바깥 상태. index.html의 isNight/isLateNight/isWeekday/isWorking 묶음.
    ///
    /// 값으로 넘기는 이유: 판정 도중 시간이 바뀌면 같은 프레임에서 조건이 엇갈릴 수 있고,
    /// 개발자 테스트 패널(DEV.time/work/day 오버라이드, 기획 S5)에서 통째로 갈아끼우기 쉽다.
    /// </summary>
    public struct WorldConditions
    {
        public bool isNight;      // 18시~06시
        public bool isLateNight;  // 00시~06시
        public bool isWeekday;    // 월~금
        public bool isFriday;
        public bool isWorking;    // 유휴 < BOOST.idleSec

        /// <summary>실제 시계 + 유휴 상태로 채운다. index.html:206-211과 같은 경계값.</summary>
        public static WorldConditions Now(bool working)
        {
            var now = System.DateTime.Now;
            int h = now.Hour;
            int dow = (int)now.DayOfWeek;   // 0=일 … 6=토, JS Date.getDay()와 같다
            return new WorldConditions
            {
                isNight     = h >= 18 || h < 6,
                isLateNight = h < 6,
                isWeekday   = dow >= 1 && dow <= 5,
                isFriday    = dow == 5,
                isWorking   = working,
            };
        }
    }
}
