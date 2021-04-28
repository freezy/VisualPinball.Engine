Shader "Visual Pinball/Dot Matrix Display Shader"
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
			#include "Srp/Display/DotMatrixDisplayShader.hlsl"

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

			fixed4 frag (v2f i) : SV_Target
			{
				float2 dotCenter;
				SamplePosition_float(i.uv, Dimensions, dotCenter);

				float4 pixelColor = tex2D(_MainTex, dotCenter);

				float4 outColor;
				RoundDot_float(i.uv, Dimensions, _Size, pixelColor, dotCenter, outColor);

				return outColor;
			}
			ENDCG
		}
	}
}
