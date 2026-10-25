// LED display surface (M17, ADR-0012): the sponsor ribbon, the perimeter boards and the
// jumbotron. Emissive (HDR, so bloom picks it up), optional horizontal scroll, and a soft
// dot-matrix mask that reads as LEDs close up and disappears with distance.
Shader "SlapMania/Arena/Led"
{
    Properties
    {
        _MainTex        ("Image", 2D) = "white" {}
        [HDR] _Tint     ("Tint", Color) = (1, 1, 1, 1)
        _Intensity      ("Intensity", Range(0, 8)) = 1.6
        _Scroll         ("Scroll (uv / s)", Float) = 0
        _GridX          ("LED dots across", Float) = 900
        _GridY          ("LED dots up", Float) = 24
        _GridStrength   ("Dot-matrix strength", Range(0, 1)) = 0.35
        _Floor          ("Black level", Range(0, 0.2)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Led"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                float _Intensity, _Scroll, _GridX, _GridY, _GridStrength, _Floor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 raw : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + float2(_Time.y * _Scroll, 0.0);
                o.raw = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float2 g = frac(i.raw * float2(_GridX, _GridY)) - 0.5;
                float dots = 1.0 - smoothstep(0.25, 0.5, length(g));
                // Fade the grid out where one dot is smaller than a pixel, so it never moires.
                float px = max(fwidth(i.raw.x * _GridX), fwidth(i.raw.y * _GridY));
                float grid = lerp(1.0, dots * 1.35, _GridStrength * saturate(1.5 - px));
                half3 col = (c * grid + _Floor) * _Tint.rgb * _Intensity;
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
