using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 본 게임 빌드. 투명 창은 에디터에서 검증할 수 없으므로 빌드가 곧 테스트다.
    ///
    /// 메뉴: [Deskmon/S0 빌드 & 실행]
    /// CLI:  Unity.exe -quit -batchmode -projectPath . -executeMethod Deskmon.EditorTools.GameBuilder.CI
    /// </summary>
    public static class GameBuilder
    {
        const string OUT_DIR = "Build/Deskmon";
        const string EXE = "Deskmon.exe";

        // 기본 메뉴 = 배포판. Development 플래그가 꺼진 빌드에서는 GameDebugHUD가
        // 스스로 사라지므로(Debug.isDebugBuild 게이트) 개발 도구가 자동으로 빠진다.
        [MenuItem("Deskmon/빌드 후 실행 (배포)")]
        public static void BuildAndRun() => Run(true, dev: false);

        [MenuItem("Deskmon/빌드만 (배포)")]
        public static void BuildOnly() => Run(false, dev: false);

        // 개발 빌드 - HUD와 F키(즉시 출몰/일괄 진화/시간 조작)가 살아있다.
        [MenuItem("Deskmon/개발 빌드 후 실행")]
        public static void BuildAndRunDev() => Run(true, dev: true);

        /// <summary>CLI용 — 실패 시 exit code 1. 배포판을 만든다.</summary>
        public static void CI()
        {
            var report = Build(dev: false);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void Run(bool launch, bool dev)
        {
            var report = Build(dev);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Deskmon] 빌드 실패: {report.summary.result}");
                return;
            }

            string exe = Path.Combine(OUT_DIR, EXE);
            Debug.Log($"[Deskmon] 빌드 성공 → {Path.GetFullPath(exe)} ({report.summary.totalSize / 1024 / 1024} MB)");

            if (launch) Process.Start(new ProcessStartInfo(Path.GetFullPath(exe)) { UseShellExecute = true });
        }

        static BuildReport Build(bool dev)
        {
            ProjectSetup.ApplyMenu();
            Directory.CreateDirectory(OUT_DIR);

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                // 본 게임 씬을 만든다. 예전에는 여기서 스파이크 씬을 만들었는데,
                // 그러면 배포물에 검증용 HUD와 고정 크리처가 들어간다.
                Debug.LogWarning("[Deskmon] 빌드 씬이 없습니다. 본 게임 씬을 먼저 만듭니다.");
                MainSceneBuilder.Build();
                scenes = EditorBuildSettings.scenes;
            }

            var opts = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(scenes, s => s.path),
                locationPathName = Path.Combine(OUT_DIR, EXE),
                target = BuildTarget.StandaloneWindows64,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };

            return BuildPipeline.BuildPlayer(opts);
        }
    }
}
