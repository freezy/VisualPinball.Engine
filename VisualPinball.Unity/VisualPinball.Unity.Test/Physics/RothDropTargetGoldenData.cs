// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Reviewable compatibility inputs extracted from the local Roth/nFozzy
	/// scripts. Phase 2 tests run production compatibility code against these
	/// cases; Phase 0 deliberately does not assert the data against itself.
	/// </summary>
	internal static class RothDropTargetGoldenData
	{
		internal const string DarkChaosSha256 = "D5DC776B80D919E4418732F03CE55BF4586C93C24AFEBABE9E4D28C98E74DD39";
		internal const string CatacombSha256 = "5A814437D836211DA377BE4EBFD91BE242043B8F97D4897D86E32C5942DE5E6B";

		internal const bool EnableBrick = false;
		internal const float TargetMass = 0.2f;
		internal const float BrickVelocity = 30f;
		internal const float BrickCenterDistance = 8f;
		internal const float DarkChaosBackHitVelocity = 15f;

		internal static readonly RothMassCase[] MassCases = {
			new RothMassCase(1f, 30f, 20f),
			new RothMassCase(1f, 15f, 10f),
			new RothMassCase(2f, 30f, 24.545454f),
		};
	}

	internal readonly struct RothMassCase
	{
		internal readonly float BallMass;
		internal readonly float IncomingNormalVelocity;
		internal readonly float ExpectedNormalVelocity;

		internal RothMassCase(float ballMass, float incomingNormalVelocity, float expectedNormalVelocity)
		{
			BallMass = ballMass;
			IncomingNormalVelocity = incomingNormalVelocity;
			ExpectedNormalVelocity = expectedNormalVelocity;
		}
	}
}
