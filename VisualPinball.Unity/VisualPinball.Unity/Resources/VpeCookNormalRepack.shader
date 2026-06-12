// Repacks a plain-RGB tangent-space normal map into HDRP's AG layout (1, y, 1, x) during the
// texture cook's blit, so huge normal maps don't need a CPU pixel pass. x = r * a covers both
// plain RGB sources (a=1) and pre-swizzled data.
Shader "Hidden/VPE/CookNormalRepack"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;

			struct Attributes
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float4 normalSample = tex2D(_MainTex, input.uv);
				return float4(1.0, normalSample.g, 1.0, normalSample.r * normalSample.a);
			}
			ENDHLSL
		}
	}
}
