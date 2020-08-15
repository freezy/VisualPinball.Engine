using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(1)]
	public struct PlungerUvBufferElement : IBufferElementData
	{
		public float2 Value;

		public PlungerUvBufferElement(float2 v)
		{
			Value = v;
		}
	}
}
