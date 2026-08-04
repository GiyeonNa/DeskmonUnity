using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 세이브 스키마. index.html freshG()와 필드 이름까지 1:1로 맞춘다 (포팅계획 §5
    /// "세이브 스키마: 현행 JSON 그대로 유지").
    ///
    /// 왜 이름을 그대로 쓰는가: Electron판 유저의 localStorage를 그대로 읽을 수 있어야 한다.
    /// 이름을 C# 관례(PascalCase)로 바꾸면 기존 세이브가 통째로 무효가 된다.
    ///
    /// Unity JsonUtility는 Dictionary를 직렬화하지 못한다. 원본의 종별 맵
    /// (creatures{}, dex{}, friend{}, fed{})은 리스트로 담고 런타임에 인덱싱한다.
    /// 그래서 이 클래스는 JsonUtility가 아니라 <see cref="SaveSystem"/>의
    /// 수동 JSON 처리를 거친다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>스키마 버전. migrateV4의 기준.</summary>
        public int v = 4;

        public double berry;

        /// <summary>보유 수 - 종별 폼별. index.html creatures{id:{s[],shiny[]}}</summary>
        public List<CreatureEntry> creatures = new List<CreatureEntry>();

        /// <summary>도감. index.html dex{id:{caught,forms[],shinyForms[],milestone}}</summary>
        public List<DexEntry> dex = new List<DexEntry>();

        /// <summary>해금한 필드 id 목록. 기본 grass 하나.</summary>
        public List<string> habitats = new List<string> { "grass" };

        /// <summary>방목 중인 개체. "id:stage" 또는 "id:stage:shiny" 형식 문자열.</summary>
        public List<string> roam = new List<string>();

        /// <summary>친밀도 - 키는 "id:stage".</summary>
        public List<IntEntry> friend = new List<IntEntry>();

        /// <summary>미끼 먹인 기록 - 키는 "id:stage". 버섯쫑 진화 조건.</summary>
        public List<BoolEntry> fed = new List<BoolEntry>();

        /// <summary>진영. null/빈 문자열이면 미선택.</summary>
        public string faction;

        /// <summary>마지막 크로노 이벤트 참여 시각 (Unix ms).</summary>
        public long lastChrono;

        public Settings settings = new Settings();
        public Milestones milestones = new Milestones();
        public Ftue ftue = new Ftue();

        /// <summary>설치 시각 (Unix ms). 신규 유저 판정(newbieWindow)에 쓴다.</summary>
        public long installT;

        /// <summary>마지막 저장 시각 (Unix ms). 오프라인 정산의 기준.</summary>
        public long lastSeen;

        [Serializable]
        public class CreatureEntry
        {
            public string id;
            /// <summary>일반 개체 수 - 인덱스가 폼(stage). 항상 길이 3.</summary>
            public int[] s = new int[3];
            /// <summary>샤이니 개체 수.</summary>
            public int[] shiny = new int[3];
        }

        [Serializable]
        public class DexEntry
        {
            public string id;
            public int caught;
            public bool[] forms = new bool[3];
            public bool[] shinyForms = new bool[3];
            public bool milestone;
        }

        [Serializable] public class IntEntry  { public string k; public int v; }
        [Serializable] public class BoolEntry { public string k; public bool v; }

        [Serializable]
        public class Settings
        {
            /// <summary>fast | normal | slow | off — data.js SPAWN.rateMul의 키.</summary>
            public string spawnRate = "normal";
            /// <summary>auto | day | night</summary>
            public string nightMode = "auto";
        }

        [Serializable]
        public class Milestones
        {
            public List<string> habitats = new List<string>();
            public bool firstShiny;
        }

        [Serializable]
        public class Ftue
        {
            public int step;
            public bool firstSpawnDone;
        }

        // ── 조회 헬퍼 ──
        // 원본이 맵이라 O(1)이던 접근이 리스트에서는 선형 탐색이 된다.
        // 종이 12개뿐이라 실측 차이가 없고, 캐시를 두면 세이브와 어긋날 위험이 생긴다.

        public CreatureEntry Creature(string id)
        {
            for (int i = 0; i < creatures.Count; i++)
                if (creatures[i].id == id) return creatures[i];

            var e = new CreatureEntry { id = id };
            creatures.Add(e);
            return e;
        }

        public DexEntry Dex(string id)
        {
            for (int i = 0; i < dex.Count; i++)
                if (dex[i].id == id) return dex[i];

            var e = new DexEntry { id = id };
            dex.Add(e);
            return e;
        }

        public int Friend(string key)
        {
            for (int i = 0; i < friend.Count; i++)
                if (friend[i].k == key) return friend[i].v;
            return 0;
        }

        public void SetFriend(string key, int value)
        {
            for (int i = 0; i < friend.Count; i++)
                if (friend[i].k == key) { friend[i].v = value; return; }
            friend.Add(new IntEntry { k = key, v = value });
        }

        public bool Fed(string key)
        {
            for (int i = 0; i < fed.Count; i++)
                if (fed[i].k == key) return fed[i].v;
            return false;
        }

        public void SetFed(string key, bool value)
        {
            for (int i = 0; i < fed.Count; i++)
                if (fed[i].k == key) { fed[i].v = value; return; }
            fed.Add(new BoolEntry { k = key, v = value });
        }

        /// <summary>친밀도/미끼 맵의 키. index.html은 spId+':'+stage 형식을 쓴다.</summary>
        public static string Key(string speciesId, int stage) => speciesId + ":" + stage;

        public bool FieldUnlocked(string fieldId) => habitats.Contains(fieldId);
    }
}
