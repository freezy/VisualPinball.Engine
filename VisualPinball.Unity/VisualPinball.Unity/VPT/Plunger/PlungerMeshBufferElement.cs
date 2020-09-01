using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(1)]
	internal struct PlungerMeshBufferElement : IBufferElementData
	{
		public float3 Value;

		public PlungerMeshBufferElement(float3 v)
		{
			Value = v;
		}
	}
}
