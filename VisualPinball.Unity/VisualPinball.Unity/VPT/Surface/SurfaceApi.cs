using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class SurfaceApi : ItemApi<Engine.VPT.Surface.Surface, Engine.VPT.Surface.SurfaceData>,
		IApiInitializable, IApiHittable, IApiSlingshot
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the surface.
		/// </summary>
		public event EventHandler Hit;

		/// <summary>
		/// Event emitted when a slingshot segment was hit.
		/// </summary>
		public event EventHandler Slingshot;

		internal SurfaceApi(Engine.VPT.Surface.Surface item, Entity entity, Player player) : base(item, entity, player)
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

		public void OnSlingshot()
		{
			Slingshot?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
