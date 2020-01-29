
using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Importer
{
	public class MaterialImporter
	{
		private readonly PbrMaterial[] _materials;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

		public MaterialImporter(PbrMaterial[] materials)
		{
			_materials = materials;
		}

		public void ImportMaterials(string materialFolder, string textureFolder)
		{
			Profiler.Start("Run materials job");
			using (var job = new MaterialJob(_materials, materialFolder)) {
				var handle = job.Schedule(_materials.Length, 64);
				handle.Complete();
			}
			Profiler.Stop("Run materials job");

			// create and write materials to disk
			foreach (var material in _materials) {
				var path = material.GetUnityFilename(materialFolder);
				var unityMaterial = material.ToUnityMaterial();
				if (material.HasMap) {
					var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(material.Map.GetUnityFilename(textureFolder));
					unityMaterial.SetTexture(MainTex, tex);
				}
				if (material.HasNormalMap) {
					var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(material.NormalMap.GetUnityFilename(textureFolder));
					unityMaterial.SetTexture(BumpMap, tex);
				}
				AssetDatabase.CreateAsset(unityMaterial, path);
			}
			AssetDatabase.SaveAssets();
			Logger.Info("Saved {0} materials to {1}.", _materials.Length, materialFolder);
		}
	}

	internal struct MaterialJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		private NativeArray<IntPtr> _materials;

		public MaterialJob(IEnumerable<PbrMaterial> materials, string materialFolder)
		{
			_materials = new NativeArray<IntPtr>(materials.Select(MemHelper.ToIntPtr).ToArray(), Allocator.Persistent);
		}

		public void Execute(int index)
		{
			// unpack pointers
			var material = MemHelper.ToObj<PbrMaterial>(_materials[index]);

			// analyze
			material.AnalyzeMap();
		}

		public void Dispose()
		{
			_materials.Dispose();
		}
	}
}
