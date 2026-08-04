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
            _stroke.Clear();

            if (name == _current && score >= _tolerance)
            {
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

            OnGlyphFail?.Invoke();
            return false;
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
