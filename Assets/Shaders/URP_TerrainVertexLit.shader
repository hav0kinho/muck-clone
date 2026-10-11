Shader "Custom/URP_TerrainVertexLit"
{
    Properties
    {
        _AmbientColor("Ambient Color", Color) = (0.45, 0.45, 0.50, 1.0)
        _ShadowIntensity("Shadow Attenuation", Range(0.3, 0.9)) = 0.65
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AmbientColor;
                float _ShadowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS   = normalInput.normalWS;
                output.color      = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);

                // Cel-Shading estilizado cartoon (Muck style)
                half NdotL = dot(normalWS, mainLight.direction);
                half celLight = NdotL > 0.08 ? 1.0 : _ShadowIntensity;

                half3 finalColor = input.color.rgb * saturate(_AmbientColor.rgb + mainLight.color * celLight);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
