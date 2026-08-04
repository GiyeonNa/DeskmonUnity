using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Deskmon.Core;
using Deskmon.Capture;
using Deskmon.Creatures;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 각인 UI 검증 씬. 투명 오버레이 없이 각인만 떼어내 확인한다.
    ///
    /// 왜 별도 씬인가: S0 씬은 투명·클릭통과 창이라 에디터 게임뷰에서 입력 경로가
    /// 실제와 다르다. 각인 UI 자체(고스트·궤적·판정·흔들림)는 그것과 무관하게
    /// 검증할 수 있어야 손을 빨리 볼 수 있다.
    ///
    /// 사용: 메뉴 [Deskmon/각인 UI 테스트 씬 생성] -> 플레이 -> 크리처를 클릭하고 문양을 그린다
    /// </summary>
    public static class SigilTestSceneBuilder
    {
        const string SCENE_PATH = "Assets/Scenes/SigilTest.unity";

        [MenuItem("Deskmon/각인 UI 테스트 씬 생성")]
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

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라: 여기서는 불투명 배경을 쓴다 ──
            // 투명이 목적이 아니라 UI를 눈으로 보는 것이 목적이므로, 어두운 배경을 깔아
            // 밝은 문양선이 잘 보이게 한다.
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.11f, 1f);
            cam.orthographicSize = Screen.currentResolution.height / 2f / 100f;
            camGO.transform.position = new Vector3(0, 0, -10);
            camGO.AddComponent<AudioListener>();

            // ── 야생 개체 ──
            var wildGO = new GameObject("Wild");
            var sr = wildGO.AddComponent<SpriteRenderer>();

            // 에픽을 기본으로 둔다 - 문양 3개라 진행 점과 연속 판정을 한 번에 볼 수 있다.
            var species = db.Get("owl") ?? (db.species.Count > 0 ? db.species[0] : null);
            if (species != null)
            {
                sr.sprite = species.SpriteAt(0);
                var appearance = wildGO.AddComponent<CreatureAppearance>();
                appearance.species = species;
            }

            var capture = wildGO.AddComponent<SigilCapture>();
            capture.db = db;

            var ui = wildGO.AddComponent<SigilUI>();
            ui.wildTarget = wildGO.transform;

            var input = wildGO.AddComponent<SigilInput>();
            input.wildTarget = wildGO.transform;

            var harness = wildGO.AddComponent<SigilTestHarness>();
            harness.db = db;
            harness.capture = capture;

            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(), SCENE_PATH);

            Debug.Log($"[Deskmon] 각인 테스트 씬 생성 완료 -> {SCENE_PATH}\n" +
                      "플레이 후 크리처를 클릭하고 화면의 점선 문양을 따라 그리세요. " +
                      "1~4 키로 희귀도(문양 개수)를 바꿀 수 있습니다.");
        }
    }
}
