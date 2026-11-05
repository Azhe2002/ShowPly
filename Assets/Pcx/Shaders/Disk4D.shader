// 4D extension of Pcx by Hiroyuki Inou
// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx
// Converted to URP HLSL for Unity 2022.3

Shader "Point Cloud/Disk4D"
{
    Properties
    {
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
        _PointSize("Point Size", Float) = 0.05
        _Translation4D("4D Translation", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _COMPUTE_BUFFER
            #include "Disk4D.hlsl"
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile _ _COMPUTE_BUFFER
            #define PCX_SHADOW_CASTER 1
            #include "Disk4D.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "Pcx.Disk4DMaterialInspector"
}
