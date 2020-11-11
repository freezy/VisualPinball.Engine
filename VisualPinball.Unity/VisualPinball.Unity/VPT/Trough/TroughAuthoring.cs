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
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
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

		private Vector3 EntryPickerPos(float height) => string.IsNullOrEmpty(Data.EntryKicker)
			? Vector3.zero
			: Table.Kicker(Data.EntryKicker).Data.Center.ToUnityVector3(height);

		private Vector3 ExitKickerPos(float height) => string.IsNullOrEmpty(Data.ExitKicker)
			? Vector3.zero
			: Table.Kicker(Data.ExitKicker).Data.Center.ToUnityVector3(height);

		private void Start()
		{
			GetComponentInParent<Player>().RegisterTrough(Item, gameObject);
		}

		public override void Restore()
		{
			Item.Name = name;
		}

		private void OnDrawGizmosSelected()
		{
			if (!string.IsNullOrEmpty(Data.EntryKicker) && !string.IsNullOrEmpty(Data.ExitKicker)) {
				var ltw = GetComponentInParent<TableAuthoring>().transform;
				var entryPos = EntryPickerPos(0f);
				var exitPos = ExitKickerPos(0f);
				var entryWorldPos = ltw.TransformPoint(entryPos);
				var exitWorldPos = ltw.TransformPoint(exitPos);
				var localPos = transform.localPosition;
				var localPos0 = new Vector3(localPos.x, localPos.y, 0f);
				var pos = ltw.TransformPoint(localPos0);
				DrawArrow(entryWorldPos, pos - entryWorldPos);
				DrawArrow(pos, exitWorldPos - pos);
			}
		}

		public void UpdatePosition()
		{
			// place trough between entry and exit kicker
			var pos = (EntryPickerPos(75f) + ExitKickerPos(75f)) / 2;
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
