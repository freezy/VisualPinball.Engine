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
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableComponent : ItemMainRenderableComponent<TableData>
	{
		[SerializeReference] public LegacyContainer LegacyContainer;
		[SerializeReference] public MappingConfig MappingConfig = new MappingConfig();

		[SerializeField] public SerializableDictionary<string, string> TableInfo = new SerializableDictionary<string, string>();
		[SerializeField] public CustomInfoTags CustomInfoTags = new CustomInfoTags();
		[SerializeField] public List<CollectionData> Collections = new List<CollectionData>();

		#region Data

		public float GlobalDifficulty = 0.2f;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Table;
		public override string ItemName => "Table";

		public override TableData InstantiateData() => new TableData();

		public override IEnumerable<Type> ValidParents => Type.EmptyTypes;

		protected override Type MeshComponentType => null;
		protected override Type ColliderComponentType => null;

		#endregion

		public SceneTableContainer TableContainer => _tableContainer ??= new SceneTableContainer(this);

		[NonSerialized]
		private SceneTableContainer _tableContainer;

		[HideInInspector] [SerializeField] public string physicsEngineId = "VisualPinball.Unity.DefaultPhysicsEngine";
		[HideInInspector] [SerializeField] public string debugUiId;

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

		public void RestoreCollections(List<CollectionData> collections)
		{
			Collections.Clear();
			Collections.AddRange(collections);
		}

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(TableData data)
		{
			GlobalDifficulty = data.GlobalDifficulty;
			return new List<MonoBehaviour> { this };
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TableData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainComponent> components)
		{
			return Array.Empty<MonoBehaviour>();
		}

		public override TableData CopyDataTo(TableData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			data.TableHeight = PlayfieldHeight;
			data.GlobalDifficulty = GlobalDifficulty;

			return data;
		}

		#endregion

		public Vector3 GetTableCenter()
		{
			var playfield = GetComponentInChildren<PlayfieldComponent>().gameObject;
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
			MappingConfig.RemoveAllSwitches();
			MappingConfig.PopulateSwitches(gle.AvailableSwitches, this);

			MappingConfig.RemoveAllCoils();
			MappingConfig.PopulateCoils(gle.AvailableCoils, this);

			MappingConfig.RemoveAllLamps();
			MappingConfig.PopulateLamps(gle.AvailableLamps, this);

			// hook up plunger
			var plunger = GetComponentInChildren<PlungerComponent>();
			if (plunger) {
				MappingConfig.AddWire(new WireMapping {
					Description = "Manual Plunger",
					Source = SwitchSource.InputSystem,
					SourceInputActionMap = InputConstants.MapCabinetSwitches,
					SourceInputAction = InputConstants.ActionPlunger,
					DestinationDevice = plunger,
					DestinationDeviceItem = PlungerComponent.PullCoilId
				});
			}
		}
	}
}
