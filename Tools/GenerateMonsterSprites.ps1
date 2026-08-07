param(
    [string]$DocsPath = "Docs/151종_몬스터_스프라이트_생성_기획서.md",
    [string]$OutputDir = "Assets/Sprites",
    [switch]$KeepMongleCanonical
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public sealed class MonsterSpritePainter {
    struct Pal {
        public Color Outline;
        public Color Dark;
        public Color Base;
        public Color Light;
        public Color Accent;
        public Color Accent2;
        public Color Eye;
    }

    static int Hash(string s) {
        unchecked {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h == Int32.MinValue ? 0 : Math.Abs(h);
        }
    }

    static Color C(int r, int g, int b) {
        return Color.FromArgb(255, Clamp(r), Clamp(g), Clamp(b));
    }

    static int Clamp(int v) {
        if (v < 0) return 0;
        if (v > 255) return 255;
        return v;
    }

    static Color Mix(Color a, Color b, int pctB) {
        int pctA = 100 - pctB;
        return C((a.R * pctA + b.R * pctB) / 100, (a.G * pctA + b.G * pctB) / 100, (a.B * pctA + b.B * pctB) / 100);
    }

    static Pal Palette(string field, string rarity, string prestige) {
        string major = field.Split('/')[0];
        Pal p;
        p.Eye = C(26, 32, 32);
        switch (major) {
            case "Grass":
                p.Outline=C(37,88,35); p.Dark=C(63,139,53); p.Base=C(111,206,83); p.Light=C(190,240,98); p.Accent=C(245,154,177); p.Accent2=C(246,223,93); break;
            case "Forest":
                p.Outline=C(65,50,30); p.Dark=C(106,91,45); p.Base=C(149,126,69); p.Light=C(210,176,96); p.Accent=C(78,156,74); p.Accent2=C(238,210,132); break;
            case "Lake":
                p.Outline=C(35,94,118); p.Dark=C(47,150,185); p.Base=C(103,211,231); p.Light=C(204,246,250); p.Accent=C(61,124,220); p.Accent2=C(236,248,174); break;
            case "Office":
                p.Outline=C(83,75,68); p.Dark=C(165,143,106); p.Base=C(239,224,184); p.Light=C(255,247,218); p.Accent=C(118,162,211); p.Accent2=C(205,92,120); break;
            case "Cave":
                p.Outline=C(45,48,58); p.Dark=C(83,91,105); p.Base=C(122,133,148); p.Light=C(183,205,216); p.Accent=C(120,218,232); p.Accent2=C(189,126,232); break;
            case "Mountain":
                p.Outline=C(64,70,82); p.Dark=C(107,124,137); p.Base=C(174,200,207); p.Light=C(235,248,247); p.Accent=C(110,172,228); p.Accent2=C(245,220,136); break;
            case "Coast":
                p.Outline=C(75,73,48); p.Dark=C(148,130,75); p.Base=C(223,194,111); p.Light=C(250,230,160); p.Accent=C(80,179,203); p.Accent2=C(245,245,219); break;
            case "Sky":
                p.Outline=C(61,92,128); p.Dark=C(100,161,203); p.Base=C(178,224,242); p.Light=C(245,252,255); p.Accent=C(244,211,105); p.Accent2=C(177,128,222); break;
            case "City":
                p.Outline=C(35,46,67); p.Dark=C(63,82,112); p.Base=C(108,129,160); p.Light=C(173,194,212); p.Accent=C(61,236,177); p.Accent2=C(255,212,79); break;
            case "Ruins":
                p.Outline=C(76,60,43); p.Dark=C(123,100,68); p.Base=C(184,157,104); p.Light=C(236,215,158); p.Accent=C(89,164,151); p.Accent2=C(244,198,65); break;
            case "Machine":
                p.Outline=C(43,55,64); p.Dark=C(91,107,120); p.Base=C(150,165,174); p.Light=C(224,232,233); p.Accent=C(96,246,144); p.Accent2=C(86,176,248); break;
            case "Dream":
                p.Outline=C(73,60,118); p.Dark=C(126,109,178); p.Base=C(184,163,226); p.Light=C(245,226,251); p.Accent=C(255,188,217); p.Accent2=C(255,236,134); break;
            case "Weather":
                p.Outline=C(47,79,122); p.Dark=C(79,142,189); p.Base=C(153,211,232); p.Light=C(240,249,255); p.Accent=C(251,225,73); p.Accent2=C(231,116,176); break;
            case "Event":
                p.Outline=C(68,54,94); p.Dark=C(117,105,158); p.Base=C(187,177,225); p.Light=C(255,246,210); p.Accent=C(255,218,79); p.Accent2=C(95,217,231); break;
            default:
                p.Outline=C(58,57,78); p.Dark=C(98,93,133); p.Base=C(162,148,207); p.Light=C(237,226,255); p.Accent=C(255,218,88); p.Accent2=C(88,222,205); break;
        }

        if (rarity == "Legendary" || prestige == "Legendary" || prestige == "Mythic" || prestige == "Transcendent") {
            p.Light = Mix(p.Light, C(255,255,255), 25);
            p.Accent = Mix(p.Accent, C(255,222,74), 35);
            p.Accent2 = Mix(p.Accent2, C(146,232,255), 25);
        }
        return p;
    }

    static Brush B(Color c) { return new SolidBrush(c); }
    static Pen P(Color c, int w) { return new Pen(c, w); }

    static Point[] Points(params int[] xy) {
        Point[] pts = new Point[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Point(xy[i*2], xy[i*2+1]);
        return pts;
    }

    static void Poly(Graphics g, Pal p, Color fill, params int[] xy) {
        Point[] pts = Points(xy);
        using (Brush fb = B(fill))
        using (Pen op = P(p.Outline, 2)) {
            g.FillPolygon(fb, pts);
            g.DrawPolygon(op, pts);
        }
    }

    static void Ellipse(Graphics g, Color outline, Color fill, int x, int y, int w, int h) {
        using (Brush ob = B(outline))
        using (Brush fb = B(fill)) {
            g.FillEllipse(ob, x-2, y-1, w+4, h+3);
            g.FillEllipse(fb, x, y, w, h);
        }
    }

    static void Rect(Graphics g, Color outline, Color fill, int x, int y, int w, int h) {
        using (Brush ob = B(outline))
        using (Brush fb = B(fill)) {
            g.FillRectangle(ob, x-2, y-2, w+4, h+4);
            g.FillRectangle(fb, x, y, w, h);
        }
    }

    static void FillRect(Graphics g, Color c, int x, int y, int w, int h) {
        using (Brush b = B(c)) g.FillRectangle(b, x, y, w, h);
    }

    static void Line(Graphics g, Color c, int x1, int y1, int x2, int y2, int w=1) {
        using (Pen pen = P(c, w)) g.DrawLine(pen, x1, y1, x2, y2);
    }

    static void EyePair(Graphics g, Pal p, int cx, int cy, int spread, bool wide=false) {
        int ew = wide ? 4 : 3;
        int eh = wide ? 4 : 5;
        FillRect(g, p.Eye, cx-spread, cy, ew, eh);
        FillRect(g, p.Eye, cx+spread-ew, cy, ew, eh);
        FillRect(g, Color.White, cx-spread+1, cy, 1, 1);
        FillRect(g, Color.White, cx+spread-ew+1, cy, 1, 1);
    }

    static void Smile(Graphics g, Pal p, int cx, int cy) {
        Line(g, p.Eye, cx-3, cy, cx-1, cy+1);
        Line(g, p.Eye, cx-1, cy+1, cx+1, cy+1);
        Line(g, p.Eye, cx+1, cy+1, cx+3, cy);
    }

    static void BodyBlob(Graphics g, Pal p, int stage, int h) {
        int w = 20 + stage * 4 + (h % 3);
        int bh = 18 + stage * 3 + (h % 2);
        int x = 24 - w/2;
        int y = 25 - bh/2 + (stage == 1 ? 2 : 0);
        Ellipse(g, p.Outline, p.Base, x, y, w, bh);
        using (Brush shade = B(p.Dark)) g.FillEllipse(shade, x+3, y+bh/2, w-6, bh/2-1);
        using (Brush hi = B(p.Light)) g.FillEllipse(hi, x+5, y+4, Math.Max(3,w/4), Math.Max(2,bh/5));
        Ellipse(g, p.Outline, p.Dark, x+3, y+bh-2, 6, 5);
        Ellipse(g, p.Outline, p.Dark, x+w-9, y+bh-2, 6, 5);
        EyePair(g, p, 24, y + bh/2 - 2, 6 + stage);
        Smile(g, p, 24, y + bh/2 + 5);
    }

    static void BodyAnimal(Graphics g, Pal p, int stage, int h) {
        int w = 20 + stage*4;
        int bh = 13 + stage*2;
        int x = 22 - w/2;
        int y = 25 - bh/2;
        Poly(g, p, p.Dark, x-7, y+4, x-1, y+2, x-2, y+8, x-8, y+10);
        Ellipse(g, p.Outline, p.Base, x, y, w, bh);
        Ellipse(g, p.Outline, p.Base, x+w-6, y-5, 13+stage, 13+stage);
        Ellipse(g, p.Outline, p.Dark, x+3, y+bh-1, 5, 5);
        Ellipse(g, p.Outline, p.Dark, x+w-9, y+bh-1, 5, 5);
        Poly(g, p, p.Accent, x+w-3, y-6, x+w+1, y-12, x+w+4, y-4);
        Poly(g, p, p.Accent, x+w+5, y-4, x+w+9, y-10, x+w+10, y-1);
        EyePair(g, p, x+w+1, y+1, 4);
    }

    static void BodyFish(Graphics g, Pal p, int stage, int h) {
        int w = 23 + stage*4;
        int bh = 13 + stage*2;
        int x = 24 - w/2;
        int y = 25 - bh/2;
        Poly(g, p, p.Accent, x-8, y+bh/2, x-1, y+1, x-1, y+bh-1);
        Ellipse(g, p.Outline, p.Base, x, y, w, bh);
        Poly(g, p, p.Light, x+w/2-2, y+1, x+w/2+6, y-5, x+w/2+9, y+3);
        Poly(g, p, p.Dark, x+w/2, y+bh-1, x+w/2+8, y+bh+5, x+w/2+10, y+bh-1);
        EyePair(g, p, x+w-7, y+3, 3, true);
    }

    static void BodyBird(Graphics g, Pal p, int stage, int h) {
        int w = 18 + stage*4;
        int bh = 20 + stage*2;
        int x = 24 - w/2;
        int y = 23 - bh/2;
        Poly(g, p, p.Dark, x-8, y+8, x, y+5, x, y+17, x-9, y+18);
        Poly(g, p, p.Dark, x+w+8, y+8, x+w, y+5, x+w, y+17, x+w+9, y+18);
        Ellipse(g, p.Outline, p.Base, x, y, w, bh);
        Poly(g, p, p.Accent2, 24, y+11, 27, y+14, 24, y+16, 21, y+14);
        EyePair(g, p, 24, y+7, 5, true);
        Ellipse(g, p.Outline, p.Dark, x+4, y+bh-1, 5, 4);
        Ellipse(g, p.Outline, p.Dark, x+w-9, y+bh-1, 5, 4);
    }

    static void BodyRock(Graphics g, Pal p, int stage, int h) {
        int w = 20 + stage*5;
        int bh = 19 + stage*4;
        int x = 24 - w/2;
        int y = 27 - bh/2;
        Poly(g, p, p.Base, x+4,y, x+w-5,y+1, x+w,y+8, x+w-3,y+bh-3, x+w/2,y+bh, x+1,y+bh-5, x,y+8);
        Line(g, p.Dark, x+7, y+6, x+14, y+10, 1);
        Line(g, p.Dark, x+w-8, y+5, x+w-13, y+13, 1);
        using (Brush hi = B(p.Light)) g.FillRectangle(hi, x+7, y+4, 5, 3);
        EyePair(g, p, 24, y+bh/2-1, 5);
    }

    static void BodyPaper(Graphics g, Pal p, int stage, int h) {
        int w = 19 + stage*4;
        int bh = 22 + stage*3;
        int x = 24 - w/2;
        int y = 24 - bh/2;
        if (h % 2 == 0) {
            Poly(g, p, p.Base, 24,y, x+w,y+bh/2, 24,y+bh, x,y+bh/2);
        } else {
            Rect(g, p.Outline, p.Base, x, y, w, bh);
        }
        Line(g, p.Dark, x+4, y+5, x+w-5, y+bh-4, 1);
        Line(g, p.Light, x+w-5, y+4, x+5, y+bh-5, 1);
        EyePair(g, p, 24, y+bh/2-3, 5);
    }

    static void BodyMachine(Graphics g, Pal p, int stage, int h) {
        int w = 20 + stage*4;
        int bh = 18 + stage*3;
        int x = 24 - w/2;
        int y = 25 - bh/2;
        Rect(g, p.Outline, p.Base, x, y, w, bh);
        FillRect(g, p.Dark, x+2, y+bh-5, w-4, 4);
        FillRect(g, p.Accent, x+4, y+4, 5, 3);
        FillRect(g, p.Accent2, x+w-8, y+4, 4, 6);
        EyePair(g, p, 24, y+bh/2-2, 6);
        Ellipse(g, p.Outline, p.Dark, x+4, y+bh, 5, 4);
        Ellipse(g, p.Outline, p.Dark, x+w-9, y+bh, 5, 4);
    }

    static void BodyCloud(Graphics g, Pal p, int stage, int h) {
        int x = 11 - stage;
        int y = 18 - stage;
        Ellipse(g, p.Outline, p.Base, x+1, y+8, 24+stage*4, 13+stage*2);
        Ellipse(g, p.Outline, p.Light, x+4, y+3, 10+stage, 11+stage);
        Ellipse(g, p.Outline, p.Base, x+14, y, 13+stage, 14+stage);
        Ellipse(g, p.Outline, p.Base, x+25, y+5, 10+stage, 11+stage);
        EyePair(g, p, 24, y+12, 6);
        Smile(g, p, 24, y+18);
    }

    static void DrawBase(Graphics g, Pal p, string id, string field, int stage, int h) {
        string s = id.ToLowerInvariant();
        if (s.Contains("fin") || s.Contains("wale") || s.Contains("fish")) BodyFish(g,p,stage,h);
        else if (s.Contains("owl") || s.Contains("bat") || s.Contains("wing") || s.Contains("kitet") || s.Contains("thra")) BodyBird(g,p,stage,h);
        else if (s.Contains("fox") || s.Contains("lynx") || s.Contains("kit") || s.Contains("pup") || s.Contains("hound") || s.Contains("bun") || s.Contains("rat") || s.Contains("goat") || s.Contains("horn")) BodyAnimal(g,p,stage,h);
        else if (s.Contains("paper") || s.Contains("origami") || s.Contains("note") || s.Contains("page") || s.Contains("book") || s.Contains("cursor") || s.Contains("window")) BodyPaper(g,p,stage,h);
        else if (s.Contains("gear") || s.Contains("cog") || s.Contains("magnet") || s.Contains("pixel") || s.Contains("core") || field.StartsWith("Machine")) BodyMachine(g,p,stage,h);
        else if (s.Contains("rock") || s.Contains("crag") || s.Contains("boulder") || s.Contains("pebbl") || s.Contains("obelisk") || s.Contains("monolith") || s.Contains("lith")) BodyRock(g,p,stage,h);
        else if (s.Contains("cloud") || s.Contains("nimb") || s.Contains("cirrus")) BodyCloud(g,p,stage,h);
        else BodyBlob(g,p,stage,h);
    }

    static void Leaf(Graphics g, Pal p, int x, int y, int dir) {
        Poly(g, p, p.Accent, x,y, x+dir*7,y-5, x+dir*10,y-1, x+dir*5,y+4);
        Line(g, p.Outline, x, y, x+dir*8, y-2, 1);
    }

    static void Flower(Graphics g, Pal p, int x, int y) {
        Ellipse(g, p.Outline, p.Accent, x-3, y-5, 6, 6);
        Ellipse(g, p.Outline, p.Accent, x-6, y-2, 6, 6);
        Ellipse(g, p.Outline, p.Accent, x, y-2, 6, 6);
        FillRect(g, p.Accent2, x-1, y-1, 3, 3);
    }

    static void Crystal(Graphics g, Pal p, int x, int y, int h) {
        Poly(g, p, p.Accent, x,y, x+4,y+h/2, x,y+h, x-4,y+h/2);
        FillRect(g, p.Light, x-1, y+3, 2, 4);
    }

    static void GearTeeth(Graphics g, Pal p, int cx, int cy, int r) {
        for (int i=0; i<8; i++) {
            double a=i*Math.PI/4.0;
            int x=cx+(int)Math.Round(Math.Cos(a)*r);
            int y=cy+(int)Math.Round(Math.Sin(a)*r);
            FillRect(g, p.Outline, x-1, y-1, 3, 3);
        }
        Ellipse(g, p.Outline, p.Base, cx-r+3, cy-r+3, (r-3)*2, (r-3)*2);
        Ellipse(g, p.Outline, p.Dark, cx-3, cy-3, 6, 6);
    }

    static void Decor(Graphics g, Pal p, string id, string field, string rarity, string prestige, int stage, int h) {
        string s = id.ToLowerInvariant();
        string major = field.Split('/')[0];
        bool legend = rarity == "Legendary" || prestige == "Legendary" || prestige == "Mythic" || prestige == "Transcendent";

        if (major=="Grass" || s.Contains("leaf") || s.Contains("mong")) {
            Leaf(g,p,22,12,-1); Leaf(g,p,26,12,1);
            if (stage >= 3 || s.Contains("bloom") || s.Contains("flor")) Flower(g,p,24,9);
        }
        if (s.Contains("flor")) { Flower(g,p,18,14); Flower(g,p,30,14); }
        if (s.Contains("budd") || s.Contains("gardo")) { Rect(g,p.Outline,p.Dark,17,15,14+stage*2,10); Leaf(g,p,24,13,1); if(stage>=3) Flower(g,p,24,10); }
        if (s.Contains("clov")) { Flower(g,p,16,13); Flower(g,p,32,13); Leaf(g,p,24,11,-1); Leaf(g,p,24,11,1); }
        if (s.Contains("sunpuff")) { for(int i=0;i<8;i++){ double a=i*Math.PI/4; Line(g,p.Light,24,17,24+(int)(Math.Cos(a)*11),17+(int)(Math.Sin(a)*11),1); } Ellipse(g,p.Outline,p.Light,13,8,22,22); }
        if (s.Contains("tang") || s.Contains("vine")) { Line(g,p.Outline,12,28,7,22,2); Line(g,p.Accent,13,27,8,22,1); Leaf(g,p,31,14,1); }
        if (s.Contains("mint")) { Leaf(g,p,14,20,-1); Leaf(g,p,34,28,1); Line(g,p.Accent,13,31,7,29,2); }
        if (s.Contains("acorn") || s.Contains("dotor") || s.Contains("oak")) { Rect(g,p.Outline,p.Dark,14,13,20,7); for(int x=15;x<34;x+=4) FillRect(g,p.Accent2,x,12,2,2); if(stage>=3) { Leaf(g,p,18,8,-1); Leaf(g,p,30,8,1); } }
        if (s.Contains("mush") || s.Contains("cap") || s.Contains("mycrown")) { Ellipse(g,p.Outline,p.Accent,12,10,24,12); FillRect(g,p.Light,18,13,4,3); FillRect(g,p.Light,28,12,3,3); }
        if (s.Contains("owl")) { FillRect(g,p.Light,17,17,5,5); FillRect(g,p.Light,26,17,5,5); Line(g,p.Accent2,24,20,21,23,1); Line(g,p.Accent2,24,20,27,23,1); }
        if (s.Contains("moss")) { for(int i=0;i<5;i++) FillRect(g,p.Accent,13+(i*5+h)%22,11+(i*7+h)%20,3,2); }
        if (s.Contains("twig") || s.Contains("branch")) { Line(g,p.Outline,16,12,10,6,2); Line(g,p.Outline,32,12,38,6,2); Line(g,p.Dark,10,6,8,3,1); Line(g,p.Dark,38,6,40,3,1); }
        if (s.Contains("bramble") || s.Contains("thorn")) { for(int i=0;i<6;i++){ int x=10+i*5; Poly(g,p,p.Accent,x,18-(i%2)*2,x+2,13-(i%2)*2,x+4,18-(i%2)*2); } }
        if (s.Contains("root") || s.Contains("warden")) { Line(g,p.Dark,18,33,15,39,2); Line(g,p.Dark,24,34,24,41,2); Line(g,p.Dark,30,33,34,39,2); }

        if (major=="Lake" || major=="Coast") {
            if (s.Contains("dew") || s.Contains("drop") || s.Contains("rain")) Crystal(g,p,24,8,10);
            if (s.Contains("shell")) { for(int i=0;i<5;i++) Line(g,p.Dark,16+i*3,15,20+i,27,1); }
            if (s.Contains("lily") || s.Contains("lotus")) { Leaf(g,p,17,18,-1); Leaf(g,p,31,18,1); if(stage>=3) Flower(g,p,24,11); }
            if (s.Contains("moon")) { Ellipse(g,p.Outline,p.Accent2,13,8,22,22); Ellipse(g,p.Outline,p.Base,19,7,20,22); }
            if (s.Contains("foam")) { Ellipse(g,p.Outline,p.Light,13,12,7,7); Ellipse(g,p.Outline,p.Light,27,10,6,6); Ellipse(g,p.Outline,p.Light,31,18,5,5); }
            if (s.Contains("tide") || s.Contains("lord")) { Poly(g,p,p.Accent,17,12,20,6,24,13,28,6,31,12); }
        }

        if (major=="Office") {
            if (s.Contains("stap")) { FillRect(g,p.Accent,15,13,18,3); FillRect(g,p.Outline,16,12,16,1); }
            if (s.Contains("cuppa") || s.Contains("mug")) { Ellipse(g,p.Outline,p.Base,14,14,18,20); Ellipse(g,p.Outline,p.Light,29,18,8,9); FillRect(g,p.Dark,16,14,14,4); }
            if (s.Contains("note") || s.Contains("page") || s.Contains("book")) { Line(g,p.Dark,17,17,31,17,1); Line(g,p.Dark,17,22,31,22,1); if(stage>=3){ Poly(g,p,p.Accent,10,20,2,14,4,31,12,28); Poly(g,p,p.Accent,38,20,46,14,44,31,36,28); } }
            if (s.Contains("ink")) { Crystal(g,p,24,11,12); FillRect(g,p.Dark,16,30,16,3); }
            if (s.Contains("dead")) { GearTeeth(g,p,24,18,9); Line(g,p.Accent2,24,18,29,14,1); }
            if (s.Contains("doz") || s.Contains("dream") || s.Contains("rever")) { Ellipse(g,p.Outline,p.Accent2,33,9,7,7); Line(g,p.Accent,11,12,16,9,1); Line(g,p.Accent,13,16,19,13,1); }
        }

        if (major=="Cave" || major=="Mountain") {
            if (s.Contains("cryst") || s.Contains("prism") || s.Contains("mica")) { Crystal(g,p,17,10,12); Crystal(g,p,31,12,10); }
            if (s.Contains("bat")) { Poly(g,p,p.Dark,8,20,2,13,4,28); Poly(g,p,p.Dark,40,20,46,13,44,28); if(stage>=2) FillRect(g,p.Accent2,23,10,3,5); }
            if (s.Contains("snow") || s.Contains("frost") || s.Contains("aval")) { FillRect(g,p.Light,14,11,20,5); FillRect(g,p.Light,17,8,14,4); }
            if (s.Contains("goat") || s.Contains("horn")) { Line(g,p.Accent2,16,12,11,5,2); Line(g,p.Accent2,32,12,37,5,2); }
            if (s.Contains("auror")) { Line(g,p.Accent2,10,10,20,5,1); Line(g,p.Accent,20,5,31,8,1); Line(g,p.Light,31,8,38,4,1); }
            if (s.Contains("echo")) { using(Pen pen=P(p.Accent,1)){ g.DrawArc(pen, 8,13,12,12,90,180); g.DrawArc(pen, 28,13,12,12,270,180); } }
            if (s.Contains("core") || s.Contains("chasm")) { Ellipse(g,p.Outline,p.Accent2,19,16,10,10); FillRect(g,p.Light,22,19,4,4); }
        }

        if (major=="Sky" || major=="Weather") {
            if (s.Contains("balloon")) { Ellipse(g,p.Outline,p.Accent,16,6,16,20); Line(g,p.Outline,24,26,21,34,1); Line(g,p.Outline,24,26,27,34,1); }
            if (s.Contains("kite")) { Poly(g,p,p.Accent,24,6,34,18,24,30,14,18); Line(g,p.Outline,24,30,20,38,1); }
            if (s.Contains("star") || s.Contains("zenith")) { Poly(g,p,p.Accent2,24,6,27,15,36,15,29,20,32,30,24,24,16,30,19,20,12,15,21,15); }
            if (s.Contains("thund") || s.Contains("storm") || s.Contains("tempest")) { Poly(g,p,p.Accent2,27,7,18,24,25,23,21,37,34,18,27,19); }
            if (s.Contains("snow")) { for(int i=0;i<4;i++){ int x=13+i*7; Line(g,p.Light,x,8,x,14,1); Line(g,p.Light,x-3,11,x+3,11,1); } }
            if (s.Contains("rain")) { for(int i=0;i<4;i++) Crystal(g,p,14+i*7,8+(i%2)*3,6); }
            if (s.Contains("prism")) { Line(g,C(246,84,107),10,13,38,13,1); Line(g,C(246,207,72),10,16,38,16,1); Line(g,C(84,207,114),10,19,38,19,1); Line(g,C(84,158,236),10,22,38,22,1); }
        }

        if (major=="City" || major=="Machine" || major=="Special" || major=="Event") {
            if (s.Contains("neon") || s.Contains("volt") || s.Contains("plug")) { Poly(g,p,p.Accent2,27,7,19,23,25,23,21,36,34,17,28,18); }
            if (s.Contains("rail") || s.Contains("metro")) { Line(g,p.Accent,13,13,35,13,1); Line(g,p.Dark,15,17,33,28,2); Line(g,p.Dark,33,17,15,28,2); }
            if (s.Contains("cog") || s.Contains("gear")) { GearTeeth(g,p,24,19,12); }
            if (s.Contains("magnet")) { Rect(g,p.Outline,p.Dark,14,10,20,22); FillRect(g,Color.Transparent,19,15,10,15); FillRect(g,p.Accent,14,27,5,5); FillRect(g,p.Accent2,29,27,5,5); }
            if (s.Contains("pixel")) { for(int i=0;i<7;i++) FillRect(g, (i%2==0?p.Accent:p.Accent2), 10+(i*5+h)%28, 10+(i*7+h)%25, 3, 3); }
            if (s.Contains("chrono")) { GearTeeth(g,p,24,21,13); Line(g,p.Accent2,24,21,24,13,1); Line(g,p.Accent2,24,21,31,24,1); }
            if (s.Contains("cursor")) { Poly(g,p,p.Light,15,7,35,25,26,27,31,38,25,40,20,29,14,35); }
            if (s.Contains("window")) { Rect(g,p.Outline,p.Dark,13,10,22,19); Line(g,p.Accent,14,16,34,16,1); Line(g,p.Accent2,24,11,24,28,1); }
            if (s.Contains("lumi")) { Ellipse(g,p.Outline,p.Light,15,9,18,22); for(int i=0;i<8;i++){ double a=i*Math.PI/4; Line(g,p.Accent2,24,20,24+(int)(Math.Cos(a)*17),20+(int)(Math.Sin(a)*17),1); } }
            if (s.Contains("deskron")) { Rect(g,p.Outline,p.Dark,10,12,28,20); Poly(g,p,p.Accent,14,15,22,11,30,15,22,19); Line(g,p.Accent2,9,9,39,35,1); }
            if (s.Contains("wish")) { Poly(g,p,p.Light,24,7,32,18,28,35,17,35,15,18); Line(g,p.Accent,18,16,30,28,1); }
        }

        if (legend) {
            for (int i=0;i<4;i++) {
                int x = 7 + ((h >> (i*3)) % 35);
                int y = 6 + ((h >> (i*2)) % 28);
                FillRect(g, p.Accent2, x, y, 2, 2);
            }
        }
    }

    static void UniqueMarks(Graphics g, Pal p, int h) {
        int count = 2 + (h % 2);
        for (int i=0; i<count; i++) {
            int x = 16 + ((h >> (i * 5)) & 15);
            int y = 29 + ((h >> (i * 4 + 3)) & 7);
            Color c = (i % 2 == 0) ? p.Accent2 : p.Dark;
            if (((h >> (i + 2)) & 1) == 0) FillRect(g, c, x, y, 2, 1);
            else FillRect(g, c, x, y, 1, 2);
        }
    }

    static void Spark(Graphics g, Color c, int x, int y) {
        FillRect(g, c, x, y-2, 1, 5);
        FillRect(g, c, x-2, y, 5, 1);
    }

    static void ArcDots(Graphics g, Color c, int cx, int cy, int r, int count) {
        for (int i=0; i<count; i++) {
            double a = Math.PI + (Math.PI * i / Math.Max(1, count-1));
            FillRect(g, c, cx + (int)Math.Round(Math.Cos(a)*r), cy + (int)Math.Round(Math.Sin(a)*r), 2, 2);
        }
    }

    static void ProfileToken(Graphics g, Pal p, string token, int stage, int h) {
        switch (token.Trim().ToLowerInvariant()) {
            case "petalhair": Flower(g,p,19,13); Flower(g,p,24,11); Flower(g,p,29,13); break;
            case "petalwings": Poly(g,p,p.Accent,10,21,3,14,5,32,14,29); Poly(g,p,p.Accent,38,21,45,14,43,32,34,29); Flower(g,p,24,10); break;
            case "seedshell": Rect(g,p.Outline,p.Dark,15,16,18,10); FillRect(g,p.Accent2,20,16,3,2); break;
            case "sproutback": Rect(g,p.Outline,p.Dark,16,16,17,9); Leaf(g,p,23,12,-1); Leaf(g,p,25,12,1); break;
            case "gardenback": Rect(g,p.Outline,p.Dark,13,16,22,11); Flower(g,p,19,12); Flower(g,p,29,12); break;
            case "cloverears": Flower(g,p,14,13); Flower(g,p,34,13); break;
            case "clovertail": Leaf(g,p,34,29,1); Flower(g,p,38,27); break;
            case "dandelion": for(int i=0;i<10;i++){ double a=i*Math.PI/5; Line(g,p.Light,24,18,24+(int)(Math.Cos(a)*14),18+(int)(Math.Sin(a)*14),1); } break;
            case "vinetail": Line(g,p.Outline,12,30,5,24,2); Line(g,p.Accent,12,29,6,24,1); Leaf(g,p,8,23,-1); break;
            case "spiralvine": using(Pen pen=P(p.Accent,2)){ g.DrawArc(pen,7,22,12,12,0,300); } break;
            case "vinecrest": Poly(g,p,p.Accent,18,11,24,4,30,11,27,17,21,17); break;
            case "mintscarf": Line(g,p.Accent,13,22,35,22,3); Leaf(g,p,14,22,-1); break;
            case "minttail": Line(g,p.Accent,33,31,43,29,3); Leaf(g,p,42,28,1); break;
            case "acornhelm": Rect(g,p.Outline,p.Dark,14,12,20,7); for(int x=16;x<33;x+=4) FillRect(g,p.Accent2,x,12,2,2); break;
            case "seedantlers": Line(g,p.Outline,16,13,11,6,2); Line(g,p.Outline,32,13,37,6,2); Leaf(g,p,11,6,-1); Leaf(g,p,37,6,1); break;
            case "oakbranch": Line(g,p.Outline,16,10,11,6,2); Line(g,p.Outline,32,10,37,6,2); Leaf(g,p,12,7,-1); Leaf(g,p,36,7,1); break;
            case "mushcap": Ellipse(g,p.Outline,p.Accent,12,9,24,12); FillRect(g,p.Light,18,12,4,3); FillRect(g,p.Light,28,12,3,3); break;
            case "sporedots": for(int i=0;i<5;i++) FillRect(g,p.Light,13+i*5,10+(i%2)*3,2,2); break;
            case "owlbrows": Line(g,p.Outline,17,16,22,14,2); Line(g,p.Outline,26,14,31,16,2); break;
            case "starwings": Poly(g,p,p.Accent2,9,18,3,11,6,29); Poly(g,p,p.Accent2,39,18,45,11,42,29); Spark(g,p.Accent2,24,10); break;
            case "mosscoat": for(int i=0;i<7;i++) FillRect(g,p.Accent,11+(i*5+h)%26,13+(i*7)%20,3,2); break;
            case "burrowclaws": Line(g,p.Light,13,35,9,38,1); Line(g,p.Light,18,36,15,40,1); Line(g,p.Light,35,35,39,38,1); break;
            case "twighorns": Line(g,p.Outline,16,12,10,6,2); Line(g,p.Outline,32,12,38,6,2); Line(g,p.Dark,10,6,8,3,1); Line(g,p.Dark,38,6,40,3,1); break;
            case "branchstaff": Line(g,p.Outline,39,12,36,35,2); Leaf(g,p,38,16,1); break;
            case "sapdrops": Crystal(g,p,17,12,8); Crystal(g,p,31,15,7); break;
            case "ambergems": Crystal(g,p,18,12,9); Crystal(g,p,30,12,9); FillRect(g,p.Accent2,22,30,4,3); break;
            case "thorncrown": for(int i=0;i<6;i++){ int x=11+i*5; Poly(g,p,p.Accent,x,18-(i%2)*2,x+2,12-(i%2)*2,x+4,18-(i%2)*2); } break;
            case "rosebloom": Flower(g,p,17,13); Flower(g,p,31,13); break;
            case "rootbeard": Line(g,p.Dark,18,31,14,39,2); Line(g,p.Dark,24,32,24,41,2); Line(g,p.Dark,30,31,35,39,2); break;
            case "waterdrop": Crystal(g,p,24,7,11); break;
            case "dewcrown": ArcDots(g,p.Light,24,20,14,7); Crystal(g,p,24,7,9); break;
            case "shoremoss": FillRect(g,p.Accent,14,32,20,3); Leaf(g,p,16,31,-1); break;
            case "stonearms": Ellipse(g,p.Outline,p.Dark,7,23,9,8); Ellipse(g,p.Outline,p.Dark,32,23,9,8); break;
            case "ripplecrown": using(Pen pen=P(p.Accent,1)){ g.DrawArc(pen,13,9,22,9,0,180); g.DrawArc(pen,16,12,16,7,0,180); } break;
            case "wavefins": Poly(g,p,p.Accent,8,24,2,17,3,32); Poly(g,p,p.Accent,40,24,46,17,45,32); break;
            case "broadfins": Poly(g,p,p.Accent,6,23,0,14,1,36,14,30); Poly(g,p,p.Accent,42,23,48,14,47,36,34,30); break;
            case "shellridges": for(int i=0;i<5;i++) Line(g,p.Dark,15+i*4,14,19+i*2,29,1); break;
            case "pearlcore": Ellipse(g,p.Outline,p.Light,19,17,10,10); Spark(g,p.Accent2,24,15); break;
            case "fogwisps": Line(g,p.Light,10,16,17,13,1); Line(g,p.Light,31,12,39,15,1); Line(g,p.Light,13,33,22,34,1); break;
            case "lilypad": Leaf(g,p,15,18,-1); Leaf(g,p,33,18,1); break;
            case "lotuscrown": Leaf(g,p,16,17,-1); Leaf(g,p,32,17,1); Flower(g,p,24,10); break;
            case "raindrops": for(int i=0;i<4;i++) Crystal(g,p,13+i*7,7+(i%2)*3,6); break;
            case "bubblecrown": for(int i=0;i<5;i++) Ellipse(g,p.Outline,p.Light,13+i*5,9+(i%2)*3,4,4); break;
            case "mooncrest": Ellipse(g,p.Outline,p.Accent2,13,8,22,22); Ellipse(g,p.Outline,p.Base,19,7,20,22); break;
            case "paperfold": Line(g,p.Dark,15,14,32,31,1); Line(g,p.Light,31,14,16,31,1); break;
            case "sleepmoon": Ellipse(g,p.Outline,p.Accent2,32,8,8,8); Ellipse(g,p.Outline,p.Base,35,7,8,8); break;
            case "dreamcloud": BodyCloud(g,p,1,h); Spark(g,p.Accent2,36,9); break;
            case "startrail": Spark(g,p.Accent2,10,12); Spark(g,p.Accent,38,17); Spark(g,p.Light,34,7); break;
            case "staplebar": Rect(g,p.Outline,p.Accent,14,13,20,4); break;
            case "staplelock": Rect(g,p.Outline,p.Dark,17,11,14,11); FillRect(g,p.Accent2,22,15,4,4); break;
            case "mughandle": Ellipse(g,p.Outline,p.Light,30,18,9,10); break;
            case "steamcurl": Line(g,p.Light,20,9,18,5,1); Line(g,p.Light,25,10,27,5,1); Line(g,p.Light,30,9,30,4,1); break;
            case "notelines": Line(g,p.Dark,17,17,31,17,1); Line(g,p.Dark,17,22,31,22,1); break;
            case "pageears": Poly(g,p,p.Light,14,12,8,17,14,19); Poly(g,p,p.Light,34,12,40,17,34,19); break;
            case "bookwings": Poly(g,p,p.Accent,10,20,2,14,4,31,12,28); Poly(g,p,p.Accent,38,20,46,14,44,31,36,28); break;
            case "inkdrop": Crystal(g,p,24,9,12); break;
            case "inkcape": Poly(g,p,p.Dark,12,22,6,34,19,31); Poly(g,p,p.Dark,36,22,42,34,29,31); break;
            case "clockmark": GearTeeth(g,p,24,18,9); Line(g,p.Accent2,24,18,29,14,1); break;
            case "stonefacet": Line(g,p.Dark,16,16,27,12,1); Line(g,p.Dark,18,28,33,25,1); break;
            case "batwings": Poly(g,p,p.Dark,8,21,2,13,4,30,14,27); Poly(g,p,p.Dark,40,21,46,13,44,30,34,27); break;
            case "lanternglow": Ellipse(g,p.Outline,p.Accent2,20,12,8,10); Spark(g,p.Light,24,17); break;
            case "crystalspikes": Crystal(g,p,16,8,12); Crystal(g,p,24,6,15); Crystal(g,p,32,9,11); break;
            case "prismtail": Crystal(g,p,36,24,12); Spark(g,p.Accent2,39,24); break;
            case "echorings": using(Pen pen=P(p.Accent,1)){ g.DrawArc(pen,6,13,14,14,90,180); g.DrawArc(pen,28,13,14,14,270,180); } break;
            case "coreglow": Ellipse(g,p.Outline,p.Accent2,19,16,10,10); FillRect(g,p.Light,22,19,4,4); break;
            case "chasmeye": FillRect(g,p.Accent2,21,18,6,3); Line(g,p.Outline,16,29,32,29,2); break;
            case "snowcap": FillRect(g,p.Light,14,11,20,5); FillRect(g,p.Light,17,8,14,4); break;
            case "snowcrest": Poly(g,p,p.Light,16,12,24,5,32,12); break;
            case "cragface": Line(g,p.Dark,16,18,21,15,1); Line(g,p.Dark,27,15,32,18,1); break;
            case "cliffhorns": Line(g,p.Outline,15,13,9,8,2); Line(g,p.Outline,33,13,39,8,2); break;
            case "peakcrown": Poly(g,p,p.Light,14,13,20,6,24,13,29,5,35,13); break;
            case "alpinewool": for(int i=0;i<6;i++) Ellipse(g,p.Outline,p.Light,11+i*5,14+(i%2)*3,7,7); break;
            case "alpinebloom": Flower(g,p,24,10); Leaf(g,p,15,18,-1); Leaf(g,p,33,18,1); break;
            case "goathorns": Line(g,p.Accent2,16,13,11,5,2); Line(g,p.Accent2,32,13,37,5,2); break;
            case "galeribbon": Line(g,p.Accent,10,14,38,10,1); Line(g,p.Accent,12,18,36,14,1); break;
            case "aurorahorns": Line(g,p.Accent2,14,13,9,4,2); Line(g,p.Accent,34,13,39,4,2); Spark(g,p.Light,24,7); break;
            case "avalanchesnow": FillRect(g,p.Light,10,27,28,8); Line(g,p.Dark,13,32,35,32,1); break;
            case "micashards": Crystal(g,p,15,10,10); Crystal(g,p,33,12,8); Spark(g,p.Light,35,9); break;
            case "sandhorns": Poly(g,p,p.Dark,14,15,9,9,18,12); Poly(g,p,p.Dark,34,15,39,9,30,12); break;
            case "castletowers": Rect(g,p.Outline,p.Dark,12,10,7,13); Rect(g,p.Outline,p.Dark,29,10,7,13); FillRect(g,p.Accent2,14,9,3,2); FillRect(g,p.Accent2,31,9,3,2); break;
            case "shellhelmet": Ellipse(g,p.Outline,p.Light,13,10,22,13); for(int i=0;i<4;i++) Line(g,p.Dark,18+i*4,12,20+i*2,22,1); break;
            case "shellclaw": Ellipse(g,p.Outline,p.Light,7,25,8,8); Ellipse(g,p.Outline,p.Light,33,25,8,8); break;
            case "foambubbles": for(int i=0;i<6;i++) Ellipse(g,p.Outline,p.Light,10+i*5,10+(i%3)*5,4,4); break;
            case "saltcrystals": Crystal(g,p,18,9,10); Crystal(g,p,30,12,8); break;
            case "brinespines": for(int i=0;i<5;i++) Poly(g,p,p.Accent,14+i*5,16,16+i*5,9,18+i*5,16); break;
            case "kelpears": Leaf(g,p,15,14,-1); Leaf(g,p,33,14,1); break;
            case "kelpmane": for(int i=0;i<5;i++) Leaf(g,p,14+i*5,13,(i%2==0?-1:1)); break;
            case "tidecrown": Poly(g,p,p.Accent,16,13,20,6,24,13,28,6,32,13); break;
            case "cloudpuffs": BodyCloud(g,p,stage,h); break;
            case "ramhorns": using(Pen pen=P(p.Accent2,2)){ g.DrawArc(pen,11,10,10,10,40,280); g.DrawArc(pen,27,10,10,10,220,280); } break;
            case "nimbuscrown": ArcDots(g,p.Accent2,24,18,16,7); break;
            case "balloonstring": Ellipse(g,p.Outline,p.Accent,16,6,16,20); Line(g,p.Outline,24,26,21,36,1); Line(g,p.Outline,24,26,27,36,1); break;
            case "kitebody": Poly(g,p,p.Accent,24,6,34,18,24,31,14,18); break;
            case "kitewings": Poly(g,p,p.Light,11,19,5,13,8,31); Poly(g,p,p.Light,37,19,43,13,40,31); break;
            case "cirruswhiskers": Line(g,p.Light,9,17,20,20,1); Line(g,p.Light,28,20,39,17,1); break;
            case "longcloudtail": Line(g,p.Light,33,29,43,27,2); Line(g,p.Light,37,32,45,34,1); break;
            case "seereye": Ellipse(g,p.Outline,p.Accent2,19,11,10,10); FillRect(g,p.Eye,23,15,2,2); break;
            case "zenithstar": Poly(g,p,p.Accent2,24,5,27,15,37,15,29,21,32,32,24,25,16,32,19,21,11,15,21,15); break;
            case "neonstripes": Line(g,p.Accent,13,15,35,15,1); Line(g,p.Accent2,15,30,33,30,1); break;
            case "neontail": Line(g,p.Accent,33,29,42,25,2); Spark(g,p.Accent2,42,25); break;
            case "railtrack": Line(g,p.Dark,13,16,35,29,2); Line(g,p.Dark,35,16,13,29,2); break;
            case "subwaymask": Rect(g,p.Outline,p.Dark,14,12,20,12); FillRect(g,p.Accent,17,15,5,4); FillRect(g,p.Accent2,26,15,5,4); break;
            case "metrocrown": Rect(g,p.Outline,p.Dark,12,9,24,20); FillRect(g,p.Accent,16,14,6,5); FillRect(g,p.Accent2,26,14,6,5); break;
            case "lightningbolt": Poly(g,p,p.Accent2,27,7,19,24,25,23,21,37,34,18,28,19); break;
            case "powerlinetail": Line(g,p.Outline,32,28,43,20,2); FillRect(g,p.Accent,40,18,5,3); break;
            case "metrowindow": Rect(g,p.Outline,p.Dark,12,11,24,19); FillRect(g,p.Accent,16,15,6,6); FillRect(g,p.Accent2,26,15,6,6); break;
            case "glyphmark": FillRect(g,p.Accent,21,15,6,2); FillRect(g,p.Accent,24,15,2,9); break;
            case "glyphhalo": using(Pen pen=P(p.Accent,1)){ g.DrawEllipse(pen,12,8,24,16); } break;
            case "obelisktop": Poly(g,p,p.Light,18,14,24,5,30,14); break;
            case "obeliskcrown": Poly(g,p,p.Accent2,14,14,20,6,24,14,29,5,35,14); break;
            case "monolithface": Rect(g,p.Outline,p.Dark,14,8,20,28); FillRect(g,p.Accent,18,17,4,3); FillRect(g,p.Accent,27,17,4,3); break;
            case "sundisk": Ellipse(g,p.Outline,p.Accent2,13,8,22,22); Spark(g,p.Light,24,19); break;
            case "sealring": using(Pen pen=P(p.Accent,2)){ g.DrawEllipse(pen,12,10,24,24); } break;
            case "sealhorns": Line(g,p.Accent2,16,13,10,7,2); Line(g,p.Accent2,32,13,38,7,2); break;
            case "magnetends": Rect(g,p.Outline,p.Dark,14,10,20,22); FillRect(g,p.Accent,14,27,5,5); FillRect(g,p.Accent2,29,27,5,5); break;
            case "gearring": GearTeeth(g,p,24,20,13); break;
            case "cogteeth": GearTeeth(g,p,24,18,10); break;
            case "machinecrown": Rect(g,p.Outline,p.Dark,15,9,18,8); FillRect(g,p.Accent,18,11,4,4); FillRect(g,p.Accent2,27,11,4,4); break;
            case "pixelbits": for(int i=0;i<8;i++) FillRect(g,(i%2==0?p.Accent:p.Accent2),9+(i*5+h)%30,8+(i*7+h)%28,3,3); break;
            case "clockcore": GearTeeth(g,p,24,20,12); Line(g,p.Accent2,24,20,24,12,1); Line(g,p.Accent2,24,20,31,24,1); break;
            case "pillowears": Poly(g,p,p.Light,15,13,10,8,18,10); Poly(g,p,p.Light,33,13,38,8,30,10); break;
            case "pillowbow": FillRect(g,p.Accent,20,12,8,4); Poly(g,p,p.Accent,20,14,14,11,14,17); Poly(g,p,p.Accent,28,14,34,11,34,17); break;
            case "sheepwool": for(int i=0;i<7;i++) Ellipse(g,p.Outline,p.Light,10+i*4,12+(i%2)*3,7,7); break;
            case "sheepplume": ArcDots(g,p.Light,24,20,15,8); Spark(g,p.Accent2,24,8); break;
            case "flockcloud": BodyCloud(g,p,2,h); ArcDots(g,p.Accent2,24,17,17,5); break;
            case "pillowstar": Poly(g,p,p.Accent2,24,6,27,15,36,15,29,20,32,30,24,24,16,30,19,20,12,15,21,15); break;
            case "raindot": Crystal(g,p,24,8,9); FillRect(g,p.Accent,17,32,14,2); break;
            case "rainboots": FillRect(g,p.Dark,16,34,6,5); FillRect(g,p.Dark,27,34,6,5); break;
            case "thunderdrum": Ellipse(g,p.Outline,p.Dark,14,12,20,18); Line(g,p.Accent2,15,15,33,28,2); break;
            case "stormcloud": BodyCloud(g,p,2,h); Poly(g,p,p.Accent2,27,22,20,34,26,33,23,42,35,27,29,28); break;
            case "tempesthorns": Line(g,p.Accent2,15,13,9,5,2); Line(g,p.Accent,33,13,39,5,2); Poly(g,p,p.Accent2,27,20,20,33,26,32,23,40,34,26,29,27); break;
            case "rainbowarc": Line(g,C(246,84,107),9,16,39,16,1); Line(g,C(246,207,72),9,19,39,19,1); Line(g,C(84,207,114),9,22,39,22,1); Line(g,C(84,158,236),9,25,39,25,1); break;
            case "lighthalo": Ellipse(g,p.Outline,p.Light,15,8,18,22); for(int i=0;i<8;i++){ double a=i*Math.PI/4; Line(g,p.Accent2,24,20,24+(int)(Math.Cos(a)*17),20+(int)(Math.Sin(a)*17),1); } break;
            case "prismstar": Poly(g,p,p.Light,24,5,28,17,40,18,30,25,33,38,24,30,15,38,18,25,8,18,20,17); break;
            case "cursorarrow": Poly(g,p,p.Light,15,7,35,25,26,27,31,38,25,40,20,29,14,35); break;
            case "cursorwings": Poly(g,p,p.Accent,9,22,2,14,5,33,15,29); Poly(g,p,p.Accent,39,22,46,14,43,33,33,29); break;
            case "screenportal": Rect(g,p.Outline,p.Dark,9,12,30,20); FillRect(g,p.Accent,13,16,8,6); FillRect(g,p.Accent2,27,20,7,5); Line(g,p.Light,9,10,39,36,1); break;
            case "clockhands": GearTeeth(g,p,24,21,13); Line(g,p.Accent2,24,21,24,11,1); Line(g,p.Accent2,24,21,33,24,1); break;
            case "wishfold": Poly(g,p,p.Light,24,7,32,18,28,35,17,35,15,18); Line(g,p.Accent,18,16,30,28,1); Spark(g,p.Accent2,35,9); break;
        }
    }

    static void ApplyProfile(Graphics g, Pal p, string profile, int stage, int h) {
        if (String.IsNullOrWhiteSpace(profile)) return;
        foreach (string token in profile.Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries)) {
            ProfileToken(g, p, token, stage, h);
        }
    }

    public static void Draw(string outPath, string id, int stage, int totalStages, string field, string rarity, string prestige, string profile) {
        int h = Hash(id + field + rarity + prestige);
        Pal p = Palette(field, rarity, prestige);
        if (stage < 1) stage = 1;
        if (stage > 3) stage = 3;
        using (Bitmap bmp = new Bitmap(48, 48, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            DrawBase(g, p, id, field, stage, h);
            Decor(g, p, id, field, rarity, prestige, stage, h);
            ApplyProfile(g, p, profile, stage, h);
            UniqueMarks(g, p, h);

            using (Pen outline = P(p.Outline, 1)) {
                g.DrawLine(outline, 18, 41, 30, 41);
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

if (-not (Test-Path -LiteralPath $DocsPath)) {
    $docCandidate = Get-ChildItem -LiteralPath "Docs" -Filter "151*.md" -File | Select-Object -First 1
    if ($null -ne $docCandidate) {
        $DocsPath = $docCandidate.FullName
    }
}

if (-not (Test-Path -LiteralPath $DocsPath)) {
    throw "Missing docs file: $DocsPath"
}
if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$rows = New-Object System.Collections.Generic.List[object]
$inside = $false
foreach ($line in Get-Content -LiteralPath $DocsPath -Encoding UTF8) {
    if ($line -match '^## 17\.') {
        $inside = $true
        continue
    }
    if ($inside -and $line -match '^## 18\.') {
        break
    }
    if (-not $inside) {
        continue
    }
    if ($line -match '^\|\s*(\d{3})\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*(\d+)\/(\d+)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|') {
        $rows.Add([pscustomobject]@{
            No = $Matches[1]
            Id = $Matches[2].Trim()
            Stage = [int]$Matches[5]
            TotalStages = [int]$Matches[6]
            Field = $Matches[7].Trim()
            Rarity = $Matches[8].Trim()
            Prestige = $Matches[9].Trim()
        }) | Out-Null
    }
}

if ($rows.Count -ne 151) {
    throw "Expected 151 dex rows from canonical section, got $($rows.Count)."
}

$DesignProfiles = @{
    mongle = "sproutback"
    leafmong = "sproutback,mintscarf"
    bloomong = "gardenback,lotuscrown"
    florin = "petalhair"
    floravia = "petalwings"
    buddle = "seedshell"
    budback = "sproutback,seedshell"
    gardobud = "gardenback,seedshell"
    clovey = "cloverears"
    cloverin = "cloverears,clovertail"
    sunpuff = "dandelion"
    tangrow = "vinetail"
    tangcurl = "spiralvine,vinetail"
    vinecrest = "vinecrest,spiralvine"
    mintot = "mintscarf"
    mintail = "minttail,mintscarf"
    acornet = "acornhelm"
    vernalorn = "seedantlers,rootbeard"
    dotori = "acornhelm,oakbranch"
    dotol = "acornhelm,sproutback"
    oakwarden = "oakbranch,rootbeard"
    mushjong = "mushcap"
    clowncap = "mushcap,sporedots"
    owloon = "owlbrows"
    sageowl = "owlbrows,starwings"
    mossmole = "mosscoat"
    mossburrow = "mosscoat,burrowclaws"
    twigimp = "twighorns"
    twigrin = "twighorns,vinetail"
    branchimp = "branchstaff,twighorns"
    saplynx = "sapdrops"
    amberlynx = "ambergems"
    bramblet = "thorncrown"
    bramblebloom = "thorncrown,rosebloom"
    thornbriar = "thorncrown,branchstaff,rosebloom"
    elderoot = "rootbeard,oakbranch"
    dewdrop = "waterdrop"
    dewcrown = "dewcrown"
    mossy = "shoremoss"
    mossgolem = "shoremoss,stonearms"
    ripplefin = "ripplecrown"
    wavefin = "wavefins,ripplecrown"
    broadfin = "broadfins,wavefins"
    shellume = "shellridges"
    pearlshell = "shellridges,pearlcore"
    foggup = "fogwisps"
    lilypad = "lilypad"
    lilihop = "lilypad,vinetail"
    lotusprince = "lotuscrown,shellridges"
    drizzlet = "raindrops"
    rainbub = "bubblecrown,raindrops"
    moonwale = "mooncrest,wavefins"
    origami = "paperfold"
    dozy = "sleepmoon"
    dreami = "dreamcloud,sleepmoon"
    reverie = "startrail,dreamcloud"
    staplit = "staplebar"
    staplock = "staplelock"
    cuppa = "mughandle"
    steamug = "mughandle,steamcurl"
    noteling = "notelines"
    pageling = "pageears,notelines"
    bookwing = "bookwings,notelines"
    inkwick = "inkdrop"
    inkveil = "inkcape,inkdrop"
    deadliner = "clockmark"
    pebblit = "stonefacet"
    cobblor = "stonefacet,stonearms"
    boulderon = "stonefacet,cliffhorns"
    battern = "batwings"
    lanternbat = "batwings,lanternglow"
    crystail = "crystalspikes"
    crystalisk = "crystalspikes,prismtail"
    prismtail = "prismtail,crystalspikes,rainbowarc"
    echopup = "echorings"
    echound = "echorings,batwings"
    corewyrm = "coreglow"
    chasmite = "chasmeye"
    snowpip = "snowcap"
    snowcrest = "snowcrest,snowcap"
    craggo = "cragface"
    cragrin = "cragface,cliffhorns"
    craglord = "peakcrown,cliffhorns"
    alpuff = "alpinewool"
    alploom = "alpinewool,alpinebloom"
    windgoat = "goathorns"
    galehorn = "goathorns,galeribbon"
    aurorhorn = "aurorahorns"
    avalamb = "avalanchesnow"
    micafox = "micashards"
    sandimp = "sandhorns"
    sandcastle = "castletowers"
    shellbit = "shellhelmet"
    shellclaw = "shellhelmet,shellclaw"
    foamlet = "foambubbles"
    saltfin = "saltcrystals"
    brinefin = "saltcrystals,brinespines"
    kelpup = "kelpears"
    kelphound = "kelpmane,kelpears"
    tidelord = "tidecrown,mooncrest"
    cloudle = "cloudpuffs"
    cloudram = "cloudpuffs,ramhorns"
    nimburel = "cloudpuffs,nimbuscrown"
    ballooni = "balloonstring"
    kitet = "kitebody"
    kitewing = "kitebody,kitewings"
    cirruskit = "cirruswhiskers"
    cirrustail = "cirruswhiskers,longcloudtail"
    cirruseer = "cirruswhiskers,seereye"
    zenithra = "zenithstar"
    neonkit = "neonstripes"
    neonstray = "neonstripes,neontail"
    railrat = "railtrack"
    railskur = "railtrack,subwaymask"
    metrorat = "metrocrown,railtrack"
    voltpup = "lightningbolt"
    volthound = "lightningbolt,powerlinetail"
    metrolith = "metrowindow"
    glyphlet = "glyphmark"
    glyphora = "glyphmark,glyphhalo"
    obeliskid = "obelisktop"
    obeliskar = "obelisktop,obeliskcrown"
    monolithon = "monolithface,obeliskcrown"
    sunidol = "sundisk"
    arkseal = "sealring,sealhorns"
    sealimp = "sealring,glyphmark"
    magnetot = "magnetends"
    magnetron = "magnetends,gearring"
    cogbit = "cogteeth"
    cogring = "cogteeth,gearring"
    gearlord = "machinecrown,gearring"
    pixelmote = "pixelbits"
    chronocore = "clockcore"
    dozbun = "pillowears"
    pillowbun = "pillowears,pillowbow"
    sheepuff = "sheepwool"
    sheeplume = "sheepwool,sheepplume"
    dreamflock = "flockcloud,sheepplume"
    pillowstar = "pillowstar"
    raindot = "raindot"
    rainstep = "raindot,rainboots"
    thundrum = "thunderdrum"
    stormdrum = "thunderdrum,stormcloud"
    tempestrum = "tempesthorns,stormcloud"
    prismtempest = "rainbowarc,tempesthorns"
    lumi = "lighthalo"
    cursorbit = "cursorarrow"
    cursorwing = "cursorarrow,cursorwings"
    deskron = "screenportal"
    chrono = "clockhands"
    wishpaper = "wishfold"
}

$missingProfiles = @($rows | Where-Object { -not $DesignProfiles.ContainsKey($_.Id) } | ForEach-Object { $_.Id })
if ($missingProfiles.Count -gt 0) {
    throw "Missing monster design profiles: $($missingProfiles -join ', ')"
}

$duplicateProfiles = @($DesignProfiles.GetEnumerator() | Group-Object Value | Where-Object Count -gt 1)
if ($duplicateProfiles.Count -gt 0) {
    $duplicateText = @($duplicateProfiles | ForEach-Object { ($_.Group.Name -join '/') + '=' + $_.Name }) -join ', '
    throw "Duplicate monster design profiles: $duplicateText"
}

foreach ($row in $rows) {
    $outPath = Join-Path $OutputDir ("{0}_stage{1}_ai_v1.png" -f $row.Id, $row.Stage)
    [MonsterSpritePainter]::Draw($outPath, $row.Id, $row.Stage, $row.TotalStages, $row.Field, $row.Rarity, $row.Prestige, $DesignProfiles[$row.Id])
}

if ($KeepMongleCanonical) {
    $copies = @(
        @("mongle.png", "mongle_stage1_ai_v1.png"),
        @("mongle_stage2.png", "leafmong_stage2_ai_v1.png"),
        @("mongle_stage3.png", "bloomong_stage3_ai_v1.png")
    )
    foreach ($copy in $copies) {
        $src = Join-Path $OutputDir $copy[0]
        $dst = Join-Path $OutputDir $copy[1]
        if (Test-Path -LiteralPath $src) {
            Copy-Item -LiteralPath $src -Destination $dst -Force
        }
    }
}

"Generated $($rows.Count) monster sprite candidates in $OutputDir"
