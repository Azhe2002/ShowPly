// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx
// Converted to URP HLSL for Unity 2022.3

Shader "Point Cloud/Disk"
{
    Properties
    {
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
        _PointSize("Point Size", Float) = 0.05
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
            #include "Disk.hlsl"
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
            #include "Disk.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "Pcx.DiskMaterialInspector"
}
