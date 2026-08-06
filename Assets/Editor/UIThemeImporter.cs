using System.IO;
using UnityEditor;
using UnityEngine;
using Deskmon.UI;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// Assets/Sprites/UI의 이미지를 UITheme 에셋에 연결한다.
    /// UI_이미지_기획서.md §4 원장 -> §10 적용 절차의 구현부.
    ///
    /// 정식 파일(<asset_id>.png)이 있으면 그것을, 없으면 후보(<asset_id>_ai_v*.png) 중
    /// 최신 버전을 연결한다 - 화면 검수(§7.3)는 승인 전 후보로 해야 하기 때문이다.
    ///
    /// 임포트 설정과 9슬라이스 Border도 여기서 강제한다. 손으로 떨어뜨린 파일은
    /// Bilinear·압축으로 들어오고 Border는 아무도 안 채운다 - 크리처와 같은 함정이다.
    ///
    /// 사용: 메뉴 [Deskmon/UI 테마 임포트] (Sprites/UI에 png가 들어오면 자동 실행)
    /// </summary>
    public static class UIThemeImporter
    {
        const string UI_DIR = "Assets/Sprites/UI";
        const string THEME_PATH = "Assets/Data/UITheme.asset";

        // 원장(§4.2)의 asset_id / 9슬라이스 border. 새 이미지가 늘면 여기와
        // UITheme 필드를 함께 늘린다.
        static readonly (string id, int border, string field)[] Ledger =
        {
            ("ui_frame_card",      12, nameof(UITheme.frameCard)),
            ("ui_frame_button",     8, nameof(UITheme.frameButton)),
            ("ui_frame_button_on",  8, nameof(UITheme.frameButtonOn)),
            ("ui_frame_cell",       5, nameof(UITheme.frameCell)),
            ("ui_icon_berry",       0, nameof(UITheme.iconBerry)),
            ("ui_icon_heart",       0, nameof(UITheme.iconHeart)),
            ("ui_icon_bag",         0, nameof(UITheme.iconBag)),
            ("ui_icon_dex",         0, nameof(UITheme.iconDex)),
            ("ui_icon_gear",        0, nameof(UITheme.iconGear)),
            ("ui_icon_sparkle",     0, nameof(UITheme.iconSparkle)),
            ("ui_icon_sleep",       0, nameof(UITheme.iconSleep)),
            ("ui_badge",            0, nameof(UITheme.badge)),
            ("fx_heart",            0, nameof(UITheme.fxHeart)),
            ("fx_spark",            0, nameof(UITheme.fxSpark)),
            ("fx_ring",             0, nameof(UITheme.fxRing)),
        };

        [MenuItem("Deskmon/UI 테마 임포트")]
        public static void Import()
        {
            if (!Directory.Exists(UI_DIR))
            {
                Debug.Log("[UI 테마] Assets/Sprites/UI 폴더가 없습니다 - 이미지가 아직 없는 상태.");
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(THEME_PATH);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, THEME_PATH);
            }

            var so = new SerializedObject(theme);
            int linked = 0;

            foreach (var (id, border, field) in Ledger)
            {
                string path = Resolve(id);
                var prop = so.FindProperty(field);
                if (prop == null) continue;

                if (path == null)
                {
                    prop.objectReferenceValue = null;   // 파일이 사라졌으면 연결도 끊는다
                    continue;
                }

                ApplyImportSettings(path, border);
                prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (prop.objectReferenceValue != null) linked++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            WireScene(theme);
            Debug.Log($"[UI 테마] 연결 {linked}/{Ledger.Length} -> {THEME_PATH}");
        }

        /// <summary>정식 파일 우선, 없으면 후보 중 가장 높은 버전.</summary>
        static string Resolve(string id)
        {
            string official = $"{UI_DIR}/{id}.png";
            if (File.Exists(official)) return official;

            string best = null;
            int bestVer = -1;
            foreach (var f in Directory.GetFiles(UI_DIR, $"{id}_ai_v*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                string tail = name.Substring(name.LastIndexOf('v') + 1);
                if (int.TryParse(tail, out int ver) && ver > bestVer)
                {
                    bestVer = ver;
                    best = f.Replace('\\', '/');
                }
            }
            return best;
        }

        static void ApplyImportSettings(string path, int border)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;

            var wantBorder = border > 0
                ? new Vector4(border, border, border, border)
                : Vector4.zero;

            bool dirty = imp.textureType != TextureImporterType.Sprite
                      || imp.filterMode != FilterMode.Point
                      || imp.textureCompression != TextureImporterCompression.Uncompressed
                      || imp.mipmapEnabled
                      || imp.spriteBorder != wantBorder;
            if (!dirty) return;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spriteBorder = wantBorder;   // 9슬라이스 - 이게 없으면 늘릴 때 깨진다
            imp.SaveAndReimport();
        }

        /// <summary>열려 있는 씬의 UIRoot에 테마를 연결한다 - 씬 재생성 없이 적용되게.</summary>
        static void WireScene(UITheme theme)
        {
            var root = Object.FindFirstObjectByType<UIRoot>();
            if (root == null || root.theme == theme) return;

            root.theme = theme;
            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[UI 테마] 씬의 UIRoot에 테마를 연결했습니다. 씬을 저장하세요 (Ctrl+S).");
        }
    }
}
