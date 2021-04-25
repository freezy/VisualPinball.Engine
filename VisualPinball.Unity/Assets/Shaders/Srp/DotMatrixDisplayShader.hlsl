void SampleDot_float(float2 uv, UnityTexture2D data, float2 dimensions, out float4 dotColor, out float2 dotCenter)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float2 dotPos = floor(uv * dimensions);
	dotCenter = dotPos * dimensionsPerDot + dimensionsPerDot * 0.5;
	dotColor = tex2D(data, dotCenter);
}

void RoundDot_float(float2 uv, float2 dimensions, float dotSize, float4 dotColor, float2 dotCenter, out float4 output)
{
	// Scale coordinates back to original ratio for rounding
	float aspectRatio = dimensions.x / dimensions.y;
	float2 uvScaled = float2(uv.x * aspectRatio, uv.y);
	float2 dotCenterScaled = float2(dotCenter.x * aspectRatio, dotCenter.y);

	// Round the dot by testing the distance of the pixel coordinate to the center
	float dist = length(uvScaled - dotCenterScaled) * dimensions;

	float4 outsideColor = dotColor;
	outsideColor.r = 0;
	outsideColor.g = 0;
	outsideColor.b = 0;
	outsideColor.a = 0;

	float distFromEdge = dotSize - dist;  // positive when inside the circle
	float thresholdWidth = .22;  // a constant you'd tune to get the right level of softness
	float antialiasedCircle = saturate((distFromEdge / thresholdWidth) + 0.5);

	output = lerp(outsideColor, dotColor, antialiasedCircle);
}
