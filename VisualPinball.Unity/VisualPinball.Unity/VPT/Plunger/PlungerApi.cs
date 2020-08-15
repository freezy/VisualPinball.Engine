using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerApi : ItemApi<Plunger, PlungerData>, IApiInitializable, IApiRotatable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the plunger moved back to the park position.
		/// </summary>
		public event EventHandler<StrokeEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the plunger was pulled back and reached its end position.
		/// </summary>
		public event EventHandler<StrokeEventArgs> LimitEos;

		// todo
		public event EventHandler Timer;

		public bool DoRetract { get; set; } = true;

		internal PlungerApi(Plunger item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		public void PullBack()
		{
			var movementData = EntityManager.GetComponentData<PlungerMovementData>(Entity);
			var velocityData = EntityManager.GetComponentData<PlungerVelocityData>(Entity);

			if (DoRetract) {
				PlungerCommands.PullBackAndRetract(Item.Data.SpeedPull, ref velocityData, ref movementData);

			} else {
				PlungerCommands.PullBack(Item.Data.SpeedPull, ref velocityData, ref movementData);
			}

			EntityManager.SetComponentData(Entity, movementData);
			EntityManager.SetComponentData(Entity, velocityData);
		}

		public void Fire()
		{
			var movementData = EntityManager.GetComponentData<PlungerMovementData>(Entity);
			var velocityData = EntityManager.GetComponentData<PlungerVelocityData>(Entity);
			var staticData = EntityManager.GetComponentData<PlungerStaticData>(Entity);

			// check for an auto plunger
			if (Item.Data.AutoPlunger) {
				// Auto Plunger - this models a "Launch Ball" button or a
				// ROM-controlled launcher, rather than a player-operated
				// spring plunger.  In a physical machine, this would be
				// implemented as a solenoid kicker, so the amount of force
				// is constant (modulo some mechanical randomness).  Simulate
				// this by triggering a release from the maximum retracted
				// position.
				PlungerCommands.Fire(1f, ref velocityData, ref movementData, in staticData);

			} else {
				// Regular plunger - trigger a release from the current
				// position, using the keyboard firing strength.

				var pos = (movementData.Position - staticData.FrameEnd) / (staticData.FrameStart - staticData.FrameEnd);
				PlungerCommands.Fire(pos, ref velocityData, ref movementData, in staticData);
			}

			EntityManager.SetComponentData(Entity, movementData);
			EntityManager.SetComponentData(Entity, velocityData);
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				LimitEos?.Invoke(this, new StrokeEventArgs { Speed = speed });
			} else {
				LimitBos?.Invoke(this, new StrokeEventArgs { Speed = speed });
			}
		}

		#endregion
	}

	public struct StrokeEventArgs
	{
		public float Speed;
	}
}
