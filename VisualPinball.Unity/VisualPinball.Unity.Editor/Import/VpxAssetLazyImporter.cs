using System.IO;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.Import.Job;

namespace VisualPinball.Unity.Editor.Import
{
	[ExecuteInEditMode]
	public class VpxAssetLazyImporter : MonoBehaviour
	{
		[SerializeField] [HideInInspector]
		private bool _importComplete = false;

		protected virtual void Awake()
		{
			if (_importComplete) return;

#if UNITY_EDITOR
			var obj = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
			if (obj == null) return;

			var path = AssetDatabase.GetAssetPath(obj);

			GameObject tableRoot = new GameObject(obj.name);
			var importer = tableRoot.AddComponent<VpxImporter>();
			var table = TableLoader.LoadTable(path);
			importer.Import(Path.GetFileName(path), table);
#endif
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
