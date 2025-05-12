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

using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Unity
{
	public struct KickerPackable
	{
		public IEnumerable<KickerCoilPackable> Coils;

		public static byte[] Pack(KickerComponent comp)
		{
			return PackageApi.Packer.Pack(new KickerPackable { Coils = comp.Coils.Select(c => new KickerCoilPackable {
					Name = c.Name,
					Id = c.Id,
					Speed = c.Speed,
					Angle = c.Angle,
					Inclination = c.Inclination,
				})
			});
		}

		public static void Unpack(byte[] bytes, KickerComponent comp)
		{
			var data = PackageApi.Packer.Unpack<KickerPackable>(bytes);
			comp.Coils = data.Coils.Select(c => new KickerCoil {
				Name = c.Name,
				Id = c.Id,
				Speed = c.Speed,
				Angle = c.Angle,
				Inclination = c.Inclination
			}).ToList();
		}
	}

	public struct KickerCoilPackable
	{
		public string Name;
		public string Id;
		public float Speed;
		public float Angle;
		public float Inclination;
	}

	public struct KickerColliderPackable
	{
		public bool IsMovable;
		public float Scatter;
		public float HitAccuracy;
		public float HitHeight;
		public float BallZOffset;
		public bool FallThrough;
		public bool FallIn;
		public bool LegacyMode;

		public static byte[] Pack(KickerColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new KickerColliderPackable {
				IsMovable = comp._isKinematic,
				Scatter = comp.Scatter,
				HitAccuracy = comp.HitAccuracy,
				HitHeight = comp.HitHeight,
				BallZOffset = comp.BallZOffset,
				FallThrough = comp.FallThrough,
				FallIn = comp.FallIn,
				LegacyMode = comp.LegacyMode
			});
		}

		public static void Unpack(byte[] bytes, KickerColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<KickerColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.Scatter = data.Scatter;
			comp.HitAccuracy = data.HitAccuracy;
			comp.HitHeight = data.HitHeight;
			comp.BallZOffset = data.BallZOffset;
			comp.FallThrough = data.FallThrough;
			comp.FallIn = data.FallIn;
			comp.LegacyMode = data.LegacyMode;
		}
	}
}
