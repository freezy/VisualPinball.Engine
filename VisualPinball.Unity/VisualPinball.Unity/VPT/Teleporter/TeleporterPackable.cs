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

namespace VisualPinball.Unity
{
	public struct TeleporterPackable
	{
		public bool EjectAfterTeleportation;
		public float EjectDelay;

		public static byte[] Pack(TeleporterComponent comp)
		{
			return PackageApi.Packer.Pack(new TeleporterPackable {
				EjectAfterTeleportation = comp.EjectAfterTeleportation,
				EjectDelay = comp.EjectDelay,
			});
		}

		public static void Unpack(byte[] bytes, TeleporterComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TeleporterPackable>(bytes);
			comp.EjectAfterTeleportation = data.EjectAfterTeleportation;
			comp.EjectDelay = data.EjectDelay;
		}
	}

	public struct TeleporterReferencesPackable
	{
		public ReferencePackable FromKickerRef;
		public ReferencePackable ToKickerRef;
		public string ToKickerItem;

		public static byte[] Pack(TeleporterComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new TeleporterReferencesPackable {
				FromKickerRef = refs.PackReference(comp.FromKicker),
				ToKickerRef = refs.PackReference(comp.ToKicker),
				ToKickerItem = comp.ToKickerItem,
			});
		}

		public static void Unpack(byte[] bytes, TeleporterComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<TeleporterReferencesPackable>(bytes);
			comp.FromKicker = refs.Resolve<KickerComponent>(data.FromKickerRef);
			comp.ToKicker = refs.Resolve<KickerComponent>(data.ToKickerRef);
			comp.ToKickerItem = data.ToKickerItem;
		}
	}
}
