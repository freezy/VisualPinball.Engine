﻿Shader "Visual Pinball/DMD Shader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Width ("Width", Float) = 128
		_Height ("Height", Float) = 32
		_Size ("Dot Size", Float) = 1.25
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

			float _Width;
			float _Height;
			float _Size;

			float4 FilterColor;
			float _IsMonochrome;

			// Static computed vars for optimization
			static float AspectRatio = _Width / _Height;
			static float2 Dimensions = float2(_Width, _Height);
			static float2 DimensionsPerDot = 1.0f / Dimensions;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			float4 setMonochrome(float4 color) : COLOR
			{
				float4 monochrome = color;
				if (((int)_IsMonochrome) == 1)
				{
					float3 rgb = color.rgb;
					float3 luminance = dot(rgb, float3(0.30, 0.59, 0.11));
					monochrome = float4(luminance * FilterColor.rgb, color.a);
				}
				return monochrome;
			}

			float4 setDmd (float2 uv, sampler2D samp) : COLOR
			{
				// Calculate dot center
				float2 dotPos = floor(uv * Dimensions);
				float2 dotCenter = dotPos * DimensionsPerDot + DimensionsPerDot * 0.5;

				// Scale coordinates back to original ratio for rounding
				float2 uvScaled = float2(uv.x * AspectRatio, uv.y);
				float2 dotCenterScaled = float2(dotCenter.x * AspectRatio, dotCenter.y);

				// Round the dot by testing the distance of the pixel coordinate to the center
				float dist = length(uvScaled - dotCenterScaled) * Dimensions;

				float4 insideColor = tex2D(samp, dotCenter);

				float4 outsideColor = insideColor;
				outsideColor.r = 0;
				outsideColor.g = 0;
				outsideColor.b = 0;
				outsideColor.a = 1;

				float distFromEdge = _Size - dist;  // positive when inside the circle
				float thresholdWidth = .22;  // a constant you'd tune to get the right level of softness
				float antialiasedCircle = saturate((distFromEdge / thresholdWidth) + 0.5);

				return lerp(outsideColor, insideColor, antialiasedCircle);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				float4 DMD = setDmd(i.uv, _MainTex);
				DMD = setMonochrome(DMD);
				return DMD;
			}
			ENDCG
		}
	}
}
