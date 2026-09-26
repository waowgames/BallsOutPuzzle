Shader "Balls Out/Feeder Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.86, 0.9, 1, 1)
        _Body ("Body Alpha", Range(0, 1)) = 0.05
        _Edge ("Edge Alpha", Range(0, 1)) = 0.65
    }
    SubShader
    {
        // Over the balls inside the tube; glass never writes depth.
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
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
                half4 _Tint;
                half _Body;
                half _Edge;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // uv.x runs across the tube (0..1), uv.y along it from the mouth to the cap.
            half4 Frag(Varyings input) : SV_Target
            {
                float across = abs(input.uv.x * 2.0 - 1.0);
                float edge = pow(across, 4.0);
                // A sharp reflection streak on the left, a soft one on the right.
                float streak = saturate(1.0 - abs(input.uv.x - 0.24) * 18.0) * 0.8 +
                               saturate(1.0 - abs(input.uv.x - 0.8) * 9.0) * 0.2;
                // The glass fades out as it meets the reservoir.
                float mouth = saturate(input.uv.y * 6.0);
                half alpha = saturate(_Body + edge * _Edge) * mouth;
                half3 rgb = _Tint.rgb * alpha + streak * mouth;
                return half4(rgb, saturate(alpha + streak * mouth));
            }
            ENDHLSL
        }
    }
}
