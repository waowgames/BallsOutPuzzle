Shader "UI/TutorialFocusMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DimColor ("Dim Color", Color) = (0,0,0,0.78)
        _HoleCenter ("Hole Center", Vector) = (0.5, 0.5, 0, 0)
        _HoleRadius ("Hole Radius (px)", Float) = 130
        _Softness ("Softness", Float) = 0.7
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
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _DimColor;
            float4 _HoleCenter;
            float _HoleRadius;
            float _Softness;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Keep Unity UI happy by supporting _MainTex (used by Image/Canvas path)
                fixed4 spriteSample = tex2D(_MainTex, i.uv) * _Color;

                float2 pixelPos = float2(i.uv.x * _ScreenParams.x, i.uv.y * _ScreenParams.y);
                float2 holePixel = float2(_HoleCenter.x * _ScreenParams.x, _HoleCenter.y * _ScreenParams.y);
                float dist = distance(pixelPos, holePixel);

                float softnessPx = max(1.0, _HoleRadius * _Softness * 0.25);
                float alphaFactor = smoothstep(_HoleRadius, _HoleRadius + softnessPx, dist);

                fixed4 col = _DimColor;
                col.a *= alphaFactor;
                col *= spriteSample;
                return col;
            }
            ENDCG
        }
    }
}
