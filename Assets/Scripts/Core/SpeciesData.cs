using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 크리처 1라인의 정의. data.js SPECIES의 항목 하나와 1:1 대응한다 (포팅계획 §5).
    ///
    /// 세이브는 종 id 문자열을 키로 쓰므로(`creatures{s[],shiny[]}`, `friend{}`, `fed{}`)
    /// <see cref="id"/>는 원본 JS의 키와 반드시 같아야 한다. 에셋 파일명이 아니라 이 값이 정본이다.
    ///
    /// 폼(진화 단계)은 배열이 아니라 forms 개수 + 이름 배열로 둔다 — 원본이 그 구조이고,
    /// 생산량 공식이 stage 인덱스를 직접 쓰기 때문이다 (<see cref="ProductionAt"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "Species_", menuName = "Deskmon/Species", order = 0)]
    public class SpeciesData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("data.js SPECIES의 키. 세이브 키로 쓰이므로 절대 바꾸지 말 것.")]
        public string id;

        [Tooltip("표시 이름 (기본형).")]
        public string displayName;

        [TextArea(2, 4)]
        public string description;

        [Header("분류")]
        public Field field = Field.Grass;

        [Tooltip("151 원장의 확장 서브필드 (예: Meadow, Clockwork). 출몰 규칙에는 아직 안 쓰이고 표기용.")]
        public string subfield;

        public Rarity rarity = Rarity.Common;

        [Tooltip("폼 수. 1=무진화, 2=1진화, 3=2진화.")]
        [Range(1, 3)] public int forms = 1;

        [Tooltip("폼별 이름. 길이는 forms와 같아야 한다.")]
        public string[] formNames = new string[1];

        [Tooltip("151 원장의 폼별 영문 id (예: mongle/leafmong/bloomong). 스프라이트 원본 추적용.")]
        public string[] formIds = new string[0];

        [Tooltip("151 원장의 폼별 도감 번호. 도감 정책상 진화체마다 개별 번호를 가진다 (기획서 §8.2).")]
        public int[] formDexNos = new int[0];

        [Header("아트")]
        [Tooltip("폼별 스프라이트. 비어 있으면 기본 스프라이트를 재사용한다(플레이스홀더 단계).")]
        public Sprite[] formSprites = new Sprite[0];

        [Tooltip("기본 팔레트 색 — data.js color.")]
        public Color bodyColor = Color.white;

        [Tooltip("샤이니 팔레트 색 — data.js shiny. 팔레트 스왑의 목표색이다 (포팅계획 §3.2).")]
        public Color shinyColor = Color.magenta;

        [Tooltip("무지개(전설 연출) — 루미·크로노. data.js rainbow.")]
        public bool rainbow;

        [Header("모션")]
        [Tooltip("점프하며 이동 — 깡총이. data.js hop. CreatureView.hop으로 전달된다.")]
        public bool hop;

        [Header("출몰 조건 (기획서 v4 §4.3)")]
        [Tooltip("밤에 출몰 가중치 2배. data.js night.")]
        public bool nightOnly;

        public SpawnGate gate = SpawnGate.None;

        [Tooltip("진영 배타. None이 아니면 해당 진영을 고른 유저에게만 출몰한다.")]
        public Faction faction = Faction.None;

        [Tooltip("이벤트 전용(크로노) — 일반 출몰 풀에서 제외된다. index.html:583")]
        public bool eventOnly;

        [Header("진화 / 포획")]
        [Tooltip("친밀도 외 추가 진화 조건.")]
        public EvolveCondition evolveCondition = EvolveCondition.None;

        [Tooltip("행동 패턴 = 접근 난이도. 포획 자체는 각인 그리기로 통일됐다 (상세기획 §12).")]
        public BehaviorPattern pattern = BehaviorPattern.Calm;

        // ── 파생 값 ──

        /// <summary>최종 진화형인가.</summary>
        public bool IsFinalStage(int stage) => stage >= forms - 1;

        /// <summary>해당 단계의 표시 이름. 범위를 벗어나면 기본 이름.</summary>
        public string NameAt(int stage)
        {
            if (formNames != null && stage >= 0 && stage < formNames.Length
                && !string.IsNullOrEmpty(formNames[stage]))
                return formNames[stage];
            return displayName;
        }

        /// <summary>해당 단계의 정본 도감 번호. 원장 미연결(구 데이터)이면 0.</summary>
        public int DexNoAt(int stage)
        {
            if (formDexNos == null || formDexNos.Length == 0) return 0;
            return formDexNos[Mathf.Clamp(stage, 0, formDexNos.Length - 1)];
        }

        /// <summary>해당 단계의 스프라이트. 아직 폼별 도트가 없으면 0번을 재사용한다.</summary>
        public Sprite SpriteAt(int stage)
        {
            if (formSprites == null || formSprites.Length == 0) return null;
            if (stage >= 0 && stage < formSprites.Length && formSprites[stage] != null)
                return formSprites[stage];
            return formSprites[0];
        }

        /// <summary>
        /// 친밀도 보정을 뺀 기초 생산량 (베리/초).
        ///
        /// index.html:288-291 prodOf() 이식:
        ///   PROD_BASE[stage + 3 - forms] * RARITY.prodMul
        ///
        /// 인덱스의 `+3 - forms`가 무진화 보정이다 — 최종형은 폼 수와 무관하게
        /// 항상 PROD_BASE의 마지막 값(3.6)을 쓰게 되어 무진화 종이 손해 보지 않는다.
        /// 친밀도 배수는 이 값을 쓰는 쪽(경제 계산)에서 곱한다.
        /// </summary>
        public float ProductionAt(int stage, BalanceData balance)
        {
            if (balance == null) return 0f;
            int idx = Mathf.Clamp(stage + 3 - forms, 0, balance.prodBase.Length - 1);
            return balance.prodBase[idx] * balance.ProdMultiplier(rarity);
        }

        void OnValidate()
        {
            // 폼 수와 이름 배열 길이가 어긋나면 도감/진화 표시가 조용히 깨진다.
            if (formNames == null || formNames.Length != forms)
            {
                var next = new string[forms];
                for (int i = 0; i < forms; i++)
                    next[i] = (formNames != null && i < formNames.Length) ? formNames[i] : "";
                formNames = next;
            }
        }
    }
}
