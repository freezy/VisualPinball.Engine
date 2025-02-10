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
	public struct ScoreReelDisplayPackable
	{
		public string Id;
		public float Speed;
		public float Wait;

		public static byte[] Pack(ScoreReelDisplayComponent comp)
		{
			return PackageApi.Packer.Pack(new ScoreReelDisplayPackable {
				Id = comp.Id,
				Speed = comp.Speed,
				Wait = comp.Wait,
			});
		}

		public static void Unpack(byte[] bytes, ScoreReelDisplayComponent comp)
		{
			var data = PackageApi.Packer.Unpack<ScoreReelDisplayPackable>(bytes);
			comp.Id = data.Id;
			comp.Speed = data.Speed;
			comp.Wait = data.Wait;
		}
	}

	public struct ScoreReelDisplayReferencesPackable
	{
		public ReferencePackable[] ReelObjectRefs;
		public ReferencePackable ScoreMotorRef;

		public static byte[] Pack(ScoreReelDisplayComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new ScoreReelDisplayReferencesPackable {
				ReelObjectRefs = refs.PackReferences(comp.ReelObjects).ToArray(),
				ScoreMotorRef = refs.PackReference(comp.ScoreMotorComponent),
			});
		}

		public static void Unpack(byte[] bytes, ScoreReelDisplayComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<ScoreReelDisplayReferencesPackable>(bytes);
			comp.ReelObjects = refs.Resolve<ScoreReelComponent>(data.ReelObjectRefs).ToArray();
			comp.ScoreMotorComponent = refs.Resolve<ScoreMotorComponent>(data.ScoreMotorRef);
		}
	}
}
