// ReSharper disable StringLiteralTypo

using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher
{
	[TableNameMatch("TomandJerry")]
	public class TomAndJerry
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
			PatcherUtil.SetOpaque(gameObject);
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
			PatcherUtil.SetAlphaCutOff(gameObject, 0.05f);
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
			PatcherUtil.SetDoubleSided(gameObject);
		}

		[NameMatch("Ramp5")]
		[NameMatch("Ramp6")]
		[NameMatch("Ramp13")]
		[NameMatch("Ramp15")]
		[NameMatch("Ramp20")]
		public void SetMetallic(GameObject gameObject)
		{
			PatcherUtil.SetMetallic(gameObject, 1.0f);
		}

		[NameMatch("Lflip", Ref = "Flippers/LeftFlipper")]
		[NameMatch("Rflip", Ref = "Flippers/RightFlipper")]
		[NameMatch("LFlip1", Ref = "Flippers/LeftFlipper1")]
		[NameMatch("Rflip1", Ref = "Flippers/RightFlipper1")]
		public void ReparentFlippers(GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
		}
	}
}
