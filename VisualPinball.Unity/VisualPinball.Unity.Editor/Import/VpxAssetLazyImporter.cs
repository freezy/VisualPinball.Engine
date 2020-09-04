// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
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
