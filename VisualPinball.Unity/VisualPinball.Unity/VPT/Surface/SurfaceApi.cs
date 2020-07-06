using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Surface
{
	public class SurfaceApi : ItemApi<Engine.VPT.Surface.Surface, Engine.VPT.Surface.SurfaceData>, IApiHittable
	{
		public event EventHandler Hit;

		public SurfaceApi(Engine.VPT.Surface.Surface item, Entity entity, Player player) : base(item, entity, player)
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
