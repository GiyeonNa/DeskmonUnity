using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 종에 속하지 않는 전역 밸런스 수치. data.js의 RARITY/PROD_BASE/SPAWN/SIGIL/FRIEND/BOOST/OFFLINE
    /// 을 한 에셋으로 모은다 (포팅계획 §5).
    ///
    /// 기획서 v4 §11에 열린 결정이 남아 있어 수치가 아직 완전히 동결되지 않았다.
    /// 값을 코드가 아니라 에셋에 두는 이유가 이것이다 — 동결 전까지 재빌드 없이 조정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Balance", menuName = "Deskmon/Balance", order = 2)]
    public class BalanceData : ScriptableObject
    {
        [System.Serializable]
        public struct RarityEntry
        {
            public Rarity rarity;
            [Tooltip("출몰 가중치 — data.js RARITY.w")]
            public float weight;
            [Tooltip("생산 배수 — prodMul")]
            public float prodMultiplier;
            [Tooltip("스카우트 성공률 — scout (0~1)")]
            public float scoutRate;
            [Tooltip("각인 문양 개수 — SIGIL.count")]
            public int sigilCount;
        }

        [Header("희귀도 (RARITY + SIGIL.count)")]
        public RarityEntry[] rarities =
        {
            new RarityEntry { rarity = Rarity.Common,    weight = 70f,  prodMultiplier = 1f,  scoutRate = 0.50f, sigilCount = 1 },
            new RarityEntry { rarity = Rarity.Rare,      weight = 22f,  prodMultiplier = 3f,  scoutRate = 0.30f, sigilCount = 2 },
            new RarityEntry { rarity = Rarity.Epic,      weight = 7f,   prodMultiplier = 10f, scoutRate = 0.15f, sigilCount = 3 },
            new RarityEntry { rarity = Rarity.Legendary, weight = 1.2f, prodMultiplier = 40f, scoutRate = 0.05f, sigilCount = 4 },
        };

        [Header("생산")]
        [Tooltip("단계별 기초 생산. 인덱스는 stage + 3 - forms — SpeciesData.ProductionAt 참고.")]
        public float[] prodBase = { 0.2f, 0.8f, 3.6f };

        [Tooltip("샤이니 생산 배수 — SHINY_MUL")]
        public float shinyMultiplier = 5f;

        [Tooltip("샤이니 확률 — SHINY_RATE")]
        [Range(0f, 1f)] public float shinyRate = 0.01f;

        [Header("출몰 (SPAWN)")]
        public Vector2 spawnIntervalNormal = new Vector2(120f, 240f);
        public Vector2 spawnIntervalNewbie = new Vector2(45f, 90f);
        [Tooltip("이 시간(초) 안에는 신규 유저 간격을 쓴다 — newbieWindow")]
        public float newbieWindow = 600f;
        [Tooltip("출몰한 크리처가 머무는 시간 — stay")]
        public Vector2 stayDuration = new Vector2(40f, 60f);
        public float scoutDelay = 10f;

        [Header("각인 포획 (SIGIL)")]
        [Tooltip("매칭 임계 (0~1, 높을수록 엄격) — tol")]
        [Range(0f, 1f)] public float sigilTolerance = 0.66f;
        public string[] sigilEasy = { "circle", "triangle", "square", "caret", "wave" };
        public string[] sigilHard = { "star", "zigzag", "spiral" };

        [Header("미끼 (BAIT)")]
        public int baitCost = 5;
        public float baitEatDuration = 4f;

        [Header("방목 (ROAM / PET / SNACK / PLAY)")]
        [Tooltip("기본 방목 슬롯 — 필드 해금당 +1. 최대 슬롯은 기획서 v4 §11 열린 결정 3.")]
        public int roamBaseSlots = 2;
        public float petCooldown = 30f;
        [Tooltip("쓰다듬기로 얻는 베리 범위 - data.js PET.min/max. 친밀도는 friendPerPet이다.")]
        public Vector2 petBerryGain = new Vector2(2f, 5f);
        public int snackCost = 3;
        public float playCooldown = 18f;
        public int playFriendGain = 3;

        [Header("친밀도 (FRIEND)")]
        [Tooltip("레벨업에 필요한 누적치 — lv")]
        public int[] friendLevels = { 8, 20, 40, 70, 110 };
        public int friendPerPet = 2;
        public int friendPerSnack = 6;
        [Tooltip("친밀 레벨당 생산 증가율 — prodPerLv")]
        public float friendProdPerLevel = 0.02f;
        [Tooltip("evoLv[stage] = 그 단계에서 진화하는 데 필요한 친밀 레벨")]
        public int[] evolveFriendLevel = { 2, 3 };

        [Header("작업 부스트 (BOOST)")]
        [Tooltip("작업 중 생산 배수")]
        public float boostProd = 1.5f;
        [Tooltip("작업 중 출몰 간격 배수 (작을수록 자주)")]
        public float boostSpawn = 0.77f;
        [Tooltip("이 시간(초) 미만 유휴면 '작업 중' — IdleTime.IsWorking과 같은 값이어야 한다.")]
        public float workingIdleSec = 60f;

        [Header("오프라인 (OFFLINE)")]
        public float offlineRate = 0.5f;
        [Tooltip("오프라인 정산 상한 (초) — 기본 8시간")]
        public float offlineCapSec = 8f * 3600f;
        public float offlineSpawnPerHour = 2f;
        [Range(0f, 1f)] public float offlineSpawnCatch = 0.5f;

        [Header("기타")]
        public int milestoneBerry = 100;
        public float catchBonusSec = 60f;
        [Tooltip("돌려보내기 = 해당 폼 이 초만큼의 생산을 베리로 — RELEASE_SEC")]
        public float releaseSec = 30f;
        [Tooltip("글로벌 동시 출몰 시각(금요일) — CHRONO.hour. 기획서 v4 §11 열린 결정 2.")]
        [Range(0, 23)] public int chronoHour = 21;

        // ── 조회 ──

        public RarityEntry Of(Rarity r)
        {
            for (int i = 0; i < rarities.Length; i++)
                if (rarities[i].rarity == r) return rarities[i];
            return rarities.Length > 0 ? rarities[0] : default;
        }

        public float SpawnWeight(Rarity r) => Of(r).weight;
        public float ProdMultiplier(Rarity r) => Of(r).prodMultiplier;
        public float ScoutRate(Rarity r) => Of(r).scoutRate;
        public int SigilCount(Rarity r) => Mathf.Max(1, Of(r).sigilCount);

        /// <summary>
        /// 누적 친밀도 → 레벨. index.html friendLv() 이식.
        /// friendLevels를 넘어선 값은 최대 레벨로 고정된다.
        /// </summary>
        public int FriendLevel(int points)
        {
            int lv = 0;
            for (int i = 0; i < friendLevels.Length; i++)
                if (points >= friendLevels[i]) lv = i + 1;
            return lv;
        }

        /// <summary>해당 단계에서 다음 폼으로 가는 데 필요한 친밀 레벨.</summary>
        public int EvolveLevelFor(int stage)
        {
            if (evolveFriendLevel == null || evolveFriendLevel.Length == 0) return int.MaxValue;
            return evolveFriendLevel[Mathf.Clamp(stage, 0, evolveFriendLevel.Length - 1)];
        }
    }
}
