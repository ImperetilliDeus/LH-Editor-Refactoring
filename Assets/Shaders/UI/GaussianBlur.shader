Shader "UI/GaussianBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 8)) = 2
        _BlurMix ("Blur Mix", Range(0, 1)) = 0.75
        _Saturation ("Saturation", Range(0, 2)) = 1.05
        _TintColor ("Overlay Tint", Color) = (1,1,1,0)
        _OverlayColor ("sRGB Overlay Color", Color) = (0,0,0,0)
        _UseSrgbOverlay ("Use sRGB Overlay", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "GaussianBlur"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _BlurSize;
            float _BlurMix;
            float _Saturation;
            fixed4 _TintColor;
            fixed4 _OverlayColor;
            float _UseSrgbOverlay;
            float _UseUIAlphaClip;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 SampleBlur(float2 uv)
            {
                float2 stepSize = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 color = tex2D(_MainTex, uv) * 4.0;

                color += tex2D(_MainTex, uv + float2(-stepSize.x, 0)) * 2.0;
                color += tex2D(_MainTex, uv + float2( stepSize.x, 0)) * 2.0;
                color += tex2D(_MainTex, uv + float2(0, -stepSize.y)) * 2.0;
                color += tex2D(_MainTex, uv + float2(0,  stepSize.y)) * 2.0;

                color += tex2D(_MainTex, uv + float2(-stepSize.x, -stepSize.y));
                color += tex2D(_MainTex, uv + float2( stepSize.x, -stepSize.y));
                color += tex2D(_MainTex, uv + float2(-stepSize.x,  stepSize.y));
                color += tex2D(_MainTex, uv + float2( stepSize.x,  stepSize.y));

                return color * 0.0625;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sourceColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                fixed4 blurredColor = SampleBlur(IN.texcoord) + _TextureSampleAdd;
                fixed4 color = lerp(sourceColor, blurredColor, _BlurMix) * IN.color;
                float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
                color.rgb = lerp(luminance.xxx, color.rgb, _Saturation);
                color.rgb = lerp(color.rgb, _TintColor.rgb, _TintColor.a);

                if (_UseSrgbOverlay > 0.5 && _OverlayColor.a > 0.0)
                {
                    float3 backgroundSrgb = LinearToGammaSpace(color.rgb);
                    float3 overlaySrgb = LinearToGammaSpace(_OverlayColor.rgb);
                    float3 blendedSrgb = lerp(backgroundSrgb, overlaySrgb, _OverlayColor.a);
                    color.rgb = GammaToLinearSpace(blendedSrgb);
                    color.a = 1.0;
                }

                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
