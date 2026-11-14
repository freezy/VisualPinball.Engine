// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// Bakes the ambient occlusion of the playfield at export, see VpeOcclusionBaker. Pass 0 renders the occluders
// seen from below the playfield along one direction and keeps, per texel, the lowest distance along that
// direction; pass 1 rasterizes the playfield in its UV space and adds up how much each texel sees along it.
Shader "Hidden/VPE/OcclusionBake"
{
	Properties
	{
		_VpeAlphaTex ("Alpha", 2D) = "white" {}
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		CGINCLUDE
		#include "UnityCG.cginc"

		float4x4 _VpeLightVp;   // world -> the direction's orthographic clip space
		float4 _VpeDir;         // xyz: direction towards the sky
		float4 _VpeUp;          // xyz: playfield normal, w: height of the playfield plane along it
		float4 _VpeParams;      // x: falloff distance, y: occlusion of transparent occluders, z: min receiver normal

		float4 ToTarget(float4 clip)
		{
			// Unity flips rendering into textures on Direct3D-like APIs; raw clip positions must do it themselves.
			#if UNITY_UV_STARTS_AT_TOP
			clip.y = -clip.y;
			#endif
			return clip;
		}
		ENDCG

		// 0: occluder distances along _VpeDir, min-blended into a float target cleared to a large value.
		Pass
		{
			Blend One One
			BlendOp Min
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			sampler2D _VpeAlphaTex;
			float4 _VpeAlphaTex_ST;
			float _VpeAlphaCutoff;  // < 0: opaque

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; };

			v2f vert(appdata v)
			{
				v2f o;
				o.world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
				o.pos = ToTarget(mul(_VpeLightVp, float4(o.world, 1)));
				o.uv = TRANSFORM_TEX(v.uv, _VpeAlphaTex);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				// Only what is above the playfield shades it.
				clip(dot(i.world, _VpeUp.xyz) - _VpeUp.w);
				if (_VpeAlphaCutoff >= 0) {
					clip(tex2D(_VpeAlphaTex, i.uv).a - _VpeAlphaCutoff);
				}
				return dot(i.world, _VpeDir.xyz).xxxx;
			}
			ENDCG
		}

		// 1: visibility along _VpeDir of each playfield texel, added up in its UV space (r: visibility, g: samples).
		Pass
		{
			Blend One One
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			Texture2D<float> _VpeOpaque;
			Texture2D<float> _VpeTransparent;
			float4 _VpeOpaque_TexelSize;
			float4 _VpeUvTransform; // xy: scale, zw: offset of the material UV
			float _VpeUvChannel;

			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; };
			struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };

			v2f vert(appdata v)
			{
				v2f o;
				o.world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
				o.normal = UnityObjectToWorldNormal(v.normal);
				float2 uv = (_VpeUvChannel > 0.5 ? v.uv1 : v.uv0) * _VpeUvTransform.xy + _VpeUvTransform.zw;
				o.pos = ToTarget(float4(uv * 2 - 1, 0.5, 1));
				return o;
			}

			float Occlusion(Texture2D<float> map, float2 uv, float receiver)
			{
				// 3x3 taps against the nearest occluder: the share that is closer than the falloff distance,
				// weighted by how close it is.
				uint width, height;
				map.GetDimensions(width, height);
				float2 texel = uv * float2(width, height) - 0.5;
				float occlusion = 0;
				[unroll] for (int y = -1; y <= 1; y++) {
					[unroll] for (int x = -1; x <= 1; x++) {
						int2 p = clamp(int2(floor(texel + 0.5)) + int2(x, y), int2(0, 0), int2(width - 1, height - 1));
						float distance = max(0, map.Load(int3(p, 0)) - receiver);
						occlusion += saturate(1 - distance / _VpeParams.x);
					}
				}
				return occlusion / 9;
			}

			float4 frag(v2f i) : SV_Target
			{
				// The board's sides and bottom share its UVs on some meshes; only its top receives.
				if (dot(normalize(i.normal), _VpeUp.xyz) < _VpeParams.z) discard;

				float4 clip = mul(_VpeLightVp, float4(i.world, 1));
				// With ToTarget's flip, row r of pass 0's target holds clip y = 2r/height - 1 on every API.
				float2 uv = clip.xy * 0.5 + 0.5;
				float receiver = dot(i.world, _VpeDir.xyz);
				float opaque = Occlusion(_VpeOpaque, uv, receiver);
				float transparent = Occlusion(_VpeTransparent, uv, receiver);
				float visibility = (1 - opaque) * (1 - transparent * _VpeParams.y);
				return float4(visibility, 1, 0, 0);
			}
			ENDCG
		}
	}
}
