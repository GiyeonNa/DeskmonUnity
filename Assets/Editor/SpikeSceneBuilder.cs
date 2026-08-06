using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Deskmon;
using Deskmon.Creatures;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// S0 스파이크 씬을 코드로 구성한다.
    /// 손으로 만든 씬은 재현이 안 되고 리뷰도 어렵다 — 씬 구성 자체를 코드로 남긴다.
    ///
    /// 사용: 메뉴 [Deskmon/S0 스파이크 씬 생성]
    /// </summary>
    public static class SpikeSceneBuilder
    {
        const string SCENE_PATH = "Assets/Scenes/S0_Spike.unity";

        [MenuItem("Deskmon/S0 스파이크 씬 생성")]
        public static void Build()
        {
            // 스프라이트가 없으면 먼저 만든다
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mongle.png");
            if (sprite == null)
            {
                PlaceholderSpriteGen.Generate();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mongle.png");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라: 투명 창의 필수 조건 = 알파 0 클리어 ──
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);   // ← 알파 0
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = -10f;
            cam.farClipPlane = 10f;
            camGO.transform.position = new Vector3(0, 0, -10);

            // 1유닛 = 100px 이 되도록 orthographicSize를 화면 높이에 맞춘다.
            // CreatureView.PixelsToUnits(px) = px/100 과 일치시켜야 이동 속도 수치가 원본과 같아진다.
            cam.orthographicSize = Screen.currentResolution.height / 2f / 100f;

            camGO.AddComponent<AudioListener>();

            // ── UniWindowController: 반드시 씬에 배치한다 ──
            // 런타임 AddComponent는 싱글톤/부착 타이밍 문제로 빌드에서 투명이 적용되지 않는
            // 사례가 있다 (UniWinC 공식 권장 = 씬 배치).
            var uniGO = new GameObject("UniWindowController");
            var uni = uniGO.AddComponent<Kirurobo.UniWindowController>();
            uni.isTransparent = true;
            uni.isTopmost = true;
            uni.isHitTestEnabled = false;   // 클릭통과는 DesktopOverlay가 직접 판정한다
            uni.shouldFitMonitor = false;   // 창 맞춤은 WindowController.FitTo가 담당

            // 전체화면으로 뜨면 투명이 성립하지 않고 화면 전체가 검게 덮인다.
            // 이전 실행에서 검은 화면이 나왔던 실패 모드가 정확히 이것이므로 강제로 창모드로 되돌린다.
            uni.forceWindowed = true;

            // UniWinC가 투명 시 카메라 배경을 알파 0 검정으로 알아서 바꿔준다.
            uni.autoSwitchCameraBackground = true;
            uni.currentCamera = cam;
            uni.transparentType = Kirurobo.UniWindowController.TransparentType.Alpha;

            // ── 비상 종료 (Ctrl+Alt+Q) ──
            // 이 앱은 포커스를 안 받고 Alt+Tab에도 안 뜬다. 렌더가 잘못돼 화면이 덮이면
            // 작업 관리자 외에 끌 방법이 없으므로 전역 핫키를 항상 넣는다. 안전장치다.
            uniGO.AddComponent<Deskmon.Native.Killswitch>();

            // ── 오버레이 컨트롤러 ──
            var overlayGO = new GameObject("DesktopOverlay");
            var overlay = overlayGO.AddComponent<DesktopOverlay>();
            overlay.logStateChanges = true;
            overlayGO.AddComponent<SpikeHUD>();

            // ── 크리처 1마리 (DoD: "블롭 1개가 바탕화면 산책") ──
            var creatureGO = new GameObject("Creature_mongle");
            var sr = creatureGO.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;
            var view = creatureGO.AddComponent<CreatureView>();
            view.hop = false;
            creatureGO.transform.position = Vector3.zero;
            creatureGO.transform.localScale = Vector3.one * 2f;   // 48px 스프라이트를 보기 좋게 확대

            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            // 빌드 대상은 건드리지 않는다. 스파이크는 투명 창을 확인하는 도구이지
            // 배포물이 아니다 - 예전에 여기서 빌드 설정을 덮어써서 본 게임 대신
            // 스파이크가 빌드되고 있었다. 본 게임 씬은 [Deskmon/본 게임 씬 생성]이 등록한다.

            Debug.Log($"[Deskmon] S0 스파이크 씬 생성 완료 → {SCENE_PATH}\n" +
                      "빌드해서 실행하세요. 에디터에서는 투명 창이 적용되지 않습니다 (의도된 동작).");
            EditorUtility.DisplayDialog("데스크몬",
                "S0 스파이크 씬을 만들었습니다.\n\n" +
                "중요: 투명 오버레이는 빌드에서만 동작합니다.\n" +
                "[Deskmon/S0 빌드 & 실행] 으로 확인하세요.", "확인");
        }
    }
}
