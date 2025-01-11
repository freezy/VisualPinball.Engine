// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	public abstract class MainComponent<TData> : ItemComponent,
		IMainComponent, ILayerableItemComponent
		where TData : ItemData
	{
		public int PlayfieldDetailLevel {
			get {
				var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
				return playfieldComponent ? playfieldComponent.PlayfieldDetailLevel : 10;
			}
		}

		public abstract IEnumerable<MonoBehaviour> SetData(TData data);

		/// <summary>
		/// Updates the component when all other components are updated with <see cref="SetData"/>, so we can reference them.
		/// Also, materials and textures are available here.
		/// </summary>
		/// <param name="data">Item data from the import</param>
		/// <param name="table">Table reference</param>
		/// <param name="materialProvider">Get the material from here</param>
		/// <param name="textureProvider">Get the texture from here</param>
		/// <param name="components">Reference to all other components, by name (which is unique during import)</param>
		/// <returns>A list of updated components (if this item has impact on other components)</returns>
		public abstract IEnumerable<MonoBehaviour> SetReferencedData(TData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components);

		public abstract TData CopyDataTo(TData data, string[] materialNames, string[] textureNames, bool forExport);

		public abstract bool HasProceduralMesh { get; }

		public abstract ItemType ItemType { get; }

		protected T FindComponent<T>(Dictionary<string, IMainComponent> components, string surfaceName) where T : class
		{
			return (components != null && components.ContainsKey(surfaceName.ToLower())
					? components[surfaceName.ToLower()]
					: null)
				as T;
		}

		#region Data

		/// <summary>
		/// Instantiates a new data object with default values. Used for exporting back to .vpx.
		/// </summary>
		/// <returns></returns>
		public abstract TData InstantiateData();

		#endregion

		#region ILayerableItemComponent

		public int EditorLayer { get => _editorLayer; set => _editorLayer = value; }
		public string EditorLayerName { get => _editorLayerName; set => _editorLayerName = value; }
		public bool EditorLayerVisibility { get => _editorLayerVisibility; set => _editorLayerVisibility = value; }

		[SerializeField] private int _editorLayer;
		[SerializeField] private string _editorLayerName  = string.Empty;
		[SerializeField] private bool _editorLayerVisibility = true;

		#endregion
	}
}
