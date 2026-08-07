using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Deskmon.Core;

namespace Deskmon.UI
{
    /// <summary>
    /// 도감 카드 이미지 저장. index.html exportDexCard() 이식 (기획 v4 §7.3 자랑 공유).
    ///
    /// Phase 1의 소셜은 서버가 아니라 "이미지 파일을 밖으로 들고 나가는 것"이다 -
    /// 카드 PNG를 저장해 주면 유저가 메신저/커뮤니티에 올리는 것으로 공유가 성립한다.
    ///
    /// 구현: 화면 밖(원점에서 먼 좌표)에 월드 캔버스로 카드를 조립하고, 임시 카메라로
    /// RenderTexture에 1회 렌더한 뒤 PNG로 저장한다. 화면에는 아무것도 비치지 않는다.
    /// </summary>
    public static class DexCardExporter
    {
        const int W = 560, H = 320;              // 원본 캔버스 크기
        static readonly Vector3 FAR = new Vector3(5000f, 5000f, 0f);   // 씬과 절대 안 겹치는 자리

        /// <summary>
        /// 카드를 만들어 저장하고 파일 경로를 돌려준다. 실패하면 null.
        /// 폼은 수집한 것 중 가장 높은 단계를 쓴다 - 카드는 자랑이니까 최고 폼이 맞다.
        /// </summary>
        public static string Export(SpeciesData sp, SaveData save, DeskmonDatabase db)
        {
            if (sp == null || save == null) return null;

            var dx = save.Dex(sp.id);
            if (dx.caught < 1) return null;

            int stage = 0;
            bool shiny = false;
            for (int i = sp.forms - 1; i >= 0; i--)
                if (dx.forms[i] || dx.shinyForms[i]) { stage = i; shiny = dx.shinyForms[i]; break; }

            // ── 조립 ──
            var canvasGO = new GameObject("DexCardCanvas", typeof(Canvas));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(W, H);
            crt.position = FAR;

            BuildCard(canvas.transform, sp, stage, shiny, dx, db, save);

            // ── 렌더 ──
            var camGO = new GameObject("DexCardCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = H * 0.5f;
            cam.transform.position = FAR + new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIKit.PanelBg;
            cam.enabled = false;   // 수동 1회 렌더만

            var rt = RenderTexture.GetTemporary(W, H, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            // ── 저장 ──
            string path = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            try
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();

                string dir = Path.Combine(Application.persistentDataPath, "cards");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, $"{sp.id}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());

                Object.Destroy(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[도감 카드] 저장 실패 - {e.Message}");
                path = null;
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(camGO);
                Object.Destroy(canvasGO);
            }

            if (path != null) Debug.Log($"[도감 카드] 저장 -> {path}");
            return path;
        }

        static void BuildCard(Transform root, SpeciesData sp, int stage, bool shiny,
                              SaveData.DexEntry dx, DeskmonDatabase db, SaveData save)
        {
            // 배경 - 테마 프레임이 있으면 그것, 없으면 단색 + 테두리
            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root, false);
            UIKit.Stretch((RectTransform)bg.transform);
            var bgImg = bg.GetComponent<Image>();
            if (UIKit.Theme != null && UIKit.Theme.frameCard != null)
            {
                bgImg.sprite = UIKit.Theme.frameCard;
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = 0.35f;   // 560px 카드에 맞게 테두리를 키운다
            }
            else bgImg.color = UIKit.PanelBg;

            // 왼쪽 - 크리처 (도트를 크게, Point 필터라 픽셀이 그대로 살아난다)
            var icon = UIKit.SpriteIcon(root, sp.SpriteAt(stage), 200f);
            var irt = (RectTransform)icon.transform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(40f, 0f);

            // 오른쪽 - 텍스트 블록
            float tx = 270f;

            var name = UIKit.Label(root, sp.NameAt(stage) + (shiny ? "  (샤이니)" : ""), 30,
                                   shiny ? UIKit.TextGold : UIKit.TextMain);
            name.fontStyle = FontStyle.Bold;
            Place(name.rectTransform, tx, 250f, 260f, 36f);

            var field = db?.GetField(sp.field);
            string fieldLine = field != null ? field.displayName : sp.field.ToString();
            if (sp.field == Field.Lake && !string.IsNullOrEmpty(save.faction))
                fieldLine += save.faction == "dew" ? " · 이슬 팀" : " · 이끼 팀";

            var meta = UIKit.Label(root,
                $"{fieldLine} · {RarityName(sp.rarity)}", 15, UIKit.Accent);
            Place(meta.rectTransform, tx, 218f, 260f, 20f);

            var desc = UIKit.Label(root, sp.description, 15, UIKit.TextSub);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(desc.rectTransform, tx, 130f, 250f, 80f);
            desc.alignment = TextAnchor.UpperLeft;

            int got = 0;
            for (int i = 0; i < sp.forms; i++) if (dx.forms[i] || dx.shinyForms[i]) got++;

            var stats = UIKit.Label(root,
                $"포획 {dx.caught}회 · 도감 {got}/{sp.forms}" + (dx.milestone ? " · 라인 완성" : ""),
                15, UIKit.TextMain);
            Place(stats.rectTransform, tx, 66f, 260f, 20f);

            var footer = UIKit.Label(root, "DESKMON", 13, UIKit.TextSub);
            Place(footer.rectTransform, tx, 28f, 260f, 18f);
        }

        /// <summary>좌하단 원점 절대 배치. 카드 한 장이라 레이아웃 그룹까지 갈 것 없다.</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "희귀";
                case Rarity.Epic: return "에픽";
                case Rarity.Legendary: return "전설";
                default: return "일반";
            }
        }
    }
}
