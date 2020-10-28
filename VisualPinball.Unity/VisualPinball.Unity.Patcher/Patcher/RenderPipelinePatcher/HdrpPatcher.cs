using NLog;
using UnityEngine;

namespace VisualPinball.Unity.Patcher.RenderPipelinePatcher
{
	public class HdrpPatcher : IRenderPipelinePatcher
	{
		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public void SetOpaque(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.SetFloat("_SurfaceType", 0);

			material.SetFloat("_DstBlend", 0);
			material.SetFloat("_ZWrite", 1);

			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.DisableKeyword("_BLENDMODE_PRE_MULTIPLY");
			material.DisableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
		}

		public void SetDoubleSided(GameObject gameObject)
		{

			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.EnableKeyword("_DOUBLESIDED_ON");
			material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

			material.SetInt("_DoubleSidedEnable", 1);
			material.SetInt("_DoubleSidedNormalMode", 1);

			material.SetInt("_CullMode", 0);
			material.SetInt("_CullModeForward", 0);
		}

		public void SetTransparentDepthPrepassEnabled(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");

			material.SetInt("_TransparentDepthPrepassEnable", 1);
			material.SetInt("_AlphaDstBlend", 10);
			material.SetInt("_ZTestModeDistortion", 4);

			material.SetShaderPassEnabled("TransparentDepthPrepass", true);
			material.SetShaderPassEnabled("RayTracingPrepass", true);

		}

		public void SetAlphaCutOff(GameObject gameObject, float value)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			// enable the property
			SetAlphaCutOffEnabled(gameObject);

			// set the cut-off value
			material.SetFloat("_AlphaCutoff", value);
		}

		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.EnableKeyword("_ALPHATEST_ON");
			material.SetInt("_AlphaCutoffEnable", 1);
		}

		public void SetNormalMapDisabled(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.SetTexture("_NormalMap", null);
			material.DisableKeyword("_NORMALMAP");
		}

		public void SetMetallic(GameObject gameObject, float value)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;
			material.SetFloat("_Metallic", value);
		}
	}
}
