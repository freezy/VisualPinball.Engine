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

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Trough")]
	public class TroughAuthoring : ItemMainAuthoring<Trough, TroughData>, ISwitchDeviceAuthoring, ICoilDeviceAuthoring
	{
		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => Item.AvailableSwitches;
		public IEnumerable<GamelogicEngineCoil> AvailableCoils => Item.AvailableCoils;

		protected override Trough InstantiateItem(TroughData data) => new Trough(data);
		public override IEnumerable<Type> ValidParents { get; } = new Type[0];

		public override void Restore()
		{
			Item.Name = name;
		}

		public void UpdatePosition()
		{
			// place trough between entry and exit kicker
			var entryPos = string.IsNullOrEmpty(Data.EntryKicker)
				? Vector3.zero
				: Table.Kicker(Data.EntryKicker).Data.Center.ToUnityVector3(100f);
			var exitPos = string.IsNullOrEmpty(Data.ExitKicker)
				? Vector3.zero
				: Table.Kicker(Data.ExitKicker).Data.Center.ToUnityVector3(100f);

			var pos = (entryPos + exitPos) / 2;
			transform.localPosition = pos;
		}

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Trough>(Name);
			}
		}
	}
}
