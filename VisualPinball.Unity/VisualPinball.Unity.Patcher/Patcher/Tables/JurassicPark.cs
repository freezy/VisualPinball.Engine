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
			PatcherUtil.SetNormalMapDisabled(gameObject);
		}


		[NameMatch("LFLogo", Ref="Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref="Flippers/RightFlipper")]
		[NameMatch("RFLogo1", Ref="Flippers/UpperRightFlipper")]
		public void ReparentFlippers(GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
		}

		[NameMatch("PLeftFlipper")]
		[NameMatch("PRightFlipper")]
		[NameMatch("PRightFlipper1")]
		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			PatcherUtil.SetAlphaCutOffEnabled(gameObject);
		}

		[NameMatch("Primitive_Plastics")]
		public void SetOpaque(GameObject gameObject)
		{
			PatcherUtil.SetOpaque(gameObject);
		}
	}
}
