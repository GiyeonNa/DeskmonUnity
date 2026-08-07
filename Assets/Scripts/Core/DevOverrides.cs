using Deskmon.Native;

namespace Deskmon.Core
{
    /// <summary>
    /// 개발자 테스트 오버라이드. index.html의 DEV{ time, work, day } 이식 (기획 S5).
    ///
    /// 왜 필요한가: 출몰 게이트가 실제 시계에 묶여 있다 - 밤 종 가중은 18시 이후,
    /// 심야 종은 0~6시, 종이접기는 평일 낮 작업 중에만. 실제 시각을 기다려서는
    /// 게이트를 검증할 수 없으므로 판정만 강제로 바꾸는 스위치를 둔다.
    ///
    /// 시계 자체를 속이지 않고 판정 결과만 바꾼다 - 세이브의 타임스탬프(오프라인 정산,
    /// 크로노 주차)까지 오염되면 테스트가 실제 데이터를 망가뜨린다.
    /// </summary>
    public static class DevOverrides
    {
        public enum TimeMode { Real, Day, Night, LateNight }

        /// <summary>시간대 강제. Real이면 실제 시계.</summary>
        public static TimeMode time = TimeMode.Real;

        /// <summary>작업 상태 강제. null이면 실제 유휴 시간 판정.</summary>
        public static bool? working;

        /// <summary>요일 강제 (0=일 ... 6=토). null이면 실제 요일.</summary>
        public static int? dayOfWeek;

        public static bool Any => time != TimeMode.Real || working != null || dayOfWeek != null;

        public static void Clear()
        {
            time = TimeMode.Real;
            working = null;
            dayOfWeek = null;
        }

        /// <summary>
        /// 실효 작업 상태. 모든 소비자(출몰 부스트/생산 부스트/게이트)가 이걸 거쳐야
        /// 오버라이드가 한 곳에서 전부 먹는다.
        /// </summary>
        public static bool Working(float idleThreshold)
            => working ?? IdleTime.IsWorking(idleThreshold);

        /// <summary>HUD 표시용 요약. 오버라이드가 없으면 null.</summary>
        public static string Summary()
        {
            if (!Any) return null;

            string t = time == TimeMode.Real ? "실제"
                     : time == TimeMode.Day ? "낮"
                     : time == TimeMode.Night ? "밤" : "심야";
            string w = working == null ? "실제" : (working.Value ? "작업중" : "휴식");
            string d = dayOfWeek == null ? "실제" : "일월화수목금토"[dayOfWeek.Value].ToString();
            return $"시간:{t} · 작업:{w} · 요일:{d}";
        }
    }
}
