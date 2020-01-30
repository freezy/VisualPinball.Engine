// ReSharper disable UnusedType.Global

using System.IO;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Importer.AssetHandler;
using VisualPinball.Unity.Importer.Job;

namespace VisualPinball.Unity.Importer.Editor
{
	[ScriptedImporter(1, "vpx")]
	public class VpxAssetImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			// create root object
			var rootGameObj = new GameObject();
			var importer = rootGameObj.AddComponent<VpxImporter>();

			// load table
			var table = TableLoader.LoadTable(ctx.assetPath);

			// instantiate asset handler
			var assetHandler = new AssetVpxHandler(ctx);

			importer.Import(Path.GetFileName(ctx.assetPath), table, assetHandler);

			ctx.AddObjectToAsset("main obj", rootGameObj);
			ctx.SetMainObject(rootGameObj);

			// select imported object
			Selection.activeObject = rootGameObj;

			Profiler.Print();
			Profiler.Reset();
		}
	}
}
