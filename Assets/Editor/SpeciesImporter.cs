using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// data.js의 SPECIES/FIELDS를 ScriptableObject로 만든다 (포팅계획 §5 "데이터 이전").
    ///
    /// 왜 손으로 안 만드는가: 12종 x 20여 필드를 인스펙터로 옮기면 오타가 조용히 섞이고,
    /// 원본 수치가 바뀌었을 때 무엇이 달라졌는지 대조할 방법이 없다. 표를 코드에 두면
    /// 원본과 diff로 맞춰볼 수 있다.
    ///
    /// 재실행해도 안전하다 - 기존 에셋이 있으면 값만 덮어쓰고 GUID는 유지하므로
    /// 씬/DB의 참조가 끊기지 않는다.
    ///
    /// 사용: 메뉴 [Deskmon/데이터 임포트 (data.js -> 에셋)]
    /// </summary>
    public static class SpeciesImporter
    {
        const string DATA_DIR = "Assets/Data";
        const string SPECIES_DIR = DATA_DIR + "/Species";
        const string FIELD_DIR = DATA_DIR + "/Fields";
        const string SPRITE_DIR = "Assets/Sprites";

        // data.js SPECIES 표를 그대로 옮긴 것. 순서도 원본과 같게 유지한다.
        struct Row
        {
            public string id, name, desc;
            public Field field;
            public Rarity rarity;
            public int forms;
            public string color, shiny;
            public string[] evo;
            public bool hop, night, rainbow, eventOnly;
            public SpawnGate gate;
            public Faction faction;
            public EvolveCondition evolve;
            public BehaviorPattern pattern;
        }

        static readonly Row[] Rows =
        {
            new Row { id="mongle", name="몽글이", field=Field.Grass, rarity=Rarity.Common, forms=3,
                      color="#8fd977", shiny="#f5a3d0", evo=new[]{"몽글이","잎몽이","꽃몽이"},
                      pattern=BehaviorPattern.Calm,
                      desc="햇살을 좋아하는 새싹 젤리. 기분이 좋으면 머리 위 잎이 살랑거린다." },

            new Row { id="kkang", name="깡총이", field=Field.Grass, rarity=Rarity.Common, forms=2,
                      color="#ffd35c", shiny="#7de8c3", evo=new[]{"깡총이","왕깡총"}, hop=true,
                      pattern=BehaviorPattern.Calm,
                      desc="가만히 있질 못하는 점프꾼. 귀가 안테나처럼 쫑긋거린다." },

            new Row { id="bandi", name="반디", field=Field.Grass, rarity=Rarity.Rare, forms=1,
                      color="#7dd6ff", shiny="#ffb0e0", evo=new[]{"반디"}, night=true,
                      pattern=BehaviorPattern.Shy,
                      desc="밤에만 반짝이는 수줍은 빛의 정령. 진화하지 않는 대신 태생이 특별하다." },

            new Row { id="dotori", name="도토리", field=Field.Forest, rarity=Rarity.Common, forms=3,
                      color="#b98d5e", shiny="#cfd6e0", evo=new[]{"도토리","도톨이","참나무지기"},
                      pattern=BehaviorPattern.Calm,
                      desc="도토리 모자를 아끼는 숲의 살림꾼. 겨울을 대비해 뭔가를 자꾸 모은다." },

            new Row { id="mush", name="버섯쫑", field=Field.Forest, rarity=Rarity.Rare, forms=2,
                      color="#ff8d7b", shiny="#7da9ff", evo=new[]{"버섯쫑","광대쫑"},
                      evolve=EvolveCondition.Fed,
                      pattern=BehaviorPattern.Shy,
                      desc="축축한 그늘에서 자라는 장난꾸러기. 갓을 흔들면 포자가 날린다." },

            new Row { id="owl", name="부엉", field=Field.Forest, rarity=Rarity.Epic, forms=2,
                      color="#a98bff", shiny="#ff9d6b", evo=new[]{"부엉","현자부엉"},
                      night=true, evolve=EvolveCondition.Night,
                      pattern=BehaviorPattern.Blink,
                      desc="숲의 밤을 지키는 관찰자. 밤이 깊어야만 다음 모습을 보여준다." },

            new Row { id="lumi", name="루미", field=Field.Special, rarity=Rarity.Legendary, forms=1,
                      color="#ffffff", shiny="#3d3b52", evo=new[]{"루미"}, rainbow=true,
                      pattern=BehaviorPattern.Drift,
                      desc="아주 드물게 나타나는 빛의 젤리. 붙잡을 수 없고, 오직 원을 그려 맞이해야 한다." },

            // ── M4 신규 ──
            new Row { id="dewdrop", name="이슬방울", field=Field.Lake, rarity=Rarity.Common, forms=2,
                      color="#8fd6e8", shiny="#ffd6a0", evo=new[]{"이슬방울","이슬왕관"},
                      faction=Faction.Dew,
                      pattern=BehaviorPattern.Shy,
                      desc="풀잎 끝에 맺히는 맑은 물방울. 이슬 팀의 상징이다." },

            new Row { id="mossy", name="이끼돌", field=Field.Lake, rarity=Rarity.Common, forms=2,
                      color="#9cc26b", shiny="#c89cff", evo=new[]{"이끼돌","이끼거인"},
                      faction=Faction.Moss,
                      pattern=BehaviorPattern.Calm,
                      desc="오래된 바위에 이끼가 자라 깨어난 정령. 이끼 팀의 상징이다." },

            new Row { id="origami", name="종이접기", field=Field.Office, rarity=Rarity.Rare, forms=1,
                      color="#f0f0f0", shiny="#ffe08a", evo=new[]{"종이접기"},
                      gate=SpawnGate.WeekdayWork,
                      pattern=BehaviorPattern.Calm,
                      desc="평일 낮, 일하는 책상에서만 접혀 나타나는 종이 생물." },

            new Row { id="dozy", name="꾸벅이", field=Field.Office, rarity=Rarity.Rare, forms=3,
                      color="#b3a6e0", shiny="#a0e0c0", evo=new[]{"꾸벅이","꿈꾸미","몽환몽"},
                      gate=SpawnGate.LateNight,
                      pattern=BehaviorPattern.Calm,
                      desc="깊은 밤에만 스르륵 나타나는 잠의 몬스터." },

            new Row { id="chrono", name="크로노", field=Field.Event, rarity=Rarity.Legendary, forms=1,
                      color="#ffffff", shiny="#3d3b52", evo=new[]{"크로노"},
                      rainbow=true, eventOnly=true,
                      pattern=BehaviorPattern.Drift,
                      desc="매주 금요일 밤, 모두의 화면에 동시에 나타나는 시간의 전설." },
        };

        // data.js FIELDS
        struct FieldRow
        {
            public Field id; public string name; public int cost;
            public string dayTop, dayBottom, nightTop, nightBottom;
        }

        static readonly FieldRow[] FieldRows =
        {
            new FieldRow { id=Field.Grass,  name="초원", cost=0,
                           dayTop="#e4f5d6", dayBottom="#bce69e", nightTop="#2b4152", nightBottom="#182b3c" },
            new FieldRow { id=Field.Forest, name="숲",   cost=200,
                           dayTop="#d2ebc6", dayBottom="#8fc07c", nightTop="#223447", nightBottom="#101f2c" },
            new FieldRow { id=Field.Lake,   name="호수", cost=600,
                           dayTop="#cfeaf0", dayBottom="#8fcdd8", nightTop="#1c3540", nightBottom="#0e2029" },
            new FieldRow { id=Field.Office, name="사무", cost=1800,
                           dayTop="#ece8e0", dayBottom="#c3bcae", nightTop="#2a2a33", nightBottom="#16161c" },
        };

        [MenuItem("Deskmon/데이터 임포트 (data.js -> 에셋)")]
        public static void Import()
        {
            Directory.CreateDirectory(SPECIES_DIR);
            Directory.CreateDirectory(FIELD_DIR);

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

            // ── 종 ──
            db.species.Clear();
            foreach (var r in Rows)
            {
                var sp = LoadOrCreate<SpeciesData>($"{SPECIES_DIR}/Species_{r.id}.asset");
                sp.id = r.id;
                sp.displayName = r.name;
                sp.description = r.desc;
                sp.field = r.field;
                sp.rarity = r.rarity;
                sp.forms = r.forms;
                sp.formNames = r.evo;
                sp.bodyColor = Hex(r.color);
                sp.shinyColor = Hex(r.shiny);
                sp.rainbow = r.rainbow;
                sp.hop = r.hop;
                sp.nightOnly = r.night;
                sp.gate = r.gate;
                sp.faction = r.faction;
                sp.eventOnly = r.eventOnly;
                sp.evolveCondition = r.evolve;
                sp.pattern = r.pattern;

                // 폼별 스프라이트 연결. 파일명 규칙:
                //   <id>.png         = 기본형 (스테이지 0)
                //   <id>_stage2.png  = 1차 진화형
                //   <id>_stage3.png  = 2차 진화형
                // 폼별 파일이 없으면 기본형을 재사용한다 - 도트가 폼별로 나오기 전까지의 상태.
                if (sp.formSprites == null || sp.formSprites.Length != r.forms)
                    sp.formSprites = new Sprite[r.forms];

                var baseSprite = LoadFormSprite(r.id, 0);
                for (int i = 0; i < r.forms; i++)
                {
                    var stageSprite = i == 0 ? baseSprite : LoadFormSprite(r.id, i);
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

            Debug.Log($"[Deskmon] 데이터 임포트 완료 - 종 {Rows.Length} · 필드 {FieldRows.Length} -> {DATA_DIR}");
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

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) ApplyPixelImportSettings(path);
            return sprite;
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
