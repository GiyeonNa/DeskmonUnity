using UnityEngine;
using Deskmon.Capture;
using Deskmon.Creatures;
using Deskmon.Native;

namespace Deskmon.Core
{
    /// <summary>
    /// 코어 루프를 잇는다: 출몰 -> 각인 포획 -> 도감 등록 -> 저장.
    /// S2의 DoD "한 종을 잡아 도감에 등록"이 성립하는 지점이다.
    ///
    /// 각 조각(SpawnScheduler / SigilCapture / CreatureRegistry / SaveSystem)은 서로를
    /// 모른다. 여기가 유일하게 전부를 아는 곳이고, 그래서 흐름을 한눈에 볼 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("데이터")]
        public DeskmonDatabase db;

        [Header("씬 참조")]
        public SpawnScheduler scheduler;
        [Tooltip("야생 개체를 만들 때 쓰는 프리팹. 비면 런타임에 최소 구성으로 만든다.")]
        public GameObject wildPrefab;

        [Header("저장")]
        [Tooltip("이 간격(초)마다 자동 저장. 0이면 끔.")]
        public float autosaveInterval = 30f;

        /// <summary>현재 세이브. 다른 시스템은 이걸 통해 상태를 읽는다.</summary>
        public SaveData Save { get; private set; }

        /// <summary>포획 성공. UI가 도감 카드/토스트를 띄울 때 구독한다.</summary>
        public event System.Action<CreatureRegistry.CatchResult> OnCaught;

        GameObject _wild;
        SigilCapture _capture;
        float _autosaveT;
        double _prodAccum;

        void Awake()
        {
            Instance = this;
            Save = SaveSystem.Load(db);

            if (db == null)
                Debug.LogError("[GameState] DeskmonDatabase가 없습니다 - 메뉴 [Deskmon/데이터 임포트]를 먼저 실행하세요.");
        }

        void Start()
        {
            ApplyOfflineEarnings();

            if (scheduler != null)
            {
                scheduler.db = db;
                scheduler.OnSpawn += HandleSpawn;
                scheduler.Bind(Save);
            }
        }

        void OnDestroy()
        {
            if (scheduler != null) scheduler.OnSpawn -= HandleSpawn;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Save == null || db?.balance == null) return;

            bool working = IdleTime.IsWorking(db.balance.workingIdleSec);

            // 생산 누적. 베리는 정수로 보여주지만 초당 소수점이라 누적해서 넘긴다.
            _prodAccum += CreatureRegistry.ProductionPerSecond(Save, db, working)
                          * Time.unscaledDeltaTime;
            if (_prodAccum >= 1.0)
            {
                double whole = System.Math.Floor(_prodAccum);
                Save.berry += whole;
                _prodAccum -= whole;
            }

            if (autosaveInterval > 0f)
            {
                _autosaveT += Time.unscaledDeltaTime;
                if (_autosaveT >= autosaveInterval) { _autosaveT = 0f; SaveSystem.Save(Save); }
            }
        }

        /// <summary>
        /// 오프라인 정산. data.js OFFLINE - 자리를 비운 동안의 생산을 일부 돌려준다.
        /// 상한(기본 8시간)이 있어 오래 비워도 무한정 쌓이지 않는다.
        /// </summary>
        void ApplyOfflineEarnings()
        {
            if (Save == null || db?.balance == null || Save.lastSeen <= 0) return;

            double sec = (SaveSystem.NowMs() - Save.lastSeen) / 1000.0;
            if (sec < 60) return;   // 방금 껐다 켠 경우는 무시

            sec = System.Math.Min(sec, db.balance.offlineCapSec);

            // 오프라인 중에는 작업 부스트를 적용하지 않는다 - 켜져 있지 않았으므로.
            float rate = CreatureRegistry.ProductionPerSecond(Save, db, false);
            double gained = rate * sec * db.balance.offlineRate;

            if (gained >= 1.0)
            {
                Save.berry += System.Math.Floor(gained);
                Debug.Log($"[GameState] 오프라인 정산 - {sec / 60:F0}분, 베리 +{System.Math.Floor(gained)}");
            }
        }

        /// <summary>출몰 - 야생 개체를 만들고 각인을 준비한다.</summary>
        void HandleSpawn(SpeciesData species, bool shiny)
        {
            if (species == null) return;
            if (_wild != null) Destroy(_wild);

            _wild = wildPrefab != null ? Instantiate(wildPrefab) : new GameObject("Wild");
            _wild.name = $"Wild_{species.id}";
            PlaceRandomly(_wild.transform);

            // 외형
            var appearance = _wild.GetComponent<CreatureAppearance>()
                             ?? _wild.AddComponent<CreatureAppearance>();
            appearance.Set(species, 0, shiny);

            // 접근 패턴
            var behavior = _wild.GetComponent<WildBehavior>() ?? _wild.AddComponent<WildBehavior>();
            behavior.species = species;
            behavior.stayTime = StayTimeFor(species);

            // 각인
            _capture = _wild.GetComponent<SigilCapture>() ?? _wild.AddComponent<SigilCapture>();
            _capture.db = db;
            _capture.Begin(species);
            behavior.capture = _capture;

            var input = _wild.GetComponent<SigilInput>() ?? _wild.AddComponent<SigilInput>();
            input.wildTarget = _wild.transform;

            var ui = _wild.GetComponent<SigilUI>() ?? _wild.AddComponent<SigilUI>();
            ui.wildTarget = _wild.transform;

            _capture.OnCaptured += () => HandleCaptured(species, shiny);
            behavior.OnLeave += HandleLeave;
        }

        /// <summary>체류시간. 전설은 더 오래 머문다 - 놓치면 다시 보기 어려우므로.</summary>
        float StayTimeFor(SpeciesData sp)
        {
            if (db?.balance == null) return 50f;
            if (sp.rarity == Rarity.Legendary) return Random.Range(75f, 100f);
            return Random.Range(db.balance.stayDuration.x, db.balance.stayDuration.y) + 10f;
        }

        void HandleCaptured(SpeciesData species, bool shiny)
        {
            var result = CreatureRegistry.Add(Save, db, species, shiny);
            SaveSystem.Save(Save);

            Debug.Log($"[GameState] 포획 - {species.displayName}{(shiny ? " (샤이니)" : "")}"
                      + (result.berryGained > 0 ? $" 베리 +{result.berryGained}" : ""));

            OnCaught?.Invoke(result);
            ClearWild();
        }

        void HandleLeave() => ClearWild();

        void ClearWild()
        {
            if (_wild != null) Destroy(_wild);
            _wild = null;
            _capture = null;
            if (scheduler != null) scheduler.EndSpawn();
        }

        void PlaceRandomly(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // overlay.html: x는 화면 25~75%, y는 20~65% 구간
            var screen = new Vector3(
                Random.Range(Screen.width * 0.25f, Screen.width * 0.75f),
                Random.Range(Screen.height * 0.35f, Screen.height * 0.80f),
                -cam.transform.position.z);

            t.position = cam.ScreenToWorldPoint(screen);
        }

        /// <summary>종료 시 저장. 이 앱은 Ctrl+Alt+Q로 죽으므로 여기가 마지막 기회다.</summary>
        void OnApplicationQuit()
        {
            if (Save != null) SaveSystem.Save(Save);
        }
    }
}
