Shader "Balls Out/Celebration Spark"
{
    Properties
    {
        _Shape ("Shape (0 star, 1 ring)", Float) = 0
    }
    SubShader
    {
        // Above boxes, balls and the ice counter; particles never write depth.
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Shape;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float glow = saturate(1.0 - r);
                glow *= glow;
                // Four-point twinkle: two thin rays crossing a soft glow, white-hot at the core.
                float rays = saturate(1.0 - abs(p.x) * 7.0) * saturate(1.0 - abs(p.y)) +
                             saturate(1.0 - abs(p.y) * 7.0) * saturate(1.0 - abs(p.x));
                float star = saturate(glow * 1.3 + rays);
                float ring = saturate(1.0 - abs(r - 0.78) * 7.0) + glow * 0.25;
                float shape = lerp(star, ring, _Shape);
                float core = saturate(1.0 - r * 2.5) * (1.0 - _Shape);
                half3 rgb = lerp(input.color.rgb, half3(1, 1, 1), core * 0.85);
                half alpha = saturate(shape) * input.color.a;
                // Premultiplied, with less coverage than colour so sparks glow on bright tiles.
                return half4(rgb * alpha * 1.15, alpha * 0.85);
            }
            ENDHLSL
        }
    }
}
