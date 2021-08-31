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

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableAuthoring : ItemMainRenderableAuthoring<TableData>
	{
		#region Table Data

		[SerializeReference] public LegacyContainer LegacyContainer;
		[SerializeReference] public MappingConfig MappingConfig = new MappingConfig();

		[SerializeField] public MappingsData Mappings;
		[SerializeField] public SerializableDictionary<string, string> TableInfo = new SerializableDictionary<string, string>();
		[SerializeField] public CustomInfoTags CustomInfoTags = new CustomInfoTags();
		[SerializeField] public List<CollectionData> Collections = new List<CollectionData>();

		#endregion

		#region Data

		public float GlobalDifficulty = 0.2f;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Table;
		public override string ItemName => "Table";

		public override TableData InstantiateData() => new TableData();

		public override IEnumerable<Type> ValidParents => Type.EmptyTypes;

		protected override Type MeshAuthoringType => null;
		protected override Type ColliderAuthoringType => null;

		#endregion


		public SceneTableContainer TableContainer => _tableContainer ??= new SceneTableContainer(this);

		[NonSerialized]
		private SceneTableContainer _tableContainer;

		[HideInInspector] [SerializeField] public string physicsEngineId = "VisualPinball.Unity.DefaultPhysicsEngine";
		[HideInInspector] [SerializeField] public string debugUiId;

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

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(TableData data)
		{
			GlobalDifficulty = data.GlobalDifficulty;
			return new List<MonoBehaviour> { this };
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TableData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			return Array.Empty<MonoBehaviour>();
		}

		public override TableData CopyDataTo(TableData data, string[] materialNames, string[] textureNames)
		{
			// update the name
			data.Name = name;
			data.TableHeight = PlayfieldHeight;
			data.GlobalDifficulty = GlobalDifficulty;

			return data;
		}

		#endregion

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
			//TableContainer.Refresh(); // todo needed?

			MappingConfig.RemoveAllSwitches();
			MappingConfig.PopulateSwitches(gle.AvailableSwitches, this);

			MappingConfig.RemoveAllCoils();
			MappingConfig.PopulateCoils(gle.AvailableCoils, this);

			// MappingConfig.RemoveAllLamps();
			// TableContainer.Mappings.PopulateLamps(gle.AvailableLamps, TableContainer.Lightables);
		}
	}
}
