// Avizenin ışıkları (HouseBuilder kurar). Pixel art evle uyumlu: değerler 64 px/m ızgarasında ve basamaklı.
//  _Mode 0 hale: kameraya bakan karede ampulün çevresinde halkalı, eklemeli parıltı
//  _Mode 1 huzme: abajurun altından yere inen koni; aşağı ve kenarlara doğru söner (havadaki toz gibi)
//  _Mode 2 ampul: düz, parlak (HDR) yüzey; sönükken gri cam
// _Glow 0..1 (HouseLights: elektrik, titreme) MaterialPropertyBlock ile gelir.
Shader "Deprem/LampGlow"
{
    Properties
    {
        _Color ("Renk (HDR, a: güç)", Color) = (1, 0.8, 0.55, 1)
        _Glow ("Parlaklık 0..1", Range(0, 1)) = 1
        _Mode ("0 hale, 1 huzme, 2 ampul", Float) = 0
        _Steps ("Basamak", Float) = 6
        _PixelsPerMeter ("Piksel / m", Float) = 64
        _Size ("Hale boyu (m)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 1
        [Toggle] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "LampGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Glow;
                float _Mode;
                float _Steps;
                float _PixelsPerMeter;
                float _Size;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half Step(half a)
            {
                return floor(a * _Steps + 0.5) / _Steps;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Mode > 1.5)
                {
                    // Ampul: yanarken HDR (bloom), sönükken soğuk gri cam
                    half3 bulb = lerp(half3(0.25, 0.25, 0.27), _Color.rgb * 3.0, _Glow);
                    return half4(bulb, 1);
                }

                half a;
                if (_Mode < 0.5)
                {
                    // Hale: karenin piksellerine oturtulmuş, halka halka sönen parıltı
                    float pixels = max(1.0, _Size * _PixelsPerMeter);
                    float2 uv = (floor(input.uv * pixels) + 0.5) / pixels;
                    half d = saturate(length(uv - 0.5) * 2.0);
                    a = Step(pow(1.0 - d, 2.4));
                }
                else
                {
                    // Huzme: üstte yoğun, aşağı doğru ve kenarlara doğru söner
                    float3 n = normalize(input.normalWS);
                    float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                    half facing = abs(dot(n, viewDir));
                    float v = floor(input.uv.y * _Steps * 3.0) / (_Steps * 3.0);
                    a = Step(pow(1.0 - v, 1.7) * pow(facing, 1.5));
                }
                half3 c = _Color.rgb * (a * _Color.a * _Glow);
                return half4(c, a * _Color.a * _Glow);
            }
            ENDHLSL
        }
    }
}
