// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Simple class to encapsulate Curves as assets
	/// </summary>
	[CreateAssetMenu]
	public class AnimationCurveAsset : ScriptableObject
	{
		public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

		public static implicit operator AnimationCurve(AnimationCurveAsset me)
		{
			return me.curve;
		}
		public static implicit operator AnimationCurveAsset(AnimationCurve curve)
		{
			AnimationCurveAsset asset = ScriptableObject.CreateInstance<AnimationCurveAsset>();
			asset.curve = curve;
			return asset;
		}
	}
}
