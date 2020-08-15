using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class GateApi : ItemApi<Engine.VPT.Gate.Gate, Engine.VPT.Gate.GateData>, IApiInitializable, IApiHittable, IApiRotatable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the gate.
		/// </summary>
		///
		/// <remarks>
		/// For two-way gates, this is emitted twice, once when entering, and
		/// once when leaving. For one-way gates, it's emitted once when the
		/// ball rolls through it, but not when the gate blocks the ball. <p/>
		///
		/// Also note that the gate must be collidable.
		/// </remarks>
		public event EventHandler Hit;

		/// <summary>
		/// Event emitted when the gate passes its parked position. Only
		/// emitted for one-way gates.
		/// </summary>
		///
		/// <remarks>
		/// Can be emitted multiple times, as the gate bounces a few times
		/// before coming to a rest.<p/>
		///
		/// Note that the gate must be collidable.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the gate rotates to its top position.
		/// </summary>
		///
		/// <remarks>
		/// The gate must be collidable.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitEos;

		// todo
		public event EventHandler Timer;

		public GateApi(Engine.VPT.Gate.Gate item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool _)
		{
			Hit?.Invoke(this, EventArgs.Empty);
		}

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			} else {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			}
		}

		#endregion

	}
}
