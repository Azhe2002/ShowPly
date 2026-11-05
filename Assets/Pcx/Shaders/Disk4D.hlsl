// 4D extension of Pcx by Hiroyuki Inou
// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx
// Converted to URP HLSL for Unity 2022.3

#ifndef PCX_DISK4D_INCLUDED
#define PCX_DISK4D_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Common.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
    half _PointSize;
    float4x4 _Transform;
    float4x4 _Rotation4D;
    float4 _Translation4D;
CBUFFER_END

#if _COMPUTE_BUFFER
struct Point4D
{
    float4 position;
    uint color;
};
StructuredBuffer<Point4D> _PointBuffer;
#endif

struct Attributes
{
#if _COMPUTE_BUFFER
    uint vertexID : SV_VertexID;
#else
    float4 position : POSITION;
    half3 color : COLOR;
    float2 uv2 : TEXCOORD1;
#endif
};

struct Varyings
{
    float4 position : SV_POSITION;
#if !PCX_SHADOW_CASTER
    half3 color : COLOR;
    real fogFactor : TEXCOORD0;
#endif
};

Varyings Vertex(Attributes input)
{
#if _COMPUTE_BUFFER
    Point4D pt = _PointBuffer[input.vertexID];
    float4 pos4d = mul(_Rotation4D, pt.position) + _Translation4D;
    float4 pos = mul(_Transform, float4(pos4d.xyz, 1));
    half3 col = PcxDecodeColor(pt.color);
#else
    float4 pos4d = float4(input.position.xyz, input.uv2.x);
    pos4d = mul(_Rotation4D, pos4d) + _Translation4D;
    float4 pos = float4(pos4d.xyz, 1);
    half3 col = input.color;
#endif

#if !PCX_SHADOW_CASTER
    col *= _Tint.rgb * 2;
#endif

    Varyings o;
    o.position = TransformObjectToHClip(pos.xyz);
#if !PCX_SHADOW_CASTER
    o.color = col;
    o.fogFactor = ComputeFogFactor(o.position.z);
#endif
    return o;
}

[maxvertexcount(36)]
void Geometry(point Varyings input[1], inout TriangleStream<Varyings> outStream)
{
    float4 origin = input[0].position;
    float2 extent = abs(UNITY_MATRIX_P._11_22 * _PointSize);

    Varyings o = input[0];

    float radius = extent.y / origin.w * _ScreenParams.y;
    uint slices = min((radius + 1) / 5, 4) + 2;
    if (slices == 2) extent *= 1.2;

    o.position.y = origin.y + extent.y;
    o.position.xzw = origin.xzw;
    outStream.Append(o);

    UNITY_LOOP for (uint i = 1; i < slices; i++)
    {
        float sn, cs;
        sincos(PI / slices * i, sn, cs);
        o.position.xy = origin.xy + extent * float2(sn, cs);
        outStream.Append(o);
        o.position.x = origin.x - extent.x * sn;
        outStream.Append(o);
    }

    o.position.x = origin.x;
    o.position.y = origin.y - extent.y;
    outStream.Append(o);

    outStream.RestartStrip();
}

half4 Fragment(Varyings input) : SV_Target
{
#if PCX_SHADOW_CASTER
    return 0;
#else
    half4 c = half4(input.color, _Tint.a);
    c.rgb = MixFog(c.rgb, input.fogFactor);
    return c;
#endif
}

#endif // PCX_DISK4D_INCLUDED
