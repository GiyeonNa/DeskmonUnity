using UnityEngine;
using Deskmon.Core;
using Deskmon.Native;
using Deskmon.Capture;

namespace Deskmon.Creatures
{
    /// <summary>
    /// 방목 개체 하나. overlay.html updateRoamers의 쓰다듬기 반응 이식.
    ///
    /// 산책은 CreatureView가, 외형은 CreatureAppearance가 이미 하므로
    /// 여기는 "방목 개체로서의 정체(어느 폼인가)"와 쓰다듬기만 담당한다.
    ///
    /// 쓰다듬기 = 방목의 핵심 보상 루프다 (기획 v4 §6.2):
    ///   클릭 -> 베리(2~5) + 친밀도(+2) -> 친밀도가 진화를 연다.
    /// </summary>
    [RequireComponent(typeof(CreatureView))]
    public class Roamer : MonoBehaviour
    {
        [Header("정체 (Save.roam 키와 대응)")]
        public string speciesId;
        public int stage;
        public bool shiny;

        [Header("연출")]
        public CaptureEffects effects;

        /// <summary>쓰다듬기 남은 쿨다운. PET.cd(30초). 런타임 상태라 저장하지 않는다.</summary>
        public float PetCooldown { get; private set; }

        /// <summary>쓰다듬기 성공. (베리, 레벨업 여부)</summary>
        public event System.Action<FriendshipSystem.PetResult> OnPetted;

        CreatureView _view;

        void Awake() => _view = GetComponent<CreatureView>();

        void Update()
        {
            if (PetCooldown > 0f) PetCooldown -= Time.unscaledDeltaTime;

            // 각인 진행 중에는 방목 클릭을 무시한다 - 문양을 그리다 방목 개체를
            // 스치면 쓰다듬기가 터져 획이 끊기는 것을 막는다. 야생 포획이 우선이다.
            var overlay = DesktopOverlay.Instance;
            if (overlay != null && overlay.captureAll) return;

            if (_view.CursorNear && GlobalKey.MouseDown()) TryPet();
        }

        void TryPet()
        {
            if (PetCooldown > 0f) return;

            var game = GameState.Instance;
            if (game?.Save == null || game.db?.balance == null) return;

            var result = FriendshipSystem.Pet(game.Save, game.db.balance, speciesId, stage);
            if (!result.ok) return;

            PetCooldown = game.db.balance.petCooldown;

            // 반응: 잠깐 멈추고 하트. overlay.html:311 - 하트 3개 시차.
            _view.Pause(1f);
            if (effects != null)
            {
                var cam = Camera.main;
                if (cam != null)
                    effects.PlayPet(cam.WorldToScreenPoint(transform.position));
            }

            SaveSystem.Save(game.Save);
            OnPetted?.Invoke(result);
        }
    }
}
