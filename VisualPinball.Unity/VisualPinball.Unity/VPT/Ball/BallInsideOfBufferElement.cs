using Unity.Entities;

namespace VisualPinball.Unity
{
	/// <summary>
	/// List of triggers and kickers the ball is now inside
	/// </summary>
	/// <remarks>It's what VPX calls `m_vpVolObjs`</remarks>
	[InternalBufferCapacity(0)]
	internal struct BallInsideOfBufferElement : IBufferElementData
	{
		public Entity Value;
	}
}
