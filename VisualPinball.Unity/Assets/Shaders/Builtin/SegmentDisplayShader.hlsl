// this shader is identical to the one in ../Srp, the only
// difference is that it uses sampler2D instead of UnityTexture2D.

float _SegmentWeight;
float _Height;
float _NumChars;
float _NumSegments;
float _SkewAngle;
float _HorizontalMiddle;

float2 _SeparatorPos;
int _SeparatorType; // 0 = none, 1 = dot, 2 = 2-segment comma
int _SeparatorEveryThreeOnly;

static float SegmentGap;

static float EdgeBlur = 0.1; // used to remove aliasing
static float SharpEdge = 0.7;
static float RoundEdge = 0.15;

static float On = 1.0;
static float Off = 0.0;

// Static computed vars for optimization
static float2 tl = float2(-.5, 1) ; // top    left  corner
static float2 tr = float2(.5, 1);   // top    right corner
static float2 ml = float2(-.5, 0);  // mid    left  corner
static float2 mr = float2(.5, 0);   // mid    right corner
static float2 bl = float2(-.5, -1); // bottom left  corner
static float2 br = float2(.5, -1);  // bottom right corner
static float2 tm;
static float2 mm; // middle
static float2 bm;

static float2 dtl;
static float2 dtr;
static float2 dtm;
static float2 dbm;
static float2 dbl;
static float2 dbr;
static float2 dp;

float4 SplitAndBlur(float v)
{
	float be = clamp(EdgeBlur, 0, 9999);
	float re = clamp(RoundEdge, 0, 9999);
	float edge = SharpEdge - re;
	float e = SharpEdge - RoundEdge;

	float r = smoothstep(e - be, e + be, v);
	float g = smoothstep(e - be, e - be, v);
	float b = smoothstep(-9999, e + be, v);
	g -= r;
	return float4(r, g, b, 1);
}

float Rounder(float x, float y, float z)
{
	if (z < 0) {
		x = lerp(min(x, z), x, clamp(1 + z, 0, 1));
	}
	float d = min(x, y);

	if (x < SharpEdge) {
		x = SharpEdge - x;
		y = SharpEdge - y;
		x = clamp(x, 0, 9999);
		y = clamp(y, 0, 9999);
		d = SharpEdge - length(float2(x, y));

	} else if (y < SharpEdge) {
		x = SharpEdge - x;
		y = SharpEdge - y;
		x = clamp(x, 0, 9999);
		y = clamp(y, 0, 9999);
		d = SharpEdge - length(float2(x, y));
	}
	return d;
}

float Rounder2(float x, float y)
{
	float d = y;
	if (d < SharpEdge && x > y) {
		float a = x - y;
		float b = SharpEdge - x;
		if (x < SharpEdge && a < b) {
			d = SharpEdge - length(float2(a, b));

		} else if (SharpEdge - 1 + x - y > 1 - x) {
			a = SharpEdge - 1 + a;
			b = 1 - x;
			d = SharpEdge - length(float2(a, b));

		} else {
			d = SharpEdge + (d - SharpEdge) * 0.70710678118654752440084436210485;
		}
	}
	return d;
}

float Manhattan(float2 v)
{
	return abs(v.x) + abs(v.y);
}

float DiagDist(float2 v)
{
	return abs(v.x);
}

float LongLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap, 1.0 - SegmentGap);
	float2 v = abs(pa - ba * t) / _SegmentWeight * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float LongLine2(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap, 1.0 - SegmentGap);
	float2 v = abs(pa - ba * t) / _SegmentWeight * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float ShortLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap * 2.0, 1.0 - SegmentGap * 2.0);
	float2 v = abs(pa - ba * t) / _SegmentWeight * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float MidLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap * 1.1, 1.0 - SegmentGap * 1.1);
	float2 v = abs(pa - ba * t) / _SegmentWeight * 0.5;

	return Rounder2(1 - v.x, 1 - v.y - v.x);
}

