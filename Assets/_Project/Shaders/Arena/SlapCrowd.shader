// Procedural crowd impostor (M15, ADR-0012). One strip per stand row; u counts people
// (1 unit = one 0.5 m seat), v runs 0..1 up a 1 m tall strip, uv2.x is the row index.
// Every person is a signed-distance silhouette (head, hair, shoulders; some standing, some
// with an arm up), jittered off the seat grid and overlapping their neighbours, so the bowl
// never reads as rows of icons. Lit the way the reference is: dark clothing, faces picked
// out by the arena wash (red near the ring, violet up high), a faint cool highlight on the
// tops of heads, phone flashes twinkling. Opaque, no textures, one draw call per bowl.
Shader "SlapMania/Arena/Crowd"
{
    Properties
    {
        _ClothDark   ("Clothing (dark)", Color) = (0.03, 0.03, 0.04, 1)
        _ClothLight  ("Clothing (light)", Color) = (0.10, 0.09, 0.11, 1)
        _SkinDark    ("Skin (dark)", Color) = (0.05, 0.028, 0.018, 1)
        _SkinLight   ("Skin (light)", Color) = (0.34, 0.21, 0.15, 1)
        _Hair        ("Hair", Color) = (0.012, 0.01, 0.01, 1)
        _Seat        ("Seats behind", Color) = (0.010, 0.008, 0.012, 1)
        [HDR] _WashLow  ("Wash near the ring", Color) = (0.55, 0.06, 0.07, 1)
        [HDR] _WashHigh ("Wash up high", Color) = (0.10, 0.05, 0.16, 1)
        _WashHeight  ("Wash height range (m)", Float) = 12
        _Ambient     ("Ambient", Range(0, 1)) = 0.2
        _Rim         ("Top highlight", Range(0, 3)) = 0.35
        [HDR] _RimColor ("Top highlight colour", Color) = (0.55, 0.62, 0.9, 1)
        _Jitter      ("Seat jitter (m)", Range(0, 0.25)) = 0.13
        _Bob         ("Bob (m)", Range(0, 0.2)) = 0.035
        _BobSpeed    ("Bob speed", Range(0, 8)) = 2.2
        _Standing    ("Standing share", Range(0, 1)) = 0.35
        _ArmsUp      ("Arm-up share", Range(0, 1)) = 0.12
        _Empty       ("Empty seats", Range(0, 0.5)) = 0.05
        _FlashShare  ("Phone flash share", Range(0, 0.5)) = 0.07
        _FlashSpeed  ("Phone flash speed", Range(0, 3)) = 0.45
        [HDR] _FlashColor ("Phone flash", Color) = (3.5, 3.8, 4.5, 1)
        _ScreenShare ("Phone screen share", Range(0, 0.3)) = 0.05
        [HDR] _ScreenColor ("Phone screen", Color) = (0.25, 0.35, 0.65, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Crowd"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ClothDark, _ClothLight, _SkinDark, _SkinLight, _Hair, _Seat, _WashLow, _WashHigh, _RimColor, _FlashColor, _ScreenColor;
                float _WashHeight, _Ambient, _Rim, _Jitter, _Bob, _BobSpeed, _Standing, _ArmsUp, _Empty;
                float _FlashShare, _FlashSpeed, _ScreenShare;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float row : TEXCOORD1; float worldY : TEXCOORD2; float fog : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.row = v.uv2.x;
                o.worldY = ws.y;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Integer hash (no sin: large sin arguments lose precision on mobile GPUs).
            float Rand(uint seed, uint k)
            {
                uint h = seed + k * 0x9E3779B9u;
                h = (h ^ 61u) ^ (h >> 16u);
                h *= 9u;
                h ^= h >> 4u;
                h *= 0x27d4eb2du;
                h ^= h >> 15u;
                return (h & 0x00FFFFFFu) / 16777215.0;
            }

            float SdRoundBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float SdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - r;
            }

            uint SeedOf(float cell, float row)
            {
                return (uint)(cell + 8192.0) * 747796405u + (uint)(row + 1.0) * 2891336453u;
            }

            // Composites the person sitting in `cell` over `col`. `p` is metres: x from the
            // strip's cell-0 origin, y up the strip. Returns the person's head top (for flashes).
            float DrawPerson(float cell, float row, float2 p, float3 wash, inout float3 col)
            {
                uint seed = SeedOf(cell, row);
                if (Rand(seed, 1u) < _Empty) return -1.0;

                bool standing = Rand(seed, 2u) < _Standing;
                float bob = sin(_Time.y * _BobSpeed * lerp(0.6, 1.4, Rand(seed, 3u)) + Rand(seed, 4u) * 6.2831853)
                          * _Bob * (standing ? 1.0 : 0.45);
                float top = (standing ? lerp(0.88, 0.99, Rand(seed, 5u)) : lerp(0.58, 0.74, Rand(seed, 5u))) + bob;
                float cx = (cell + 0.5) * 0.5 + (Rand(seed, 6u) - 0.5) * 2.0 * _Jitter;
                float2 q = float2(p.x - cx, p.y);

                float headR = lerp(0.082, 0.108, Rand(seed, 7u));
                float2 headC = float2((Rand(seed, 16u) - 0.5) * 0.04, top - headR);
                float dHead = length((q - headC) * float2(1.0, 0.9)) - headR;
                float shoulderY = top - 2.2 * headR;
                float bodyHalfW = lerp(0.15, 0.22, Rand(seed, 8u));
                float dBody = SdRoundBox(q - float2(0.0, shoulderY * 0.5), float2(bodyHalfW, shoulderY * 0.5), 0.075);
                float dNeck = SdRoundBox(q - float2(headC.x * 0.5, shoulderY + 0.02), float2(0.045, 0.05), 0.02);
                float d = min(min(dHead, dBody), dNeck);
                float dArm = 1e3;
                if (Rand(seed, 9u) < _ArmsUp)
                {
                    float side = Rand(seed, 10u) < 0.5 ? -1.0 : 1.0;
                    float2 a = float2(side * bodyHalfW * 0.75, shoulderY - 0.02);
                    float2 b = float2(side * (bodyHalfW + 0.05), top + lerp(0.12, 0.24, Rand(seed, 17u)) + bob);
                    dArm = SdCapsule(q, a, b, 0.04);
                    d = min(d, dArm);
                }

                float aa = max(fwidth(d), 1e-4);
                float inside = 1.0 - smoothstep(-aa, aa, d);
                if (inside <= 0.0) return top;

                // Albedo: clothing, skin on the face and a raised arm, hair on the crown.
                float3 cloth = lerp(_ClothDark.rgb, _ClothLight.rgb, Rand(seed, 11u));
                float3 skin = lerp(_SkinDark.rgb, _SkinLight.rgb, Rand(seed, 18u));
                float onHead = 1.0 - smoothstep(-aa, aa, dHead);
                float onArm = 1.0 - smoothstep(-aa, aa, dArm);
                float hairLine = headC.y + headR * lerp(0.1, 0.55, Rand(seed, 19u));
                float3 albedo = cloth;
                albedo = lerp(albedo, skin, max(onHead, onArm * 0.8));
                albedo = lerp(albedo, _Hair.rgb, onHead * smoothstep(hairLine - 0.01, hairLine + 0.01, q.y));

                // Front-lit by the wash, darker towards the feet; a cool highlight on the crown.
                float lift = saturate((q.y - shoulderY + 0.25) * 2.5);
                float3 lit = albedo * (_Ambient + wash * (0.6 + 1.2 * lift));
                float crown = smoothstep(-0.03, 0.0, d) * saturate((q.y - (top - 0.07)) * 14.0);
                lit += _RimColor.rgb * crown * _Rim;

                col = lerp(col, lit, inside);
                return top;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;
                float2 p = float2(u * 0.5, i.uv.y);
                float wh = saturate(i.worldY / _WashHeight);
                float3 wash = lerp(_WashLow.rgb, _WashHigh.rgb, wh);
                float3 col = _Seat.rgb + wash * 0.03;

                // Neighbours overlap. Even cells stand in front of odd ones, so every pixel
                // agrees on who occludes whom: draw the odd cells first, the even ones on top.
                float c = floor(u);
                bool even = fmod(c, 2.0) < 0.5;
                float topC = -1.0;
                if (even)
                {
                    DrawPerson(c - 1.0, i.row, p, wash, col);
                    DrawPerson(c + 1.0, i.row, p, wash, col);
                    topC = DrawPerson(c, i.row, p, wash, col);
                }
                else
                {
                    topC = DrawPerson(c, i.row, p, wash, col);
                    DrawPerson(c - 1.0, i.row, p, wash, col);
                    DrawPerson(c + 1.0, i.row, p, wash, col);
                }

                uint seed = SeedOf(c, i.row);
                float r = Rand(seed, 12u);
                if (topC > 0.0 && r < _FlashShare)
                {
                    float2 pc = float2((c + 0.5) * 0.5 + (Rand(seed, 13u) - 0.5) * 0.25, topC + 0.1);
                    float t = frac(_Time.y * _FlashSpeed * lerp(0.6, 1.4, Rand(seed, 14u)) + Rand(seed, 15u));
                    float on = smoothstep(0.86, 0.9, t) * (1.0 - smoothstep(0.93, 0.98, t));
                    float glow = exp(-length(p - pc) * 55.0);
                    col += _FlashColor.rgb * glow * (on + 0.04);
                }
                else if (topC > 0.0 && r < _FlashShare + _ScreenShare)
                {
                    float2 pc = float2((c + 0.5) * 0.5 + (Rand(seed, 13u) - 0.5) * 0.2, topC + 0.08);
                    float scr = 1.0 - smoothstep(0.0, 0.01, SdRoundBox(p - pc, float2(0.03, 0.05), 0.01));
                    col += _ScreenColor.rgb * scr;
                }

                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
