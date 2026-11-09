// Lit surface whose texture is laid out in world metres, so walls, floors and roads built from stretched
// boxes all get the same texel size with no UV work. Built-in render pipeline.
Shader "Rooftop/WorldLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Scale ("Metres per tile", Float) = 2
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Emission ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _Emission;
        float _Scale;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(IN.worldNormal);
            float2 uv = n.y > 0.5 ? IN.worldPos.xz : (n.x > n.z ? IN.worldPos.zy : IN.worldPos.xy);
            fixed4 c = tex2D(_MainTex, uv / _Scale) * _Color;
            o.Albedo = c.rgb;
            o.Emission = _Emission.rgb * c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
