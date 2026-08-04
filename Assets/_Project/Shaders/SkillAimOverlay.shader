// ============================================================================
// SkillAimOverlay.shader
// 스킬 조준원(SkillAimReticle) 전용 오버레이 셰이더.
//
// 무엇을 푸는가(초급자용 설명):
//   조준원은 지면(XZ 평면)에 아주 낮게 깔린 평평한 스프라이트다. 3D 타일의 윗면과 높이가
//   거의 같아(coplanar) 깊이 테스트에서 서로 물고 뜯는 z-fighting이 발생해, 조준원이 타일에
//   "파묻혀" 겹치는 부분이 안 보였다.
//
//   이 셰이더는 두 가지로 그 문제를 균형 있게 해결한다:
//     1) Offset -1, -1  : 조준원 픽셀의 깊이를 카메라 쪽으로 아주 살짝 당겨,
//        같은 높이의 타일 윗면과의 z-fighting에서 "항상 이기게" 한다 → 지형에 안 파묻힘.
//     2) ZTest LEqual   : 그래도 깊이 테스트 자체는 수행하므로, 조준원보다 확실히 앞에 있는
//        불투명 물체(유닛·건물 3D 메시)는 조준원을 정상적으로 가린다 → 유닛이 원 위에 그려짐.
//
//   즉 "지형에는 안 가려지고, 유닛/건물에는 가려지는" 지면 데칼 느낌을 만든다.
//   (ZTest Always 같은 전면 오버레이는 유닛까지 뚫고 그려지므로 쓰지 않는다.)
//
// 렌더링 메모:
//   - Transparent 큐 + ZWrite Off + 알파 블렌드(기존 스프라이트와 동일한 반투명 룩).
//   - SpriteRenderer가 넘기는 _MainTex(원 스프라이트) × 정점 색(renderer.color)으로 색을 낸다.
//   - URP/빌트인 모두에서 동작하는 단순 unlit 구성(라이팅·그림자 없음).
//   - Cull Off: 조준원 루트가 X축 90도로 눕어 있어 뒷면도 보일 수 있으므로 양면 렌더.
// ============================================================================
Shader "Hexiege/SkillAimOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color; // SpriteRenderer.color(정점 색) × 머티리얼 틴트.
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
