﻿using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperHitData : IComponentData
	{
		public bool LastHitFace;
		public float2 HitVelocity;
		public bool HitMomentBit;
		public float2 ZeroAngNorm;
	}
}
