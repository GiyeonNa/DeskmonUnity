using UnityEditor;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// Assets/Sprites에 png가 추가/변경되면 데이터 임포트를 자동 실행한다.
    ///
    /// 왜 필요한가: 폼별 스프라이트 연결은 [Deskmon/데이터 임포트]가 하는데,
    /// 도트를 넣고 그 실행을 잊으면 파일은 있어도 SpeciesData가 옛 그림을 가리킨다.
    /// 실제로 진화형 도트(mongle_stage2/3)를 넣고도 전 폼이 기본형 그림으로 나왔다 -
    /// "파일을 두면 끝"이어야 하는 파이프라인에 수동 단계가 숨어 있으면 반드시 잊는다.
    ///
    /// 재귀 주의: Import()가 임포트 설정을 고치며 SaveAndReimport를 부르면 이 훅이
    /// 다시 불린다. delayCall 예약 + 가드로 한 프레임에 한 번만 돌게 한다.
    /// (두 번째 호출에서는 설정이 이미 맞아 reimport가 없으므로 자연히 멈춘다.)
    /// </summary>
    class SpriteAutoLink : AssetPostprocessor
    {
        static bool _scheduled;

        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_scheduled) return;

            bool creature = false, ui = false;
            foreach (var path in imported)
            {
                if (!path.EndsWith(".png")) continue;
                if (path.StartsWith("Assets/Sprites/UI/")) ui = true;
                else if (path.StartsWith("Assets/Sprites/")) creature = true;
            }
            if (!creature && !ui) return;

            _scheduled = true;
            bool doCreature = creature, doUi = ui;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                if (doCreature) SpeciesImporter.Import();
                if (doUi) UIThemeImporter.Import();
            };
        }
    }
}
