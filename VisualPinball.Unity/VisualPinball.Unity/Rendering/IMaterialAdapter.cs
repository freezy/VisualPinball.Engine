// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
	public interface IMaterialAdapter
	{
		/// <summary>
		/// Set the material of the gameobject to opaque.
		///
		/// </summary>
		/// <param name="gameObject"></param>
		void SetOpaque(GameObject gameObject);

		/// <summary>
		/// Set the material of the gameobject to double sided.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetDoubleSided(GameObject gameObject);

		void SetTransparentDepthPrepassEnabled(GameObject gameObject);

		/// <summary>
		/// Set the AlphaCutOff value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetAlphaCutOff(GameObject gameObject, float value);

		/// <summary>
		/// Enable AlphaCutOff for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetAlphaCutOffEnabled(GameObject gameObject);

		/// <summary>
		/// Disable NormalMap for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetNormalMapDisabled(GameObject gameObject);

		/// <summary>
		/// Set the Metallic value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetMetallic(GameObject gameObject, float value);
	}
}
