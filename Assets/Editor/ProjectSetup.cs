using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 데스크탑 오버레이에 필요한 프로젝트 설정을 코드로 강제한다.
    ///
    /// 왜 코드로 하는가: 투명 창은 아래 설정 중 하나만 틀려도 조용히 실패한다
    /// (창에 검은 배경이 깔리거나, 전체화면으로 뜨거나, 스플래시가 먼저 뜬다).
    /// 손으로 체크박스를 맞추는 대신 스크립트로 고정해 재현 가능하게 만든다.
    ///
    /// 최초 임포트 시 자동 1회 실행 + 메뉴에서 수동 재적용 가능.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string DONE_KEY = "Deskmon.ProjectSetup.Done";

        static ProjectSetup()
        {
            if (SessionState.GetBool(DONE_KEY, false)) return;
            SessionState.SetBool(DONE_KEY, true);
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("Deskmon/프로젝트 설정 적용")]
        public static void ApplyMenu() => Apply(true);

        static void Apply(bool verbose)
        {
            PlayerSettings.companyName = "Deskmon";
            PlayerSettings.productName = "Deskmon";

            // ── 창: 테두리 없는 창모드. 전체화면이면 투명이 성립하지 않는다 ──
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.visibleInBackground = true;      // 포커스를 잃어도 계속 보임
            PlayerSettings.runInBackground = true;          // 백그라운드에서도 Update
            PlayerSettings.usePlayerLog = true;

            // 참고: 해상도 선택 다이얼로그는 Unity 2019+ 에서 제거되어 별도 설정이 필요 없다.

            // 창 크기 기본값 — 런타임에 WindowController.FitTo가 화면 전체로 다시 맞춘다
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;

            // ── 스플래시: 오버레이 앱에 스플래시가 뜨면 부팅이 어색하다 (Personal 라이선스는 강제될 수 있음) ──
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // ── 렌더: 알파를 보존해야 창이 투명해진다 ──
            //
            // 플립 모델 스왑체인(DXGI_SWAP_EFFECT_FLIP_*)은 레이어드 윈도우의 per-pixel
            // 알파와 함께 쓸 수 없다. 플립 모델로 present하면 DWM이 알파 채널을 버리고
            // 불투명하게 합성하므로, 카메라를 알파 0으로 클리어해도 창이 새까맣게 나온다.
            // → 반드시 BitBlt 모델로 되돌린다. 투명 오버레이의 필수 조건.
            PlayerSettings.useFlipModelSwapchain = false;

            // 그래픽 API를 D3D11로 고정한다. 자동(빈 배열)이면 환경에 따라 D3D12가 선택될 수
            // 있는데, D3D12는 BitBlt 모델을 지원하지 않아 투명이 성립하지 않는다.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });

            // 컬러스페이스는 리니어 유지 — 검증된 Tr/UniWinC 프로젝트도 리니어에서 투명이
            // 정상 동작한다. 투명 실패의 원인이 아니다.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.useHDRDisplay = false;
            PlayerSettings.gpuSkinning = false;

            // ── 백엔드 ──
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);

            // 상주 앱이므로 프레임을 묶어 전력을 아낀다 (overlay.html의 상시 rAF 문제 회피)
            QualitySettings.vSyncCount = 1;

            EnsureIncludedShaders();

            AssetDatabase.SaveAssets();
            if (verbose) Debug.Log("[Deskmon] 프로젝트 설정 적용 완료.");
        }

        /// <summary>
        /// 코드에서만 Shader.Find로 찾는 셰이더를 빌드에 강제로 포함시킨다.
        ///
        /// 왜 필요한가: 어떤 씬이나 머티리얼 에셋도 참조하지 않는 셰이더는 빌드에서
        /// 통째로 빠진다. 그러면 에디터에서는 멀쩡하다가 빌드에서만 Shader.Find가
        /// null을 돌려주고, 샤이니와 아웃라인이 조용히 사라진다.
        /// </summary>
        static void EnsureIncludedShaders()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PaletteSwap.shader");
            if (shader == null) return;

            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null || graphics.Length == 0) return;

            var so = new SerializedObject(graphics[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();

            Debug.Log("[Deskmon] PaletteSwap 셰이더를 빌드 포함 목록에 추가했습니다.");
        }
    }
}
