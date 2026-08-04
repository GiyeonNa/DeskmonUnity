namespace Deskmon.Core
{
    /// <summary>희귀도. data.js RARITY의 키.</summary>
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>
    /// 출몰 풀. data.js FIELDS의 id + 코드에만 있는 두 개.
    ///
    /// special/event는 FIELDS 배열에 없지만 SPECIES가 참조한다(루미=special, 크로노=event).
    /// 해금 대상이 아니라 "일반 필드 규칙에서 빠지는 특수 풀"이라 별도 값으로 둔다.
    /// index.html:585 — field==='special'은 해금 검사 대신 서식지 2개 이상 조건을 탄다.
    /// </summary>
    public enum Field { Grass, Forest, Lake, Office, Special, Event }

    /// <summary>
    /// 출몰 게이트. data.js SPECIES의 gate 필드 (index.html:582-583).
    /// None이면 시간/작업 조건 없이 항상 출몰 가능.
    /// </summary>
    public enum SpawnGate
    {
        None,
        /// <summary>평일(월~금) + 작업 중. 종이접기.</summary>
        WeekdayWork,
        /// <summary>심야 0~06시. 꾸벅이.</summary>
        LateNight,
    }

    /// <summary>진영 배타 (기획서 v4 §7.1). 이슬/이끼는 가안 — §11 열린 결정 1.</summary>
    public enum Faction { None, Dew, Moss }

    /// <summary>
    /// 진화 조건. index.html:389-390 evoBlock().
    /// 친밀도는 전 종 공통이라 별도 값이 아니고, 아래는 그 위에 얹히는 추가 조건이다.
    /// </summary>
    public enum EvolveCondition
    {
        None,
        /// <summary>밤에만 진화 가능. 부엉 (nightEvolve).</summary>
        Night,
        /// <summary>미끼를 먹여야 진화. 버섯쫑 (fedEvolve).</summary>
        Fed,
    }

    /// <summary>포획 미니게임 (기획서 v4 §5). 기본값은 희귀도 매핑을 따르되 종별 예외 지정 가능.</summary>
    public enum CaptureStyle
    {
        /// <summary>희귀도 기본값 사용 — 일반·희귀=그랩, 에픽=타이밍, 전설=올가미.</summary>
        ByRarity,
        Grab,
        Timing,
        Lasso,
    }
}
