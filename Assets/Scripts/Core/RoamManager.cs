using System.Collections.Generic;
using UnityEngine;
using Deskmon.Capture;
using Deskmon.Creatures;

namespace Deskmon.Core
{
    /// <summary>
    /// Save.roam 목록을 씬의 방목 개체로 투영한다. overlay.html의 roam-set 수신부에 대응.
    ///
    /// 목록이 정본이고 씬은 투영이다 - 토글/진화/돌려보내기는 전부 세이브를 고친 뒤
    /// Refresh()를 부른다. 씬 상태를 직접 만지는 API는 일부러 두지 않는다.
    /// 두 곳을 따로 고치기 시작하면 반드시 어긋난다.
    /// </summary>
    public class RoamManager : MonoBehaviour
    {
        [Tooltip("방목 개체 확대 배율. 야생과 같은 값이어야 크기가 튀지 않는다.")]
        public float roamerScale = 2f;

        readonly Dictionary<string, Roamer> _active = new Dictionary<string, Roamer>();

        CaptureEffects _effects;
        GameState _game;

        /// <summary>현재 방목 중인 개체 수.</summary>
        public int Count => _active.Count;

        /// <summary>첫 방목 개체 (개발용 HUD의 간식 대상 등).</summary>
        public Roamer First
        {
            get
            {
                foreach (var r in _active.Values) return r;
                return null;
            }
        }

        void Start()
        {
            // GameState는 DefaultExecutionOrder(-50)이라 Awake에서 세이브를 이미 읽었다.
            _game = GameState.Instance;
            Refresh();
        }

        /// <summary>
        /// 세이브의 roam 목록과 씬을 맞춘다. 목록에 없어진 개체는 지우고 새 키는 만든다.
        /// 이미 있는 개체는 그대로 둔다 - 매번 다 지우고 다시 만들면 위치가 초기화되어
        /// 토글 한 번에 전원이 순간이동하는 것처럼 보인다.
        /// </summary>
        public void Refresh()
        {
            if (_game?.Save == null) return;

            RoamSystem.Validate(_game.Save);

            // 제거된 키 정리
            var gone = new List<string>();
            foreach (var kv in _active)
                if (!_game.Save.roam.Contains(kv.Key)) gone.Add(kv.Key);

            foreach (var key in gone)
            {
                if (_active[key] != null) Destroy(_active[key].gameObject);
                _active.Remove(key);
            }

            // 새 키 생성
            foreach (var key in _game.Save.roam)
            {
                if (_active.ContainsKey(key)) continue;
                if (!RoamSystem.TryParse(key, out string id, out int stage, out bool shiny)) continue;

                var sp = _game.db?.Get(id);
                if (sp == null) continue;

                _active[key] = Spawn(key, sp, stage, shiny);
            }
        }

        Roamer Spawn(string key, SpeciesData sp, int stage, bool shiny)
        {
            var go = new GameObject($"Roamer_{key}");
            go.transform.localScale = Vector3.one * roamerScale;
            PlaceRandomly(go.transform);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;   // 야생(10)보다 뒤 - 출몰이 항상 앞에 보이게

            var appearance = go.AddComponent<CreatureAppearance>();
            appearance.Set(sp, stage, shiny);

            var view = go.AddComponent<CreatureView>();
            view.hop = sp.hop;

            var roamer = go.AddComponent<Roamer>();
            roamer.speciesId = sp.id;
            roamer.stage = stage;
            roamer.shiny = shiny;
            roamer.effects = EnsureEffects();

            return roamer;
        }

        void PlaceRandomly(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var screen = new Vector3(
                Random.Range(Screen.width * 0.15f, Screen.width * 0.85f),
                Random.Range(Screen.height * 0.15f, Screen.height * 0.6f),
                -cam.transform.position.z);
            t.position = cam.ScreenToWorldPoint(screen);
        }

        /// <summary>하트 연출용. 방목 전체가 하나를 공유한다.</summary>
        CaptureEffects EnsureEffects()
        {
            if (_effects != null) return _effects;
            var go = new GameObject("RoamEffects");
            go.transform.SetParent(transform, false);
            _effects = go.AddComponent<CaptureEffects>();
            return _effects;
        }
    }
}
