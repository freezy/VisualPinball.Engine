// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable MemberCanBePrivate.Global

using UnityEngine;

namespace VisualPinball.Unity
{
	public struct TroughPackable
	{
		public int Type;
		public int BallCount;
		public int SwitchCount;
		public bool JamSwitch;
		public int RollTime;
		public int TransitionTime;
		public int KickTime;

		public static byte[] Pack(TroughComponent comp)
		{
			return PackageApi.Packer.Pack(new TroughPackable {
				Type = comp.Type,
				BallCount = comp.BallCount,
				SwitchCount = comp.SwitchCount,
				JamSwitch = comp.JamSwitch,
				RollTime = comp.RollTime,
				TransitionTime = comp.TransitionTime,
				KickTime = comp.KickTime,
			});
		}

		public static void Unpack(byte[] bytes, TroughComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TroughPackable>(bytes);
			comp.Type = data.Type;
			comp.BallCount = data.BallCount;
			comp.SwitchCount = data.SwitchCount;
			comp.JamSwitch = data.JamSwitch;
			comp.RollTime = data.RollTime;
			comp.TransitionTime = data.TransitionTime;
			comp.KickTime = data.KickTime;
		}
	}

	public struct TroughReferencesPackable
	{
		public ReferencePackable PlayfieldEntrySwitchRef;
		public string PlayfieldEntrySwitchItem;
		public ReferencePackable PlayfieldExitKickerRef;
		public string PlayfieldExitKickerItem;

		public static byte[] Pack(TroughComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new TroughReferencesPackable {
				PlayfieldEntrySwitchRef = refs.PackReference(comp._playfieldEntrySwitch),
				PlayfieldEntrySwitchItem = comp.PlayfieldEntrySwitchItem,
				PlayfieldExitKickerRef = refs.PackReference(comp.PlayfieldExitKicker),
				PlayfieldExitKickerItem = comp.PlayfieldExitKickerItem,
			});
		}

		public static void Unpack(byte[] bytes, TroughComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<TroughReferencesPackable>(bytes);
			comp._playfieldEntrySwitch = refs.Resolve<MonoBehaviour, ITriggerComponent>(data.PlayfieldEntrySwitchRef);
			comp.PlayfieldEntrySwitchItem = data.PlayfieldEntrySwitchItem;
			comp.PlayfieldExitKicker = refs.Resolve<KickerComponent>(data.PlayfieldExitKickerRef);
			comp.PlayfieldExitKickerItem = data.PlayfieldExitKickerItem;
		}
	}
}
