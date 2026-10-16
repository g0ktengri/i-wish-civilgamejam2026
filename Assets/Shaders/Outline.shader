// Ruh fazında sabitlenebilir eşyaların konturu (GDD: "Sabitlenebilir eşyalar kontur ile vurgulanır").
// Ters gövde: mesh merkezden dışarı biraz büyütülür, sadece arka yüzleri çizilir; eşyanın kenarında çerçeve kalır.
// Mesh'lerin merkezi nesnenin merkezinde olmalı (HouseBuilder'ın kutuları öyle).
Shader "Deprem/Outline"
{
    Properties
    {
        _OutlineColor ("Renk", Color) = (0.55, 0.85, 1, 1)
        _OutlineWidth ("Kalınlık (m)", Float) = 0.035
        _PulseSpeed ("Nabız hızı", Float) = 3
        _PulseAmount ("Nabız miktarı", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Kutunun köşelerinde yüzler ayrılmasın diye normal yerine merkezden dışarı yön
                float3 direction = normalize(input.positionOS.xyz + float3(1e-5, 1e-5, 1e-5));
                float3 position = input.positionOS.xyz + direction * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(position);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1 - _PulseAmount + _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return half4(_OutlineColor.rgb * pulse * 1.6, 1);
            }
            ENDHLSL
        }
    }
}
