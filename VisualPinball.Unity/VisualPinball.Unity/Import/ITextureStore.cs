using UnityEngine;

namespace VisualPinball.Unity.Import
{
	public interface ITextureStore
	{
		void AddTexture(string name, Texture2D texture);
		Texture2D GetTexture(string name);
	}
}
