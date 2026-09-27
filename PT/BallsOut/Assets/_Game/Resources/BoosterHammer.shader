Shader "Balls Out/Booster Hammer"
{
    Properties
    {
        _BodyColor ("Body", Color) = (0.56, 0.3, 0.95, 1)
        _TrimColor ("Trim", Color) = (1, 0.78, 0.2, 1)
        _FaceStart ("Striking face |x| from", Float) = 0.5
        _CapStart ("Top cap y from", Float) = 1.32
        _KnobEnd ("Handle knob y below", Float) = -0.14
    }
    SubShader
    {
        // Paints _ART/Hammer.fbx like the HUD icon without a texture: purple head and handle,
        // gold striking faces, top cap and handle knob, picked by object-space position.
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _TrimColor;
                float _FaceStart;
                float _CapStart;
                float _KnobEnd;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionOS;
                bool trim = abs(p.x) > _FaceStart || p.y > _CapStart || p.y < _KnobEnd;
                half3 albedo = trim ? _TrimColor.rgb : _BodyColor.rgb;
                float3 n = normalize(input.normalWS);
                Light light = GetMainLight();
                // Soft toon ramp plus a rim so it reads like the flat HUD art.
                half diffuse = saturate(dot(n, light.direction) * 0.5 + 0.5);
                diffuse = lerp(0.62, 1.0, smoothstep(0.35, 0.65, diffuse));
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(n, view)), 3.0) * 0.35;
                return half4(albedo * diffuse + rim, 1);
            }
            ENDHLSL
        }
    }
}
