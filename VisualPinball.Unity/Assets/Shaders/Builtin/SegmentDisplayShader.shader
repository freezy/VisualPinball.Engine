Shader "Visual Pinball/Segment Display Shader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}

		__NumChars ("Num Chars", Float) = 7
		__SegmentType ("Segment Type", Int) = 0
		__NumSegments ("Segments", Float) = 16

		__SeparatorType ("Separator Type", Int) = 2
		__SeparatorEveryThreeOnly ("Separator Every Three Only", Int) = 0
		__SeparatorPos ("Separator Position", Vector) = (1.5, 0.0, 0, 0)

		__LitColor ("Color", Color) = (1.0, 0.4, 0, 1.0)
		__SegmentWeight ("Weight", Float) = 0.05
		__SkewAngle ("Skew Angle", Float) = 0.2
		__Padding ("Padding", Vector) = (0.4, 0.15, 0, 0)
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
			#include "SegmentDisplayShader.hlsl"

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

			float __NumChars;
			int __SegmentType;
			int __NumSegments;

			int __SeparatorType;
			int __SeparatorEveryThreeOnly;
			float2 __SeparatorPos;

			fixed4 __LitColor;
			float __SegmentWeight;
			float __SkewAngle;
			float2 __Padding;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float pixelAlpha;
				SegmentDisplay_float(i.uv, _MainTex, __SegmentType, __NumChars, __NumSegments,
					__SeparatorType, __SeparatorEveryThreeOnly, __SeparatorPos, __SegmentWeight,
					__SkewAngle, __Padding, pixelAlpha);

				return pixelAlpha * __LitColor;
			}

			ENDCG
		}
	}
}
