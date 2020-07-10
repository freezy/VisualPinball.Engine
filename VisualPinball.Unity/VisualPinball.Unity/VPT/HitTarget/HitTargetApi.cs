using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.HitTarget
{
	public class HitTargetApi : ItemApi<Engine.VPT.HitTarget.HitTarget, Engine.VPT.HitTarget.HitTargetData>,
		IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the hit target.
		/// </summary>
		public event EventHandler Hit;

		internal HitTargetApi(Engine.VPT.HitTarget.HitTarget item, Entity entity, Player player) : base(item, entity, player)
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

		#endregion
	}
}
