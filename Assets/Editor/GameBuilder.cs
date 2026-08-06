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

        [MenuItem("Deskmon/빌드 후 실행")]
        public static void BuildAndRun() => Run(true);

        [MenuItem("Deskmon/빌드만")]
        public static void BuildOnly() => Run(false);

        /// <summary>CLI용 — 실패 시 exit code 1.</summary>
        public static void CI()
        {
            var report = Build();
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void Run(bool launch)
        {
            var report = Build();
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Deskmon] 빌드 실패: {report.summary.result}");
                return;
            }

            string exe = Path.Combine(OUT_DIR, EXE);
            Debug.Log($"[Deskmon] 빌드 성공 → {Path.GetFullPath(exe)} ({report.summary.totalSize / 1024 / 1024} MB)");

            if (launch) Process.Start(new ProcessStartInfo(Path.GetFullPath(exe)) { UseShellExecute = true });
        }

        static BuildReport Build()
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
                options = BuildOptions.None,
            };

            return BuildPipeline.BuildPlayer(opts);
        }
    }
}
