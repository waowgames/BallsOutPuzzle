Shader "Balls Out/Ice Box"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Baked top (rgb) + signed distance (a)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Range ("Distance range (cells)", Float) = 0.3
        _Flash ("White flash", Range(0, 1)) = 0
    }
    SubShader
    {
        // After opaque boxes, before TextMeshPro (3000) so the counter always reads on top.
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Range;
                half _Flash;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                // x: 0 slab layer (vertex color), 1 top face (baked color), 2 plain (shards).
                // y: how far this layer is pulled inside the outline, in cells.
                float2 layer : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 layer : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.layer = input.layer;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                if (input.layer.x < 1.5)
                {
                    half4 baked = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    // Alpha stores the rounded footprint's signed distance; each
                    // layer cuts it at its own offset, giving soft rounded walls.
                    float edge = (0.5 - baked.a) * 2.0 * _Range + input.layer.y;
                    float coverage = saturate(0.5 - edge / max(fwidth(edge), 1e-4));
                    clip(coverage - 0.01);
                    if (input.layer.x > 0.5) color.rgb = baked.rgb;
                    color.a *= coverage;
                }
                color *= _Color;
                color.rgb = lerp(color.rgb, half3(1, 1, 1), _Flash);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
