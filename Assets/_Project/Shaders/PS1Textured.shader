// Gemelo de "PS1 Vertex Snap" para las mallas que SÍ traen UVs: los props
// importados de los packs.
//
// La diferencia es una sola línea del fragment. El shader original proyecta la
// textura desde el mundo en XZ porque las mallas que genero por código no tienen
// UVs. Estos props sí las tienen, y apuntan a una paleta: una grilla de cuadrados
// de color donde cada cara del modelo elige el suyo. Proyectando desde el mundo,
// las UVs se ignoran y el modelo entero sale de un color solo.
//
// Todo lo demás es idéntico a propósito — el mismo snap de vértices, el mismo piso
// de ambiente, las mismas luces puntuales y la misma niebla — para que un árbol
// importado y una roca generada por código se vean del mismo juego.
Shader "Cielo Profundo/PS1 Textured"
{
    Properties
    {
        _BaseColor ("Tinte", Color) = (1,1,1,1)
        _MainTex ("Paleta (por UV)", 2D) = "white" {}
        _SnapResolution ("Resolución de snap (px virtuales)", Float) = 140
        _AmbientFloor ("Piso de luz ambiente", Range(0,1)) = 0.16
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float fogFactor    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                float _SnapResolution;
                float _AmbientFloor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                float4 clipPos = vpi.positionCS;

                float2 ndc = clipPos.xy / clipPos.w;
                float2 grid = _SnapResolution;
                ndc = floor(ndc * grid) / grid;
                clipPos.xy = ndc * clipPos.w;

                OUT.positionHCS = clipPos;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(clipPos.z);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;

                float3 normalWS = normalize(IN.normalWS);

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = (_AmbientFloor + (1.0 - _AmbientFloor) * ndotl) * mainLight.color;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light extra = GetAdditionalLight(li, IN.positionWS);
                    half atten = extra.distanceAttenuation * saturate(dot(normalWS, extra.direction));
                    lighting += extra.color * atten;
                }
                #endif

                half3 lit = _BaseColor.rgb * tex * lighting;
                lit = MixFog(lit, IN.fogFactor);
                return half4(lit, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
