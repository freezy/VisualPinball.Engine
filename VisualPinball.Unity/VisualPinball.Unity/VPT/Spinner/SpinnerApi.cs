// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using Unity.Entities;
using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Unity
{
	public class SpinnerApi : ItemApi<Engine.VPT.Spinner.Spinner, Engine.VPT.Spinner.SpinnerData>,
		IApiInitializable, IApiRotatable, IApiSpinnable, IApiSwitchable
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
			GamelogicEngineWithSwitches?.Switch(Item.Name, true);
		}

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			} else {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			}
		}

		void IApiSwitchable.SetGamelogicEngine(IGamelogicEngineWithSwitches gle) => GamelogicEngineWithSwitches = gle;

		#endregion
	}

}
