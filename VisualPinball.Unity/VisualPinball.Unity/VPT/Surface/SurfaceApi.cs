using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Surface
{
	public class SurfaceApi : ItemApi<Engine.VPT.Surface.Surface, Engine.VPT.Surface.SurfaceData>, IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event triggered when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event triggered when the ball hits the surface.
		/// </summary>
		public event EventHandler Hit;

		internal SurfaceApi(Engine.VPT.Surface.Surface item, Entity entity, Player player) : base(item, entity, player)
		{
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

		#endregion
	}
}
