using UnityEngine;

namespace VisualPinball.Unity.Extensions
{
	public static class LightExtension
	{
		public static GameObject ToUnityPointLight(this Engine.VPT.Light.Light vpxLight)
		{
			var lightGameObject = new GameObject(vpxLight.Name);
			var lightComp = lightGameObject.AddComponent<Light>();

			// Set color and position
			lightComp.color = vpxLight.Data.Color.ToUnityColor();
			lightComp.intensity = vpxLight.Data.Intensity;
			lightComp.range = vpxLight.Data.Falloff * 0.01f;

			// Set the position (or any transform property)
			lightGameObject.transform.position = Mesh.GlobalMatrix.MultiplyPoint(vpxLight.Data.Center.ToUnityVector3(50f));

			return lightGameObject;
		}
	}
}
