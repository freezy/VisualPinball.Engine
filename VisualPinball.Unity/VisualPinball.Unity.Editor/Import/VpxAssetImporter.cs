// ReSharper disable UnusedType.Global

using NLog;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[ScriptedImporter(2, "vpx")]
	public class VpxAssetImporter : ScriptedImporter
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override void OnImportAsset(AssetImportContext ctx)
		{
			Logger.Info("Importing VPX table at {0}...", ctx.assetPath);

			// create root object
			var rootGameObj = new GameObject();
			// add lazy importer, will do a normal in memory import once the object ends up in a scene
			rootGameObj.AddComponent<VpxAssetLazyImporter>();

			ctx.AddObjectToAsset("main obj", rootGameObj);
			ctx.SetMainObject(rootGameObj);
		}
	}
}
