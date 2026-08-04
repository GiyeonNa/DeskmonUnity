using System;
using System.IO;
using UnityEngine;

namespace Deskmon.Core
{
    /// <summary>
    /// 세이브 로드/저장 + 마이그레이션. index.html의 save()/load()/migrateV4() 대응.
    ///
    /// 저장 위치는 Application.persistentDataPath/save.json.
    /// 원본은 localStorage였지만 파일이 백업·이관이 쉽고, 손상 시 원인을 볼 수 있다.
    ///
    /// 쓰기는 임시 파일에 쓴 뒤 교체한다(원자적 쓰기). 이 앱은 사용자가 Ctrl+Alt+Q로
    /// 언제든 죽일 수 있고 전체화면 감지로도 상태가 바뀌므로, 쓰는 도중 종료되면
    /// 세이브가 반토막 난 채 남는다. 그러면 다음 실행에서 전부 잃는다.
    /// </summary>
    public static class SaveSystem
    {
        const string FILE = "save.json";
        const string TEMP = "save.tmp";
        const string BACKUP = "save.bak";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FILE);

        /// <summary>
        /// 신규 세이브. index.html freshG().
        /// 종 목록이 필요하므로 DB를 받는다 - 종마다 빈 항목을 만들어 둔다.
        /// </summary>
        public static SaveData Fresh(DeskmonDatabase db)
        {
            var g = new SaveData
            {
                v = 4,
                berry = 0,
                installT = NowMs(),
                lastSeen = NowMs(),
            };

            if (db != null)
            {
                foreach (var sp in db.species)
                {
                    if (sp == null || string.IsNullOrEmpty(sp.id)) continue;
                    g.creatures.Add(new SaveData.CreatureEntry { id = sp.id });
                    g.dex.Add(new SaveData.DexEntry { id = sp.id });
                }
            }
            return g;
        }

        /// <summary>
        /// 로드. 파일이 없거나 깨졌으면 신규 세이브를 돌려준다.
        ///
        /// 원본 load()는 try/catch로 전부 삼키는데, 그러면 세이브가 왜 날아갔는지
        /// 알 수 없다. 여기서는 로그를 남기고 깨진 파일을 .bak으로 보존한다.
        /// </summary>
        public static SaveData Load(DeskmonDatabase db)
        {
            string path = SavePath;
            if (!File.Exists(path)) return Fresh(db);

            SaveData g = null;
            try
            {
                string json = File.ReadAllText(path);
                g = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 읽기 실패 - {e.Message}");
            }

            if (g == null)
            {
                // 깨진 파일을 덮어쓰기 전에 남긴다. 복구 시도의 유일한 근거다.
                try { File.Copy(path, Path.Combine(Application.persistentDataPath, BACKUP), true); }
                catch { /* 백업 실패가 신규 시작을 막을 이유는 없다 */ }

                Debug.LogWarning($"[Save] 세이브가 손상되어 새로 시작합니다. 원본은 {BACKUP}에 보관했습니다.");
                return Fresh(db);
            }

            EnsureEntries(g, db);
            Migrate(g, db);
            return g;
        }

        /// <summary>저장. 실패하면 false - 호출부가 사용자에게 알릴 수 있게 한다.</summary>
        public static bool Save(SaveData g)
        {
            if (g == null) return false;
            g.lastSeen = NowMs();

            string dir = Application.persistentDataPath;
            string path = Path.Combine(dir, FILE);
            string temp = Path.Combine(dir, TEMP);

            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(temp, JsonUtility.ToJson(g));

                // 임시 파일이 온전히 쓰인 뒤에만 교체한다.
                // File.Replace는 대상이 없으면 실패하므로 최초 저장은 Move로 처리한다.
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패 - {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// DB에 있는 종이 세이브에 없으면 채운다. index.html load()의 보정 루프.
        /// 새 종이 추가된 빌드로 넘어갔을 때 필요하다.
        /// </summary>
        static void EnsureEntries(SaveData g, DeskmonDatabase db)
        {
            if (db == null) return;

            foreach (var sp in db.species)
            {
                if (sp == null || string.IsNullOrEmpty(sp.id)) continue;
                g.Creature(sp.id);   // 없으면 만들어 넣는다
                g.Dex(sp.id);
            }

            if (g.habitats == null || g.habitats.Count == 0)
                g.habitats = new System.Collections.Generic.List<string> { "grass" };

            if (g.settings == null) g.settings = new SaveData.Settings();
            if (string.IsNullOrEmpty(g.settings.spawnRate)) g.settings.spawnRate = "normal";
            if (string.IsNullOrEmpty(g.settings.nightMode)) g.settings.nightMode = "auto";
            if (g.milestones == null) g.milestones = new SaveData.Milestones();
            if (g.ftue == null) g.ftue = new SaveData.Ftue { step = 5, firstSpawnDone = true };
        }

        /// <summary>
        /// v4 마이그레이션. index.html migrateV4() 이식.
        ///
        /// v4에서 일부 라인이 3단 -> 2폼으로 재편됐다(깡총이·버섯쫑·부엉 등). 구 세이브에는
        /// 이제 존재하지 않는 stage 2 개체가 남아 있는데, 그냥 두면 도감에 안 보이면서
        /// 생산은 계산되지 않는 유령이 된다. 최고 폼으로 합쳐서 유저가 잃지 않게 한다.
        /// </summary>
        static void Migrate(SaveData g, DeskmonDatabase db)
        {
            if (g.v >= 4 || db == null) return;

            foreach (var sp in db.species)
            {
                if (sp == null || string.IsNullOrEmpty(sp.id)) continue;

                int maxStage = sp.forms - 1;
                var c = g.Creature(sp.id);
                var dx = g.Dex(sp.id);

                for (int st = maxStage + 1; st < 3; st++)
                {
                    c.s[maxStage] += c.s[st];
                    c.s[st] = 0;

                    c.shiny[maxStage] += c.shiny[st];
                    c.shiny[st] = 0;

                    if (dx.forms[st]) { dx.forms[maxStage] = true; dx.forms[st] = false; }
                    if (dx.shinyForms[st]) { dx.shinyForms[maxStage] = true; dx.shinyForms[st] = false; }
                }
            }

            g.v = 4;
            Debug.Log("[Save] v4 마이그레이션 완료 - 축소된 폼의 개체를 최고 폼으로 합쳤습니다.");
        }

        public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
