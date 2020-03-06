// ReSharper disable StringLiteralTypo

using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity.Patcher.Matcher.Item;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher.Patcher.Tables
{
	[MetaMatch(TableName = "Indiana Jones - The Pinball Adventure", AuthorName = "ninuzzu,tom tower")]
	public class IndianaJones
	{
		[NameMatch("LeftFlipperSh", IgnoreCase = false)]
		[NameMatch("RightFlipperSh")]
		public void RemoveFlipperShadow(GameObject gameObject)
		{
			gameObject.SetActive(false);
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
