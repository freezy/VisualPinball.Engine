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
using UnityEngine.InputSystem;
using System;
using System.IO;
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
		const string resourcesPath = "Assets/Resources";
		const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";

		protected override string DataTypeName => "Switch";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private List<string> _ids = new List<string>();
		private List<ISwitchableAuthoring> _switchables = new List<ISwitchableAuthoring>();
		private List<SwitchListData> _listData = new List<SwitchListData>();
		private List<string> _inputSystem = new List<string>();
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

			_listViewItemRenderer = new SwitchListViewItemRenderer(_ids, _switchables, _inputSystem);

			RefreshInputActions();

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
						MappingEntryData entry = new MappingEntryData();
						entry.ID = id;
						entry.Element = switchableItem.Name;
						entry.Source = SwitchSource.Playfield;

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
					});

					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(data, cellRect, column, () => { });
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			List<SwitchListData> data = new List<SwitchListData>();

			var mappingConfigData = FindSwitchMappingConfig();

			foreach (var mappingEntry in mappingConfigData.MappingEntries)
			{
				data.Add(new SwitchListData(mappingEntry));
			}
			
			RefreshSwitchables();
			RefreshIDs();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			var mappingConfigData = FindSwitchMappingConfig();

			var entry = new MappingEntryData();
			entry.ID = "";

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

		private void RefreshInputActions()
		{
			InputActionAsset asset = null;

			try
			{
				if (!Directory.Exists(resourcesPath))
				{
					Directory.CreateDirectory(resourcesPath);
				}

				var inputActionsPath = resourcesPath + "/" + InputManager.RESOURCE_NAME + ".inputactions";

				if (File.Exists(inputActionsPath))
				{
					asset = InputActionAsset.FromJson(File.ReadAllText(inputActionsPath));
				}
				else
				{
					asset = InputManager.GetDefaultInputActionAsset();

					File.WriteAllText(inputActionsPath, asset.ToJson());
					AssetDatabase.Refresh();
				}
			}

			catch (Exception e)
			{
				Debug.Log(e);
			}

			if (asset == null)
			{
				asset = InputManager.GetDefaultInputActionAsset();
			}

			_inputSystem.Clear();

			foreach (var map in asset.actionMaps)
			{
				foreach (var inputAction in map.actions)
				{
					_inputSystem.Add(inputAction.name);

					Debug.Log(inputAction.name);
				}
			}
		}

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
	}
}
