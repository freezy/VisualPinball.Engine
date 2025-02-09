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
	public struct ScoreMotorPackable
	{
		public int Duration;
		public int Steps;
		public bool BlockScoring;
		public ScoreMotorTiming[] ScoreMotorTimingList;

		public static byte[] Pack(ScoreMotorComponent comp)
		{
			return PackageApi.Packer.Pack(new ScoreMotorPackable {
				Duration = comp.Duration,
				Steps = comp.Steps,
				BlockScoring = comp.BlockScoring,
				ScoreMotorTimingList = comp.ScoreMotorTimingList.ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, ScoreMotorComponent comp)
		{
			var data = PackageApi.Packer.Unpack<ScoreMotorPackable>(bytes);
			comp.Duration = data.Duration;
			comp.Steps = data.Steps;
			comp.BlockScoring = data.BlockScoring;
			comp.ScoreMotorTimingList = data.ScoreMotorTimingList.ToList();
		}
	}
}
