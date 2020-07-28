using System.IO;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.Import.Job;

namespace VisualPinball.Unity.Editor.Import
{
	/// <summary>
	/// This component is attached to a game object when using the scripted importer for vpx files
	/// (i.e. when you have a .vpx in the unity project itself). When the asset is then placed in
	/// a scene, this executes the table importer flow and destroys itself.
	/// </summary>
	[ExecuteInEditMode]
	public class VpxAssetLazyImporter : MonoBehaviour
	{
		[SerializeField] [HideInInspector]
		private bool _importComplete = false;

		protected virtual void Awake()
		{
			if (_importComplete) return;

			var obj = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
			if (obj == null) return;

			var path = AssetDatabase.GetAssetPath(obj);

			GameObject tableRoot = new GameObject(obj.name);
			var converter = tableRoot.AddComponent<VpxConverter>();
			var table = TableLoader.LoadTable(path);
			converter.Convert(Path.GetFileName(path), table);

			_importComplete = true;
		}

		protected virtual void Update()
		{
			if (_importComplete) {
				DestroyImmediate(gameObject);
			}
		}
	}
}
