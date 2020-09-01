﻿using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The ball's last positions
	/// </summary>
	[InternalBufferCapacity(BallRingCounterSystem.MaxBallTrailPos)]
	internal struct BallLastPositionsBufferElement : IBufferElementData
	{
		public float3 Value;
	}
}
