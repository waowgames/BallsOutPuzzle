Shader "UI/TutorialFocusMask"
{
    // Full-screen tutorial shade with up to four rounded-rect holes cut into it.
    // Holes are given in screen pixels (centre.xy, half size.zw); a glowing rim and an
    // outward ripple circle each hole so the eye lands on it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DimColor ("Dim Color", Color) = (0.02,0.03,0.08,0.78)
        _Resolution ("Resolution (px)", Vector) = (1080, 1920, 0, 0)
        _HoleCount ("Hole Count", Float) = 0
        _Hole0 ("Hole 0", Vector) = (0,0,0,0)
        _Hole1 ("Hole 1", Vector) = (0,0,0,0)
        _Hole2 ("Hole 2", Vector) = (0,0,0,0)
        _Hole3 ("Hole 3", Vector) = (0,0,0,0)
        _HoleRadius ("Corner Radius per hole (px)", Vector) = (24,24,24,24)
        _Softness ("Edge Softness (px)", Float) = 10

        _RingColor ("Ring Color", Color) = (1,0.93,0.55,1)
        _RingWidth ("Ring Width (px)", Float) = 5
        _RingStrength ("Ring Strength", Range(0,1)) = 1
        _RipplePhase ("Ripple Phase", Range(0,1)) = 0
        _RippleDistance ("Ripple Distance (px)", Float) = 46
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
        ZTest [unity_GUIZTestMode]
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
            float4 _Resolution;
            float _HoleCount;
            float4 _Hole0, _Hole1, _Hole2, _Hole3;
            float4 _HoleRadius;
            float _Softness;
            fixed4 _RingColor;
            float _RingWidth;
            float _RingStrength;
            float _RipplePhase;
            float _RippleDistance;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            // Signed distance to a rounded rectangle: negative inside, positive outside (px).
            float RoundedRect(float2 p, float4 hole, float radius)
            {
                float r = min(radius, min(hole.z, hole.w));
                float2 q = abs(p - hole.xy) - (hole.zw - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The shade image stretches over the whole screen, so its UV is the screen UV.
                float2 p = i.uv * _Resolution.xy;

                float d = 100000.0;
                if (_HoleCount > 0.5) d = min(d, RoundedRect(p, _Hole0, _HoleRadius.x));
                if (_HoleCount > 1.5) d = min(d, RoundedRect(p, _Hole1, _HoleRadius.y));
                if (_HoleCount > 2.5) d = min(d, RoundedRect(p, _Hole2, _HoleRadius.z));
                if (_HoleCount > 3.5) d = min(d, RoundedRect(p, _Hole3, _HoleRadius.w));

                float dimA = _DimColor.a * smoothstep(0.0, max(1.0, _Softness), d);

                float ringA = 0.0;
                if (_HoleCount > 0.5)
                {
                    // Steady glowing rim hugging the hole.
                    float rim = saturate(1.0 - abs(d - _RingWidth) / max(1.0, _RingWidth));
                    // A ripple that leaves the rim and fades as it travels outward.
                    float rippleD = _RipplePhase * _RippleDistance + _RingWidth;
                    float ripple = saturate(1.0 - abs(d - rippleD) / max(1.0, _RingWidth * 0.8)) * (1.0 - _RipplePhase);
                    ringA = saturate(rim * 0.9 + ripple * 0.7) * _RingColor.a * _RingStrength;
                }

                float a = ringA + dimA * (1.0 - ringA);
                float3 rgb = (_RingColor.rgb * ringA + _DimColor.rgb * dimA * (1.0 - ringA)) / max(a, 0.0001);

                fixed4 col = fixed4(rgb, a) * i.color;
                col.a *= tex2D(_MainTex, i.uv).a;
                return col;
            }
            ENDCG
        }
    }
}
