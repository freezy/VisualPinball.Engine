// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using UnityEngine;
using UnityEditor;

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
	public class PhysicsMaterial : ScriptableObject
	{
		public bool RubberDampeners;
		public float Elasticity;
		public float ElasticityFalloff;
		public AnimationCurve ElasticityCurve;
		public float Friction;
		public float ScatterAngle;

		public enum Season
		{
			Spring, 
			Summer,
			Autumn, 
			Winter
		}
		public Season sea;

		
	}
}
