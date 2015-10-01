struct VS_OUTPUT
{
    float3 cpoint : CPOINT;
};

struct HS_CONSTANT_OUTPUT
{
    float edges[2] : SV_TessFactor;
};

struct HS_OUTPUT
{
    float3 cpoint : CPOINT;
};

HS_CONSTANT_OUTPUT HSConst()
{
    HS_CONSTANT_OUTPUT output;

    output.edges[0] = 1.0f; // Detail factor (see below for explanation)
    output.edges[1] = 8.0f; // Density factor

    return output;
}

[domain("isoline")]
[partitioning("integer")]
[outputtopology("line")]
[outputcontrolpoints(4)]
[patchconstantfunc("HSConst")]
HS_OUTPUT main(InputPatch<VS_OUTPUT, 4> ip, uint id : SV_OutputControlPointID)
{
    HS_OUTPUT output;
    output.cpoint = ip[id].cpoint;
    return output;
}
