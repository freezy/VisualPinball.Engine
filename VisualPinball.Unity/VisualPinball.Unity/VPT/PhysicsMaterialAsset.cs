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

using System;
using MemoryPack;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A physical material used by the physics engine<p/>
	///
	/// Materials are actually a big deal in VP, authors as well as players
	/// tweak them all the time, so getting those from external assets instead
	/// of writing them into the scene seems a good plan.
	/// </summary>
	[CreateAssetMenu(fileName = "PhysicsMaterial", menuName = "Visual Pinball/Physics Material", order = 100)]
	//[CustomEditor(typeof(PhysicsMaterial))]
	public partial class PhysicsMaterialAsset : ScriptableObject
	{
		public float Elasticity;
		public float ElasticityFalloff;

		public bool UseElasticityOverVelocity => ElasticityOverVelocity.keys.Length > 0;
		public AnimationCurve ElasticityOverVelocity;

		public float Friction;

		public bool UseFrictionOverVelocity => FrictionOverVelocity.keys.Length > 0;
		public AnimationCurve FrictionOverVelocity;

		// public AnimationCurve FrictionOverAngularMomentum;
		public float ScatterAngle;

		/// <summary>
		/// Returns a lookup-table of 128 values. <br/>
		///
		/// The range goes from 0 to 64 velocity units, meaning an entry covers 0.5 units.
		/// </summary>
		/// <returns>Lookup table</returns>
		/// <exception cref="InvalidOperationException"></exception>
		public FixedList512Bytes<float> GetElasticityOverVelocityLUT()
		{
			var lut = new FixedList512Bytes<float>();
			if (ElasticityOverVelocity.keys.Length == 0) {
				throw new InvalidOperationException("Curve ElasticityOverVelocity is empty.");
			}
			for (var i = 0; i < 127; i++) {
				lut.Add(ElasticityOverVelocity.Evaluate(i / 2f));
			}
			return lut;
		}

		public FixedList512Bytes<float> GetFrictionOverVelocityLUT()
		{
			var lut = new FixedList512Bytes<float>();
			if (FrictionOverVelocity.keys.Length == 0) {
				throw new InvalidOperationException("Curve ElasticityOverVelocity is empty.");
			}
			for (var i = 0; i < 127; i++) {
				lut.Add(FrictionOverVelocity.Evaluate(i));
			}
			return lut;
		}
	}
}
