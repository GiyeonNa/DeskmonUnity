using UnityEngine;

namespace Deskmon.UI
{
    /// <summary>
    /// UI 이미지 테마. UI_이미지_기획서.md §4 원장의 15항목과 1:1.
    ///
    /// 비어 있는 칸은 코드 드로잉 플레이스홀더로 폴백한다 - 이미지가 하나씩
    /// 승인되는 파이프라인이라, 일부만 있어도 화면이 깨지면 안 된다.
    ///
    /// 에셋 생성/연결은 [Deskmon/UI 테마 임포트]가 한다. 손으로 채우지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Deskmon/UI Theme", order = 4)]
    public class UITheme : ScriptableObject
    {
        [Header("프레임 (9슬라이스)")]
        public Sprite frameCard;
        public Sprite frameButton;
        public Sprite frameButtonOn;
        public Sprite frameCell;

        [Header("아이콘")]
        public Sprite iconBerry;
        public Sprite iconHeart;
        public Sprite iconBag;
        public Sprite iconDex;
        public Sprite iconGear;
        public Sprite iconSparkle;
        public Sprite iconSleep;
        public Sprite badge;

        [Header("연출")]
        public Sprite fxHeart;
        public Sprite fxSpark;
        public Sprite fxRing;
    }
}
