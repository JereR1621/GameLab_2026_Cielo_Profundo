Shader "Cielo Profundo/PS1 Vertex Snap"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _MainTex ("Textura (proyectada desde el mundo)", 2D) = "white" {}
        _TexScale ("Escala de textura", Float) = 0.35
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
            // Sin esto el shader solo ve la luz direccional y cualquier luz puntual
            // (bengalas, fuego) no ilumina absolutamente nada.
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float fogFactor    : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                float _TexScale;
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
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Proyección planar desde el mundo: las mallas del terreno no tienen UVs,
                // y así la textura además queda continua entre piedras vecinas.
                float2 uv = IN.positionWS.xz * _TexScale;
                half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;

                float3 normalWS = normalize(IN.normalWS);

                // El piso de ambiente es bajo a propósito: en el espacio lo que no
                // recibe luz directa se va casi a negro. Con un piso alto todo queda
                // parejo y plano, y se pierde la sensación de vacío.
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = (_AmbientFloor + (1.0 - _AmbientFloor) * ndotl) * mainLight.color;

                // Luces puntuales: es lo que hace que una bengala en el piso dibuje
                // un círculo de luz sobre el terreno en vez de ser un punto brillante
                // flotando sin efecto.
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
