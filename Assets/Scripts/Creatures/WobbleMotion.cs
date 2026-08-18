using UnityEngine;

namespace Deskmon.Creatures
{
    /// <summary>
    /// 이동 중 "꿀렁" 연출의 공용 계산부 (기획 결정 2026-08-13).
    ///
    /// 원래 걷기는 프레임 애니메이션(MonsterWalk64 도트)으로 갈 계획이었지만 151종
    /// x 폼별 제작량이 기획상 어려워, 정지 도트 1장 + 코드 모션으로 확정했다.
    /// 대기(호흡)와 걷기(꿀렁)를 구분하는 것이 핵심이다 - 항상 같은 진동이면
    /// "살아있음"은 전달돼도 "걷고 있음"은 전달되지 않는다.
    ///
    /// 상태 전환에서 진폭/기울기를 즉시 바꾸면 스케일이 툭 튄다. 목표값으로
    /// 지수 보간하고, 위상은 주파수를 적분해 누적한다 - 주파수가 바뀌어도
    /// 위상이 연속이라 꺾임이 없다.
    ///
    /// 구조체로 둔 이유: 방목(CreatureView)과 야생(WildBehavior)이 같은 손맛을
    /// 공유하되, MonoBehaviour 상속 관계는 서로 얽히지 않게 한다.
    /// </summary>
    [System.Serializable]
    public struct WobbleMotion
    {
        float _phase;          // 적분된 위상 (라디안)
        float _amp, _tilt;     // 보간된 현재 진폭/기울기
        float _seed;           // 개체별 위상 오프셋 - 여럿이 동기화되면 군무처럼 보인다

        const float LERP_SPEED = 6f;

        public void Seed(float seed) => _seed = seed;

        /// <summary>매 프레임 목표 파라미터로 진행시킨다.</summary>
        public void Step(float dt, float freq, float targetAmp, float targetTiltDeg)
        {
            _phase += freq * dt;
            if (_phase > Mathf.PI * 20f) _phase -= Mathf.PI * 20f;   // 부동소수 정밀도 보호
            float k = 1f - Mathf.Exp(-LERP_SPEED * dt);
            _amp += (targetAmp - _amp) * k;
            _tilt += (targetTiltDeg - _tilt) * k;
        }

        /// <summary>부피 보존 스쿼시 (creature.js:252). sy가 줄면 sx가 늘어난다.</summary>
        public void Scale(out float sx, out float sy)
        {
            sy = 1f + Mathf.Sin(_phase + _seed) * _amp;
            sx = 1f / sy;
        }

        /// <summary>좌우 기우뚱 (도). 스쿼시와 같은 박자로 흔들린다.</summary>
        public float TiltDeg() => Mathf.Sin(_phase + _seed) * _tilt;

        /// <summary>
        /// 발걸음 바운스 높이 (0~1). 위상 절반 속도의 |sin| - 스쿼시 두 번에 한 발.
        /// 호출부가 px 진폭을 곱해 쓴다.
        /// </summary>
        public float Bounce() => Mathf.Abs(Mathf.Sin((_phase + _seed) * 0.5f));
    }
}
