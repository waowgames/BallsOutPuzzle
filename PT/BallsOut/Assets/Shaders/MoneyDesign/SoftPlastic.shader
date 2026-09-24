Shader "Money Design/Soft Plastic"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Gloss ("Soft highlight", Range(0,1)) = 0.35
        _Shade ("Side shading", Range(0,1)) = 0.35
        _Gradient ("Background gradient", Range(0,1)) = 0
        _FakeInset ("Grid inset shading", Range(0,1)) = 0
        _BallStyle ("Ball soft highlight", Range(0,1)) = 0
        [Toggle(_BOX_GLASS)] _BoxGlass ("Recessed box shading", Float) = 0
        _InnerColor ("Box interior", Color) = (0.1,0.1,0.2,1)
        _InnerHighlight ("Box inner top highlight", Range(0,1)) = 0.25
        [HideInInspector] _BoxBounds ("Box bounds", Vector) = (-0.5,0.5,-0.5,0.5)
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
            Blend One Zero
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MYSTERY_PATTERN
            #pragma shader_feature_local _BOX_GLASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _InnerColor;
                half _Gloss, _Shade, _Gradient, _FakeInset, _BallStyle, _InnerHighlight;
                half _MysteryPattern;
                half _BoxGlass;
                half _MysteryDensity;
                half _MysteryScrollSpeed, _MysteryShineSpeed, _MysteryShineStrength;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(PerBox)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BoxBounds)
            UNITY_INSTANCING_BUFFER_END(PerBox)
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
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                output.positionOS = input.positionOS.xyz;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal = normalize(input.normalWS);
                if (_BallStyle > 0.5)
                {
                    // Smooth sphere normals and a broad studio light stay legible at mobile size.
                    // No shadow maps, extra renderers, textures or per-ball materials.
                    half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 keyLight = normalize(half3(-0.55, 0.75, -0.36));
                    half ndl = saturate(dot(normal, keyLight));
                    half facing = saturate(dot(normal, viewDir));
                    half halfDot = saturate(dot(normal, normalize(keyLight + viewDir)));
                    half edge = smoothstep(0.0, 0.65, facing);
                    half contact = smoothstep(-0.65, 0.35, normal.y);
                    half3 body = _BaseColor.rgb * (0.28 + 0.72 * ndl);
                    body *= lerp(0.58, 1.0, edge) * lerp(0.65, 1.0, contact);
                    half broad = pow(halfDot, 14.0);
                    half glint = pow(halfDot, 64.0);
                    half bounce = saturate(dot(normal, normalize(half3(0.65, 0.1, 0.7))));
                    body += _BaseColor.rgb * bounce * 0.13;
                    body += lerp(_BaseColor.rgb, half3(1, 1, 1), 0.6) * broad * 0.28;
                    body += glint * (0.35 + _Gloss * 0.45);
                    return half4(body, 1);
                }
                half3 faceNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                half flatTop = step(0.999, abs(faceNormal.y));
                // Flat tray floors must not inherit triangular highlights from the rim normals.
                normal = normalize(lerp(normal, half3(0, 1, 0), flatTop));
                // A broad studio reflection plus a small hot spot keeps the plastic saturated.
                half3 light = normalize(half3(-0.38, 0.82, 0.44));
                half diffuse = saturate(dot(normal, light));
                half shade = 1 - _Shade + _Shade * diffuse / light.y;
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half reflection = saturate(dot(normal, normalize(light + view)));
                half specular = pow(reflection, 80);
                half softReflection = pow(reflection, 12);
                half rim = pow(1 - saturate(dot(normal, view)), 4);
                half3 highlightTint = lerp(_BaseColor.rgb, half3(1, 1, 1), 0.32);
                half3 color = _BaseColor.rgb * shade + _Gloss *
                    (highlightTint * softReflection * 0.24 + specular * 0.65 + rim * 0.12);
                // Recessed grid cells get painted edge light/shadow without extra geometry.
                if (_FakeInset > 0)
                {
                    half topShadow = smoothstep(0.25, 0.46, input.positionOS.z);
                    half leftShadow = 1 - smoothstep(-0.46, -0.25, input.positionOS.x);
                    half bottomLight = 1 - smoothstep(-0.47, -0.33, input.positionOS.z);
                    half rightLight = smoothstep(0.33, 0.47, input.positionOS.x);
                    color *= lerp(1, 1 - 0.42 * max(topShadow, leftShadow) +
                        0.16 * max(bottomLight, rightLight), _FakeInset * flatTop);
                }
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half gradient = saturate(1 - length((screenUV - float2(0.50, 0.52)) * float2(1.35, 1.2)));
                color *= lerp(1, 0.52 + gradient * 0.65, _Gradient);
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
                #if defined(_BOX_GLASS)
                // The same tray mesh supplies the bright lip, dark well, and fake top bevel.
                float4 box = UNITY_ACCESS_INSTANCED_PROP(PerBox, _BoxBounds);
                half topGlow = 1 - smoothstep(0.10, 0.40, abs(box.w - input.positionOS.z));
                half leftShade = 1 - smoothstep(0.10, 0.27, abs(input.positionOS.x - box.x));
                half bottomGlow = 1 - smoothstep(0.10, 0.30, abs(input.positionOS.z - box.z));
                half wellDepth = saturate(abs(input.positionOS.z - box.z) / max(abs(box.w - box.z), 0.01));
                half3 inner = _InnerColor.rgb * lerp(1.12, 0.72, wellDepth);
                inner = lerp(inner, _BaseColor.rgb, topGlow * _InnerHighlight);
                inner *= 1 - 0.18 * leftShade + 0.08 * bottomGlow;
                half floorFace = (1 - smoothstep(0.061, 0.075, input.positionOS.y)) * flatTop;
                half outerEdge = min(min(abs(input.positionOS.x - box.x), abs(box.y - input.positionOS.x)),
                    min(abs(input.positionOS.z - box.z), abs(box.w - input.positionOS.z)));
                half innerWall = step(0.05, outerEdge) * (1 - flatTop) *
                    smoothstep(0.075, 0.10, input.positionOS.y) *
                    (1 - smoothstep(0.25, 0.28, input.positionOS.y));
                half wallHeight = smoothstep(0.084, 0.266, input.positionOS.y);
                half3 wallColor = lerp(_InnerColor.rgb * 0.65, _BaseColor.rgb, wallHeight * wallHeight);
                color = lerp(color, wallColor, innerWall * 0.85);
                color = lerp(color, inner, floorFace);
                // Mesh rings: base to 0.084, wall to 0.266, bright lip to 0.29.
                half wall = smoothstep(0.084, 0.105, input.positionOS.y) *
                    (1 - smoothstep(0.245, 0.266, input.positionOS.y));
                color += wall * rim * half3(0.1, 0.13, 0.2);
                return half4(color, 1);
                #else
                return half4(color, 1);
                #endif
            }
            ENDHLSL
        }
    }
}
