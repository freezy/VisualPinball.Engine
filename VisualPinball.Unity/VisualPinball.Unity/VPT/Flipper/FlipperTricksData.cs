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

using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct FlipperTricksData : IComponentData
	{
		// used in flippertricks
		// internals
		public float OriginalAngleEnd;
		public float OriginalRampUpSpeed;
		public float OriginalTorqueDamping;
		public float OriginalTorqueDampingAngle;
		public float ElasticityMultiplier;
		public bool lastSolState;
		// the following four variables are also present in flippertricksdata. While they are static at flipperstaticData, they may change in flipperTricksData (here) at runtime.
		// if no flippertricks are used, for simplicity also the variables at flipperTricksData are used (but not changed while runtime)
		public float TorqueDamping; //
		public float TorqueDampingAngle; //
		public float AngleEnd; //
		public float RampUpSpeed; //

		public bool WasInContact;

		// time used for live Catch
		public double FlipperAngleEndTime;

		// externals
		//  Flipper Tricks
		public bool UseFlipperTricksPhysics;
		public float SOSRampUp;
		public float SOSEM;
		public float EOSReturn;
		public float EOSTNew;
		public float EOSANew;
		public float EOSRampup;
		public float Overshoot;
		
		//  Live Catch
		public bool UseFlipperLiveCatch;
		public float LiveCatchDistanceMin; // vp units from base
		public float LiveCatchDistanceMax; // vp units from base
		public float LiveCatchMinimalBallSpeed; 
		public float LiveCatchPerfectTime;
		public float LiveCatchFullTime;
		public float LiveCatchMinimalBounceSpeedMultiplier;
		public float LiveCatchInaccurateBounceSpeedMultiplier;

	}
}
