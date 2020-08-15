using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class TriggerApi : ItemApi<Engine.VPT.Trigger.Trigger, Engine.VPT.Trigger.TriggerData>,
		IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball glides on the trigger.
		/// </summary>
		public event EventHandler Hit;

		/// <summary>
		/// Event emitted when the ball leaves the trigger.
		/// </summary>
		public event EventHandler UnHit;

		internal TriggerApi(Engine.VPT.Trigger.Trigger item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool isUnHit)
		{
			if (isUnHit) {
				UnHit?.Invoke(this, EventArgs.Empty);

			} else {
				Hit?.Invoke(this, EventArgs.Empty);
			}
		}

		#endregion
	}
}
