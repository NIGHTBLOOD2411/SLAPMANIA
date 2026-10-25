// Presents a UI Toolkit panel that was rendered in gamma space (PanelSettings.forceGammaRendering
// into a linear-format RenderTexture) on this Linear-colour-space project's screen (M10, ADR-0013).
//
// The texture holds sRGB-encoded values stored raw. Decoding them here, once, lets the sRGB
// framebuffer re-encode the exact same values - so translucent UI blends the way the browser
// mockups did (in sRGB), not the way a Linear project blends (in linear light, visibly lighter).
Shader "SlapMania/UI/GammaComposite"
{
    Properties
    {
        _MainTex ("Panel", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                c.rgb = GammaToLinearSpace(c.rgb);
                #endif
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
