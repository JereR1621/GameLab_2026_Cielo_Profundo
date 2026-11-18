Shader "Cielo Profundo/PS1 Sky"
{
    Properties
    {
        _ColorTop ("Color arriba", Color) = (0.043,0.024,0.125,1)
        _ColorHorizon ("Color horizonte", Color) = (0.165,0.086,0.4,1)
        _StarDensity ("Densidad de estrellas", Range(50,400)) = 180
        _SunDir ("Dirección de la estrella", Vector) = (0.35,0.45,0.6,0)
        _SunColor ("Color de la estrella", Color) = (1,0.92,0.8,1)
        _SunSize ("Tamaño de la estrella", Range(0.9,0.9995)) = 0.9955
        _HaloStrength ("Fuerza del halo", Range(0,2)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorTop;
            fixed4 _ColorHorizon;
            float _StarDensity;
            float4 _SunDir;
            fixed4 _SunColor;
            float _SunSize;
            float _HaloStrength;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float t = saturate(dir.y * 0.5 + 0.5);
                fixed3 col = lerp(_ColorHorizon.rgb, _ColorTop.rgb, t);

                float3 cell = floor(dir * _StarDensity);
                float h = hash(cell);
                float star = h > 0.995 ? 1.0 : 0.0;
                float brightness = frac(h * 91.31);
                col += star * brightness * saturate(dir.y + 0.3);

                // La estrella del sistema: disco duro más un halo que la funde con la bruma.
                float d = dot(dir, normalize(_SunDir.xyz));
                float disc = smoothstep(_SunSize, _SunSize + 0.0015, d);
                float halo = pow(saturate(d), 90.0) * _HaloStrength;
                col += _SunColor.rgb * (disc + halo);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
