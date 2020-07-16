using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Spinner
{
	public class SpinnerApi : ItemApi<Engine.VPT.Spinner.Spinner, Engine.VPT.Spinner.SpinnerData>,
		IApiInitializable, IApiRotatable, IApiSpinnable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		public event EventHandler<RotationEventArgs> LimitBos;

		public event EventHandler<RotationEventArgs> LimitEos;

		public event EventHandler Spin;

		// todo
		public event EventHandler Timer;

		public SpinnerApi(Engine.VPT.Spinner.Spinner item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiSpinnable.OnSpin()
		{
			Spin?.Invoke(this, EventArgs.Empty);
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

	public struct RotationEventArgs
	{
		public float AngleSpeed;
	}
}
