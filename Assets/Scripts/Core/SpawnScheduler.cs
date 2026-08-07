using System.Collections.Generic;
using UnityEngine;
using Deskmon.Native;

namespace Deskmon.Core
{
    /// <summary>
    /// 출몰 스케줄러. index.html의 scheduleNext()/triggerSpawn() + 루프의 spawnIn 감산 이식.
    ///
    /// 시간을 재는 주체가 여기 하나여야 한다 - 원본은 setInterval 루프 안에서
    /// spawnIn을 깎았는데, 이 앱은 전체화면 감지로 카메라가 꺼지는 구간이 있어
    /// deltaTime 기반으로 두면 숨어 있는 동안 시간이 멈춘 것처럼 보인다.
    /// unscaledDeltaTime을 쓰는 이유가 그것이다.
    /// </summary>
    public class SpawnScheduler : MonoBehaviour
    {
        [Header("데이터")]
        public DeskmonDatabase db;

        [Header("상태 (읽기 전용)")]
        [SerializeField] float _spawnIn;
        [SerializeField] bool _spawnActive;

        /// <summary>다음 출몰까지 남은 초.</summary>
        public float SpawnIn => _spawnIn;
        public bool SpawnActive => _spawnActive;

        /// <summary>출몰 발생. (종, 샤이니 여부)</summary>
        public event System.Action<SpeciesData, bool> OnSpawn;

        SaveData _save;
        readonly List<SpeciesData> _pool = new List<SpeciesData>();

        public void Bind(SaveData save)
        {
            _save = save;
            ScheduleNext();
        }

        /// <summary>index.html scheduleNext() - 신규 유저는 더 자주 만난다.</summary>
        public void ScheduleNext()
        {
            if (db == null || db.balance == null) { _spawnIn = float.MaxValue; return; }

            var b = db.balance;
            Vector2 range = IsNewbie() ? b.spawnIntervalNewbie : b.spawnIntervalNormal;
            _spawnIn = Random.Range(range.x, range.y) * RateMultiplier();
        }

        /// <summary>설정의 출몰 빈도 배수. data.js SPAWN.rateMul. off면 무한대(출몰 없음).</summary>
        float RateMultiplier()
        {
            string rate = _save?.settings?.spawnRate ?? "normal";
            switch (rate)
            {
                case "fast": return 0.5f;
                case "slow": return 2f;
                case "off":  return float.MaxValue;
                default:     return 1f;
            }
        }

        /// <summary>설치 후 newbieWindow(기본 600초) 안인가.</summary>
        bool IsNewbie()
        {
            if (_save == null || db?.balance == null) return false;
            double elapsedSec = (SaveSystem.NowMs() - _save.installT) / 1000.0;
            return elapsedSec < db.balance.newbieWindow;
        }

        void Update()
        {
            if (_save == null || db?.balance == null || _spawnActive) return;
            if (_spawnIn >= float.MaxValue) return;   // off

            // 작업 중이면 더 자주 나온다. index.html:1151 - spawnIn을 1/boostSpawn 배속으로 깎는다.
            bool working = IdleTime.IsWorking(db.balance.workingIdleSec);
            float speed = working ? 1f / db.balance.boostSpawn : 1f;

            _spawnIn -= Time.unscaledDeltaTime * speed;
            if (_spawnIn <= 0f) { Trigger(false); return; }

            CheckChrono();
        }

        // ── 크로노 - 글로벌 동시 출몰 (기획 v4 §7.2) ──
        // 서버 없이 "전 세계가 같은 순간을 겪는" 장치다. 모두의 시계가 금요일
        // chronoHour(기본 21시)를 가리키는 순간 각자 로컬에서 출몰한다.
        // index.html:1145-1146 + triggerChrono() 이식.

        /// <summary>주 단위 ID. index.html chronoWeekId() - 주 1회 제한의 키.</summary>
        public static long ChronoWeekId() => SaveSystem.NowMs() / 604_800_000L;

        void CheckChrono()
        {
            if (_save?.ftue == null || !_save.ftue.firstSpawnDone) return;

            var now = System.DateTime.Now;
            if (now.DayOfWeek != System.DayOfWeek.Friday) return;
            if (db?.balance == null || now.Hour != db.balance.chronoHour) return;
            if (_save.lastChrono == ChronoWeekId()) return;

            TriggerChrono(markWeek: true);
        }

