Shader "Money Design/Polished Money"
{
    Properties
    {
        _BaseColor ("Money color", Color) = (1,1,1,1)
        _Gloss ("Highlight", Range(0,1)) = 0.38
        _OutlineColor ("Outline", Color) = (0.015,0.018,0.022,1)
        _OutlineWidth ("Outline width", Range(0,0.12)) = 0.045
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _OutlineColor;
                half _Gloss, _OutlineWidth;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 face : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 face : TEXCOORD2;
                half paper : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                if (input.face.w > 1.5)
                    input.positionOS.xyz += input.face.xyz * (clamp(_OutlineWidth, 0, 0.12) - 0.045);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.face = input.face;
                // Existing money UV islands separate the bill body and its printed motif.
                output.paper = smoothstep(0.5, 0.75, input.uv.x);
                return output;
            }
            float DollarDistance(float2 p)
            {
                // Two rounded arcs form the S; the vertical stroke completes the dollar.
                float2 top = p - float2(0, 0.14);
                float2 bottom = p + float2(0, 0.14);
                float upper = top.x > 0 && top.y < 0
                    ? min(length(top - float2(0.14, 0)), length(top + float2(0, 0.14)))
                    : abs(length(top) - 0.14);
                float lower = bottom.x < 0 && bottom.y > 0
                    ? min(length(bottom + float2(0.14, 0)), length(bottom - float2(0, 0.14)))
                    : abs(length(bottom) - 0.14);
                float stem = length(float2(p.x, max(abs(p.y) - 0.34, 0)));
                return min(min(upper, lower) - 0.045, stem - 0.025);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (input.face.w > 1.5)
                {
                    clip(_OutlineWidth - 0.001);
                    return half4(_OutlineColor.rgb, 1);
                }
                half3 normal = normalize(input.normalWS);
                half3 light = normalize(half3(0.42, 0.82, 0.38));
                half diffuse = saturate(dot(normal, light));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half specular = pow(saturate(dot(normal, normalize(light + view))), 40);
                half shade = 0.66 + 0.34 * diffuse;
                half3 color = _BaseColor.rgb * shade;
                // Light ivory ink keeps the oval/corner pattern readable without dulling the body.
                half3 paper = half3(2.65, 2.58, 2.30) + _BaseColor.rgb * 0.08;
                color = lerp(color, paper * (0.90 + diffuse * 0.10), input.paper);
                color += _Gloss * specular;
                float dollar = DollarDistance(input.face.xy);
                float inkAA = max(fwidth(dollar), 0.001);
                half ink = 1 - smoothstep(-inkAA, inkAA, dollar);
                ink *= step(0.85, abs(input.face.w));
                color = lerp(color, half3(2.65, 2.65, 2.65), ink);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
