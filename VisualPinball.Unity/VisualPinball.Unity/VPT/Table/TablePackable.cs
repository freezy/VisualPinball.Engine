﻿// Visual Pinball Engine
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
	public struct TablePackable
	{
		public float GlobalDifficulty;

		public static byte[] Pack(TableComponent comp)
		{
			return PackageApi.Packer.Pack(new TablePackable {
				GlobalDifficulty = comp.GlobalDifficulty,
			});
		}

		public static void Unpack(byte[] bytes, TableComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TablePackable>(bytes);
			comp.GlobalDifficulty = data.GlobalDifficulty;
		}
	}
}
