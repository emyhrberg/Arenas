sampler image0 : register(s0);

float globalTime;
float3 borderColor;
float3 passableColor;
float opacity;
float flipGradient;
float pulseStrength;
float shimmerStrength;
float shimmerScale;
float shimmerSpeed;

float SmootherStep(float edge0, float edge1, float value)
{
    float t = saturate((value - edge0) / (edge1 - edge0));
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(image0, coords);
    float gradientPosition = lerp(coords.x, 1.0 - coords.x, saturate(flipGradient));
    gradientPosition = SmootherStep(0.0, 1.0, gradientPosition);
    float pulse = 0.5 + 0.5 * sin(globalTime * 0.075);
    float shimmer = 0.5 + 0.5 * sin((coords.x + coords.y) * shimmerScale + globalTime * shimmerSpeed);
    float glow = 1.0 + pulse * pulseStrength + shimmer * shimmerStrength;
    float3 color = lerp(borderColor, passableColor, gradientPosition) * glow;
    float alpha = tex.a * sampleColor.a * opacity;
    return float4(color * alpha, alpha);
}

technique t0
{
    pass p0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
