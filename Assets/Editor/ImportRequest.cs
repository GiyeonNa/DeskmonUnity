using System.IO;
using UnityEditor;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 파일 마커로 데이터 임포트를 요청하는 훅.
    ///
    /// 왜 필요한가: 이 프로젝트는 CLI(Claude)와 에디터를 오가며 작업한다. CLI 쪽에서
    /// 원장/도트를 바꾼 뒤 임포트까지 끝내려면 사람이 메뉴를 눌러줘야 하는 수동 단계가
    /// 남는데, 그 단계는 반드시 잊힌다 (SpriteAutoLink와 같은 이유). CLI가 프로젝트
    /// 루트에 마커 파일을 만들어 두면, 에디터가 포커스를 받아 컴파일/리로드될 때
    /// 여기서 임포트를 대신 실행한다.
    ///
    /// 마커는 실행 전에 지운다 - 임포트가 예외로 죽어도 무한 재시도 루프에 빠지지 않게.
    /// </summary>
    [InitializeOnLoad]
    public static class ImportRequest
    {
        const string IMPORT_MARKER = ".deskmon-import-request";
        const string SELFCHECK_MARKER = ".deskmon-selfcheck-request";

        static ImportRequest()
        {
            Schedule(IMPORT_MARKER, SpeciesImporter.Import);
            Schedule(SELFCHECK_MARKER, S3SelfCheck.Run);
        }

        static void Schedule(string marker, System.Action action)
        {
            if (!File.Exists(marker)) return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(marker)) return;   // delayCall 사이 중복 방지
                File.Delete(marker);
                action();
            };
        }
    }
}
