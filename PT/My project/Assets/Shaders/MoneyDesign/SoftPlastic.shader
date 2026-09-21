Shader "Money Design/Soft Plastic"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Gloss ("Soft highlight", Range(0,1)) = 0.35
        _Shade ("Side shading", Range(0,1)) = 0.35
        _Gradient ("Background gradient", Range(0,1)) = 0
        [Toggle(_MYSTERY_PATTERN)] _MysteryPattern ("Hidden box question marks", Float) = 0
        _MysteryScrollSpeed ("Question mark scroll speed", Range(0,1)) = 0.12
        _MysteryDensity ("Question marks per cell axis", Range(1,5)) = 3
        _MysteryShineSpeed ("White stripe speed", Range(0,1)) = 0.35
        _MysteryShineStrength ("White stripe brightness", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MYSTERY_PATTERN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Gloss, _Shade, _Gradient;
                half _MysteryPattern;
                half _MysteryDensity;
                half _MysteryScrollSpeed, _MysteryShineSpeed, _MysteryShineStrength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                #if defined(_MYSTERY_PATTERN)
                float3 positionOS : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_MYSTERY_PATTERN)
                output.positionOS = input.positionOS.xyz;
                #endif
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 faceNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                half flatTop = step(0.999, abs(faceNormal.y));
                // Flat tray floors must not inherit triangular highlights from the rim normals.
                normal = normalize(lerp(normal, half3(0, 1, 0), flatTop));
                // Broad upper-right studio light, independent of the garage's lighting.
                half3 light = normalize(half3(0.42, 0.82, 0.38));
                half diffuse = saturate(dot(normal, light));
                half shade = 1 - _Shade + _Shade * diffuse / light.y;
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half specular = pow(saturate(dot(normal, normalize(light + view))), 48) * lerp(1, 0.08, flatTop);
                half rim = pow(1 - saturate(dot(normal, view)), 4);
                half3 color = _BaseColor.rgb * shade + _Gloss * (specular + rim * 0.16);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half gradient = saturate(1 - length((screenUV - float2(0.53, 0.60)) * float2(1.1, 0.75)));
                color *= lerp(1, 0.82 + gradient * 0.27, _Gradient);
                #if defined(_MYSTERY_PATTERN)
                // Animate on the GPU, sharing one material across all hidden boxes.
                float2 p = frac(input.positionOS.xz * _MysteryDensity + 0.5 +
                    _Time.y * _MysteryScrollSpeed * float2(0.5, 1)) - 0.5;
                float2 hook = p - float2(0, 0.12);
                float ring = abs(length(hook) - 0.14) - 0.035;
                ring = max(ring, min(-hook.x, -hook.y));
                float stem = max(abs(p.x) - 0.035, abs(p.y + 0.065) - 0.065);
                float dotMark = length(p - float2(0, -0.24)) - 0.043;
                float distance = min(ring, min(stem, dotMark));
                float aa = max(fwidth(distance), 0.001);
                half mark = 1 - smoothstep(-aa, aa, distance);
                float3 localFace = normalize(cross(ddy(input.positionOS), ddx(input.positionOS)));
                mark *= step(0.9, abs(localFace.y));
                color = lerp(color, half3(0.92, 0.92, 0.92) * shade, mark);
                float stripePosition = dot(input.positionOS.xz, float2(0.25, 0.15));
                float stripe = abs(frac(stripePosition - _Time.y * _MysteryShineSpeed) - 0.5);
                half shine = (1 - smoothstep(0.025, 0.10, stripe)) *
                    _MysteryShineStrength * step(0.9, abs(localFace.y));
                color = lerp(color, half3(1, 1, 1), shine);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
