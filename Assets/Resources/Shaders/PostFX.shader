Shader "IronDistrict/PostFX"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _Exposure;
            float _Saturation;
            float _Contrast;
            float _Vignette;
            float _Grain;
            float _Lift;
            float _Seed;

            // ACES filmic tonemap (Narkowicz fit)
            float3 ACESFilm(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + _Seed) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float3 col = tex2D(_MainTex, uv).rgb;

                // exposure + filmic curve
                col *= _Exposure;
                col = ACESFilm(col);

                // subtle desaturation towards a filmic look
                float luma = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(float3(luma, luma, luma), col, _Saturation);

                // cool shadows / warm highlights split-tone
                float3 cool = float3(0.94, 0.98, 1.06);
                float3 warm = float3(1.05, 1.01, 0.95);
                col *= lerp(cool, warm, saturate(luma * 1.6));

                // contrast around mid grey
                col = (col - 0.5) * _Contrast + 0.5;
                col += _Lift * (1.0 - luma);

                // vignette
                float2 d = uv - 0.5;
                float vig = 1.0 - dot(d, d) * _Vignette * 2.4;
                col *= saturate(vig);

                // film grain
                float g = hash(uv * _ScreenParams.xy) - 0.5;
                col += g * _Grain;

                // keep the top a touch brighter for readability of the HUD
                col = saturate(col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
