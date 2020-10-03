using NLog;
using System;
using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher;
using RenderPipeline = VisualPinball.Unity.Patcher.Matcher.RenderPipeline;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Common methods for patching a table during import.
	/// </summary>
	static class PatcherUtil
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Hide the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void Hide(GameObject gameObject)
		{
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Set a new parent for the given child while keeping the position and rotation.
		/// </summary>
		/// <param name="child"></param>
		/// <param name="parent"></param>
		public static void Reparent(GameObject child, GameObject parent)
		{
			var rot = child.transform.rotation;
			var pos = child.transform.position;

			// re-parent the child
			child.transform.SetParent(parent.transform, false);

			child.transform.rotation = rot;
			child.transform.position = pos;
		}

		/// <summary>
		/// Set the material of the gameobject to opaque.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetOpaque(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetOpaque", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					material.SetFloat("_SurfaceType", 0);

					material.SetFloat("_DstBlend", 0);
					material.SetFloat("_ZWrite", 1);

					material.DisableKeyword("_ALPHATEST_ON");
					material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
					material.DisableKeyword("_BLENDMODE_PRE_MULTIPLY");
					material.DisableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Set the material of the gameobject to double sided.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetDoubleSided(GameObject gameObject)
		{

			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetDoubleSided", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					material.EnableKeyword("_DOUBLESIDED_ON");
					material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

					material.SetInt("_DoubleSidedEnable", 1);
					material.SetInt("_DoubleSidedNormalMode", 1);

					material.SetInt("_CullMode", 0);
					material.SetInt("_CullModeForward", 0);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Set the AlphaCutOff value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetAlphaCutOff(GameObject gameObject, float value)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetAlphaCutOff", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					// enable the property
					SetAlphaCutOffEnabled(gameObject);

					// set the cut-off value
					material.SetFloat("_AlphaCutoff", value);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Enable AlphaCutOff for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetAlphaCutOffEnabled", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					material.EnableKeyword("_ALPHATEST_ON");
					material.SetInt("_AlphaCutoffEnable", 1);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Disable NormalMap for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetNormalMapDisabled(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetNormalMapDisabled", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					material.SetTexture("_NormalMap", null);
					material.DisableKeyword("_NORMALMAP");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Set the Metallic value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void SetMetallic(GameObject gameObject, float value)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
				case RenderPipelineType.Urp:
					Logger.Info("Not implemented for {0}: {1} for {2}", gameObject.name, "SetMetallic", RenderPipeline.Current);
					break;

				case RenderPipelineType.Hdrp:
					var unityMat = gameObject.GetComponent<Renderer>().sharedMaterial;
					unityMat.SetFloat("_Metallic", value);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
