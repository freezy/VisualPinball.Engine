using System.IO;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer.AssetHandler
{
	internal static class AssetUtility
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void CreateTexture(Texture vpxTex, string textureFolder)
		{
			Profiler.Start("ToUnityTexture");
			var unityTex = vpxTex.ToUnityTexture();
			Profiler.Stop("ToUnityTexture");
			byte[] bytes = null;
			if (vpxTex.IsHdr) {
				Profiler.Start("HDR");
				// this is a hack to decompress the texture or unity will throw an error as it cant write compressed files.
				var renderTex = RenderTexture.GetTemporary(
					unityTex.width,
					unityTex.height,
					0,
					RenderTextureFormat.Default,
					RenderTextureReadWrite.Linear
				);
				var previous = RenderTexture.active;
				RenderTexture.active = renderTex;
				Graphics.Blit(unityTex, renderTex);
				var rawImage = new Texture2D(unityTex.width, unityTex.height, TextureFormat.RGBAFloat, false);
				rawImage.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
				rawImage.Apply();
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(renderTex);
				bytes = rawImage.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
				Profiler.Stop("HDR");

			} else {
				Profiler.Start("EncodeToPNG");
				bytes = unityTex.EncodeToPNG();
				Profiler.Stop("EncodeToPNG");
			}

			Profiler.Start("I/O");
			var path = vpxTex.GetUnityFilename(textureFolder);
			File.WriteAllBytes(path, bytes);
			Profiler.Stop("I/O");
			Profiler.Start("ImportAsset");
			AssetDatabase.ImportAsset(path);
			Profiler.Stop("ImportAsset");
		}
	}
}
