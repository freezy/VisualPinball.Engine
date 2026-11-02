Shader "VisualPinball/CabinetOutput"
{
	Properties { _MainTex ("Source", 2D) = "black" {} }
	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float4 _Content;
			float _Encoded, _Alpha, _FlipY;
			float3 Decode(float3 value) { return float3(GammaToLinearSpaceExact(value.r), GammaToLinearSpaceExact(value.g), GammaToLinearSpaceExact(value.b)); }
			float3 Encode(float3 value) { return float3(LinearToGammaSpaceExact(value.r), LinearToGammaSpaceExact(value.g), LinearToGammaSpaceExact(value.b)); }
			float4 frag(v2f_img input) : SV_Target
			{
				float2 uv = (input.uv - _Content.xy) / _Content.zw;
				if (any(uv < 0) || any(uv > 1)) return float4(0, 0, 0, 1);
				if (_FlipY > .5) uv.y = 1 - uv.y;
				uv = clamp(uv, abs(_MainTex_TexelSize.xy) * .5, 1 - abs(_MainTex_TexelSize.xy) * .5);
				float4 color = tex2D(_MainTex, uv);
				float alpha = _Alpha < .5 ? 1 : saturate(color.a);
				// Unpremultiply in the producer's encoding before converting to linear light.
				if (_Alpha > 1.5) color.rgb = alpha > .00001 ? color.rgb / alpha : 0;
				float3 light = _Encoded > .5 ? Decode(color.rgb) : color.rgb;
				// Cabinet endpoints are opaque: compose over black, then encode exactly once.
				return float4(Encode(max(0, light * alpha)), 1);
			}
			ENDCG
		}
	}
}
