void SamplePosition_float(float2 uv, float2 dimensions, out float2 dotCenter)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float2 dotPos = floor(uv * dimensions);
	dotCenter = dimensionsPerDot * (dotPos + 0.5);
}

float Circle(float2 uv, float _radius, float _sharpness) {
	float2 dist = uv - float2(0.5, 0.5);
	return 1. - smoothstep(
		_radius - (_radius * _sharpness),
		_radius + (_radius * _sharpness),
		dot(dist, dist) * 4.0
	);
}

void RoundDot_float(float2 uv, float2 dimensions, float scale, float sharpness, float4 color, out float4 output)
{
	float2 pos = frac(uv * dimensions);
	output = color * Circle(pos, scale, sharpness);
}
