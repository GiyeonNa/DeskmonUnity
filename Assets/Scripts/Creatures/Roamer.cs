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

        /// <summary>공놀이 남은 쿨다운. PLAY.cd(18초).</summary>
        public float PlayCooldown { get; private set; }

        /// <summary>쓰다듬기 성공. (베리, 레벨업 여부)</summary>
        public event System.Action<FriendshipSystem.PetResult> OnPetted;

        CreatureView _view;

        // 공놀이 상태. overlay.html의 r.fetching / r.returning / r.playHome 이식.
        bool _fetching, _returning;
        Vector2 _playHome;

        void Awake() => _view = GetComponent<CreatureView>();

        void OnDestroy()
        {
            // 물러 가던 개체가 회수/진화로 사라지면 공이 고아가 된다 - 같이 치운다
            if (_fetching || _returning) BallToy.Clear();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (PetCooldown > 0f) PetCooldown -= dt;
            if (PlayCooldown > 0f) PlayCooldown -= dt;

            // 공놀이 중에는 다른 조작을 받지 않는다 - 원본도 fetching 동안 상호작용을 껐다
            if (_fetching || _returning) { DriveFetch(dt); return; }

            // 각인 진행 중에는 방목 클릭을 무시한다 - 문양을 그리다 방목 개체를
            // 스치면 쓰다듬기가 터져 획이 끊기는 것을 막는다. 야생 포획이 우선이다.
            var overlay = DesktopOverlay.Instance;
            if (overlay != null && overlay.captureAll) return;

            if (_view.CursorNear && GlobalKey.MouseDown()) TryPet();
            else if (_view.CursorNear && GlobalKey.MouseRightDown()) TryThrowBall();
        }

        // ── 공놀이 (기획 v4 §6.2 "데스크탑 펫의 핵심 손맛") ──

        /// <summary>
        /// 공 던지기. overlay.html throwBall() 이식 - 랜덤 방향 130~230px 포물선.
        /// 원본도 조준이 아니라 랜덤이다. "어디로 갈지 모르는 공을 쫓는 것"이 재미의 핵심.
        /// </summary>
        void TryThrowBall()
        {
            if (BallToy.Current != null || PlayCooldown > 0f) return;

            var game = GameState.Instance;
            if (game?.Save == null || game.db?.balance == null) return;

            Vector2 self = ScreenPos();
            float dir = Random.value < 0.5f ? -1f : 1f;

            // 원본 좌표는 Y가 아래로 증가한다 - ty의 rand(-30,50)은 우리 좌표로 rand(-50,30)
            var to = new Vector2(
                Mathf.Clamp(self.x + dir * Random.Range(130f, 230f), 40f, Screen.width - 40f),
                Mathf.Clamp(self.y + Random.Range(-50f, 30f), 70f, Screen.height * 0.7f));

            BallToy.Throw(self + new Vector2(0f, 8f), to);
            Sfx.Grab();   // 던지기음 - overlay.html throwBall

            _playHome = self;
            _fetching = true;
            _view.ExternallyDriven = true;
        }

        /// <summary>공 쫓기 -> 물기 -> 제자리 복귀. overlay.html updateRoamers의 fetch 분기.</summary>
        void DriveFetch(float dt)
        {
            const float SPEED = 160f;      // px/s
            const float GRAB_DIST = 16f;
            const float HOME_DIST = 10f;

            if (_fetching)
            {
                var ball = BallToy.Current;
                if (ball == null) { _fetching = false; _returning = true; return; }

                // 나는 동안은 떨어질 곳을 보며 기다린다
                if (ball.phase == BallToy.Phase.Fly) { _view.state = CreatureView.State.Idle; return; }

                Vector2 self = ScreenPos();
                Vector2 d = ball.ScreenPos - self;
                float dist = d.magnitude;

                _view.FaceDir(d.x > 0f ? 1 : -1);
                _view.state = CreatureView.State.Walk;

                if (dist > GRAB_DIST) MoveScreen(d / dist * SPEED * dt);
                else { BallToy.Clear(); _fetching = false; _returning = true; }
                return;
            }

            if (_returning)
            {
                Vector2 self = ScreenPos();
                Vector2 d = _playHome - self;
                float dist = d.magnitude;

                _view.FaceDir(d.x > 0f ? 1 : -1);
                _view.state = CreatureView.State.Walk;

                if (dist > HOME_DIST) { MoveScreen(d / dist * SPEED * dt); return; }

                // 도착 - 보상. 무료 친밀도라 쿨다운이 밸런스의 전부다.
                _returning = false;
                _view.ExternallyDriven = false;

                var game = GameState.Instance;
                if (game?.Save != null && game.db?.balance != null)
                {
                    PlayCooldown = game.db.balance.playCooldown;
                    FriendshipSystem.Play(game.Save, game.db.balance, speciesId, stage, out _);
                    SaveSystem.Save(game.Save);
                }

                if (effects != null) effects.PlayPet(ScreenPos());
                Sfx.Pet();   // 물어온 보상음
                _view.Pause(1f);
            }
        }

        Vector2 ScreenPos()
        {
            var cam = Camera.main;
            return cam != null ? (Vector2)cam.WorldToScreenPoint(transform.position) : Vector2.zero;
        }

        /// <summary>스크린 픽셀 이동량을 월드로. 1유닛 = 100px 전제.</summary>
        void MoveScreen(Vector2 deltaPx)
            => transform.position += (Vector3)(deltaPx / 100f);

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
            Sfx.Pet();
            OnPetted?.Invoke(result);
        }
    }
}
