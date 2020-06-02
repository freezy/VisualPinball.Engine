using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.DebugAndPhysicsComunicationProxy;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.DebugUI
{
	public interface IDebugUINew : IEngine
	{

		void Init(TableBehavior tableBehavior);

		void OnPhysicsUpdate(int numSteps, float elapsedTotalMilliseconds);

		void OnRegisterFlipper(Entity entity, string name);

		void OnCreateBall(Entity entity);

		// void ManualBallRoller(Entity entity, float3 targetPosition);
		//
		// // ========================================================================== accesible from DebugUI ===
		// bool GetFlipperState(Entity entity, out DebugFlipperState flipperState);
		// float GetFloat(Params param);
		// void SetFloat(Params param, float val);

	}
}
