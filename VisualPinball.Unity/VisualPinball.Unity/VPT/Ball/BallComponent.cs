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

// ReSharper disable InconsistentNaming

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class BallComponent : MonoBehaviour
	{
		public int Id => gameObject.GetInstanceID();
		public float Radius = 25;
		public float Mass = 1;
		public float3 Velocity;
		public bool IsFrozen;


		internal BallState CreateState() => new BallState {
			Id = Id,
			IsFrozen = IsFrozen,
			Position = transform.localPosition.TranslateToVpx(),
			Radius = Radius,
			Mass = Mass,
			Velocity = Velocity,
			BallOrientation = float3x3.identity,
			BallOrientationForUnity = float3x3.identity,
			RingCounterOldPos = 0,
			AngularMomentum = float3.zero
		};
	}
}
