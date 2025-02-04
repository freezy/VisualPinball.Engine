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

using MemoryPack;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public readonly partial struct TroughPackable
	{
		public readonly int Type;
		public readonly int BallCount;
		public readonly int SwitchCount;
		public readonly bool JamSwitch;
		public readonly int RollTime;
		public readonly int TransitionTime;
		public readonly int KickTime;

		public TroughPackable(int type, int ballCount, int switchCount, bool jamSwitch, int rollTime, int transitionTime, int kickTime)
		{
			Type = type;
			BallCount = ballCount;
			SwitchCount = switchCount;
			JamSwitch = jamSwitch;
			RollTime = rollTime;
			TransitionTime = transitionTime;
			KickTime = kickTime;
		}

		public static TroughPackable Unpack(byte[] data) => PackageApi.Packer.Unpack<TroughPackable>(data);

		public byte[] Pack() => PackageApi.Packer.Pack(this);
	}

	[MemoryPackable]
	public readonly partial struct TroughReferencesPackable
	{
		public readonly ReferencePackable PlayfieldEntrySwitchRef;
		public readonly string PlayfieldEntrySwitchItem;
		public readonly ReferencePackable PlayfieldExitKickerRef;
		public readonly string PlayfieldExitKickerItem;

		public TroughReferencesPackable(
			ReferencePackable playfieldEntrySwitchRef, string playfieldEntrySwitchItem,
			ReferencePackable playfieldExitKickerRef, string playfieldExitKickerItem)
		{
			PlayfieldEntrySwitchRef = playfieldEntrySwitchRef;
			PlayfieldEntrySwitchItem = playfieldEntrySwitchItem;
			PlayfieldExitKickerRef = playfieldExitKickerRef;
			PlayfieldExitKickerItem = playfieldExitKickerItem;
		}

		public static TroughReferencesPackable Unpack(byte[] data) => PackageApi.Packer.Unpack<TroughReferencesPackable>(data);

		public byte[] Pack() => PackageApi.Packer.Pack(this);
	}
}
