using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deskmon.EditorTools
{
    /// <summary>
    /// 플레이스홀더 픽셀 스프라이트 생성기.
    ///
    /// 진짜 도트를 그리기 전까지 파이프라인(Point 필터 · 팔레트 · 임포트 설정)을 먼저 완성하기 위한 것.
    /// creature.js의 젤리 블롭 형태(타원 몸통 + 하이라이트 + 눈)를 48px 그리드에 픽셀로 근사한다.
    /// 나중에 Aseprite 산출물로 파일만 교체하면 임포트 설정은 그대로 유지된다.
    ///
    /// 사용: 메뉴 [Deskmon/플레이스홀더 스프라이트 생성]
    /// </summary>
    public static class PlaceholderSpriteGen
    {
        const int SIZE = 48;
        const string DIR = "Assets/Sprites";

        // data.js SPECIES의 color 값 — 종별 팔레트 기준색
        static readonly (string id, string hex)[] Species =
        {
            ("mongle",  "#8fd977"), ("kkang",   "#ffd35c"), ("bandi",   "#7dd6ff"),
            ("dotori",  "#b98d5e"), ("mush",    "#ff8d7b"), ("owl",     "#a98bff"),
            ("lumi",    "#ffffff"), ("dewdrop", "#8fd6e8"), ("mossy",   "#9cc26b"),
            ("origami", "#f0f0f0"), ("dozy",    "#b3a6e0"), ("chrono",  "#ffffff"),
        };

        [MenuItem("Deskmon/플레이스홀더 스프라이트 생성")]
        public static void Generate()
        {
            Directory.CreateDirectory(DIR);

            foreach (var (id, hex) in Species)
            {
                ColorUtility.TryParseHtmlString(hex, out Color body);
                var tex = RenderBlob(body);
                string path = $"{DIR}/{id}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.Refresh();

            // 임포트 설정: 픽셀아트 파이프라인 (포팅계획서 §3.4)
            foreach (var (id, _) in Species)
            {
                string path = $"{DIR}/{id}.png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;          // 도트가 뭉개지지 않게
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.spritePixelsPerUnit = 100f;             // CreatureView.PixelsToUnits와 일치
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }

            Debug.Log($"[Deskmon] 플레이스홀더 스프라이트 {Species.Length}종 생성 완료 → {DIR}");
        }

        /// <summary>creature.js draw()의 몸통·하이라이트·눈을 픽셀로 근사.</summary>
        static Texture2D RenderBlob(Color body)
        {
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            var px = new Color[SIZE * SIZE];

            Color outline = Shade(body, -0.30f);
            Color shadow = Shade(body, -0.12f);
            Color hi = Color.white;

            float cx = SIZE * 0.5f - 0.5f;
            float cy = SIZE * 0.5f - 0.5f;
            float rx = SIZE * 0.40f;
            float ry = SIZE * 0.37f;   // creature.js: 몸통은 r × r*0.92

            for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                // 텍스처는 좌하단 원점 — 그리기는 좌상단 기준이라 y를 뒤집는다
                float dx = (x - cx) / rx;
                float dy = ((SIZE - 1 - y) - cy) / ry;
                float d = dx * dx + dy * dy;

                Color c = new Color(0, 0, 0, 0);

                if (d <= 1.0f)
                {
                    c = body;
                    if (d > 0.80f) c = outline;                    // 아웃라인 링
                    else if (dy > 0.35f) c = shadow;               // 아래쪽 음영
                    // 하이라이트 (creature.js: -r*0.32, -r*0.38 위치의 타원)
                    float hx = (dx + 0.34f) / 0.30f, hy = (dy + 0.40f) / 0.20f;
                    if (hx * hx + hy * hy <= 1f) c = Color.Lerp(c, hi, 0.55f);
                }

                px[y * SIZE + x] = c;
            }

            // 눈 — creature.js drawFace: ex = r*0.34, ey = -r*0.1
            int eyeY = Mathf.RoundToInt(cy - ry * 0.10f);
            int eyeDX = Mathf.RoundToInt(rx * 0.34f);
            DrawEye(px, Mathf.RoundToInt(cx) - eyeDX, eyeY);
            DrawEye(px, Mathf.RoundToInt(cx) + eyeDX, eyeY);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void DrawEye(Color[] px, int cxi, int cyi)
        {
            // 2×3 흰자 + 1×2 눈동자 (Gen-1 픽셀 밀도에 맞춘 최소 표현)
            for (int y = -1; y <= 1; y++)
            for (int x = 0; x <= 1; x++)
                Set(px, cxi + x, cyi + y, Color.white);

            Set(px, cxi, cyi, new Color(0.20f, 0.18f, 0.21f));
            Set(px, cxi, cyi + 1, new Color(0.20f, 0.18f, 0.21f));
        }

        static void Set(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || x >= SIZE || y < 0 || y >= SIZE) return;
            px[(SIZE - 1 - y) * SIZE + x] = c;   // 좌상단 기준 좌표를 텍스처 좌표로
        }

        /// <summary>creature.js shade() 이식.</summary>
        static Color Shade(Color c, float amt)
        {
            return new Color(
                Mathf.Clamp01(c.r + amt),
                Mathf.Clamp01(c.g + amt),
                Mathf.Clamp01(c.b + amt),
                c.a);
        }
    }
}
