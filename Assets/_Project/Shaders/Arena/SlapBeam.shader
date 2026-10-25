// Fake volumetric light beam (M15, ADR-0012): an open cone mesh, additive, double-sided.
// Bright where the cone faces the camera, soft at its silhouette, fading along its length,
// with slow 3D value noise for drifting haze dust. Replaces raymarched volumetrics, which a
// portrait-mobile frame cannot afford. The cone mesh: apex at the origin, base at z = 1,
// uv.y = 0 at the apex and 1 at the base.
Shader "SlapMania/Arena/Beam"
{
    Properties
    {
        [HDR] _Color     ("Colour", Color) = (1, 0.95, 0.9, 1)
        _Intensity       ("Intensity", Range(0, 5)) = 0.6
        _EdgeSoftness    ("Edge softness", Range(0.5, 8)) = 2.5
        _Falloff         ("Length falloff", Range(0.2, 4)) = 1.3
        _StartFade       ("Fade-in near the lamp", Range(0.001, 0.3)) = 0.04
        _Dust            ("Dust", Range(0, 1)) = 0.35
        _DustScale       ("Dust scale", Range(0.05, 4)) = 0.6
        _DustSpeed       ("Dust speed", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _EdgeSoftness, _Falloff, _StartFade, _Dust, _DustScale, _DustSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float fog : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 N = normalize(i.normalWS);
                float edge = pow(saturate(abs(dot(N, V))), _EdgeSoftness);
                float along = saturate(i.uv.y);
                float fall = pow(1.0 - along, _Falloff) * smoothstep(0.0, _StartFade, along);
                float3 q = i.positionWS * _DustScale + float3(0.0, -_Time.y * _DustSpeed, _Time.y * _DustSpeed * 0.5);
                float dust = Noise3(q) * 0.65 + Noise3(q * 2.3) * 0.35;
                float3 col = _Color.rgb * (_Intensity * edge * fall * lerp(1.0, dust * 1.7, _Dust));
                col = MixFogColor(col, half3(0, 0, 0), i.fog); // additive: fade out, don't tint
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
