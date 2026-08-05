using System.Collections.Generic;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인 포획. 상세기획 §12에서 홀드/타이밍/올가미 3종 미니게임을 이것 하나로 통합했다.
    /// overlay.html의 pickGlyphs/endStroke + wild의 각인 상태 이식.
    ///
    /// 흐름:
    ///   야생 근처 클릭 -> 몬스터 고정(Engaged) -> 목표 문양이 고스트로 표시
    ///   -> 마우스로 따라 그림 -> 뗄 때 판정 -> 맞으면 다음 문양, 다 맞추면 포획
    ///
    /// 철칙 (상세기획 §12): **손실 0.** 틀려도 잃는 것이 없고 흔들리기만 한다.
    /// 야생이 떠나는 유일한 이유는 체류시간 만료다. 이 규칙을 깨면 포획이 도박이 된다.
    /// </summary>
    public class SigilCapture : MonoBehaviour
    {
        [Header("데이터")]
        public DeskmonDatabase db;

        [Header("상태 (읽기 전용)")]
        [SerializeField] bool _engaged;
        [SerializeField] bool _drawing;
        [SerializeField] int _index;
        [SerializeField] string _current;

        /// <summary>각인 진행 중인가. 이 동안에는 화면 전체가 입력을 받아야 한다.</summary>
        public bool Engaged => _engaged;
        public bool Drawing => _drawing;

        /// <summary>지금 그려야 할 문양. 없으면 null.</summary>
        public string CurrentGlyph => _current;

        /// <summary>남은 문양 수.</summary>
        public int Remaining => Mathf.Max(0, _glyphs.Count - _index);
        public int TotalGlyphs => _glyphs.Count;

        /// <summary>그리는 중인 획. 화면에 궤적을 그릴 때 읽는다.</summary>
        public IReadOnlyList<Vector2> Stroke => _stroke;

        /// <summary>현재 문양의 진행 인덱스 (0부터). UI의 진행 점 표시에 쓴다.</summary>
        public int Index => _index;

        /// <summary>실패 흔들림 남은 시간. overlay.html wild.shakeT (0.4초).</summary>
        public float ShakeT { get; private set; }

        /// <summary>성공 플래시 남은 시간. overlay.html wild.okFlash (0.35초).</summary>
        public float OkFlash { get; private set; }

        /// <summary>UI 애니메이션용 누적 시간 (안내 빛점이 문양 경로를 도는 데 쓴다).</summary>
        public float Time01 { get; private set; }

        // ── 마지막 판정 결과 (진단용) ──
        // 인식이 안 될 때 원인을 좁히려면 이 값들이 필요하다. 목표 점수는 높은데
        // 분류가 다른 문양으로 갔다면 임계값을 낮춰도 소용없다 - Passes()가 분류 일치를
        // 요구하기 때문이다.

        /// <summary>마지막 획의 점 개수. 8 미만이면 판정 자체를 하지 않는다.</summary>
        public int LastPointCount { get; private set; }
        /// <summary>마지막 획이 무엇으로 분류됐는가.</summary>
        public string LastRecognized { get; private set; }
        /// <summary>그 분류의 점수.</summary>
        public float LastScore { get; private set; }
        /// <summary>목표 문양과의 직접 점수. 이게 높은데 실패하면 분류에서 밀린 것이다.</summary>
        public float LastTargetScore { get; private set; }

        /// <summary>문양 하나 성공.</summary>
        public event System.Action OnGlyphSuccess;
        /// <summary>판정 실패. 흔들림 연출용 - 페널티는 없다.</summary>
        public event System.Action OnGlyphFail;
        /// <summary>전부 성공 = 포획.</summary>
        public event System.Action OnCaptured;

        readonly List<string> _glyphs = new List<string>();
        readonly List<Vector2> _stroke = new List<Vector2>();

        SpeciesData _target;
        float _tolerance = 0.66f;

        /// <summary>
        /// 야생이 출몰했을 때 호출. 희귀도에 맞는 문양 목록을 뽑는다.
        /// </summary>
        public void Begin(SpeciesData species)
        {
            _target = species;
            _engaged = false;
            _drawing = false;
            _index = 0;
            _stroke.Clear();
            ShakeT = 0f;
            OkFlash = 0f;

            if (db?.balance != null)
                _tolerance = db.balance.sigilTolerance;

            int count = (db?.balance != null && species != null)
                ? db.balance.SigilCount(species.rarity)
                : 1;

            PickGlyphs(species != null ? species.rarity : Rarity.Common, count);
            _current = _glyphs.Count > 0 ? _glyphs[0] : null;
        }

        /// <summary>
        /// 미끼 사용 - 각인 1개 감소. overlay.html:200.
        /// 마지막 하나는 남긴다(0개면 그릴 것이 없어진다).
        /// </summary>
        public void ApplyBait()
        {
            if (_glyphs.Count > 1)
            {
                _glyphs.RemoveAt(_glyphs.Count - 1);
                RefreshCurrent();
            }
        }

        /// <summary>각인 시작 - 야생 근처를 눌렀을 때. 몬스터가 고정된다.</summary>
        public void Engage() => _engaged = true;

        /// <summary>획 시작.</summary>
        public void BeginStroke(Vector2 screenPos)
        {
            if (_target == null) return;
            _engaged = true;
            _drawing = true;
            _stroke.Clear();
            _stroke.Add(screenPos);
        }

        /// <summary>획 진행 - 드래그 중 매 프레임.</summary>
        public void AddPoint(Vector2 screenPos)
        {
            if (!_drawing) return;

            // 같은 자리에 머물면 점만 쌓여 리샘플이 왜곡된다. 최소 간격을 둔다.
            if (_stroke.Count > 0 && Vector2.Distance(_stroke[_stroke.Count - 1], screenPos) < 2f)
                return;

            _stroke.Add(screenPos);
        }

        /// <summary>
        /// 획 종료 = 판정. overlay.html endStroke() 이식.
        /// 반환값은 성공 여부지만, 실패해도 잃는 것은 없다.
        /// </summary>
        public bool EndStroke()
        {
            if (!_drawing) return false;
            _drawing = false;

            // 오클릭이나 너무 짧은 획은 판정 자체를 하지 않는다 - 페널티 없음.
            // 실수로 클릭한 것을 실패로 세면 "손실 0" 철칙이 체감상 깨진다.
            if (_stroke.Count < 8)
            {
                _stroke.Clear();
                return false;
            }

            var (name, score) = SigilRecognizer.Recognize(_stroke);

            // 실패 원인 추적용. 인식이 안 될 때 "무엇으로 인식됐고 목표 점수는 얼마였는지"를
            // 남기지 않으면 임계값 문제인지 분류 문제인지 구분할 수 없다.
            LastPointCount = _stroke.Count;
            LastRecognized = name;
            LastScore = score;
            LastTargetScore = SigilRecognizer.Match(_stroke, _current);

            _stroke.Clear();

            if (name == _current && score >= _tolerance)
            {
                OkFlash = 0.35f;
                _index++;
                if (_index >= _glyphs.Count)
                {
                    _current = null;
                    _engaged = false;
                    OnCaptured?.Invoke();
                    return true;
                }

                RefreshCurrent();
                OnGlyphSuccess?.Invoke();
                return true;
            }

            ShakeT = 0.4f;
            OnGlyphFail?.Invoke();
            return false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Time01 += dt;
            if (ShakeT > 0f) ShakeT -= dt;
            if (OkFlash > 0f) OkFlash -= dt;
        }

        /// <summary>
        /// 문양 목록을 직접 지정한다. **디버그 전용.**
        /// 무작위 추첨으로는 8종을 다 만나는 것이 보장되지 않아, 전수 확인용으로 둔다.
        /// </summary>
        public void DebugSetGlyphs(IList<string> glyphs)
        {
            if (glyphs == null || glyphs.Count == 0) return;

            _glyphs.Clear();
            for (int i = 0; i < glyphs.Count; i++) _glyphs.Add(glyphs[i]);

            _index = 0;
            RefreshCurrent();
            _stroke.Clear();
            _drawing = false;
            ShakeT = 0f;
            OkFlash = 0f;
        }

        /// <summary>
        /// 판정을 건너뛰고 문양 인덱스를 직접 옮긴다. **디버그 전용.**
        ///
        /// 각인 UI를 확인하려면 문양 8종이 실제로 어떻게 그려지는지 다 봐야 하는데,
        /// 정상 흐름에서는 앞 문양을 맞춰야만 다음으로 넘어가서 어려운 문양(나선·번개)에
        /// 막히면 뒤를 볼 수가 없다. 그래서 순회 수단을 따로 둔다.
        ///
        /// 포획으로 이어지지 않는다 - 마지막을 넘어가면 처음으로 돌아간다. 이 메서드로는
        /// OnCaptured가 발화하지 않으므로 도감/세이브에 영향을 주지 않는다.
        /// </summary>
        public void DebugStepGlyph(int delta)
        {
            if (_glyphs.Count == 0) return;

            _index += delta;

            // 순환시킨다 - 끝에서 막히면 순회가 아니라 또 다른 벽이 된다.
            if (_index < 0) _index = _glyphs.Count - 1;
            else if (_index >= _glyphs.Count) _index = 0;

            RefreshCurrent();

            // 넘어가는 순간의 잔상을 지운다. 실패 직후 넘겼는데 빨갛게 남아 있으면
            // 새 문양이 실패한 것처럼 보인다.
            _stroke.Clear();
            _drawing = false;
            ShakeT = 0f;
            OkFlash = 0f;
        }

        /// <summary>야생이 떠났을 때 - 상태를 비운다.</summary>
        public void Cancel()
        {
            _engaged = false;
            _drawing = false;
            _stroke.Clear();
            _glyphs.Clear();
            _current = null;
            _index = 0;
            _target = null;
            ShakeT = 0f;
            OkFlash = 0f;
        }

        void RefreshCurrent()
            => _current = (_index >= 0 && _index < _glyphs.Count) ? _glyphs[_index] : null;

        /// <summary>
        /// 문양 뽑기. overlay.html pickGlyphs() 이식.
        /// 에픽·전설은 50% 확률로 어려운 문양(별·번개·나선) 풀에서 뽑는다.
        /// 같은 문양이 연속으로 나오지 않게 최대 6회 다시 뽑는다.
        /// </summary>
        void PickGlyphs(Rarity rarity, int n)
        {
            _glyphs.Clear();
            if (db?.balance == null) { _glyphs.Add("circle"); return; }

            var easy = db.balance.sigilEasy;
            var hard = db.balance.sigilHard;
            if (easy == null || easy.Length == 0) { _glyphs.Add("circle"); return; }

            bool canHard = (rarity == Rarity.Epic || rarity == Rarity.Legendary)
                           && hard != null && hard.Length > 0;

            string last = null;
            for (int i = 0; i < n; i++)
            {
                var pool = (canHard && Random.value < 0.5f) ? hard : easy;

                string g = null;
                for (int tries = 0; tries < 6; tries++)
                {
                    g = pool[Random.Range(0, pool.Length)];
                    if (g != last) break;
                }

                _glyphs.Add(g);
                last = g;
            }
        }
    }
}
