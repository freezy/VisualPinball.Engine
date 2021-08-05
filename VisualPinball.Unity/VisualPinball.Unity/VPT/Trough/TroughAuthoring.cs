// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Trough")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/troughs.html")]
	public class TroughAuthoring : ItemMainAuthoring<Trough, TroughData>, ISwitchDeviceAuthoring, ICoilDeviceAuthoring
	{
		#region Data

		public int Type = TroughType.ModernOpto;

		public string PlayfieldEntrySwitch = string.Empty;

		public string PlayfieldExitKicker = string.Empty;

		public int BallCount = 6;

		public int SwitchCount = 6;

		public bool JamSwitch;

		public int RollTime = 300;

		public int TransitionTime = 50;

		public int KickTime = 100;

		#endregion

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => Item.AvailableSwitches;
		public IEnumerable<GamelogicEngineCoil> AvailableCoils => Item.AvailableCoils;
		public SwitchDefault SwitchDefault => Item.Data.Type == TroughType.ModernOpto ? SwitchDefault.NormallyClosed : SwitchDefault.NormallyOpen;

		protected override Trough InstantiateItem(TroughData data) => new Trough(data);
		public override IEnumerable<Type> ValidParents { get; } = new Type[0];

		private Vector3 EntryPos(float height)
		{
			if (string.IsNullOrEmpty(Data.PlayfieldEntrySwitch)) {
				return Vector3.zero;
			}
			if (TableContainer.Has<Trigger>(Data.PlayfieldEntrySwitch)) {
				return TableContainer.Get<Trigger>(Data.PlayfieldEntrySwitch).Data.Center.ToUnityVector3(height);
			}
			return TableContainer.Has<Kicker>(Data.PlayfieldEntrySwitch)
				? TableContainer.Get<Kicker>(Data.PlayfieldEntrySwitch).Data.Center.ToUnityVector3(height)
				: Vector3.zero;
		}

		private Vector3 ExitPos(float height) => string.IsNullOrEmpty(Data.PlayfieldExitKicker)
			? Vector3.zero
			: !TableContainer.Has<Kicker>(Data.PlayfieldExitKicker)
				? Vector3.zero
				: TableContainer.Get<Kicker>(Data.PlayfieldExitKicker).Data.Center.ToUnityVector3(height);

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterTrough(Item, gameObject);
		}

		public override void SetData(TroughData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Type = data.Type;
			PlayfieldEntrySwitch = data.PlayfieldEntrySwitch;
			PlayfieldExitKicker = data.PlayfieldExitKicker;
			BallCount = data.BallCount;
			SwitchCount = data.SwitchCount;
			JamSwitch = data.JamSwitch;
			RollTime = data.RollTime;
			TransitionTime = data.TransitionTime;
			KickTime = data.KickTime;
		}

		public override TroughData CopyDataTo(TroughData data)
		{
			data.Name = name;

			data.Type = Type;
			data.PlayfieldEntrySwitch = PlayfieldEntrySwitch;
			data.PlayfieldExitKicker = PlayfieldExitKicker;
			data.BallCount = BallCount;
			data.SwitchCount = SwitchCount;
			data.JamSwitch = JamSwitch;
			data.RollTime = RollTime;
			data.TransitionTime = TransitionTime;
			data.KickTime = KickTime;

			return data;
		}

		private void OnDrawGizmosSelected()
		{
			Profiler.BeginSample("TroughAuthoring.OnDrawGizmosSelected");
			if (!string.IsNullOrEmpty(Data.PlayfieldEntrySwitch) && !string.IsNullOrEmpty(Data.PlayfieldExitKicker)) {
				var ltw = GetComponentInParent<TableAuthoring>().transform;
				var entryPos = EntryPos(0f);
				var exitPos = ExitPos(0f);
				var entryWorldPos = ltw.TransformPoint(entryPos);
				var exitWorldPos = ltw.TransformPoint(exitPos);
				var localPos = transform.localPosition;
				var localPos0 = new Vector3(localPos.x, localPos.y, 0f);
				var pos = ltw.TransformPoint(localPos0);
				DrawArrow(entryWorldPos, pos - entryWorldPos);
				DrawArrow(pos, exitWorldPos - pos);
			}
			Profiler.EndSample();
		}

		public void UpdatePosition()
		{
			// place trough between entry and exit kicker
			var pos = (EntryPos(75f) + ExitPos(75f)) / 2;
			transform.localPosition = pos;
		}
	}
}
