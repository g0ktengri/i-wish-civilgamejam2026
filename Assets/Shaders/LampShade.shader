// Avizenin kumaş abajuru (tavan lambası, HouseBuilder kurar). Pixel art evle uyumlu olsun diye gölgeleme
// nesne uzayında 64 px/m ızgarasına oturtulur ve renk basamaklanır. Elektrik varken kumaş içten aydınlanır:
// alta doğru parlar, alt kenarında ince parlak şerit, iç yüz (alttan görünen) sıcak ve parlak. _Glow 0..1
// (HouseLights: elektrik, titreme) MaterialPropertyBlock ile gelir; 0'da kumaş loş ortamda sönük kalır.
Shader "Deprem/LampShade"
{
    Properties
    {
        _Color ("Kumaş", Color) = (0.62, 0.24, 0.18, 1)
        _GlowColor ("İç ışık (HDR)", Color) = (1, 0.78, 0.5, 1)
        _TrimColor ("Alt kenar şeridi", Color) = (0.95, 0.72, 0.35, 1)
        _Glow ("Parlaklık 0..1", Range(0, 1)) = 1
        _InnerBoost ("İç yüz parlaklığı", Float) = 2.6
        _Ambient ("Sönükken ortam", Float) = 0.3
        _Pleats ("Pli sayısı", Float) = 18
        _Steps ("Renk basamağı", Float) = 14
        _PixelsPerMeter ("Piksel / m", Float) = 64
        _Top ("Abajurun üstü (nesne y)", Float) = 0
        _Height ("Abajur boyu (m)", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "LampShade"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GlowColor;
                half4 _TrimColor;
                half _Glow;
                half _InnerBoost;
                half _Ambient;
                float _Pleats;
                float _Steps;
                float _PixelsPerMeter;
                float _Top;
                float _Height;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half3 Posterize(half3 c)
            {
                return floor(c * _Steps + 0.5) / _Steps;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Piksel ızgarası: dikey gradyan ve pliler 1/64 m basamaklarla
                float3 p = floor(input.positionOS * _PixelsPerMeter + 0.5) / _PixelsPerMeter;
                half v = saturate((_Top - p.y) / _Height); // 0 üst, 1 alt
                half pleat = frac(input.uv.x * _Pleats) < 0.5 ? 1.0 : 0.9;
                half glow = _Glow;

                if (!IS_FRONT_VFACE(face, true, false))
                {
                    // İç yüz: açık renk astar, ampulün doğrudan aydınlattığı (alttan bakınca sıcak beyaz)
                    half3 inner = _GlowColor.rgb * (_InnerBoost * glow * lerp(0.75, 1.0, v)) + half3(0.9, 0.85, 0.75) * _Ambient * 0.6;
                    return half4(Posterize(inner), 1);
                }

                float3 n = normalize(input.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                // Önden-üstten hafif şekil ışığı ve kenarlara doğru kararma (yuvarlak dursun)
                half shape = 0.55 + 0.45 * saturate(dot(n, normalize(float3(-0.35, 0.55, -0.75))));
                half facing = saturate(dot(n, viewDir));
                half edge = 1.0 - 0.4 * pow(1.0 - facing, 2.0);

                // İçten geçen ışık: alta doğru artar (kumaşın rengini sıcak yakar)
                half transmit = glow * lerp(0.45, 1.25, v * v);
                half3 c = _Color.rgb * pleat * (_Ambient * shape + _GlowColor.rgb * transmit) * edge;
                // Üst ve alt kenarda pirinç şerit (sönükken de seçilir)
                if (v > 0.86 || v < 0.06) c = _TrimColor.rgb * shape * (_Ambient + 1.0 * glow) * edge;
                return half4(Posterize(c), 1);
            }
            ENDHLSL
        }
    }
}
