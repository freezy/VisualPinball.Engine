// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct PhysicsState
	{
		internal PhysicsEnv Env;

		/// <summary>
		/// Our static octree.
		/// </summary>
		internal NativeOctree<int> Octree;

		/// <summary>
		/// All static colliders (the ones the original VPX physics engine supports).
		/// </summary>
		internal NativeColliders Colliders;

		/// <summary>
		/// All kinematic colliders, with their transformation applied for fully transformable colliders, and without for the others.
		///
		/// Fully transformable colliders are updated at <see cref="PhysicsState.TransformKinematicColliders"/>.
		///
		/// This is used for:
		///   - The hit test in the narrow phase (like <see cref="Colliders"/>)
		///   - Contact resolution in each cycle (like <see cref="Colliders"/>)
		///   - Collision (like <see cref="Colliders"/>)
		///
		/// This basically adds another step to the existing static and dynamic simulation. The oct tree comes from
		/// <see cref="KinematicCollidersAtIdentity"/>, and the narrow and collision phase is done with this. The mechanism
		/// for non-transformable colliders is the same, the only difference is that in <see cref="GetNonTransformableColliderMatrix"/>,
		/// the ball-to-item-space matrix is calculated based off <see cref="KinematicTransforms"/> instead of <see cref="_nonTransformableColliderTransforms"/>.
		///
		/// </summary>
		internal NativeColliders KinematicColliders;

		/// <summary>
		/// All kinematic colliders, without any transformation applied (for those fully transformable, the others aren't transformed anyway).
		///
		/// It's set in PhysicsEngine.Start() through ColliderReference.TransformToIdentity()
		///
		/// This is used for:
		///   - Transform fully-transformable colliders in PhysicsUpdate.Execute() with PhysicsKinematics.StepKinematics()
		///   - Computing the AABBs for the octree in PhysicsUpdateJob.Execute()
		/// </summary>
		internal NativeColliders KinematicCollidersAtIdentity;

		/// <summary>
		/// Maps an item ID to the target transformation matrix of kinematic items that have moved.
		/// The current pose (<see cref="KinematicTransforms"/>) is stepped toward this target
		/// inside the tick loop (<see cref="PhysicsKinematics.StepKinematics"/>), capped per tick,
		/// so a fast-moving collider can't skip past a ball within one frame.
		/// <para><c>int</c> is the <see cref="ColliderComponent{TData,TMainComponent}.ItemId"/> of the collider.</para>
		/// <para><c>float4x4</c> Target LocalToPlayfieldMatrixInVpx of the item.</para>
		/// </summary>
		internal NativeParallelHashMap<int, float4x4> KinematicTargetTransforms;

		/// <summary>
		/// The current LocalToPlayfieldMatrixInVpx of all kinematic colliders, fully transformable
		/// or not. While an item moves, this is stepped toward <see cref="KinematicTargetTransforms"/>
		/// per tick and matches the pose the colliders are actually baked at.
		/// </summary>
		/// <remarks>
		/// <para><c>int</c> is the <see cref="ColliderComponent{TData,TMainComponent}.ItemId"/> of the collider.</para>
		/// <para><c>float4x4</c> LocalToPlayfieldMatrixInVpx of the item.</para>
		/// </remarks>
		internal NativeParallelHashMap<int, float4x4> KinematicTransforms;

		/// <summary>
		/// The current velocities of all kinematic items that have moved, derived from their transform updates.
		/// </summary>
		/// <remarks>
		/// <para><c>int</c> is the <see cref="ColliderComponent{TData,TMainComponent}.ItemId"/> of the collider.</para>
		/// <para>Read during collision and contact resolution via <see cref="GetKinematicSurfaceVelocity"/>.</para>
		/// </remarks>
		internal NativeParallelHashMap<int, KinematicVelocityState> KinematicVelocities;

		/// <summary>
		/// The LUT of elasticity over velocity for all colliders where activated. Goes from 0 to 64 velocity units.
		/// </summary>
		internal NativeParallelHashMap<int, FixedList512Bytes<float>> ElasticityOverVelocityLUTs;

		/// <summary>
		/// The LUT of friction over velocity for all colliders where activated. Goes from 0 to 128 velocity units.
		/// </summary>
		internal NativeParallelHashMap<int, FixedList512Bytes<float>> FrictionOverVelocityLUTs;

		/// <summary>
		/// The LocalToPlayfieldMatrixInVpx of all colliders that aren't fully transformable.
		///
		/// This map is updated when the colliders get added at the beginning of the game with ColliderReference.Add().
		/// </summary>
		/// <remarks>
		/// <para><c>int</c> is the <see cref="ColliderComponent{TData,TMainComponent}.ItemId"/> of the collider.</para>
		/// <para><c>float4x4</c> LocalToPlayfieldMatrixInVpx of the item.</para>
		/// </remarks>
		private readonly NativeParallelHashMap<int, float4x4> _nonTransformableColliderTransforms;

		/// <summary>
		/// Maps an item ID to a list of collider IDs that reference this item, for all kinematic items.
		/// </summary>
		/// <remarks>
		/// Created by <see cref="ColliderReference.CreateLookup"/>.
		/// <para><c>int</c> is the <see cref="ColliderComponent{TData,TMainComponent}.ItemId"/> of the collider.</para>
		/// <para><c>NativeColliderIds</c> IDs of all colliders of the item referenced by `ItemId`.</para>
		/// </remarks>
		internal NativeParallelHashMap<int, NativeColliderIds> KinematicColliderLookups;

		internal NativeQueue<EventData>.ParallelWriter EventQueue;
		internal InsideOfs InsideOfs;
		internal NativeParallelHashMap<int, BallState> Balls;
		internal NativeParallelHashMap<int, BumperState> BumperStates;
		internal NativeParallelHashMap<int, DropTargetState> DropTargetStates;
		internal NativeParallelHashMap<int, FlipperState> FlipperStates;
		internal NativeParallelHashMap<int, GateState> GateStates;
		internal NativeParallelHashMap<int, HitTargetState> HitTargetStates;
		internal NativeParallelHashMap<int, KickerState> KickerStates;
		internal NativeParallelHashMap<int, MagnetState> MagnetStates;
		internal NativeParallelHashMap<int, PlungerState> PlungerStates;
		internal NativeParallelHashMap<int, SpinnerState> SpinnerStates;
		internal NativeParallelHashMap<int, SurfaceState> SurfaceStates;
		internal NativeParallelHashMap<int, TurntableState> TurntableStates;
		internal NativeParallelHashMap<int, TriggerState> TriggerStates;
		internal NativeParallelHashSet<int> DisabledCollisionItems;

		[MarshalAs(UnmanagedType.U1)]
		internal bool SwapBallCollisionHandling;

		public PhysicsState(ref PhysicsEnv env, ref NativeOctree<int> octree, ref NativeColliders colliders,
			ref NativeColliders kinematicColliders, ref NativeColliders kinematicCollidersAtIdentity,
			ref NativeParallelHashMap<int, float4x4> kinematicTransforms,
			ref NativeParallelHashMap<int, float4x4> kinematicTargetTransforms,
			ref NativeParallelHashMap<int, float4x4> nonTransformableColliderTransforms,
			ref NativeParallelHashMap<int, NativeColliderIds> kinematicColliderLookups, ref NativeQueue<EventData>.ParallelWriter eventQueue,
			ref InsideOfs insideOfs, ref NativeParallelHashMap<int, BallState> balls,
			ref NativeParallelHashMap<int, BumperState> bumperStates, ref NativeParallelHashMap<int, DropTargetState> dropTargetStates,
			ref NativeParallelHashMap<int, FlipperState> flipperStates, ref NativeParallelHashMap<int, GateState> gateStates,
			ref NativeParallelHashMap<int, HitTargetState> hitTargetStates, ref NativeParallelHashMap<int, KickerState> kickerStates,
			ref NativeParallelHashMap<int, MagnetState> magnetStates,
			ref NativeParallelHashMap<int, PlungerState> plungerStates, ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			ref NativeParallelHashMap<int, SurfaceState> surfaceStates, ref NativeParallelHashMap<int, TurntableState> turntableStates,
			ref NativeParallelHashMap<int, TriggerState> triggerStates,
			ref NativeParallelHashSet<int> disabledCollisionItems, ref bool swapBallCollisionHandling,
			ref NativeParallelHashMap<int, FixedList512Bytes<float>> elasticityOverVelocityLUTs,
			ref NativeParallelHashMap<int, FixedList512Bytes<float>> frictionOverVelocityLUTs,
			ref NativeParallelHashMap<int, KinematicVelocityState> kinematicVelocities)
		{
			Env = env;
			Octree = octree;
			Colliders = colliders;
			KinematicColliders = kinematicColliders;
			KinematicCollidersAtIdentity = kinematicCollidersAtIdentity;
			KinematicTransforms = kinematicTransforms;
			KinematicTargetTransforms = kinematicTargetTransforms;
			_nonTransformableColliderTransforms = nonTransformableColliderTransforms;
			KinematicColliderLookups = kinematicColliderLookups;
			EventQueue = eventQueue;
			InsideOfs = insideOfs;
			Balls = balls;
			BumperStates = bumperStates;
			DropTargetStates = dropTargetStates;
			FlipperStates = flipperStates;
			GateStates = gateStates;
			HitTargetStates = hitTargetStates;
			KickerStates = kickerStates;
			MagnetStates = magnetStates;
			PlungerStates = plungerStates;
			SpinnerStates = spinnerStates;
			SurfaceStates = surfaceStates;
			TurntableStates = turntableStates;
			TriggerStates = triggerStates;
			DisabledCollisionItems = disabledCollisionItems;
			SwapBallCollisionHandling = swapBallCollisionHandling;

			ElasticityOverVelocityLUTs = elasticityOverVelocityLUTs;
			FrictionOverVelocityLUTs = frictionOverVelocityLUTs;
			KinematicVelocities = kinematicVelocities;
		}

		internal ref ColliderHeader GetColliderHeader(ref NativeColliders colliders, int colliderId) => ref colliders.GetHeader(colliderId);
		internal ColliderType GetColliderType(ref NativeColliders colliders, int colliderId) => colliders.GetHeader(colliderId).Type;

		internal bool IsColliderActive(ref NativeColliders colliders, int colliderId) => !DisabledCollisionItems.Contains(colliders.GetItemId(colliderId));

		internal void EnableColliders(int itemId)
		{
			if (DisabledCollisionItems.Contains(itemId)) {
				DisabledCollisionItems.Remove(itemId);
			}
		}

		internal void DisableColliders(int itemId) {
			if (!DisabledCollisionItems.Contains(itemId)) {
				DisabledCollisionItems.Add(itemId);
			}
		}

		#region States

		internal ref FlipperState GetFlipperState(int colliderId, ref NativeColliders colliders) => ref FlipperStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref PlungerState GetPlungerState(int colliderId, ref NativeColliders colliders) => ref PlungerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref SpinnerState GetSpinnerState(int colliderId, ref NativeColliders colliders) => ref SpinnerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref TriggerState GetTriggerState(int colliderId, ref NativeColliders colliders) => ref TriggerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref KickerState GetKickerState(int colliderId, ref NativeColliders colliders) => ref KickerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal bool HasDropTargetState(int colliderId, ref NativeColliders colliders) => DropTargetStates.ContainsKey(colliders.GetItemId(colliderId));

		internal bool HasHitTargetState(int colliderId, ref NativeColliders colliders) => HitTargetStates.ContainsKey(colliders.GetItemId(colliderId));

		internal ref DropTargetState GetDropTargetState(int colliderId, ref NativeColliders colliders) => ref DropTargetStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref HitTargetState GetHitTargetState(int colliderId, ref NativeColliders colliders) => ref HitTargetStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref BumperState GetBumperState(int colliderId, ref NativeColliders colliders) => ref BumperStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref GateState GetGateState(int colliderId, ref NativeColliders colliders) => ref GateStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref SurfaceState GetSurfaceState(int colliderId, ref NativeColliders colliders) => ref SurfaceStates.GetValueByRef(colliders.GetItemId(colliderId));

		#endregion

		#region Transform

		/// <summary>
		/// Returns the matrix of a collider that cannot be transformed, i.e. the ball has to be projected into the
		/// collider's space.
		/// </summary>
		/// <remarks>
		/// Depending on whether the collider is kinematic or not, the matrix is taken from either
		/// the <see cref="KinematicTransforms"/> or <see cref="_nonTransformableColliderTransforms"/>.
		///
		/// Basically, this is the magic that makes kinematic transformations work for non-transformable
		/// items: Instead of projecting the ball with a static matrix, we'll just use the one of the
		/// kinematic colliders, which is updated every frame.
		/// </remarks>
		/// <param name="colliderId">ID of the collider</param>
		/// <param name="colliders">Collider references</param>
		/// <returns>Transformation matrix</returns>
		internal ref float4x4 GetNonTransformableColliderMatrix(int colliderId, ref NativeColliders colliders)
		{
			var itemId = colliders.GetItemId(colliderId);
			if (colliders.IsKinematic) {
				return ref KinematicTransforms.GetValueByRef(itemId);
			}
			return ref _nonTransformableColliderTransforms.GetValueByRef(itemId);
		}

		/// <summary>
		/// Returns the surface velocity of a kinematic collider at a given contact point,
		/// in the space the ball currently lives in. Returns zero for static colliders and
		/// for kinematic colliders that aren't currently moving.
		/// </summary>
		/// <remarks>
		/// This is what makes a moving kinematic collider impart momentum and friction to
		/// the ball: collision and contact resolution compute the ball's velocity relative
		/// to this velocity instead of treating the surface as static.
		///
		/// For non-transformed colliders, the ball (and thus the contact point) has been
		/// projected into the collider's local space, so the contact point is converted to
		/// playfield space to evaluate the velocity, and the result is rotated back into
		/// local space — consistent with <see cref="BallState.Transform"/>, which rotates
		/// the ball's velocity as well.
		/// </remarks>
		/// <param name="collEvent">The collision event, identifying the collider</param>
		/// <param name="contactPoint">The contact point, in the ball's current space</param>
		/// <returns>Surface velocity at the contact point, in the ball's current space</returns>
		internal float3 GetKinematicSurfaceVelocity(in CollisionEventData collEvent, in float3 contactPoint)
		{
			if (!collEvent.IsKinematic) {
				return float3.zero;
			}
			return GetKinematicSurfaceVelocity(ref KinematicColliders, collEvent.ColliderId, in contactPoint);
		}

		/// <summary>
		/// Same as <see cref="GetKinematicSurfaceVelocity(in CollisionEventData,in float3)"/>,
		/// but by collider id. Returns zero for non-kinematic collider sets.
		/// </summary>
		internal float3 GetKinematicSurfaceVelocity(ref NativeColliders colliders, int colliderId, in float3 position)
		{
			if (!colliders.IsKinematic) {
				return float3.zero;
			}
			var itemId = colliders.GetItemId(colliderId);
			if (!TryGetKinematicVelocity(itemId, out var linear, out var angular, out var pivot)) {
				return float3.zero;
			}

			if (colliders.IsTransformed(colliderId)) {
				return linear + math.cross(angular, position - pivot);
			}
			ref var matrix = ref KinematicTransforms.GetValueByRef(itemId);
			var velocityAtPosition = linear + math.cross(angular, matrix.MultiplyPoint(position) - pivot);
			return math.inverse(matrix).MultiplyVector(velocityAtPosition);
		}

		/// <summary>
		/// Velocity of a kinematic item at a given playfield-space point,
		/// including the step-rate substitution while the pose catch-up is
		/// rate-limited. Zero for unknown or resting items. Used by force
		/// fields (magnets) whose held balls move with the item.
		/// </summary>
		internal float3 GetKinematicVelocityAt(int itemId, in float3 position)
		{
			if (!TryGetKinematicVelocity(itemId, out var linear, out var angular, out var pivot)) {
				return float3.zero;
			}
			return linear + math.cross(angular, position - pivot);
		}

		private bool TryGetKinematicVelocity(int itemId, out float3 linear, out float3 angular, out float3 pivot)
		{
			if (!KinematicVelocities.TryGetValue(itemId, out var velocity)) {
				linear = float3.zero;
				angular = float3.zero;
				pivot = float3.zero;
				return false;
			}

			// while the pose catch-up is rate-limited (clamped stepping), the actual
			// step rate is the honest surface velocity and can exceed the derived one
			linear = velocity.LinearVelocity;
			angular = velocity.AngularVelocity;
			pivot = velocity.Pivot;
			if (math.lengthsq(velocity.StepVelocity) > math.lengthsq(linear)
			    || math.lengthsq(velocity.StepAngularVelocity) > math.lengthsq(angular)) {
				linear = velocity.StepVelocity;
				angular = velocity.StepAngularVelocity;
			}
			return math.lengthsq(linear) >= 1e-8f || math.lengthsq(angular) >= 1e-8f;
		}

		/// <summary>
		/// Transforms a collider with a given transformation matrix. The matrix can be anything,
		/// so colliders here must be 100% transformable (i.e. ICollider.IsFullyTransformable = true).
		///
		/// </summary>
		/// <param name="colliderId">The ID of the collider</param>
		/// <param name="matrix">The transformation matrix</param>
		internal void TransformKinematicColliders(int colliderId, float4x4 matrix)
		{
			switch (GetColliderType(ref KinematicColliders, colliderId)) {
				case ColliderType.Point:
					ref var pointCollider = ref KinematicColliders.Point(colliderId);
					pointCollider.Transform(KinematicCollidersAtIdentity.Point(colliderId), matrix);
					break;

				case ColliderType.Line3D:
					ref var line3DCollider = ref KinematicColliders.Line3D(colliderId);
					line3DCollider.Transform(KinematicCollidersAtIdentity.Line3D(colliderId), matrix);
					break;

				case ColliderType.SweptCircle:
					ref var sweptCircleCollider = ref KinematicColliders.SweptCircle(colliderId);
					sweptCircleCollider.Transform(KinematicCollidersAtIdentity.SweptCircle(colliderId), matrix);
					break;

				case ColliderType.Triangle:
					ref var triangleCollider = ref KinematicColliders.Triangle(colliderId);
					triangleCollider.Transform(KinematicCollidersAtIdentity.Triangle(colliderId), matrix);
					break;
			}
		}

		#endregion

		#region Hit Test

		internal float HitTest(ref NativeColliders colliders, int colliderId, ref BallState ball, ref CollisionEventData newCollEvent, ref NativeList<ContactBufferElement> contacts)
		{
			if (IsInactiveDropTarget(ref colliders, colliderId)) {
				return -1f;
			}
			switch (GetColliderType(ref colliders, colliderId)) {
				case ColliderType.Bumper:
					return colliders.Circle(colliderId).HitTestBasicRadius(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime, direction:false, lateral:true, rigid:false);

				case ColliderType.Circle:
					return colliders.Circle(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Gate:
					return colliders.Gate(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Line:
					ref var lineCollider = ref colliders.Line(colliderId);
					if (lineCollider.ItemType == ItemType.Trigger) {
						return colliders.Line(colliderId).HitTestBasic(ref newCollEvent, ref InsideOfs, in ball,
							ball.CollisionEvent.HitTime, false, false, false);
					}
					return colliders.Line(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.LineZ:
					return colliders.LineZ(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Line3D:
					return colliders.Line3D(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.LineSlingShot:
					return colliders.LineSlingShot(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Point:
					return colliders.Point(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plane:
					return colliders.Plane(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Spinner:
					return colliders.Spinner(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.SweptCircle:
					return colliders.SweptCircle(colliderId).HitTest(ref newCollEvent, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Triangle:
					return colliders.Triangle(colliderId).HitTest(ref newCollEvent, in InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle:
					return colliders.Circle(colliderId).HitTestBasicRadius(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.Flipper:
					ref var flipperState = ref GetFlipperState(colliderId, ref colliders);
					ref var flipperCollider = ref colliders.Flipper(colliderId);
					return flipperCollider.HitTest(ref newCollEvent, ref InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plunger:
					ref var plungerState = ref GetPlungerState(colliderId, ref colliders);
					return colliders.Plunger(colliderId).HitTest(ref newCollEvent, ref InsideOfs, ref plungerState.Movement,
						in plungerState.Static, in ball, ball.CollisionEvent.HitTime);
			}
			return -1f;
		}

		private bool IsInactiveDropTarget(ref NativeColliders colliders, int colliderId)
		{
			if (colliders.GetItemType(colliderId) == ItemType.HitTarget && HasDropTargetState(colliderId, ref colliders)) {
				ref var dropTargetState = ref GetDropTargetState(colliderId, ref colliders);
				if (dropTargetState.Animation.IsDropped || dropTargetState.Animation.MoveAnimation) {  // QUICKFIX so that DT is not triggered twice
					return true;
				}
			}
			return false;
		}

		#endregion
	}
}
