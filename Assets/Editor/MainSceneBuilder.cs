using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 본 게임 씬을 코드로 구성한다.
    ///
    /// S0 스파이크 씬과의 차이:
    ///   스파이크 씬은 "투명 창이 되는가"만 확인하려고 만든 것이라 검증 HUD와
    ///   고정 크리처 1마리가 박혀 있다. 본 게임은 GameState가 세이브를 읽고
    ///   출몰 스케줄에 따라 야생을 만든다.
    ///
    /// 씬을 코드로 만드는 이유는 스파이크 때와 같다 - 손으로 만든 씬은 재현이 안 되고
    /// 무엇이 왜 그렇게 설정됐는지 리뷰할 수가 없다.
    ///
    /// 사용: 메뉴 [Deskmon/본 게임 씬 생성]
    /// </summary>
    public static class MainSceneBuilder
    {
        const string SCENE_PATH = "Assets/Scenes/Main.unity";

        [MenuItem("Deskmon/본 게임 씬 생성")]
        public static void Build()
        {
            var db = AssetDatabase.LoadAssetAtPath<DeskmonDatabase>("Assets/Data/DeskmonDB.asset");
            if (db == null)
            {
                if (!EditorUtility.DisplayDialog("데이터가 없습니다",
                    "Assets/Data/DeskmonDB.asset이 없습니다.\n먼저 데이터 임포트를 실행할까요?",
                    "임포트 실행", "취소"))
                    return;

                SpeciesImporter.Import();
                db = AssetDatabase.LoadAssetAtPath<DeskmonDatabase>("Assets/Data/DeskmonDB.asset");
                if (db == null) { Debug.LogError("[Deskmon] 데이터 임포트 실패."); return; }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라: 투명 창의 필수 조건 = 알파 0 클리어 ──
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.allowHDR = false;      // HDR 버퍼는 알파를 보존하지 않는 경우가 있다
            cam.allowMSAA = false;
            cam.nearClipPlane = -10f;
            cam.farClipPlane = 10f;
            camGO.transform.position = new Vector3(0, 0, -10);

            // 1유닛 = 100px. CreatureView.PixelsToUnits(px)=px/100 과 맞춰야
            // 원본의 이동 속도 수치가 그대로 성립한다.
            cam.orthographicSize = Screen.currentResolution.height / 2f / 100f;
            camGO.AddComponent<AudioListener>();

            // ── UniWindowController: 반드시 씬에 배치 ──
            // 런타임 AddComponent는 부착 타이밍 문제로 빌드에서 투명이 안 먹는 사례가 있다.
            var uniGO = new GameObject("UniWindowController");
            var uni = uniGO.AddComponent<Kirurobo.UniWindowController>();
            uni.isTransparent = true;
            uni.isTopmost = true;
            uni.isHitTestEnabled = false;   // 클릭통과는 DesktopOverlay가 직접 판정한다
            uni.shouldFitMonitor = false;
            uni.forceWindowed = true;       // 전체화면이면 투명이 성립하지 않는다
            uni.autoSwitchCameraBackground = true;
            uni.currentCamera = cam;
            uni.transparentType = Kirurobo.UniWindowController.TransparentType.Alpha;

            // ── 비상 종료 ──
            // 이 앱은 포커스를 안 받고 Alt+Tab에도 안 뜬다. 렌더가 잘못되면 작업 관리자
            // 외에 끌 방법이 없으므로 전역 핫키를 항상 넣는다. 디버그 기능이 아니라 안전장치다.
            uniGO.AddComponent<Deskmon.Native.Killswitch>();

            // ── 오버레이 ──
            var overlayGO = new GameObject("DesktopOverlay");
            overlayGO.AddComponent<DesktopOverlay>();

            // ── 게임 ──
            // 출몰 스케줄러와 GameState를 한 오브젝트에 둔다. 둘은 항상 함께 살고
            // 죽으므로 나눌 이유가 없다.
            var gameGO = new GameObject("Game");
            var scheduler = gameGO.AddComponent<SpawnScheduler>();
            scheduler.db = db;

            var state = gameGO.AddComponent<GameState>();
            state.db = db;
            state.scheduler = scheduler;

            // 방목 - 세이브의 roam 목록을 씬으로 투영한다 (S3)
            gameGO.AddComponent<RoamManager>();

            // 베이스캠프 카드 UI (S3). 자기 영역을 클릭통과 판정에 등록하므로
            // 카드 위에서만 입력을 받고 나머지는 뒤쪽 창으로 통과된다.
            var uiGO = new GameObject("UI");
            var uiRoot = uiGO.AddComponent<Deskmon.UI.UIRoot>();
            uiRoot.theme = AssetDatabase.LoadAssetAtPath<Deskmon.UI.UITheme>("Assets/Data/UITheme.asset");

            // 개발용 HUD. 출몰 간격이 2~4분이라 이게 없으면 실행해도 확인할 것이 없다.
            // 배포 전에 show=false로 두거나 컴포넌트를 뺀다.
            var hud = gameGO.AddComponent<GameDebugHUD>();
            hud.game = state;
            hud.scheduler = scheduler;

            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            // 빌드에는 본 게임 씬만 넣는다. 스파이크/테스트 씬은 개발용 도구다.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };

            // 만든 씬을 열어둔 채로 끝낸다.
            // 이걸 안 하면 씬 파일만 생기고 에디터는 이전 씬(스파이크/테스트)에 머문다.
            // 그 상태로 Play를 누르면 GameState가 없어 "데이터 임포트를 먼저" 오류가 나는데,
            // 데이터는 멀쩡하므로 원인을 찾기가 어렵다.
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            Debug.Log($"[Deskmon] 본 게임 씬 생성 완료 -> {SCENE_PATH}\n" +
                      "이 씬을 열어두었고 빌드 대상으로 등록했습니다. " +
                      "투명 오버레이는 빌드에서만 동작합니다 (에디터 Play에서는 로직만 확인 가능).");
        }
    }
}
