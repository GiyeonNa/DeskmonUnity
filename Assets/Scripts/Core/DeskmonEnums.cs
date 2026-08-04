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

    /// <summary>
    /// 행동 패턴 = 접근 난이도. data.js SPECIES의 pattern.
    ///
    /// 상세기획 §12에서 포획은 각인 그리기 하나로 통합됐지만, "다가가는 미니게임"인
    /// 이 패턴들은 그대로 남았다. 각인을 시작하려면 먼저 야생 근처를 눌러야 하는데
    /// 겁쟁이는 도망가고 순간이동형은 사라지므로, 여기서 손맛이 갈린다 (§3.3).
    /// </summary>
    public enum BehaviorPattern
    {
        /// <summary>느긋함 - 사인곡선 산책. 사실상 확정 포획.</summary>
        Calm,
        /// <summary>겁쟁이 - 커서 150px 접근 시 도주, 4초 도망가면 2.5초 헐떡(무방비).</summary>
        Shy,
        /// <summary>순간이동 - 주기적으로 페이드아웃 후 랜덤 위치 재등장.</summary>
        Blink,
        /// <summary>표류 - 전설(루미·크로노)의 느린 부유.</summary>
        Drift,
    }
}
