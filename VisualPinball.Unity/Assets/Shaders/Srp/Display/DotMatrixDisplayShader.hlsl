// DXR-safe AA width helper: uses fwidth in raster, constant UV feather in ray tracing.
inline float AAWidth(float d, float2 dimensions)
{
	#ifdef SHADER_STAGE_RAY_TRACING
        // Feather in UVs of a single dot cell. Tune the scale as needed.
        float uvFeather = max(1.0 / max(dimensions.x, dimensions.y), 1e-4) * 0.75;
        return uvFeather;
	#else
	return fwidth(d);
	#endif
}

void SamplePosition_float(float2 uv, float2 dimensions, out float2 dotCenter)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float2 dotPos = floor(uv * dimensions);
	dotCenter = dimensionsPerDot * (dotPos + 0.5);
}

float Ellipse(float2 _uv, float _padding, float2 dimensions)
{
	float len = 1.0 - _padding;
	float d = length((_uv * 2 - 1) / float2(len, len));
	float w = AAWidth(d, dimensions);
	return saturate((1 - d) / w);
}

float RoundedRectangle(float2 _uv, float _padding, float _radius, float2 dimensions)
{
	float len = 1.0 - _padding;
	_radius = max(min(min(abs(_radius * 2), abs(len)), abs(len)), 1e-5);
	float2 uv = abs(_uv * 2 - 1) - float2(len, len) + _radius;
	float d = length(max(0, uv)) / _radius;
	float w = AAWidth(d, dimensions);
	return saturate((1 - d) / w);
}

float Rectangle(float2 _uv, float _padding, float2 dimensions)
{
	float len = 1.0 - _padding;
	float2 d = abs(_uv * 2 - 1) - float2(len, len);
	// Use a single AA width for both axes to avoid per-axis derivatives.
	float w = AAWidth(max(d.x, d.y), dimensions);
	float2 s = 1 - d / w;
	return saturate(min(s.x, s.y));
}

void Dot_float(float2 uv, float2 dimensions, float padding, float roundness, float4 color, out float4 output)
{
	float2 pos = frac(uv * dimensions);

	if (roundness >= 0.5) {
		output = color * Ellipse(pos, padding, dimensions);
	} else if (roundness <= 0)	{
		output = color * Rectangle(pos, padding, dimensions);
	} else{
		output = color * RoundedRectangle(pos, padding, roundness, dimensions);
	}
}
