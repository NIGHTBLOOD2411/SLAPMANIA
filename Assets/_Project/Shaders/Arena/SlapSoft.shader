// Soft additive glow (M15, ADR-0012): lamp lenses, the warm light pool under the key light,
// and — with noise on — drifting haze sheets. Radial falloff from the quad centre; optional
// scrolling value noise; near-camera fade so a big haze sheet never fills the lens.
Shader "SlapMania/Arena/Soft"
{
    Properties
    {
        [HDR] _Color     ("Colour", Color) = (1, 1, 1, 1)
        _Intensity       ("Intensity", Range(0, 20)) = 1
        _Power           ("Radial falloff", Range(0.2, 8)) = 2
        _Noise           ("Noise", Range(0, 1)) = 0
        _NoiseScale      ("Noise scale", Range(0.1, 20)) = 3
        _NoiseSpeed      ("Noise speed", Range(0, 1)) = 0.05
        _NearFade        ("Near fade (m)", Range(0, 10)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Soft"
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
                float _Intensity, _Power, _Noise, _NoiseScale, _NoiseSpeed, _NearFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash2(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float Noise2(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x),
                            lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);
                float a = pow(saturate(1.0 - r), _Power);
                if (_Noise > 0.0)
                {
                    float2 q = i.uv * _NoiseScale + _Time.y * _NoiseSpeed * float2(1.0, 0.37);
                    float n = Noise2(q) * 0.6 + Noise2(q * 2.1 + 7.3) * 0.4;
                    a *= lerp(1.0, n * 1.8, _Noise);
                }
                if (_NearFade > 0.0)
                    a *= saturate(length(i.positionWS - GetCameraPositionWS()) / _NearFade - 0.5);
                float3 col = _Color.rgb * (_Intensity * a);
                col = MixFogColor(col, half3(0, 0, 0), i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
