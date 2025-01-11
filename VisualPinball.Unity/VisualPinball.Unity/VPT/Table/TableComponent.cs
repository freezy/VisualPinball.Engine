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

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableComponent : MainRenderableComponent<TableData>
	{
		[SerializeReference] public LegacyContainer LegacyContainer;
		[SerializeReference] public MappingConfig MappingConfig = new MappingConfig();

		[SerializeField] public SerializableDictionary<string, string> TableInfo = new SerializableDictionary<string, string>();
		[SerializeField] [Obsolete("Use MappingConfig")] public CustomInfoTags CustomInfoTags = new CustomInfoTags();
		[SerializeField] public List<CollectionData> Collections = new List<CollectionData>();

		#region Data

		public float GlobalDifficulty = 0.2f;
		public int OverridePhysics;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Table;
		public override string ItemName => "Table";

		public override TableData InstantiateData() => new TableData();

		public override bool HasProceduralMesh => true;

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
			OverridePhysics = data.OverridePhysics;
			return new List<MonoBehaviour> { this };
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TableData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			return Array.Empty<MonoBehaviour>();
		}

		public override TableData CopyDataTo(TableData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			data.TableHeight = 0;
			data.GlobalDifficulty = GlobalDifficulty;
			data.OverridePhysics = OverridePhysics;

			return data;
		}

		#endregion

		public Vector3 GetTableCenter()
		{
			var playfield = GetComponentInChildren<PlayfieldComponent>();
			var mr = playfield.GetComponent<MeshRenderer>();
			if (mr) {
				return mr.bounds.center;
			}
			return new Vector3(
				Physics.ScaleToWorld(playfield.Width / 2),
				0,
				-Physics.ScaleToWorld(playfield.Height / 2)
			);
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
			MappingConfig.Clear(false);
			MappingConfig.PopulateSwitches(gle.RequestedSwitches, this);
			MappingConfig.PopulateLamps(gle.RequestedLamps, this);
			MappingConfig.PopulateCoils(gle.RequestedCoils, this);
			MappingConfig.PopulateWires(gle.AvailableWires, this);

			// hook up plunger
			var plunger = GetComponentInChildren<PlungerComponent>();
			if (plunger) {
				const string description = "Manual Plunger";
				var plungerMapping = MappingConfig.Wires.FirstOrDefault(mc => mc.Description == description);
				if (plungerMapping == null) {
					var mapping = new WireMapping().WithId();
					mapping.Description = description;
					mapping.Source = SwitchSource.InputSystem;
					mapping.SourceInputActionMap = InputConstants.MapCabinetSwitches;
					mapping.SourceInputAction = InputConstants.ActionPlunger;
					mapping.DestinationDevice = plunger;
					mapping.DestinationDeviceItem = PlungerComponent.PullCoilId;
					MappingConfig.AddWire(mapping);
				}
			}
		}


		public override void CopyFromObject(GameObject go)
		{
			throw new Exception("Copying object data is currently only used for replacing objects. Don't replace the table. Refactor this if necessary in the future.");
		}
	}
}
