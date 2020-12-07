using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Logic Engine/WPCEmu DMD Texture Replacer")]
	public class WPCEmuDMDTextureReplacer : MonoBehaviour
	{
		void Start()
		{
			var wpcEmuGameEngineAuthoring = FindObjectOfType<WPCEmuGameEngineAuthoring>();
			if (wpcEmuGameEngineAuthoring != null)
			{
				GetComponent<MeshRenderer>().material.mainTexture = wpcEmuGameEngineAuthoring.Texture;
			}
		}
	}
}
