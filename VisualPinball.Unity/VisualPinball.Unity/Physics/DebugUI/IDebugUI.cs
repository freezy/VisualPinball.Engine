using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.DebugAndPhysicsComunicationProxy;

namespace VisualPinball.Unity.Physics.DebugUI
{
	public interface IDebugUINew : IEngine
	{
		void ManualBallRoller(Entity entity, float3 targetPosition);

		// ========================================================================== accesible from DebugUI ===
		bool GetFlipperState(Entity entity, out DebugFlipperState flipperState);
		float GetFloat(Params param);
		void SetFloat(Params param, float val);
	}
}
