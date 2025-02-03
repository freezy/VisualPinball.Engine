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

using System.Collections.Generic;
using MemoryPack;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct KickerPackable
	{
		public List<KickerCoilPackable> Coils { get; set; }

		public KickerPackable(List<KickerCoilPackable> coils)
		{
			Coils = coils;
		}

		public static KickerPackable Unpack(byte[] data) => MemoryPackSerializer.Deserialize<KickerPackable>(data);

		public byte[] Pack() => MemoryPackSerializer.Serialize(this);
	}

	[MemoryPackable]
	public partial struct KickerCoilPackable {

		public string Name { get; }
		public string Id { get; }
		public float Speed { get; }
		public float Angle { get; }
		public float Inclination { get; }

		public KickerCoilPackable(string name, string id, float speed, float angle, float inclination)
		{
			Name = name;
			Id = id;
			Speed = speed;
			Angle = angle;
			Inclination = inclination;
		}
	}
}