        /// <summary>
        /// 크로노 강제 출몰. markWeek이면 이번 주 참여로 기록한다 -
        /// 개발자 테스트(F7)는 기록하지 않아 본 이벤트에 영향을 주지 않는다.
        /// </summary>
        public bool TriggerChrono(bool markWeek)
        {
            if (_spawnActive || db == null || _save == null) return false;

            var sp = db.Get("chrono");
            if (sp == null) return false;

            if (markWeek) _save.lastChrono = ChronoWeekId();

            _spawnActive = true;
            if (_save.ftue != null) _save.ftue.firstSpawnDone = true;

            OnSpawn?.Invoke(sp, Random.value < db.balance.shinyRate);
            return true;
        }

        /// <summary>
        /// 출몰 실행. index.html triggerSpawn() 이식.
        /// tutorial이면 몽글이 고정 + 샤이니 없음 (첫 경험을 통제한다).
        /// </summary>
        public void Trigger(bool tutorial)
        {
            if (db == null || _save == null) return;

            BuildPool(tutorial);
            if (_pool.Count == 0)
            {
                // 조건이 맞는 종이 하나도 없을 수 있다(심야 전용만 남은 낮 등).
                // 그냥 다음 기회로 넘긴다 - 원본도 return만 한다.
                ScheduleNext();
                return;
            }

            var w = CurrentConditions();
            SpeciesData sp = tutorial ? db.Get("mongle") : db.PickWeighted(_pool, w);
            if (sp == null) { ScheduleNext(); return; }

            bool shiny = !tutorial && Random.value < db.balance.shinyRate;

            _spawnActive = true;
            if (_save.ftue != null) _save.ftue.firstSpawnDone = true;

            OnSpawn?.Invoke(sp, shiny);
        }

        /// <summary>출몰이 끝났을 때(포획/도망) 호출. 다음 일정을 잡는다.</summary>
        public void EndSpawn()
        {
            _spawnActive = false;
            ScheduleNext();
        }

        /// <summary>
        /// 출몰 후보 풀. index.html triggerSpawn()의 filter 이식.
        ///
        /// 순서가 중요하다 - 이벤트 종 제외 -> special 특례 -> 해금 검사 -> 신규 유저 제한
        /// -> 조건 판정. special(루미)은 해금 대상이 아니라서 필드 검사를 건너뛴다.
        /// </summary>
        void BuildPool(bool tutorial)
        {
            _pool.Clear();
            var w = CurrentConditions();
            Faction faction = ParseFaction(_save.faction);
            bool newbie = IsNewbie();

            foreach (var sp in db.species)
            {
                if (sp == null) continue;

                // 크로노 등 이벤트 전용은 일반 풀에 절대 넣지 않는다
                if (sp.eventOnly) continue;

                if (sp.field == Field.Special)
                {
                    // 루미: 튜토리얼/신규 구간 제외 + 서식지 2개 이상일 때만
                    if (tutorial || newbie) continue;
                    if (_save.habitats.Count < 2) continue;
                    if (!DeskmonDatabase.SpawnEligible(sp, w, faction)) continue;
                    _pool.Add(sp);
                    continue;
                }

                if (!_save.FieldUnlocked(FieldId(sp.field))) continue;

                // 신규 유저에게는 일반 등급만 - 첫 15분에 전설이 나오면 밸런스가 무너진다
                if (newbie && sp.rarity != Rarity.Common) continue;

                if (!DeskmonDatabase.SpawnEligible(sp, w, faction)) continue;
                _pool.Add(sp);
            }
        }

        WorldConditions CurrentConditions()
        {
            bool working = db?.balance != null && IdleTime.IsWorking(db.balance.workingIdleSec);
            return WorldConditions.Now(working);
        }

        /// <summary>세이브의 habitats는 문자열이다 - 열거형을 그 표기로 맞춘다.</summary>
        public static string FieldId(Field f) => f.ToString().ToLowerInvariant();

        static Faction ParseFaction(string s)
        {
            if (string.IsNullOrEmpty(s)) return Faction.None;
            if (s == "dew") return Faction.Dew;
            if (s == "moss") return Faction.Moss;
            return Faction.None;
        }
    }
}
