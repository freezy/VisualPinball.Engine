using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class SpinnerApi : ItemApi<Engine.VPT.Spinner.Spinner, Engine.VPT.Spinner.SpinnerData>,
		IApiInitializable, IApiRotatable, IApiSpinnable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the spinner reaches the minimal angle.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted if min and max angle are different, otherwise
		/// subscribe to the <see cref="Spin"/> event.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the spinner reaches the maximal angle.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted if min and max angle are different, otherwise
		/// subscribe to the <see cref="Spin"/> event.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitEos;

		/// <summary>
		/// Event emitted when the spinner performs one spin.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted when min and max angles are the same, i.e. the spinner
		/// is able to rotate entirely without rotated back at a given angle.
		/// </remarks>
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

}
