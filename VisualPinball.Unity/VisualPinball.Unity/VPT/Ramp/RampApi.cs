using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class RampApi : ItemApi<Engine.VPT.Ramp.Ramp, Engine.VPT.Ramp.RampData>, IApiInitializable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal RampApi(Engine.VPT.Ramp.Ramp item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
