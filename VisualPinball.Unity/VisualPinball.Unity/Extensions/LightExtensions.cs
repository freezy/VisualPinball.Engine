using UnityEngine;

namespace VisualPinball.Unity.Extensions
{
	public static class LightExtensions
	{
		public static GameObject ToUnityPointLight(this Engine.VPT.Light.Light vpxLight)
		{
			var lightGameObject = new GameObject(vpxLight.Name);
			var lightComp = lightGameObject.AddComponent<Light>();
			lightGameObject.isStatic = true;

			// Set color and position
			lightComp.color = vpxLight.Data.Color.ToUnityColor();
			lightComp.intensity = vpxLight.Data.Intensity / 5f;
			lightComp.range = vpxLight.Data.Falloff * 0.01f;

			// Set the position (or any transform property)
			lightGameObject.transform.position = vpxLight.Data.Center.ToUnityVector3(50f);

			return lightGameObject;
		}
	}
}
