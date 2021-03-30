Shader "Visual Pinball/Alphanumeric Shader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}

		_Color ("Color", Color) = (1.0, 0.9, 0, 1.0)
		_SegmentWidth ("SegmentWidth", Float) = 0.07
		_TargetWidth ("TargetWidth", Float) = 1280
		_TargetHeight ("TargetHeight", Float) = 120
		_NumLines ("NumLines", Float) = 1
		_NumChars ("NumChars", Float) = 7
		_NumSegments ("NumSegments", Float) = 16
		_SkewAngle ("SkewAngle", Float) = 0.2

		_InnerPaddingX ("InnerPaddingX", Float) = 0.5
		_InnerPaddingY ("InnerPaddingY", Float) = 0.4
		_OuterPaddingX ("OuterPaddingX", Float) = 0.2
		_OuterPaddingY ("OuterPaddingY", Float) = 0.1
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			fixed4 _Color;
			float _SegmentWidth;
			float _Height;
			float _TargetWidth;
			float _TargetHeight;
			float _NumLines;
			float _NumChars;
			float _NumSegments;
			float _SkewAngle;
			float _InnerPaddingX;
			float _InnerPaddingY;
			float _OuterPaddingX;
			float _OuterPaddingY;

			static float SegmentGap = _SegmentWidth * 1.2;

			static float EdgeBlur = 0.1; // used to remove aliasing
			static float SharpEdge = 0.7;
			static float RoundEdge = 0.15;

			static float On = 0.81;
			static float Off = 0.012;

			// Static computed vars for optimization
			static float2 tl = float2(-.5, 1) ; // top    left  corner
			static float2 tr = float2(.5, 1);   // top    right corner
			static float2 ml = float2(-.5, 0);  // mid    left  corner
			static float2 mr = float2(.5, 0);   // mid    right corner
			static float2 bl = float2(-.5, -1); // bottom left  corner
			static float2 br = float2(.5, -1);  // bottom right corner
			static float2 tm = float2(.0, 1);
			static float2 mm = float2(.0, 0);   // middle
			static float2 bm = float2(.0, -1);

			static float2 dtl = tl + float2(0.0, -_SegmentWidth);
			static float2 dtr = tr + float2(0.0, -_SegmentWidth);
			static float2 dtm = mm + float2(0.0, _SegmentWidth);
			static float2 dbm = mm + float2(0.0, -_SegmentWidth);
			static float2 dbl = bl + float2(0.0, _SegmentWidth);
			static float2 dbr = br + float2(0.0, _SegmentWidth);

			static float2 dp = br + float2(_SegmentWidth * 4.0, SegmentGap);

			static float2 resolution = float2(_TargetWidth, _TargetHeight);

			static float2 outerPadding = float2(_OuterPaddingX * resolution.y / resolution.x, _OuterPaddingY);
			static float2 innerPadding = float2(_InnerPaddingX * resolution.y / resolution.x, _InnerPaddingY);

			static float2 cellSize = float2(
				1. / _NumChars + innerPadding.x,
				1. / _NumLines * 2. + _SegmentWidth * 2.
			);

			static float2 originPos = float2(
				-.5 + cellSize.x / 2. - innerPadding.x / 2. + outerPadding.x,
				_SegmentWidth + outerPadding.y
			);

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

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
				float2 v = abs(pa - ba * t) / _SegmentWidth * 0.5;

				return Rounder2(1 - v.x, 1 - v.y - v.x);
			}

			float LongLine2(float2 a, float2 b, float2 p)
			{
				float2 pa = p - float2(a.x, -a.y);
				float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
				float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap, 1.0 - SegmentGap);
				float2 v = abs(pa - ba * t) / _SegmentWidth * 0.5;

				return Rounder2(1 - v.x, 1 - v.y - v.x);
			}

			float ShortLine(float2 a, float2 b, float2 p)
			{
				float2 pa = p - float2(a.x, -a.y);
				float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
				float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap * 2.0, 1.0 - SegmentGap * 2.0);
				float2 v = abs(pa - ba * t) / _SegmentWidth * 0.5;

				return Rounder2(1 - v.x, 1 - v.y - v.x);
			}

			float MidLine(float2 a, float2 b, float2 p)
			{
				float2 pa = p - float2(a.x, -a.y);
				float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
				float t = clamp(dot(pa, ba) / dot(ba, ba), SegmentGap * 1.1, 1.0 - SegmentGap * 1.1);
				float2 v = abs(pa - ba * t) / _SegmentWidth * 0.5;

				return Rounder2(1 - v.x, 1 - v.y - v.x);
			}

			float DiagLine(float2 a, float2 b, float2 p)
			{
				float2 pa = p - float2(a.x, -a.y);
				float2 ba = float2(b.x, -b.y) - float2(a.x, -a.y);
				float t = pa.x / ba.x;
				float2 intersectP = abs(pa - ba * t);
				float xl = clamp(1 - (p.x - a.x + SegmentGap + _SegmentWidth) / _SegmentWidth * 0.5, -9999, 1);
				float xr = clamp(0 + (p.x - b.x - SegmentGap + _SegmentWidth) / _SegmentWidth * 0.5, -9999, 1);

				float t2 = pa.y / ba.y;
				float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
				float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);

				return Rounder(
					1 - intersectP.y / (_SegmentWidth * 3.0),
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
				float xr = clamp(0 + (p.x - a.x - SegmentGap + _SegmentWidth) / _SegmentWidth * 0.5, -9999, 1);
				float xl = clamp(1 - (p.x - b.x + SegmentGap + _SegmentWidth) / _SegmentWidth * 0.5, -9999, 1);

				float t2 = pa.y / ba.y;
				float yu = clamp(t2 + 1.0 - SegmentGap * 2, -9999, 1);
				float yd = clamp(2 - t2 - SegmentGap * 2, -9999, 1);

				return Rounder(
					1 - intersectP.y / (_SegmentWidth * 3.0),
					xl * xr,
					(yd * yu) / SegmentGap * 0.5 - 4.0
				);
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

			bool ShowSeg(int charIndex, int segIndex)
			{
				float2 d = float2(1. / _NumSegments, 1. / _NumChars);
				float2 pos = float2(float(segIndex), float(charIndex));
				float4 pixel = tex2Dlod(_MainTex, float4(d.x * (pos.x + .5), d.y * (pos.y + .5), 0., 0.));
				if (pixel.b > .5) {
					return true;
				}
				return false;
			}

			float3 SegDisp(int charIndex, float2 p)
			{
				float3 r = (0.);
				p.x -= p.y * _SkewAngle;

				r = Combine(r, MidLine(tl, tr, p), ShowSeg(charIndex, 0));
				r = Combine(r, LongLine(tr, mr, p), ShowSeg(charIndex, 1));
				r = Combine(r, LongLine(mr, br, p), ShowSeg(charIndex, 2));
				r = Combine(r, MidLine(br, bl, p), ShowSeg(charIndex, 3));
				r = Combine(r, LongLine(bl, ml, p), ShowSeg(charIndex, 4));
				r = Combine(r, LongLine(ml, tl, p), ShowSeg(charIndex, 5));
				r = Combine(r, ShortLine(mm, ml, p), ShowSeg(charIndex, 6));
				r = Combine(r, ShortLine(dp - float2(SegmentGap * 0.9, SegmentGap), dp - float2(SegmentGap * 1.0, SegmentGap), p), ShowSeg(charIndex, 7));
				r = Combine(r, DiagLine2(dtl, dtm, p), ShowSeg(charIndex, 8));
				r = Combine(r, LongLine2(tm, mm, p), ShowSeg(charIndex, 9));
				r = Combine(r, DiagLine(dtr, dtm, p), ShowSeg(charIndex, 10));
				r = Combine(r, ShortLine(mm, mr, p), ShowSeg(charIndex, 11));
				r = Combine(r, DiagLine2(dbm, dbr, p), ShowSeg(charIndex, 12));
				r = Combine(r, LongLine(mm, bm, p), ShowSeg(charIndex, 13));
				r = Combine(r, DiagLine(dbm, dbl, p), ShowSeg(charIndex, 14));

				return r;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = float2(
					(i.uv.x * (1. + (_NumChars - 1.) * innerPadding.x + 4.0 * outerPadding.x) - 0.5 - outerPadding.x),
					((i.uv.y * 2.) * (1. + _SegmentWidth + 2. * outerPadding.y) - 1. - outerPadding.y)
				);

				float2 pos = originPos;
				float3 d = (0.);

				int charIndex = 0;
				float2 f = float2(_NumChars * (1. + innerPadding.x), _NumLines);
				for (int currLine = 0; currLine < 2; currLine++) {
					for (int character = 0; character < 20; character++) {

						d += SegDisp(charIndex, (uv - pos) * f);
						pos.x += cellSize.x;
						charIndex++;

						if (character >= _NumChars - 1.) {
							break;
						}
					}
					pos.x = originPos.x;
					pos.y -= cellSize.y;
					if (character >= _NumLines - 1.) {
						break;
					}
				}

				return d.r * _Color;
			}

			ENDCG
		}
	}
}
