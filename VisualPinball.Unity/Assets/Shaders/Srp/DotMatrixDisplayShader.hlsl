void SampleDot_float(float2 uv, UnityTexture2D data, float2 dimensions, out float4 output)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float2 dotPos = floor(uv * dimensions);
	float2 dotCenter = dotPos * dimensionsPerDot + dimensionsPerDot * 0.5;
	output = tex2D(data, dotCenter);
}

void DotMatrixDisplay_float(float2 uv, UnityTexture2D data, float2 dimensions, float dotSize, out float4 output)
{
	float2 dimensionsPerDot = 1. / dimensions;
	float aspectRatio = dimensions.x / dimensions.y;

	// Calculate dot center
	float2 dotPos = floor(uv * dimensions);
	float2 dotCenter = dotPos * dimensionsPerDot + dimensionsPerDot * 0.5;

	// Scale coordinates back to original ratio for rounding
	float2 uvScaled = float2(uv.x * aspectRatio, uv.y);
	float2 dotCenterScaled = float2(dotCenter.x * aspectRatio, dotCenter.y);

	// Round the dot by testing the distance of the pixel coordinate to the center
	float dist = length(uvScaled - dotCenterScaled) * dimensions;

	float4 insideColor = tex2D(data, dotCenter);

	float4 outsideColor = insideColor;
	outsideColor.r = 0;
	outsideColor.g = 0;
	outsideColor.b = 0;
	outsideColor.a = 0;

	float distFromEdge = dotSize - dist;  // positive when inside the circle
	float thresholdWidth = .22;  // a constant you'd tune to get the right level of softness
	float antialiasedCircle = saturate((distFromEdge / thresholdWidth) + 0.5);

	output = lerp(outsideColor, insideColor, antialiasedCircle);
}
