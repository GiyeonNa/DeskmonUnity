using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 출몰 풀 하나. data.js FIELDS의 항목과 1:1 (포팅계획 §5).
    /// 기획서 v4 §4.2 — "필드는 위젯 면적과 무관한 도감 구조", 베리로 해금한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Field_", menuName = "Deskmon/Field", order = 1)]
    public class FieldData : ScriptableObject
    {
        [Tooltip("data.js FIELDS의 id. 세이브의 habitats 배열에 이 값이 들어간다.")]
        public Field id = Field.Grass;

        public string displayName;

        [Tooltip("해금 비용 (베리). 초원 0 / 숲 200 / 호수 600 / 사무 1800")]
        public int unlockCost;

        [Header("배경 그라디언트")]
        public Color dayTop = Color.white;
        public Color dayBottom = Color.gray;
        public Color nightTop = Color.blue;
        public Color nightBottom = Color.black;

        /// <summary>기본 해금 여부 — 초원은 시작부터 열려 있다.</summary>
        public bool UnlockedByDefault => unlockCost <= 0;
    }
}
