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

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableAuthoring : ItemMainRenderableAuthoring<Table, TableData>
	{
		#region Table Data

		[SerializeReference] public LegacyContainer LegacyContainer;
		[SerializeField] public MappingsData Mappings;
		[SerializeField] public SerializableDictionary<string, string> TableInfo = new SerializableDictionary<string, string>();
		[SerializeField] public CustomInfoTags CustomInfoTags = new CustomInfoTags();
		[SerializeField] public List<CollectionData> Collections = new List<CollectionData>();

		#endregion

		#region Data

		public float TableHeight;

		#endregion

		protected override Table InstantiateItem(TableData data) => new Table(TableContainer, data);
		protected override TableData InstantiateData() => new TableData();

		protected override Type MeshAuthoringType { get; } = null;
		protected override Type ColliderAuthoringType { get; } = null;

		public override IEnumerable<Type> ValidParents => new Type[0];

		public new Table Table => Item;
		public new SceneTableContainer TableContainer => _tableContainer ??= new SceneTableContainer(this);

		[NonSerialized]
		private SceneTableContainer _tableContainer;

		[HideInInspector] [SerializeField] public string physicsEngineId = "VisualPinball.Unity.DefaultPhysicsEngine";
		[HideInInspector] [SerializeField] public string debugUiId;

		private readonly Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();
		// note: this cache needs to be keyed on the engine material itself so that when its recreated due to property changes the unity material
		// will cache miss and get recreated as well
		private readonly Dictionary<string, Material> _unityMaterials = new Dictionary<string, Material>();
		/// <summary>
		/// Keeps a list of serializables names that need recreation, serialized and
		/// lazy so when undo happens they'll be considered dirty again
		/// </summary>
		[HideInInspector] [SerializeField] private readonly Dictionary<Type, List<string>> _dirtySerializables = new Dictionary<Type, List<string>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private void Reset()
		{
			_tableContainer ??= new SceneTableContainer(this);
		}

		//Private runtime values needed for camera adjustments.
		[HideInInspector] [SerializeField] public  Bounds _tableBounds;
		[HideInInspector] [SerializeField] public  Vector3 _tableCenter;

		public void Awake()
		{
			//Store table information
			_tableBounds = GetTableBounds();
			_tableCenter = GetTableCenter();
		}

		protected virtual void Start()
		{

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().Init(this);
			}
		}

		public void RestoreCollections(List<CollectionData> collections)
		{
			Collections.Clear();
			Collections.AddRange(collections);
		}

		public void RestoreMappings(MappingsData mappings)
		{
			Mappings.Coils = mappings.Coils.ToArray();
			Mappings.Switches = mappings.Switches.ToArray();
			Mappings.Wires = mappings.Wires.ToArray();
		}

		public void MarkDirty<T>(string name) where T : IItem
		{
			if (!_dirtySerializables.ContainsKey(typeof(T))) {
				_dirtySerializables[typeof(T)] = new List<string>();
			}
			_dirtySerializables[typeof(T)].Add(name.ToLower());
		}

		private bool CheckDirty<T>(string name, Func<bool> action = null) where T : IItem
		{
			List<string> lst;
			if (_dirtySerializables.TryGetValue(typeof(T), out lst)) {
				if (lst.Contains(name)) {
					var remove = true;
					if (action != null) {
						remove = action.Invoke();
					}
					if (remove) {
						_dirtySerializables[typeof(T)].Remove(name);
					}
					return true;
				}
			}

			return false;
		}

		public override IEnumerable<MonoBehaviour> SetData(TableData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			TableHeight = data.TableHeight;
			return new List<MonoBehaviour> { this };
		}

		public override TableData CopyDataTo(TableData data, string[] materialNames, string[] textureNames)
		{
			// update the name
			data.Name = name;

			return data;
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			// check to see if the texture we're after has been flagged as dirty and thus needs to be recreated from table data
			var forceRecreate = CheckDirty<Texture>(lowerName);

			// don't need to recreate it, and we have the texture in cache
			if (!forceRecreate && _unityTextures.ContainsKey(lowerName)) {
				return _unityTextures[lowerName];
			}
			// create unity texture from vpe data and put in a cache for future retrievals
			var tableTex = Table.GetTexture(lowerName);
			if (tableTex != null) {
				var unityTex = tableTex.ToUnityTexture();
				_unityTextures[lowerName] = unityTex;
				return unityTex;
			}
			return null;
		}

		public void AddMaterial(PbrMaterial vpxMat, Material material)
		{
			_unityMaterials.TryGetValue(vpxMat.Id, out var oldMaterial);
			_unityMaterials[vpxMat.Id] = material;
			if (oldMaterial != null) {
				Destroy(oldMaterial);
			}
		}

		public Material GetMaterial(PbrMaterial vpxMat)
		{
			if (_unityMaterials.ContainsKey(vpxMat.Id)) {
				return _unityMaterials[vpxMat.Id];
			}
			return null;
		}

		public Vector3 GetTableCenter()
		{
			var playfield = GetComponentInChildren<PlayfieldAuthoring>().gameObject;
			return playfield.GetComponent<MeshRenderer>().bounds.center;
		}

		public Bounds GetTableBounds()
		{

			var tableBounds = new Bounds();

			var mrs = GetComponentsInChildren<Renderer>();
			foreach(var mr in mrs)
			{
				tableBounds.Encapsulate(mr.bounds);
			}

			return tableBounds;
		}

		public void RepopulateHardware(IGamelogicEngine gle)
		{
			TableContainer.Refresh();

			Mappings.RemoveAllSwitches();
			TableContainer.Mappings.PopulateSwitches(gle.AvailableSwitches, TableContainer.Switchables, TableContainer.SwitchableDevices);

			Mappings.RemoveAllCoils();
			TableContainer.Mappings.PopulateCoils(gle.AvailableCoils, TableContainer.Coilables, TableContainer.CoilableDevices);

			Mappings.RemoveAllLamps();
			TableContainer.Mappings.PopulateLamps(gle.AvailableLamps, TableContainer.Lightables);
		}
	}
}
