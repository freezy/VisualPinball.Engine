// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity.Editor
{
	internal class VpxPlayfieldPrefab : IVpxPrefab
	{
		public GameObject GameObject { get; }
		public IItemMainAuthoring MainComponent => _playfieldComponent;
		public IEnumerable<GameObject> MeshGameObjects => Array.Empty<GameObject>();
		public IRenderable Renderable => _primitive;
		public bool ExtractMesh => true;
		public bool SkipParenting => true;

		private readonly Primitive _primitive;
		private readonly PlayfieldAuthoring _playfieldComponent;
		private readonly List<MonoBehaviour> _updatedComponents = new List<MonoBehaviour>();

		public VpxPlayfieldPrefab(GameObject playfieldGo, Primitive item)
		{
			_primitive = item;
			_playfieldComponent = playfieldGo.GetComponent<PlayfieldAuthoring>();
			GameObject = playfieldGo;
		}

		public void SetReferencedData(IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var playfieldComp = GameObject.GetComponent<PlayfieldAuthoring>();
			if (playfieldComp) {
				var updatedComponents = playfieldComp.SetReferencedData(_primitive.Data, materialProvider, textureProvider);
				_updatedComponents.AddRange(updatedComponents);
			}
		}

		public void PersistData()
		{
			EditorUtility.SetDirty(GameObject);
			PrefabUtility.RecordPrefabInstancePropertyModifications(GameObject.transform);
			foreach (var comp in _updatedComponents.Distinct()) {
				PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
			}
		}

		public void FreeBinaryData() => _primitive.Data.FreeBinaryData();
	}
}
