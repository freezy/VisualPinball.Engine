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

using NLog;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using System;
using VisualPinball.Engine.VPT.MappingConfig;
using System.Linq;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE switches
	/// </summary>
	///

	class SwitchManager : ManagerWindow<SwitchListData>
	{
		const string RESOURCE_PATH = "Assets/Resources";
		const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";

		protected override string DataTypeName => "Switch";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private List<string> _ids = new List<string>();
		private List<ISwitchableAuthoring> _switchables = new List<ISwitchableAuthoring>();
		private InputManager _inputManager;
		private SwitchListViewItemRenderer _listViewItemRenderer;

		[MenuItem("Visual Pinball/Switch Manager", false, 106)]
		public static void ShowWindow()
		{
			GetWindow<SwitchManager>();
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Switch Manager",
				AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_switch_no.png"));

			_inputManager = new InputManager(RESOURCE_PATH);
			AssetDatabase.Refresh();

			_listViewItemRenderer = new SwitchListViewItemRenderer(_ids, _switchables, _inputManager);

			base.OnEnable();

			ResizeToFit();
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				var mappingConfigData = FindSwitchMappingConfig();

				if (mappingConfigData != null)
				{ 
					FindSwSwitchables((switchableItem, id) =>
					{
						if (FindSwitchMappingEntryByID(id) == null)
						{
							MappingEntryData entry = new MappingEntryData
							{
								ID = id,
								Element = switchableItem.Name,
								Source = SwitchSource.Playfield
							};

							if (switchableItem is BumperAuthoring)
							{
								entry.Description = "Bumper";
							}
							else if (switchableItem is FlipperAuthoring)
							{
								entry.Description = "Flipper";
							}
							else if (switchableItem is GateAuthoring)
							{
								entry.Description = "Gate";
							}
							else if (switchableItem is HitTargetAuthoring)
							{
								entry.Description = "Target";
							}
							else if (switchableItem is KickerAuthoring)
							{
								entry.Description = "Kicker";
							}
							else if (switchableItem is PrimitiveAuthoring)
							{
								entry.Description = "Primitive";
							}
							else if (switchableItem is RubberAuthoring)
							{
								entry.Description = "Rubber";
							}
							else if (switchableItem is SurfaceAuthoring)
							{
								entry.Description = "Surface";
							}
							else if (switchableItem is TriggerAuthoring)
							{
								entry.Description = "Trigger";
							}
							else if (switchableItem is SpinnerAuthoring)
							{
								entry.Description = "Spinner";
							}

							if (switchableItem is KickerAuthoring || switchableItem is TriggerAuthoring)
							{
								entry.Type = SwitchType.OnOff;
							}
							else
							{
								entry.Type = SwitchType.Pulse;
							}

							mappingConfigData.MappingEntries =
							   mappingConfigData.MappingEntries.Append(entry).ToArray();
						}
					});

					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(data, cellRect, column, (switchListData) => {
				switchListData.Update();
			});
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			List<SwitchListData> data = new List<SwitchListData>();

			var mappingConfigData = FindSwitchMappingConfig();

			foreach (var mappingEntryData in mappingConfigData.MappingEntries)
			{
				data.Add(new SwitchListData(mappingEntryData));
			}

			RefreshSwitchables();
			RefreshIDs();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			var mappingConfigData = FindSwitchMappingConfig();

			var entry = new MappingEntryData
			{
				ID = ""
			};

			mappingConfigData.MappingEntries =
			   mappingConfigData.MappingEntries.Append(entry).ToArray();
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
		}
		#endregion

		#region Helper methods
		private void RefreshSwitchables()
		{
			_switchables.Clear();

			if (_table != null)
			{
				foreach (var item in _table.GetComponentsInChildren<ISwitchableAuthoring>())
				{
					_switchables.Add(item);
				}

				_switchables.Sort((s1, s2) => s1.Name.CompareTo(s2.Name));
			}
		}

		private void RefreshIDs()
		{
			FindSwSwitchables((item, id) =>
			{
				if (_ids.IndexOf(id) == -1)
				{
					_ids.Add(id);
				}
			});

			_ids.Sort();
		}

		private void FindSwSwitchables(Action<ISwitchableAuthoring, string> action)
		{
			foreach (var item in _switchables)
			{
				var match = new Regex(@"^(sw)(\d+)$").Match(item.Name);
				if (match.Success)
				{
					action(item, match.Groups[2].Value);
				}
			}
		}

		private MappingConfigData FindSwitchMappingConfig()
		{
			if (_table != null)
			{
				if (_table.MappingConfigs.Count == 0)
				{
					_table.MappingConfigs.Add(new MappingConfigData("Switch", new MappingEntryData[0]));
					_table.Item.Data.NumMappingConfigs = 1;
				}

				return _table.MappingConfigs[0];
			}

			return null;
		}

		private MappingEntryData FindSwitchMappingEntryByID(string id)
		{
			var mappingConfigData = FindSwitchMappingConfig();

			if (mappingConfigData != null)
			{
				foreach (var mappingEntryData in mappingConfigData.MappingEntries)
				{
					if (mappingEntryData.ID == id)
					{
						return mappingEntryData;
					}
				}
			}

			return null;
		}
		#endregion
	}
}
