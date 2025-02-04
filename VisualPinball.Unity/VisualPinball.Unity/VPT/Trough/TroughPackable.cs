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

using MemoryPack;
using UnityEngine;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct TroughPackable
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

	[MemoryPackable]
	public partial struct TroughReferencesPackable
	{
		public ReferencePackable PlayfieldEntrySwitchRef;
		public string PlayfieldEntrySwitchItem;
		public ReferencePackable PlayfieldExitKickerRef;
		public string PlayfieldExitKickerItem;

		public static byte[] Pack(TroughComponent comp, Transform root, PackNameLookup packNameLookup)
		{
			var playfieldEntrySwitch = comp._playfieldEntrySwitch != null
				? new ReferencePackable(comp._playfieldEntrySwitch.transform.GetPath(root), packNameLookup.GetName(comp._playfieldEntrySwitch.GetType()))
				: new ReferencePackable(null, null);

			var playfieldExitKicker = comp.PlayfieldExitKicker != null
				? new ReferencePackable(comp.PlayfieldExitKicker.transform.GetPath(root), packNameLookup.GetName(comp.PlayfieldExitKicker.GetType()))
				: new ReferencePackable(null, null);

			return PackageApi.Packer.Pack(new TroughReferencesPackable {
				PlayfieldEntrySwitchRef = playfieldEntrySwitch,
				PlayfieldEntrySwitchItem = comp.PlayfieldEntrySwitchItem,
				PlayfieldExitKickerRef = playfieldExitKicker,
				PlayfieldExitKickerItem = comp.PlayfieldExitKickerItem,
			});
		}

		public static void Unpack(byte[] bytes, TroughComponent comp, Transform root, PackNameLookup packNameLookup)
		{
			var data = PackageApi.Packer.Unpack<TroughReferencesPackable>(bytes);
			comp._playfieldEntrySwitch = data.PlayfieldEntrySwitchRef.Resolve<MonoBehaviour, ITriggerComponent>(root, packNameLookup);
			comp.PlayfieldEntrySwitchItem = data.PlayfieldEntrySwitchItem;
			comp.PlayfieldExitKicker = data.PlayfieldExitKickerRef.Resolve<KickerComponent>(root, packNameLookup);
			comp.PlayfieldExitKickerItem = data.PlayfieldExitKickerItem;
		}
	}
}
