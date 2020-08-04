// ReSharper disable StringLiteralTypo

using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher.Item;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher.Patcher.Tables
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
	}
}
