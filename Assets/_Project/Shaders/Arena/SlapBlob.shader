// Blob contact shadow (M15, ADR-0012): a multiplicative radial darkening on the ring floor
// under each fighter and the podium. Mobile additional lights cast no shadows
// (Mobile_RPAsset), so this is what grounds the fighters under the key light.
Shader "SlapMania/Arena/Blob"
{
    Properties
    {
        _Darkness ("Darkness at centre", Range(0, 1)) = 0.25
        _Power    ("Falloff", Range(0.2, 6)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Blob"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Darkness, _Power;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);
                float a = pow(saturate(1.0 - r), _Power);
                return half4(lerp(1.0, _Darkness, a).xxx, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
