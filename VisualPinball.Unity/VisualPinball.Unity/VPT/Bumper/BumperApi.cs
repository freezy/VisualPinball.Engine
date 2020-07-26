using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Bumper
{
	public class BumperApi : ItemApi<Engine.VPT.Bumper.Bumper, BumperData>, IApiInitializable, IApiHittable
	{

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the bumper.
		/// </summary>
		public event EventHandler Hit;

		public BumperApi(Engine.VPT.Bumper.Bumper item, Entity entity, Player player) : base(item, entity, player)
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
