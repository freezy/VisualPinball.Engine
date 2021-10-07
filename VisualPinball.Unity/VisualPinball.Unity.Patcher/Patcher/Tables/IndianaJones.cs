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

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity.VisualPinball.Unity.Patcher.Matcher;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Indiana Jones - The Pinball Adventure", AuthorName = "ninuzzu,tom tower")]
	public class IndianaJones : TablePatcher
	{
		[NameMatch("LeftFlipperSh", IgnoreCase = false)]
		[NameMatch("RightFlipperSh")]
		public void RemoveFlipperShadow(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("Primitive21")]
		public void FixWhateverPrimitive21Is(GameObject gameObject)
		{
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;
			unityMat.SetFloat("_Mode", 1);
			unityMat.SetInt("_SrcBlend", (int)BlendMode.One);
			unityMat.SetInt("_DstBlend", (int)BlendMode.Zero);
			unityMat.SetInt("_ZWrite", 1);
			unityMat.EnableKeyword("_ALPHATEST_ON");
			unityMat.DisableKeyword("_ALPHABLEND_ON");
			unityMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			unityMat.renderQueue = 2450;
		}
	}
}
