Shader "PointCloud/UnlitVertexColor"
{
    Properties
    {
        _PointSize("Point Size", Range(0.1, 20)) = 3
        _DepthMin("Depth Min (Z)", Float) = -1000
        _DepthMax("Depth Max (Z)", Float) = 1000
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  depth        : TEXCOORD0;
                float  psize        : PSIZE;
            };

            CBUFFER_START(UnityPerMaterial)
                float _PointSize;
                float _DepthMin;
                float _DepthMax;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.psize = _PointSize;
                float range = max(_DepthMax - _DepthMin, 0.001);
                // near(z=min)=white, far(z=max)=black
                OUT.depth = 1.0 - saturate((IN.positionOS.z - _DepthMin) / range);
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
