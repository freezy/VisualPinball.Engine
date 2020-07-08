// ReSharper disable EventNeverSubscribedTo.Global
#pragma warning disable 67

using System;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Engine;

namespace VisualPinball.Unity.VPT.Flipper
{
	public class FlipperApi : ItemApi<Engine.VPT.Flipper.Flipper, FlipperData>, IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event triggered when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event triggered when the flipper was touched by the ball, but did
		/// not collide.
		/// </summary>
		public event EventHandler Hit;

		/// <summary>
		/// Event triggered when the flipper collided with the ball.
		/// </summary>
		public event EventHandler<CollideEventArgs> Collide;

		/// <summary>
		/// Event triggered when the flipper comes to rest, i.e. moves back to
		/// the resting position.
		/// </summary>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event triggered when the flipper reaches the end position.
		/// </summary>
		public event EventHandler<RotationEventArgs> LimitEos;

		// todo
		public event EventHandler Timer;

		public FlipperApi(Engine.VPT.Flipper.Flipper flipper, Entity entity, Player player) : base(flipper, entity, player)
		{
		}

		/// <summary>
		/// Enables the flipper's solenoid, making the flipper to start moving
		/// to its end position.
		/// </summary>
		public void RotateToEnd()
		{
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToEnd(Entity);
		}

		/// <summary>
		/// Disables the flipper's solenoid, making the flipper rotate back to
		/// its resting position.
		/// </summary>
		public void RotateToStart()
		{
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToStart(Entity);
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit()
		{
			Hit?.Invoke(this, EventArgs.Empty);
		}

		internal void OnCollide(float flipperHit)
		{
			Collide?.Invoke(this, new CollideEventArgs { FlipperHit = flipperHit });
		}

		internal void OnRotationEvent(FlipperRotationEvent rotationEvent)
		{
			if (rotationEvent.Direction) {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotationEvent.AngleSpeed });
			} else {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotationEvent.AngleSpeed });
			}
		}

		#endregion
	}

	/// <summary>
	/// Event data when the flipper either reaches resting or end
	/// position.
	/// </summary>
	public struct RotationEventArgs
	{
		/// <summary>
		/// Angle speed with which the new position was reached.
		/// </summary>
		public float AngleSpeed;
	}

	/// <summary>
	/// Event data when the ball collides with the flipper.
	/// </summary>
	public struct CollideEventArgs
	{
		/// <summary>
		/// The relative normal velocity with which the flipper was hit.
		/// </summary>
		public float FlipperHit;
	}
}
