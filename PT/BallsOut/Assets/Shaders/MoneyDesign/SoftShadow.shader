Shader "Money Design/Soft Shadow"
{
    Properties
    {
        _BaseColor ("Shadow color (alpha = strength)", Color) = (0.05,0.02,0.16,0.55)
        [NoScaleOffset] _MainTex ("Baked mask", 2D) = "black" {}
    }
    SubShader
    {
        // Drawn after opaques so the casters themselves hide the part of the blob beneath them.
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
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
            half4 Frag(Varyings input) : SV_Target
            {
                // The mask is already blurred on the CPU; one bilinear tap gives the soft falloff.
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(_BaseColor.rgb, _BaseColor.a * mask);
            }
            ENDHLSL
        }
    }
}
