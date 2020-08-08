// ReSharper disable StringLiteralTypo

using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher.Item;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher.Patcher.Tables
{
	// TODO Problem: the table has wrong meta info, we should adapt the metamatch
	[MetaMatch(TableName = "Beach Bums (HH Mod - Gottlieb 1986)", AuthorName = "Retro Bash")]
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
	}
}
