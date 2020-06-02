using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.Engine
{
	public interface IPhysicsEngineNew : IEngine
	{
		void Init(TableBehavior tableBehavior);

		DebugFlipperState[] GetDebugFlipperStates();

		Entity CreateBall(Mesh mesh, Material material, in float3 worldPos, in float3 localPos, in float3 localVel,
			in float scale, in float mass, in float radius);

		void FlipperRotateToEnd(in Entity entity);

		void FlipperRotateToStart(in Entity entity);

		void ManualBallRoller(in Entity ballEntity, in float3 targetWorldPosition);
	}
}
