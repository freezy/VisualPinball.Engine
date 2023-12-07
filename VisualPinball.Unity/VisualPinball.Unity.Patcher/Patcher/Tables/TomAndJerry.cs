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

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher
{
	[TableNameMatch("TomandJerry")]
	public class TomAndJerry : TablePatcher
	{
		[NameMatch("ShadowsRamp")]
		[NameMatch("JerryHAMMERshadow")]
		[NameMatch("MuscleswithknifeSHADOW")]
		public void HideGameObject(GameObject gameObject)
		{
			PatcherUtil.Hide(gameObject);
		}

		[NameMatch("sw51")]
		[NameMatch("sw40")]
		[NameMatch("sw50")]
		[NameMatch("sw60")]
		[NameMatch("sw41")]
		[NameMatch("sw61")]
		[NameMatch("sw63")]
		[NameMatch("sw53")]
		[NameMatch("sw43")]
		[NameMatch("sw44")]
		[NameMatch("sw54")]
		[NameMatch("sw64")]
		[NameMatch("sw73")]
		[NameMatch("sw73a")]
		[NameMatch("BumperCap2")] // Jerry Bumper
		[NameMatch("BumperCap3")] // Tom Bumper
		public void SetOpaque(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetOpaque(gameObject);
		}

		/// <summary>
		/// The transparent plastic glass had some strange artefacts. We need to adjust the alpha clip.
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("Primitive67")]
		[NameMatch("JerryHammer")]
		[NameMatch("MusclesKnife")]
		public void SetAlphaClip(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetAlphaCutOff(gameObject, 0.05f);
		}

		/// <summary>
		/// Make the children of the ramps doublesided or else the ramps wouldn't be visible.
		/// This patches all children (e. g. Floor, LeftWall, RightWall of the ramps) of the item the same way
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("Ramp5")]
		[NameMatch("Ramp6")]
		[NameMatch("Ramp13")]
		[NameMatch("Ramp15")]
		[NameMatch("Ramp20")]
		[NameMatch("Primitive52")] // side-wall
		[NameMatch("Primitive66")] // jerry at plunger
		public void SetDoubleSided(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetDoubleSided(gameObject);
		}

		[NameMatch("Ramp5")]
		[NameMatch("Ramp6")]
		[NameMatch("Ramp13")]
		[NameMatch("Ramp15")]
		[NameMatch("Ramp20")]
		public void SetMetallic(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetMetallic(gameObject, 1.0f);
		}

		[NameMatch("Lflip", Ref = "Playfield/Flippers/LeftFlipper")]
		[NameMatch("Rflip", Ref = "Playfield/Flippers/RightFlipper")]
		[NameMatch("LFlip1", Ref = "Playfield/Flippers/LeftFlipper1")]
		[NameMatch("Rflip1", Ref = "Playfield/Flippers/RightFlipper1")]
		public void ReparentFlippers(PrimitiveComponent primitive, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);

			primitive.Position = Vector3.zero;
			// primitive.Rotation.y = 0;
		}
	}
}
