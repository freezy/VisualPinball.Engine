using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;
using Unity.Collections;
using Unity.Jobs;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Import.AssetHandler;

namespace VisualPinball.Unity.Import.Job
{
	public class MaterialImporter
	{
		private readonly PbrMaterial[] _materials;
		private readonly IAssetHandler _assetHandler;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public MaterialImporter(PbrMaterial[] materials, IAssetHandler assetHandler)
		{
			_materials = materials;
			_assetHandler = assetHandler;
		}

		public void ImportMaterials()
		{
			var debug = new StringBuilder();

			using (var job = new MaterialJob(_materials, _assetHandler)) {
				var handle = job.Schedule(_materials.Length, 64);
				handle.Complete();
			}

			// create and write materials to disk
			foreach (var material in _materials) {
				debug.Append(material);
				_assetHandler.SaveMaterial(material, material.ToUnityMaterial(_assetHandler, null, debug));
				debug.AppendLine("============================================================");
			}

			// dump debug log
			// var writer = new StreamWriter("Assets/debug2.txt", false);
			// writer.Write(debug.ToString());
			// writer.Close();

			_assetHandler.OnMaterialsSaved(_materials);
		}
	}

	internal struct MaterialJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		private NativeArray<IntPtr> _materials;

		public MaterialJob(IEnumerable<PbrMaterial> materials, IAssetHandler assetHandler)
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
