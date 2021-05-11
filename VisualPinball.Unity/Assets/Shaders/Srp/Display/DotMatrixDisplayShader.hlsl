void SamplePosition_float(float2 uv, float2 dimensions, out float2 dotCenter)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float2 dotPos = floor(uv * dimensions);
	dotCenter = dimensionsPerDot * (dotPos + 0.5);
}

float Ellipse(float2 _uv, float _padding)
{
	float len = 1.0 - _padding;
	float d = length((_uv * 2 - 1) / float2(len, len));
	return saturate((1 - d) / fwidth(d));
}

float RoundedRectangle(float2 _uv, float _padding, float _radius)
{
	float len = 1.0 - _padding;
	_radius = max(min(min(abs(_radius * 2), abs(len)), abs(len)), 1e-5);
	float2 uv = abs(_uv * 2 - 1) - float2(len, len) + _radius;
	float d = length(max(0, uv)) / _radius;
	return saturate((1 - d) / fwidth(d));
}

float Rectangle(float2 _uv, float _padding)
{
	float len = 1.0 - _padding;
	float2 d = abs(_uv * 2 - 1) - float2(len, len);
	d = 1 - d / fwidth(d);
	return saturate(min(d.x, d.y));
}

void Dot_float(float2 uv, float2 dimensions, float padding, float roundness, float4 color, out float4 output)
{
	float2 pos = frac(uv * dimensions);

	if (roundness >= 0.5) {
		output = color * Ellipse(pos, padding);

	} else if (roundness <= 0) {
		output = color * Rectangle(pos, padding);

	} else {
		output = color * RoundedRectangle(pos, padding, roundness);
	}
}
