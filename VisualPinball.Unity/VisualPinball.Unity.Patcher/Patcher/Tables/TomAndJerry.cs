// ReSharper disable StringLiteralTypo

using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;
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
			gameObject.SetActive(false);
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
		public void SetOpaque(GameObject gameObject)
		{
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;

			unityMat.SetFloat("_SurfaceType", 0);

			unityMat.SetFloat("_DstBlend", 0);
			unityMat.SetFloat("_ZWrite", 1);

			unityMat.DisableKeyword("_ALPHATEST_ON");
			unityMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			unityMat.DisableKeyword("_BLENDMODE_PRE_MULTIPLY");
			unityMat.DisableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
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
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;

			unityMat.EnableKeyword("_ALPHATEST_ON");
			unityMat.SetFloat("_AlphaCutoff", 0.05f);
			unityMat.SetInt("_AlphaCutoffEnable", 1);
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
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;

			unityMat.EnableKeyword("_DOUBLESIDED_ON");
			unityMat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

			unityMat.SetInt("_DoubleSidedEnable", 1);
			unityMat.SetInt("_DoubleSidedNormalMode", 1);

			unityMat.SetInt("_CullMode", 0);
			unityMat.SetInt("_CullModeForward", 0);
		}

		[NameMatch("Ramp5")]
		[NameMatch("Ramp6")]
		[NameMatch("Ramp13")]
		[NameMatch("Ramp15")]
		[NameMatch("Ramp20")]
		public void SetMetallic(GameObject gameObject)
		{
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;
			unityMat.SetFloat("_Metallic", 1.0f);
		}
	}
}
