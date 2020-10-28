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
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	public class TroughApi : ItemApi<Trough, TroughData>,
		IApiInitializable
	{
		private KickerApi _entryKicker;
		private KickerApi _exitKicker;

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal TroughApi(Trough item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			_entryKicker = TableApi.Kicker(Data.EntryKicker);
			_exitKicker = TableApi.Kicker(Data.ExitKicker);

			if (_entryKicker != null)
			{
				_entryKicker.Hit += OnEntryKickerHit;
			}

			if (_exitKicker != null)
			{
				_exitKicker.Hit += OnExitKickerFire;
			}
		
			Init?.Invoke(this, EventArgs.Empty);
		}
		
		void OnEntryKickerHit(object sender, EventArgs args)
		{
			(sender as KickerApi)?.DestroyBall();
		}
		
		void OnExitKickerFire(object sender, EventArgs args)
		{
			(sender as KickerApi)?.CreateBall();
		}

		#endregion
	}
}
