// Pcx Point Cloud — URP depth grayscale shader
// Near = white, Far = black
Shader "Point Cloud/Point"
{
    Properties
    {
        _PointSize("Point Size", Float) = 2
        _DepthMin("Depth Min (Z)", Float) = -1000
        _DepthMax("Depth Max (Z)", Float) = 1000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float depth : TEXCOORD0;
            };

            float _PointSize;
            float _DepthMin;
            float _DepthMax;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Remap Z: 0=near, 1=far, then invert: near=1, far=0
                OUT.depth = 1.0 - saturate((IN.positionOS.z - _DepthMin) / max(_DepthMax - _DepthMin, 0.001));
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return float4(IN.depth, IN.depth, IN.depth, 1);
            }
            ENDHLSL
        }
    }
}
