Shader "NARA/Avatar Cutout"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo and alpha", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.41
        _Glossiness ("Smoothness", Range(0, 1)) = 0.16
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off
        CGPROGRAM
        #pragma surface Surface Standard alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;

        struct Input { float2 uv_MainTex; float facing : VFACE; };

        void Surface(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 color = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = color.rgb;
            output.Alpha = color.a;
            output.Normal = float3(0, 0, input.facing);
            output.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
