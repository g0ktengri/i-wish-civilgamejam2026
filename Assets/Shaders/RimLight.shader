// Karakter kenar ışığı (2.5D): düz pixel art karakterin ışığa bakan kenarında 2 piksellik parlak şerit.
// Karakterin quad'ının hemen önünde, aynı kareyle çizilen eklemeli (additive) bir kopya. Pikselin kendisi
// doluysa ve ışık yönünde 1-2 piksel ötesi boşsa (silüetin ışığa bakan kenarı) ışık rengi eklenir.
// Işık yönü ve rengi Visual.CharacterRimLight'tan (her kare, sahnedeki yakın ışıklara göre) gelir.
Shader "Deprem/RimLight"
{
    Properties
    {
        _BaseMap ("Kare", 2D) = "white" {}
        _RimColor ("Renk (gücü dahil)", Color) = (0, 0, 0, 0)
        _RimDir ("Işık yönü (UV, birim)", Vector) = (1, 0, 0, 0)
        _RimTexel ("Doku pikseli (1/en, 1/boy)", Vector) = (0.0078125, 0.0078125, 0, 0)
        _RimPixels ("Kalınlık (piksel)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RimLight"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _RimColor;
                float4 _RimDir;
                float4 _RimTexel;
                float _RimPixels;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half Alpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (Alpha(input.uv) < 0.5) return 0;

                // Işık yönünde 1..N piksel ötesinden biri boşsa bu piksel ışığa bakan kenardadır
                float2 pixelStep = _RimDir.xy * _RimTexel.xy;
                half edge = 0;
                int pixels = (int)max(1, _RimPixels);
                [unroll] for (int i = 1; i <= 4; i++)
                {
                    // step(a, 0.5) = 1 when the neighbour is transparent; en dış piksel tam, içerisi yarım
                    if (i <= pixels) edge = max(edge, step(Alpha(input.uv + pixelStep * i), 0.5) / i);
                }
                return half4(_RimColor.rgb * edge, 0);
            }
            ENDHLSL
        }
    }
}
