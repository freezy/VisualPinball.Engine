using UnityEngine;
using Material = VisualPinball.Engine.VPT.Material;

namespace VisualPinball.Unity.VisualPinball.Unity.Scene
{
	public class MaterialAsset : ScriptableObject
	{
		public Material Material;

		public MaterialAsset(Material material)
		{
			Material = material;
		}
	}
}
