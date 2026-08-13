using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 151 원장(기획서 §17)을 SpeciesData/FieldData 에셋으로 만든다.
    ///
    /// v1은 data.js의 12종 표를 코드에 옮겨 적는 방식이었다. 151종부터는 기획서 §17
    /// 표가 정본이므로 DexLedger로 문서를 직접 파싱한다 — 문서 수정만으로 게임 데이터가
    /// 따라오고, 코드와 문서가 두 벌로 갈라지지 않는다.
    ///
    /// 원장에 없는 게임플레이 값은 두 층으로 채운다:
    ///   1) 휴리스틱 — 희귀도->행동패턴, 서브필드->출몰 게이트, 스프라이트->대표색.
    ///   2) Overrides — data.js에서 손튜닝돼 넘어온 초기 종들의 색/게이트/진영/진화조건.
    ///      세이브 호환 때문에 id가 원장과 다른 종(mush, owl)의 별칭도 여기서 흡수한다.
    ///
    /// 재실행해도 안전하다 - 기존 에셋이 있으면 값만 덮어쓰고 GUID는 유지하므로
    /// 씬/DB의 참조가 끊기지 않는다.
    ///
    /// 사용: 메뉴 [Deskmon/데이터 임포트 (151 원장 -> 에셋)]
    /// </summary>
    public static class SpeciesImporter
    {
        const string DATA_DIR = "Assets/Data";
        const string SPECIES_DIR = DATA_DIR + "/Species";
        const string FIELD_DIR = DATA_DIR + "/Fields";
        const string SPRITE_DIR = "Assets/Sprites";
        const string GEN_DIR = SPRITE_DIR + "/MonsterGenV2_64";

        /// <summary>
        /// 원장 기준 id -> 세이브가 쓰는 런타임 id.
        /// 세이브는 종 id 문자열을 키로 쓰므로(<see cref="SpeciesData.id"/>) 초기 12종
        /// 시절의 id를 바꿀 수 없다. 원장 재편에서 이름이 갈린 라인만 여기 적는다.
        /// </summary>
        static readonly Dictionary<string, string> RuntimeAlias = new Dictionary<string, string>
        {
            { "mushjong", "mush" },
            { "owloon", "owl" },
        };

        /// <summary>data.js에서 손튜닝돼 넘어온 값. 키는 런타임 id.</summary>
        struct Override
        {
            public string color, shiny;
            public bool night, rainbow;
            public SpawnGate gate;
            public Faction faction;
            public EvolveCondition evolve;
            public BehaviorPattern? pattern;
        }

        static readonly Dictionary<string, Override> Overrides = new Dictionary<string, Override>
        {
            { "mongle",  new Override { color="#8fd977", shiny="#f5a3d0" } },
            { "dotori",  new Override { color="#b98d5e", shiny="#cfd6e0" } },
            { "mush",    new Override { color="#ff8d7b", shiny="#7da9ff", evolve=EvolveCondition.Fed } },
            { "owl",     new Override { color="#a98bff", shiny="#ff9d6b", night=true, evolve=EvolveCondition.Night,
                                        pattern=BehaviorPattern.Blink } },
            { "lumi",    new Override { color="#ffffff", shiny="#3d3b52", rainbow=true } },
            { "dewdrop", new Override { color="#8fd6e8", shiny="#ffd6a0", faction=Faction.Dew,
                                        pattern=BehaviorPattern.Shy } },
            { "mossy",   new Override { color="#9cc26b", shiny="#c89cff", faction=Faction.Moss,
                                        pattern=BehaviorPattern.Calm } },
            { "origami", new Override { color="#f0f0f0", shiny="#ffe08a", gate=SpawnGate.WeekdayWork,
                                        pattern=BehaviorPattern.Calm } },
            { "dozy",    new Override { color="#b3a6e0", shiny="#a0e0c0", gate=SpawnGate.LateNight,
                                        pattern=BehaviorPattern.Calm } },
            { "chrono",  new Override { color="#ffffff", shiny="#3d3b52", rainbow=true } },
        };

        /// <summary>
        /// 도감에서 제외돼 원장에 없는 구 종. 에셋과 플레이스홀더 도트를 정리한다.
        /// 구 세이브에 잡힌 개체는 db 조회가 null을 돌려주는 경로로 조용히 무시된다.
        /// </summary>
        static readonly string[] Retired = { "kkang", "bandi" };

        // data.js FIELDS + 151 원장 확장 필드.
        //
        // 해금 비용은 시뮬레이션으로 보정한 값 (2026-08-13). 보유 크리처 전원이 상시
        // 생산하는 경제라 수입이 포획 수에 비례해 폭증한다 - 초기 가안(5천~1,200만)은
        // 하루 만에 전부 뚫렸다. 현 사다리는 하루 9시간 온라인/포획률 25% 기준
        // 첫날 3필드, 이후 2~6일 간격, 18일 차 완주가 나오게 잡았다 (포획률 60%면 12일).
        // 진화/친밀도 수입은 뺀 보수적 추정이므로 실제는 다소 빠르다. 실측 후 재조정.
        struct FieldRow
        {
            public Field id; public string name; public int cost;
            public string dayTop, dayBottom, nightTop, nightBottom;
        }

        static readonly FieldRow[] FieldRows =
        {
            new FieldRow { id=Field.Grass,    name="초원", cost=0,
                           dayTop="#e4f5d6", dayBottom="#bce69e", nightTop="#2b4152", nightBottom="#182b3c" },
            new FieldRow { id=Field.Forest,   name="숲",   cost=200,
                           dayTop="#d2ebc6", dayBottom="#8fc07c", nightTop="#223447", nightBottom="#101f2c" },
            new FieldRow { id=Field.Lake,     name="호수", cost=600,
                           dayTop="#cfeaf0", dayBottom="#8fcdd8", nightTop="#1c3540", nightBottom="#0e2029" },
            new FieldRow { id=Field.Office,   name="사무", cost=1800,
                           dayTop="#ece8e0", dayBottom="#c3bcae", nightTop="#2a2a33", nightBottom="#16161c" },
            new FieldRow { id=Field.Cave,     name="동굴", cost=100000,
                           dayTop="#cfc8d8", dayBottom="#9f93b0", nightTop="#241e33", nightBottom="#120f1f" },
            new FieldRow { id=Field.Mountain, name="산",   cost=700000,
                           dayTop="#e8f2f8", dayBottom="#b8d0dc", nightTop="#263a4d", nightBottom="#14222f" },
            new FieldRow { id=Field.Coast,    name="해안", cost=2000000,
                           dayTop="#cfeef2", dayBottom="#eeddb2", nightTop="#1c3140", nightBottom="#241f33" },
            new FieldRow { id=Field.Sky,      name="하늘", cost=7000000,
                           dayTop="#d8ecfb", dayBottom="#aacdf0", nightTop="#202c4d", nightBottom="#101830" },
            new FieldRow { id=Field.City,     name="도시", cost=20000000,
                           dayTop="#e6e2ea", dayBottom="#b8b2c2", nightTop="#2b2340", nightBottom="#171129" },
            new FieldRow { id=Field.Ruins,    name="유적", cost=60000000,
                           dayTop="#eee6d2", dayBottom="#c2b491", nightTop="#2e2a3a", nightBottom="#181524" },
            new FieldRow { id=Field.Machine,  name="기계", cost=150000000,
                           dayTop="#e2e6ea", dayBottom="#aab4bd", nightTop="#232833", nightBottom="#12161e" },
            new FieldRow { id=Field.Dream,    name="꿈",   cost=350000000,
                           dayTop="#ece2f6", dayBottom="#c3b0e0", nightTop="#2c2347", nightBottom="#160f2b" },
            new FieldRow { id=Field.Weather,  name="날씨", cost=700000000,
                           dayTop="#dfe9f0", dayBottom="#9fb8c8", nightTop="#26303f", nightBottom="#131a26" },
        };

        [MenuItem("Deskmon/데이터 임포트 (151 원장 -> 에셋)")]
        public static void Import()
        {
            Directory.CreateDirectory(SPECIES_DIR);
            Directory.CreateDirectory(FIELD_DIR);

            var lines = DexLedger.ParseLines();

            var db = LoadOrCreate<DeskmonDatabase>($"{DATA_DIR}/DeskmonDB.asset");
            db.balance = LoadOrCreate<BalanceData>($"{DATA_DIR}/Balance.asset");

            // ── 필드 ──
            db.fields.Clear();
            foreach (var fr in FieldRows)
            {
                var fd = LoadOrCreate<FieldData>($"{FIELD_DIR}/Field_{fr.id}.asset");
                fd.id = fr.id;
                fd.displayName = fr.name;
                fd.unlockCost = fr.cost;
                fd.dayTop = Hex(fr.dayTop);
                fd.dayBottom = Hex(fr.dayBottom);
                fd.nightTop = Hex(fr.nightTop);
                fd.nightBottom = Hex(fr.nightBottom);
                EditorUtility.SetDirty(fd);
                db.fields.Add(fd);
            }

            // ── 도감 제외 종 정리 ──
            foreach (var id in Retired)
            {
                AssetDatabase.DeleteAsset($"{SPECIES_DIR}/Species_{id}.asset");
                AssetDatabase.DeleteAsset($"{SPRITE_DIR}/{id}.png");
            }

            // ── 종 (원장 라인 순서 = 도감 순서) ──
            db.species.Clear();
            int copied = 0;

            foreach (var line in lines)
            {
                var baseE = line.Base;
                string rid = RuntimeAlias.TryGetValue(baseE.id, out var alias) ? alias : baseE.id;

                copied += SyncLineSprites(line, rid);

                var sp = LoadOrCreate<SpeciesData>($"{SPECIES_DIR}/Species_{rid}.asset");
                sp.id = rid;
                sp.displayName = baseE.koName;
                sp.description = baseE.desc;
                sp.field = baseE.field;
                sp.subfield = baseE.subfield;
                sp.rarity = baseE.rarity;
                sp.forms = line.forms.Count;

                sp.formNames = new string[line.forms.Count];
                sp.formIds = new string[line.forms.Count];
                sp.formDexNos = new int[line.forms.Count];
                for (int i = 0; i < line.forms.Count; i++)
                {
                    sp.formNames[i] = line.forms[i].koName;
                    sp.formIds[i] = line.forms[i].id;
                    sp.formDexNos[i] = line.forms[i].no;
                }

                // ── 게임플레이 값: 휴리스틱 + 손튜닝 오버라이드 ──
                Overrides.TryGetValue(rid, out var ov);
                bool hasOv = Overrides.ContainsKey(rid);

                // hop(점프 이동)은 깡총이 전용이었다 - 도감 제외로 현재 0종. 점프 라인이
                // 다시 생기면 Override에 되살린다.
                sp.hop = false;
                sp.rainbow = ov.rainbow;
                sp.faction = ov.faction;
                sp.evolveCondition = ov.evolve;

                // 밤 전용: 밤숲 서브필드 또는 손튜닝. 심야 게이트: 사무/심야 서브필드.
                sp.nightOnly = ov.night || baseE.subfield == "Nightwood";
                sp.gate = ov.gate != SpawnGate.None ? ov.gate
                        : baseE.subfield == "LateNight" ? SpawnGate.LateNight
                        : SpawnGate.None;

                // 이벤트 필드는 일반 출몰 풀에서 제외 (크로노·소원지).
                sp.eventOnly = baseE.field == Field.Event;

                // 행동 패턴 = 접근 난이도. 희귀할수록 까다롭게, 전설은 부유(Drift).
                sp.pattern = ov.pattern ?? baseE.rarity switch
                {
                    Rarity.Common => BehaviorPattern.Calm,
                    Rarity.Rare => BehaviorPattern.Shy,
                    Rarity.Epic => BehaviorPattern.Blink,
                    _ => BehaviorPattern.Drift,
                };

                // 대표색: 손튜닝이 없으면 실제 도트에서 추출한다. 팔레트 스왑(_BaseColor)의
                // 기준색이므로 스프라이트의 실제 몸색과 일치해야 샤이니가 제대로 물든다.
                if (hasOv && !string.IsNullOrEmpty(ov.color))
                {
                    sp.bodyColor = Hex(ov.color);
                    sp.shinyColor = Hex(ov.shiny);
                }
                else
                {
                    sp.bodyColor = DominantColor($"{SPRITE_DIR}/{rid}.png", sp.bodyColor);
                    sp.shinyColor = DeriveShiny(rid, sp.bodyColor);
                }

                // 폼별 스프라이트 연결. 파일명 규칙:
                //   <id>.png / <id>_stage2.png / <id>_stage3.png
                if (sp.formSprites == null || sp.formSprites.Length != sp.forms)
                    sp.formSprites = new Sprite[sp.forms];

                var baseSprite = LoadFormSprite(rid, 0);
                for (int i = 0; i < sp.forms; i++)
                {
                    var stageSprite = i == 0 ? baseSprite : LoadFormSprite(rid, i);
                    if (stageSprite != null) sp.formSprites[i] = stageSprite;
                    else if (sp.formSprites[i] == null) sp.formSprites[i] = baseSprite;
                }

                EditorUtility.SetDirty(sp);
                db.species.Add(sp);
            }

            EditorUtility.SetDirty(db.balance);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Deskmon] 데이터 임포트 완료 - 라인 {db.species.Count} (도감 {CountForms(lines)}) · " +
                      $"필드 {FieldRows.Length} · 도트 복사 {copied} -> {DATA_DIR}");
        }

        static int CountForms(List<DexLedger.Line> lines)
        {
            int n = 0;
            foreach (var l in lines) n += l.forms.Count;
            return n;
        }

        /// <summary>
        /// 생성 배치(MonsterGenV2_64)의 도트를 런타임 이름으로 복사한다.
        /// 내용이 같으면 건너뛰어 불필요한 재임포트를 막는다. 반환값은 복사한 파일 수.
        /// </summary>
        static int SyncLineSprites(DexLedger.Line line, string rid)
        {
            int copied = 0;
            for (int i = 0; i < line.forms.Count; i++)
            {
                var e = line.forms[i];
                string src = $"{GEN_DIR}/{e.no:000}_{e.id}_64.png";
                string dst = i == 0 ? $"{SPRITE_DIR}/{rid}.png" : $"{SPRITE_DIR}/{rid}_stage{i + 1}.png";

                if (!File.Exists(src))
                {
                    Debug.LogWarning($"[Deskmon] 생성 도트 없음: {src} - {dst}는 기존 파일/플레이스홀더 유지");
                    continue;
                }

                var srcBytes = File.ReadAllBytes(src);
                if (File.Exists(dst))
                {
                    var dstBytes = File.ReadAllBytes(dst);
                    if (BytesEqual(srcBytes, dstBytes)) continue;
                }

                File.WriteAllBytes(dst, srcBytes);
                AssetDatabase.ImportAsset(dst);
                copied++;
            }
            return copied;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// 도트에서 대표(최빈) 몸색을 뽑는다. 외곽선(어두운 픽셀)은 제외한다.
        ///
        /// 임포터 설정과 무관하게 읽기 위해 png 바이트를 직접 디코드한다
        /// (에셋 텍스처는 Read/Write 비활성이라 GetPixels가 막힌다).
        /// </summary>
        static Color DominantColor(string path, Color fallback)
        {
            if (!File.Exists(path)) return fallback;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return fallback;

                var buckets = new Dictionary<int, (int count, float r, float g, float b)>();
                foreach (var p in tex.GetPixels())
                {
                    if (p.a < 0.5f) continue;
                    if (Mathf.Max(p.r, Mathf.Max(p.g, p.b)) < 0.25f) continue; // 외곽선/짙은 그림자

                    int key = ((int)(p.r * 15f) << 8) | ((int)(p.g * 15f) << 4) | (int)(p.b * 15f);
                    buckets.TryGetValue(key, out var acc);
                    buckets[key] = (acc.count + 1, acc.r + p.r, acc.g + p.g, acc.b + p.b);
                }

                int best = -1; (int count, float r, float g, float b) bestAcc = default;
                foreach (var kv in buckets)
                    if (kv.Value.count > bestAcc.count) { best = kv.Key; bestAcc = kv.Value; }

                if (best < 0) return fallback;
                return new Color(bestAcc.r / bestAcc.count, bestAcc.g / bestAcc.count, bestAcc.b / bestAcc.count);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 샤이니색 자동 생성 - 몸색의 색상(H)을 종별 고정 오프셋만큼 돌린다.
        /// id 해시 기반이라 재임포트해도 같은 색이 나온다. 무채색 몸(종이·돌)은
        /// 채도를 끌어올려 스왑이 눈에 보이게 한다.
        /// </summary>
        static Color DeriveShiny(string id, Color body)
        {
            Color.RGBToHSV(body, out float h, out float s, out float v);

            uint hash = 5381;
            foreach (char c in id) hash = hash * 33 + c;

            float offset = 0.28f + (hash % 40u) / 100f;   // 0.28 ~ 0.67 회전
            h = (h + offset) % 1f;
            if (s < 0.15f) s = 0.45f;
            v = Mathf.Clamp(v, 0.35f, 0.95f);
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// 폼별 스프라이트 로드 + 픽셀아트 임포트 설정 강제.
        ///
        /// 설정을 여기서도 강제하는 이유: 손으로 그린 도트를 Assets/Sprites에 떨어뜨리면
        /// Unity가 기본 설정(Bilinear·압축)으로 임포트한다. 그대로 두면 도트가 뭉개진
        /// 채 연결되고, 원인이 임포트 설정이라는 것을 화면만 봐서는 알기 어렵다.
        /// </summary>
        static Sprite LoadFormSprite(string id, int stage)
        {
            string path = stage == 0
                ? $"{SPRITE_DIR}/{id}.png"
                : $"{SPRITE_DIR}/{id}_stage{stage + 1}.png";

            // 설정 강제를 로드보다 먼저 한다. 갓 복사된 png는 기본 설정(텍스처 타입
            // Default)으로 임포트되는데, 그 상태로는 LoadAssetAtPath<Sprite>가 null을
            // 돌려줘 "로드 성공 시에만 설정"이 영원히 안 돈다 (실제로 신규 70라인이
            // 전부 미연결로 나왔다).
            if (File.Exists(path)) ApplyPixelImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>포팅계획 §3.4의 픽셀아트 파이프라인. PlaceholderSpriteGen과 같은 값.</summary>
        static void ApplyPixelImportSettings(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;

            bool dirty = imp.textureType != TextureImporterType.Sprite
                      || imp.filterMode != FilterMode.Point
                      || imp.textureCompression != TextureImporterCompression.Uncompressed
                      || imp.spritePixelsPerUnit != 100f
                      || imp.mipmapEnabled;
            if (!dirty) return;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = 100f;   // CreatureView.PixelsToUnits와 일치
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        /// <summary>있으면 로드, 없으면 생성. GUID를 지켜 기존 참조가 끊기지 않게 한다.</summary>
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
