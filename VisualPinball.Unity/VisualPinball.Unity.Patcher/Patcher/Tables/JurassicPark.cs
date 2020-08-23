// ReSharper disable StringLiteralTypo

using UnityEngine;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Jurassic Park (Data East)", AuthorName = "Dark & Friends")]
	public class JurassicPark
	{
		/// <summary>
		/// Removing the normal map.
		/// The normal map of the TRex Head is bad and contains invalid data.
		/// This causes the entire unity editor window to become black and the play mode flicker if normal map scale is higher than 0.
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("TrexMain")]
		public void FixBrokenNormalMap(GameObject gameObject)
		{
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;
			unityMat.SetTexture("_NormalMap", null);
			unityMat.DisableKeyword("_NORMALMAP");
		}


		[NameMatch("LFLogo", Ref="Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref="Flippers/RightFlipper")]
		[NameMatch("RFLogo1", Ref="Flippers/UpperRightFlipper")]
		public void ReparentFlippers(GameObject gameObject, ref GameObject parent)
		{
			var rot = gameObject.transform.rotation;
			var pos = gameObject.transform.position;

			// re-parent the child
			gameObject.transform.SetParent(parent.transform, false);

			gameObject.transform.rotation = rot;
			gameObject.transform.position = pos;
		}

		[NameMatch("PLeftFlipper")]
		[NameMatch("PRightFlipper")]
		[NameMatch("PRightFlipper1")]
		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;
			unityMat.SetFloat("_AlphaCutoffEnable", 1);
			unityMat.EnableKeyword("_ALPHATEST_ON");
		}

		[NameMatch("Primitive_Plastics")]
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
	}
}
