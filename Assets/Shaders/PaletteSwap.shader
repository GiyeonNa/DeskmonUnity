// 샤이니 팔레트 스왑 (포팅계획 §3.2 / §3.4)
//
// 기획: 27폼 전부에 샤이니 스프라이트를 따로 그리면 아트량이 2배가 된다.
// 그래서 스프라이트는 한 장만 그리고, 색만 런타임에 바꿔 샤이니를 만든다.
//
// 방식 - 색조 회전(hue rotation)이 아니라 "기준색 -> 목표색" 이동:
//   도트 한 장은 기본색의 명암 변주(아웃라인/음영/하이라이트)로 이루어져 있다.
//   픽셀별로 기본색과의 색조 거리를 재서, 가까운 픽셀일수록 목표색 쪽으로 옮긴다.
//   명도(V)와 채도 관계는 유지하므로 아웃라인과 음영 구조가 그대로 살아남는다.
//   눈의 흰자/검은자처럼 무채색인 픽셀은 채도가 낮아 자동으로 보존된다.
//
// data.js가 종별로 color/shiny 쌍을 이미 갖고 있어(_BaseHue/_TargetHue) 그대로 꽂으면 된다.
Shader "Deskmon/PaletteSwap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BaseColor   ("기준색 (data.js color)", Color) = (1,1,1,1)
        _TargetColor ("목표색 (data.js shiny)", Color) = (1,1,1,1)

        // 0이면 원본 그대로, 1이면 완전히 목표색으로. 포획 연출에서 0->1로 올리면 변신처럼 보인다.
        _Swap ("스왑 강도", Range(0,1)) = 0

        // 기준색에서 이 색조 거리(0~1) 안쪽만 바꾼다. 넓히면 눈/무채색까지 물들 수 있다.
        _HueRange ("색조 허용 범위", Range(0.01,1)) = 0.25

        // 전설(루미/크로노)용 무지개. data.js rainbow.
        _Rainbow ("무지개 강도", Range(0,1)) = 0
        _RainbowSpeed ("무지개 속도", Float) = 0.35

        // 데스크탑 배경 위에서 묻히지 않게 하는 아웃라인 (포팅계획 §3.5)
        _OutlineColor ("아웃라인 색", Color) = (0,0,0,0.55)
        _OutlineWidth ("아웃라인 두께 (px)", Range(0,4)) = 0

        // 스프라이트 렌더러 표준 프로퍼티 (없으면 SpriteRenderer가 경고를 낸다)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // 스프라이트 표준 - 미리 곱해진 알파

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _RendererColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _BaseColor;
            fixed4 _TargetColor;
            float _Swap;
            float _HueRange;
            float _Rainbow;
            float _RainbowSpeed;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            // ── RGB <-> HSV ──
            // 분기 없는 표준 구현. 픽셀 수가 적어도 셰이더에서 분기는 피한다.
            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // 색조는 원형이라 0.95와 0.05는 0.1만큼 떨어져 있다. 단순 뺄셈이면 0.9로 잘못 나온다.
            float HueDistance(float a, float b)
            {
                float d = abs(a - b);
                return min(d, 1.0 - d);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // ── 팔레트 스왑 ──
                // 알파가 0인 픽셀은 건드려도 보이지 않으므로 계산만 낭비다. 하지만 분기 대신
                // 곱셈으로 처리해 워프 발산을 피한다 (아래 최종 곱에서 알파가 0이면 전부 0).
                float3 hsv     = RGBtoHSV(tex.rgb);
                float3 baseHSV = RGBtoHSV(_BaseColor.rgb);
                float3 tgtHSV  = RGBtoHSV(_TargetColor.rgb);

                // 기준색과 색조가 가까울수록 1. 채도가 낮은 픽셀(눈 흰자/검은자, 회색 아웃라인)은
                // 색조 자체가 불안정하므로 채도로 한 번 더 눌러 보호한다.
                float hueMask = 1.0 - smoothstep(0.0, _HueRange, HueDistance(hsv.x, baseHSV.x));
                float satMask = smoothstep(0.05, 0.25, hsv.y);
                float mask = hueMask * satMask * _Swap;

                // 색조는 목표색으로 옮기고, 채도는 기준색 대비 비율을 유지한다.
                // 명도(V)는 손대지 않는다 - 도트의 명암 구조가 여기 들어 있다.
                float satRatio = tgtHSV.y / max(baseHSV.y, 1.0e-4);
                float3 swapped = float3(tgtHSV.x, saturate(hsv.y * satRatio), hsv.z);

                // 무지개: 색조를 시간에 따라 돌린다. 전설 종 연출용.
                float rainbowHue = frac(hsv.x + _Time.y * _RainbowSpeed);
                swapped.x = lerp(swapped.x, rainbowHue, _Rainbow);
                float rainbowMask = satMask * _Rainbow;

                float3 outRGB = lerp(tex.rgb, HSVtoRGB(swapped), saturate(mask + rainbowMask));

                fixed4 c = fixed4(outRGB, tex.a) * IN.color;

                // ── 아웃라인 ──
                // 배경이 사용자 바탕화면이라 무엇이 깔릴지 알 수 없다. 스프라이트가 묻히지 않게
                // 이웃 4방향에 알파가 있으면 테두리를 그린다. 두께 0이면 비용도 0에 가깝다.
                if (_OutlineWidth > 0.0 && tex.a < 0.95)
                {
                    float2 o = _MainTex_TexelSize.xy * _OutlineWidth;
                    float neighbor =
                          tex2D(_MainTex, IN.texcoord + float2( o.x, 0)).a
                        + tex2D(_MainTex, IN.texcoord + float2(-o.x, 0)).a
                        + tex2D(_MainTex, IN.texcoord + float2(0,  o.y)).a
                        + tex2D(_MainTex, IN.texcoord + float2(0, -o.y)).a;

                    float edge = saturate(neighbor) * (1.0 - tex.a) * _OutlineColor.a * IN.color.a;
                    c.rgb = lerp(c.rgb, _OutlineColor.rgb * IN.color.a, edge);
                    c.a = saturate(c.a + edge);
                }

                // Blend One OneMinusSrcAlpha 전제 - 색을 알파와 미리 곱한다.
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
