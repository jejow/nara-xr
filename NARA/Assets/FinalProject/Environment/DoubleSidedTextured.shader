Shader "NARA/DoubleSidedTextured"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Base color", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _TransmissionTint ("Backlit fabric", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off

        CGPROGRAM
        #pragma surface Surface Standard fullforwardshadows addshadow
        #pragma target 3.5
        #pragma shader_feature_local _NORMALMAP
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _BumpScale;
        half _Metallic;
        half _Glossiness;
        half _TransmissionTint;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float facing : VFACE;
        };

        void Surface(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = tex2D(_MainTex, input.uv_MainTex).rgb * _Color.rgb;
            half3 normal = half3(0, 0, 1);
            #if defined(_NORMALMAP)
                normal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_BumpMap), _BumpScale);
            #endif
            normal.z *= input.facing > 0 ? 1 : -1;
            output.Normal = normal;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness;
            output.Emission = output.Albedo * _TransmissionTint;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
