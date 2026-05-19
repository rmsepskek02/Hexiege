Shader "UI/Custom/RotatingBorder1"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _Speed ("Rotation Speed", Float) = 2.0
        _Thickness ("Border Thickness", Range(0, 0.5)) = 0.05
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _HighlightLength ("Highlight Length", Range(0, 1)) = 0.2
        _HighlightSoftness ("Highlight Softness", Range(0, 1)) = 0.1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            half4 _Color;
            half4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Speed;
            float _Thickness;
            half4 _HighlightColor;
            float _HighlightLength;
            float _HighlightSoftness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = TransformObjectToHClip(OUT.worldPosition.xyz);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                // Border calculation
                float2 centerUV = uv - 0.5;
                float dist = max(abs(centerUV.x), abs(centerUV.y)) * 2.0;
                float borderMask = step(1.0 - _Thickness * 2.0, dist);

                // Rotation effect
                float angle = atan2(centerUV.y, centerUV.x) / (2.0 * 3.14159265) + 0.5;
                float time = _Time.y * _Speed * 0.1;
                
                // Two highlights
                float h1 = frac(angle + time);
                float h2 = frac(angle + time + 0.5);
                
                float highlight = max(
                    smoothstep(1.0 - _HighlightLength - _HighlightSoftness, 1.0 - _HighlightLength, h1) * (1.0 - smoothstep(1.0 - _HighlightSoftness, 1.0, h1)),
                    smoothstep(1.0 - _HighlightLength - _HighlightSoftness, 1.0 - _HighlightLength, h2) * (1.0 - smoothstep(1.0 - _HighlightSoftness, 1.0, h2))
                );

                // Combine
                float finalHighlight = highlight * borderMask;
                color.rgb = lerp(color.rgb, _HighlightColor.rgb, finalHighlight * _HighlightColor.a);
                color.a = max(color.a, finalHighlight * _HighlightColor.a);

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDHLSL
        }
    }
}
