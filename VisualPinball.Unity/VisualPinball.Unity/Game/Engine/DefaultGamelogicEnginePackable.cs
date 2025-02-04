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
	public readonly partial struct DefaultGamelogicEnginePackable
	{
		public readonly float GlobalDifficulty;

		public DefaultGamelogicEnginePackable(float globalDifficulty)
		{
			GlobalDifficulty = globalDifficulty;
		}

		public void Apply(TableComponent table)
		{
			table.GlobalDifficulty = GlobalDifficulty;
		}

		public static DefaultGamelogicEnginePackable Unpack(byte[] data) => PackageApi.Packer.Unpack<DefaultGamelogicEnginePackable>(data);
		public byte[] Pack() => PackageApi.Packer.Pack(this);
	}
}
