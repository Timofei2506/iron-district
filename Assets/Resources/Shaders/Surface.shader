Shader "IronDistrict/Surface"
{
    // Standard lighting + baked vertex AO (ground grime gradient, downwards faces darker).
    // Falls back to the built-in Standard shader if this one fails to compile.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _AOStrength ("Vertex AO", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        half _Metallic;
        half _Glossiness;
        half _AOStrength;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // clamp: meshes without baked colours read white (1.0), and a stray 0 can't blank the world
            float ao = lerp(1.0, clamp(IN.color.r, 0.45, 1.0), _AOStrength);
            o.Albedo = c.rgb * ao;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Occlusion = ao;
            o.Normal = normalize(UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap)) * float3(_BumpScale, _BumpScale, 1));
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
