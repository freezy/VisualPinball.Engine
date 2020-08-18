using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A swappable engine that implements VPE's rigid body physics.
	/// </summary>
	public interface IPhysicsEngine : IEngine
	{
		/// <summary>
		/// Initialize and enable the engine.
		/// </summary>
		/// <remarks>
		/// All engines are instantiated, but they should only activate
		/// themselves when this method is called.
		/// </remarks>
		/// <param name="tableAuthoring"></param>
		void Init(TableAuthoring tableAuthoring);

		/// <summary>
		/// Create a new ball and returns its entity.
		/// </summary>
		/// <param name="mesh">Ball mesh</param>
		/// <param name="material">Material to use with the mesh</param>
		/// <param name="worldPos">Position in world space</param>
		/// <param name="localPos">Position in local space</param>
		/// <param name="localVel">Velocity in local space</param>
		/// <param name="scale">Scale relative to ball mesh</param>
		/// <param name="mass">Physics mass</param>
		/// <param name="radius">Radius in local space</param>
		void BallCreate(Mesh mesh, Material material, in float3 worldPos, in float3 localPos, in float3 localVel,
			in float scale, in float mass, in float radius);

		/// <summary>
		/// Rolls the ball manually to a position on the playfield.
		/// </summary>
		/// <param name="ballEntity">Ball entity</param>
		/// <param name="targetWorldPosition">New position in world space</param>
		void BallManualRoll(in Entity ballEntity, in float3 targetWorldPosition);

		/// <summary>
		/// Rotate the flipper "up" (button pressed)
		/// </summary>
		/// <param name="entity"></param>
		void FlipperRotateToEnd(in Entity entity);

		/// <summary>
		/// Rotate the flipper "down" (button released)
		/// </summary>
		/// <param name="entity"></param>
		void FlipperRotateToStart(in Entity entity);

		/// <summary>
		/// Returns a simplified version of all flipper states for the
		/// debug UI to deal with.
		/// </summary>
		/// <returns>All flipper states</returns>
		DebugFlipperState[] FlipperGetDebugStates();

		/// <summary>
		/// Returns which sliders for flippers the debug UI should display.
		/// </summary>
		DebugFlipperSlider[] FlipperGetDebugSliders();

		/// <summary>
		/// Updates a flipper property during gameplay.
		/// </summary>
		/// <param name="param">Which property to update</param>
		/// <param name="v">New value</param>
		void SetFlipperDebugValue(DebugFlipperSliderParam param, float v);

		/// <summary>
		/// Retrieves a flipper property during gameplay.
		/// </summary>
		/// <param name="param">Which property to retrieve</param>
		/// <returns>Value of the property</returns>
		float GetFlipperDebugValue(DebugFlipperSliderParam param);
	}
}
