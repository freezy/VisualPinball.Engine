using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	public class BumperApi : ItemApi<Bumper, BumperData>, IApiInitializable, IApiHittable
	{

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the bumper.
		/// </summary>
		public event EventHandler Hit;

		public BumperApi(Bumper item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool isUnHit)
		{
			Hit?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
