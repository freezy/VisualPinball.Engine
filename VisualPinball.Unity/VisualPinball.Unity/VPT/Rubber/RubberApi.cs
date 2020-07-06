using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Rubber
{
	public class RubberApi : ItemApi<Engine.VPT.Rubber.Rubber, Engine.VPT.Rubber.RubberData>, IApiHittable
	{
		public event EventHandler Hit;

		public RubberApi(Engine.VPT.Rubber.Rubber item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiHittable.OnHit()
		{
			Hit?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
