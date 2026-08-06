using UnityEngine;
using Deskmon.Core;

namespace Deskmon.Creatures
{
    /// <summary>
    /// 종/폼/샤이니에 맞춰 스프라이트와 팔레트를 세팅한다 (포팅계획 §3.2).
    ///
    /// MaterialPropertyBlock을 쓰는 이유: 크리처마다 색이 다르다고 Material을 복제하면
    /// 인스턴스가 늘어나는 만큼 배칭이 깨지고 누수도 생긴다. 프로퍼티 블록은 머티리얼
    /// 하나를 공유한 채 인스턴스별 값만 덮어쓴다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CreatureAppearance : MonoBehaviour
    {
        static readonly int BaseColorId   = Shader.PropertyToID("_BaseColor");
        static readonly int TargetColorId = Shader.PropertyToID("_TargetColor");
        static readonly int SwapId        = Shader.PropertyToID("_Swap");
        static readonly int RainbowId     = Shader.PropertyToID("_Rainbow");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        [Header("정체")]
        public SpeciesData species;
        [Tooltip("진화 단계. 0=기본형.")]
        public int stage;
        public bool shiny;

        [Header("표현")]
        [Tooltip("바탕화면 위에서 묻히지 않게 하는 테두리 (포팅계획 §3.5).")]
        [Range(0f, 4f)] public float outlineWidth = 1f;
        public Color outlineColor = new Color(0f, 0f, 0f, 0.55f);

        SpriteRenderer _sr;
        MaterialPropertyBlock _mpb;

        /// <summary>
        /// 팔레트 스왑 머티리얼. 전 크리처가 하나를 공유한다.
        ///
        /// 인스턴스별 색은 MaterialPropertyBlock으로 덮어쓰므로 머티리얼을 복제할 필요가 없다.
        /// 복제하면 배칭이 깨지고 누수도 생긴다.
        /// </summary>
        static Material _shared;

        static Material Shared
        {
            get
            {
                if (_shared != null) return _shared;

                var shader = Shader.Find("Deskmon/PaletteSwap");
                if (shader == null)
                {
                    // 셰이더가 빌드에 포함되지 않으면 여기서 걸린다. 스프라이트는 보이되
                    // 샤이니와 아웃라인만 안 먹는 상태가 되므로 조용히 넘어가면 원인을 못 찾는다.
                    Debug.LogError("[CreatureAppearance] Deskmon/PaletteSwap 셰이더를 찾지 못했습니다. " +
                                   "기본 스프라이트 머티리얼로 대체합니다 (샤이니/아웃라인 비활성).");
                    return null;
                }

                _shared = new Material(shader) { name = "PaletteSwap (shared)" };
                return _shared;
            }
        }

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();

            // 셰이더를 붙이지 않으면 _BaseColor/_Swap 같은 프로퍼티가 갈 곳이 없어
            // 팔레트 스왑과 아웃라인이 통째로 무시된다.
            var mat = Shared;
            if (mat != null) _sr.sharedMaterial = mat;

            Apply();
        }

        /// <summary>종/폼/샤이니를 한 번에 지정한다. 포획·진화 시 호출.</summary>
        public void Set(SpeciesData sp, int formStage, bool isShiny)
        {
            species = sp;
            stage = formStage;
            shiny = isShiny;
            Apply();
        }

        /// <summary>현재 필드 값을 렌더러에 반영한다.</summary>
        public void Apply()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (species != null)
            {
                var sprite = species.SpriteAt(stage);
                if (sprite != null) _sr.sprite = sprite;
            }

            _sr.GetPropertyBlock(_mpb);

            Color body  = species != null ? species.bodyColor  : Color.white;
            Color shine = species != null ? species.shinyColor : Color.magenta;

            _mpb.SetColor(BaseColorId, body);
            _mpb.SetColor(TargetColorId, shine);
            _mpb.SetFloat(SwapId, shiny ? 1f : 0f);

            // 무지개는 전설 종에만. 샤이니 여부와 무관하게 종 속성이다 (data.js rainbow).
            _mpb.SetFloat(RainbowId, species != null && species.rainbow ? 1f : 0f);

            _mpb.SetColor(OutlineColorId, outlineColor);
            _mpb.SetFloat(OutlineWidthId, outlineWidth);

            _sr.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 스왑 강도를 직접 지정한다 (0~1). 포획 연출에서 0에서 1로 올리면 변신처럼 보인다.
        /// Apply()를 다시 부르면 shiny 값 기준으로 되돌아간다.
        /// </summary>
        public void SetSwapAmount(float t)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(SwapId, Mathf.Clamp01(t));
            _sr.SetPropertyBlock(_mpb);
        }

        void OnValidate()
        {
            if (species != null) stage = Mathf.Clamp(stage, 0, Mathf.Max(0, species.forms - 1));
            if (Application.isPlaying || _sr != null) Apply();
        }
    }
}