float DiagLine(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = pa.x / ba.x;
	float2 intersectP = abs(pa - ba * t);
	float xl = clamp(1 - (p.x - a.x + SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);
	float xr = clamp(0 + (p.x - b.x - SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);

	float t2 = pa.y / ba.y;
	float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
	float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);

	return Rounder(
		1 - intersectP.y / (_SegmentWeight * 4.0),
		xl * xr,
		(yd * yu) / SegmentGap * 0.5 - 4.0
	);
}

float DiagLine2(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = pa.x / ba.x;
	float2 intersectP = abs(pa - ba * t);
	float xr = clamp(0 + (p.x - a.x - SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);
	float xl = clamp(1 - (p.x - b.x + SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);

	float t2 = pa.y / ba.y;
	float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
	float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);

	return Rounder(
		1 - intersectP.y / (_SegmentWeight * 4.0),
		xl * xr,
		(yd * yu) / SegmentGap * 0.5 - 4.0
	);
}

float DiagLine3(float2 a, float2 b, float2 p)
{
	float2 pa = p - float2(a.x, -a.y);
	float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
	float t = pa.x / ba.x;
	float2 intersectP = abs(pa - ba * t);
	float xl = clamp(1 - (p.x - a.x + SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);
	float xr = clamp(0 + (p.x - b.x - SegmentGap + _SegmentWeight) / _SegmentWeight * 0.5, -9999, 1);

	float t2 = pa.y / ba.y;
	float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
	float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);

	return Rounder(
		1 - intersectP.y / (_SegmentWeight * 3.0),
		xl * xr,
		(yd * yu) / SegmentGap * 0.5 - 4.0
	);
}

float2 Translate(float2 coord, float2 translate) {
	return coord - translate;
}

float Circle(float2 _st, float _radius, float smooth)
{
	_st.x += _st.y * -_SkewAngle; // un-angle
	_st.x += _SkewAngle * 0.5; // un-angle
	_st.y *= 0.9; // unstretch
	float2 dist = _st - float2(0.5, 0.5);
	return 1. - smoothstep(
		_radius - (_radius * smooth),
		_radius + (_radius * smooth),
		dot(dist, dist) * 4.0
	);
}

float Comma(float2 uv)
{
	if (uv.y < 0.61) {
		return 0.;
	}

	float c = Circle(Translate(uv, float2(-0.15, 0.1)), 0.15, .7);   // plus
	c *= 1. - Circle(Translate(uv, float2(-0.01, 0.05)), 0.01, 2.5); // minus top
	c *= 1. - Circle(Translate(uv, float2(-0.42, .13)), 0.48, .3);   // minus left
	return c;
}

float3 Combine(float3 accu, float val, bool showSeg)
{
	float lev = (showSeg ? 1. : 0.) * (On - Off) + Off;
	float4 v = SplitAndBlur(val);
	v.a *= lev;

	return float3(
		max(accu.r, v.r * lev),
		length(float2(accu.g, v.g * lev)),
		length(float2(accu.b, v.b * v.a))
	);
}

bool ShowSeg(sampler2D data, int charIndex, int segIndex)
{
	float2 d = float2(1. / 16, 1. / _NumChars);
	float2 pos = float2(float(segIndex), float(charIndex));
	float4 pixel = tex2Dlod(data, float4(d.x * (pos.x + .5), d.y * (pos.y + .5), 0., 0.));
	if (pixel.b > .5) {
		return true;
	}
	return false;
}

float3 SegDispSeparator(sampler2D data, int charIndex, int segIndex, float2 p, float3 r)
{
	bool isThree = fmod(_NumChars - charIndex + 2, 3) == 0 && charIndex != _NumChars - 1;
	bool separatorEveryThreeOnly = _SeparatorEveryThreeOnly == 1;
	float2 pos = float2(-0.5, 0.43);

	if (!separatorEveryThreeOnly || isThree) {

		switch (_SeparatorType) {
			case 0:
				return r;
			case 1:
				r = Combine(r, Circle(p - pos - _SeparatorPos / 2., 0.025, 1.5), ShowSeg(data, charIndex, segIndex));
				break;
			case 2:
				r = Combine(r, Circle(p - pos - _SeparatorPos / 2., 0.025, 1.5), ShowSeg(data, charIndex, segIndex));
				r = Combine(r, Comma(p - pos - _SeparatorPos / 2.), ShowSeg(data, charIndex, segIndex));
				break;
		}
	}
	return r;
}

float3 SegDisp7(sampler2D data, int charIndex, float2 p, float3 r)
{
	r = Combine(r, MidLine(tl, tr, p), ShowSeg(data, charIndex, 0));
	r = Combine(r, LongLine(tr, mr, p), ShowSeg(data, charIndex, 1));
	r = Combine(r, LongLine(mr, br, p), ShowSeg(data, charIndex, 2));
	r = Combine(r, MidLine(br, bl, p), ShowSeg(data, charIndex, 3));
	r = Combine(r, LongLine(bl, ml, p), ShowSeg(data, charIndex, 4));
	r = Combine(r, LongLine(ml, tl, p), ShowSeg(data, charIndex, 5));
	r = Combine(r, MidLine(mr, ml, p), ShowSeg(data, charIndex, 6));
	r = SegDispSeparator(data, charIndex, 7, p, r);

	return r;
}

float3 SegDisp9(sampler2D data, int charIndex, float2 p, float3 r)
{
	r = Combine(r, MidLine(tl, tr, p), ShowSeg(data, charIndex, 0));
	r = Combine(r, LongLine(tr, mr, p), ShowSeg(data, charIndex, 1));
	r = Combine(r, LongLine(mr, br, p), ShowSeg(data, charIndex, 2));
	r = Combine(r, MidLine(br, bl, p), ShowSeg(data, charIndex, 3));
	r = Combine(r, LongLine(bl, ml, p), ShowSeg(data, charIndex, 4));
	r = Combine(r, LongLine(ml, tl, p), ShowSeg(data, charIndex, 5));
	r = Combine(r, MidLine(mr, ml, p), ShowSeg(data, charIndex, 6));
	r = SegDispSeparator(data, charIndex, 7, p, r);
	r = Combine(r, DiagLine3(dtr, dtm, p), ShowSeg(data, charIndex, 8));
	r = Combine(r, DiagLine3(dbm, dbl, p), ShowSeg(data, charIndex, 9));

	return r;
}

float3 SegDisp14(sampler2D data, int charIndex, float2 p, float3 r)
{
	r = Combine(r, MidLine(tl, tr, p), ShowSeg(data, charIndex, 0));
	r = Combine(r, LongLine(tr, mr, p), ShowSeg(data, charIndex, 1));
	r = Combine(r, LongLine(mr, br, p), ShowSeg(data, charIndex, 2));
	r = Combine(r, MidLine(br, bl, p), ShowSeg(data, charIndex, 3));
	r = Combine(r, LongLine(bl, ml, p), ShowSeg(data, charIndex, 4));
	r = Combine(r, LongLine(ml, tl, p), ShowSeg(data, charIndex, 5));
	r = Combine(r, ShortLine(mm, ml, p), ShowSeg(data, charIndex, 6));
	r = SegDispSeparator(data, charIndex, 7, p, r);
	r = Combine(r, DiagLine2(dtl, dtm, p), ShowSeg(data, charIndex, 8));
	r = Combine(r, LongLine2(tm, mm, p), ShowSeg(data, charIndex, 9));
	r = Combine(r, DiagLine(dtr, dtm, p), ShowSeg(data, charIndex, 10));
	r = Combine(r, ShortLine(mm, mr, p), ShowSeg(data, charIndex, 11));
	r = Combine(r, DiagLine2(dbm, dbr, p), ShowSeg(data, charIndex, 12));
	r = Combine(r, LongLine(mm, bm, p), ShowSeg(data, charIndex, 13));
	r = Combine(r, DiagLine(dbm, dbl, p), ShowSeg(data, charIndex, 14));

	return r;
}

float3 SegDisp16(sampler2D data, int charIndex, float2 p, float3 r)
{
	r = Combine(r, ShortLine(tl, tm, p), ShowSeg(data, charIndex, 0)); // a
	r = Combine(r, ShortLine(tm, tr, p), ShowSeg(data, charIndex, 1)); // b
	r = Combine(r, LongLine(tr, mr, p), ShowSeg(data, charIndex, 2));  // c
	r = Combine(r, LongLine(mr, br, p), ShowSeg(data, charIndex, 3));  // d
	r = Combine(r, ShortLine(br, bm, p), ShowSeg(data, charIndex, 4)); // e
	r = Combine(r, ShortLine(bm, bl, p), ShowSeg(data, charIndex, 5)); // f
	r = Combine(r, LongLine(bl, ml, p), ShowSeg(data, charIndex, 6));  // g
	r = Combine(r, LongLine(ml, tl, p), ShowSeg(data, charIndex, 7));  // h
	r = Combine(r, DiagLine2(dtl, dtm, p), ShowSeg(data, charIndex, 8)); // i
	r = Combine(r, LongLine2(tm, mm, p), ShowSeg(data, charIndex, 9));   // j
	r = Combine(r, DiagLine(dtr, dtm, p), ShowSeg(data, charIndex, 10)); // k
	r = Combine(r, ShortLine(mm, mr, p), ShowSeg(data, charIndex, 11));  // l
	r = Combine(r, DiagLine2(dbm, dbr, p), ShowSeg(data, charIndex, 12));// m
	r = Combine(r, LongLine(mm, bm, p), ShowSeg(data, charIndex, 13));   // n
	r = Combine(r, DiagLine(dbm, dbl, p), ShowSeg(data, charIndex, 14)); // o
	r = Combine(r, ShortLine(mm, ml, p), ShowSeg(data, charIndex, 15));   // p

	return r;
}

float3 SegDisp(sampler2D data, int charIndex, float2 p)
{
	float3 r = (0.);
	p.x -= p.y * -_SkewAngle;
	switch (_NumSegments) {
		case 7: return SegDisp7(data, charIndex, p, r);
		case 9: return SegDisp9(data, charIndex, p, r);
		case 14: return SegDisp14(data, charIndex, p, r);
		case 16: return SegDisp16(data, charIndex, p, r);
	}
	return r;
}

void SegmentDisplay_float(float2 coords, sampler2D data, float numChars, float numSegments,
	int separatorType, int separatorEveryThreeOnly, float2 separatorPos, float segmentWeight,
	float horizontalMiddle, float skewAngle, float2 padding, out float output)
{
	_SegmentWeight = segmentWeight;
	_NumChars = numChars;
	_NumSegments = numSegments;
	_SkewAngle = skewAngle;
	_HorizontalMiddle = horizontalMiddle;
	_SeparatorType = separatorType;
	_SeparatorEveryThreeOnly = separatorEveryThreeOnly;
	_SeparatorPos = separatorPos;

	SegmentGap = segmentWeight * 1.5;
	dtl = tl + float2(0.0, -segmentWeight);
	dtr = tr + float2(0.0, -segmentWeight);
	dtm = mm + float2(0.0 + _HorizontalMiddle, segmentWeight);
	dbm = mm + float2(0.0 + _HorizontalMiddle, -segmentWeight);
	dbl = bl + float2(0.0, segmentWeight);
	dbr = br + float2(0.0, segmentWeight);
	dp = br + float2(segmentWeight * 4.0, SegmentGap);
	mm = float2(.0 + _HorizontalMiddle, 0);   // middle
	tm = float2(.0 + _HorizontalMiddle, 1);
	bm = float2(.0 + _HorizontalMiddle, -1);


	float cellWidth = 1. / _NumChars;

	float2 originPos = float2(
		-.5 + cellWidth / 2.,
		0
	);

	float2 uv = float2(
		coords.x - 0.5,
		1 - (coords.y * 2.)
	);

	float2 pos = originPos;
	float3 d = (0.);

	float2 f = float2(numChars * (1. + padding.x + segmentWeight * 2.), 1. + (padding.y + segmentWeight));
	for (int character = 0; character < numChars; character++) {

		d += SegDisp(data, character, (uv - pos) * f);
		pos.x += cellWidth;

		if (character >= numChars - 1.) {
			break;
		}
	}

	output = d.r;
}
