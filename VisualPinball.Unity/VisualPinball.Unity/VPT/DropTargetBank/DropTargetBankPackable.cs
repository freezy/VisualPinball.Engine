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

using System.Linq;

namespace VisualPinball.Unity
{
	public struct DropTargetBankPackable
	{
		public int BankSize;

		public static byte[] Pack(DropTargetBankComponent comp)
		{
			return PackageApi.Packer.Pack(new DropTargetBankPackable {
				BankSize = comp.BankSize,
			});
		}

		public static void Unpack(byte[] bytes, DropTargetBankComponent comp)
		{
			var data = PackageApi.Packer.Unpack<DropTargetBankPackable>(bytes);
			comp.BankSize = data.BankSize;
		}
	}

	public struct DropTargetBankReferencesPackable
	{
		public ReferencePackable[] DropTargetRefs;

		public static byte[] Pack(DropTargetBankComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new DropTargetBankReferencesPackable {
				DropTargetRefs = comp.DropTargets.Select(refs.PackReference).ToArray()
			});
		}

		public static void Unpack(byte[] bytes, DropTargetBankComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<DropTargetBankReferencesPackable>(bytes);
			comp.DropTargets = data.DropTargetRefs.Select(refs.Resolve<DropTargetComponent>).ToArray();
		}
	}
}
