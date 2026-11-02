// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT

Shader "VisualPinball/CabinetOutput"
{
	Properties { _MainTex ("Source", 2D) = "black" {} }
	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _SourceTexel;
			float4 _Content, _Viewport, _Mirror;
			float _Encoded, _Alpha, _FlipY, _Rotation;
			float3 Decode(float3 value) { return float3(GammaToLinearSpaceExact(value.r), GammaToLinearSpaceExact(value.g), GammaToLinearSpaceExact(value.b)); }
			float3 Encode(float3 value) { return float3(LinearToGammaSpaceExact(value.r), LinearToGammaSpaceExact(value.g), LinearToGammaSpaceExact(value.b)); }
			float4 frag(v2f_img input) : SV_Target
			{
				float2 viewportUv = (input.uv - _Viewport.xy) / _Viewport.zw;
				if (any(viewportUv < 0) || any(viewportUv > 1)) return float4(0, 0, 0, 0);
				float2 uv = (input.uv - _Content.xy) / _Content.zw;
				if (any(uv < 0) || any(uv > 1)) return float4(0, 0, 0, 1);
				if (_Mirror.x > .5) uv.x = 1 - uv.x;
				if (_Mirror.y > .5) uv.y = 1 - uv.y;
				if (_Rotation > 2.5) uv = float2(uv.y, 1 - uv.x);
				else if (_Rotation > 1.5) uv = 1 - uv;
				else if (_Rotation > .5) uv = float2(1 - uv.y, uv.x);
				if (_FlipY > .5) uv.y = 1 - uv.y;
				uv = clamp(uv, abs(_SourceTexel.xy) * .5, 1 - abs(_SourceTexel.xy) * .5);
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
