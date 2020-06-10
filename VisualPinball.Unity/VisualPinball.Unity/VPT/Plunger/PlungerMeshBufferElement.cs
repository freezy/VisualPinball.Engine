using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Plunger
{
	[InternalBufferCapacity(1)]
	public struct PlungerMeshBufferElement : IBufferElementData
	{
		public float3 Value;

		public PlungerMeshBufferElement(float3 v)
		{
			Value = v;
		}
	}
}
