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

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE switches
	/// </summary>
	///

	class SwitchManager : ManagerWindow<SwitchListData>
	{
		const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";

		protected override string DataTypeName => "Switch";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private List<string> _ids = new List<string>();
		private List<ISwitchableAuthoring> _switchables = new List<ISwitchableAuthoring>();

		private List<SwitchListData> _listData = new List<SwitchListData>();

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

			_listViewItemRenderer = new SwitchListViewItemRenderer(_ids, _switchables);

			base.OnEnable();

			ResizeToFit();
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				FindSwSitchables((switchableItem, id) =>
				{
					if (_listData.Find((listDataItem) => listDataItem.ID == id) == null)
					{
						_listData.Add(new SwitchListData(id, switchableItem));
					}
				});

				Reload();
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(data, cellRect, column, () => {});
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			RefreshSwitchables();
			RefreshIDs();

			return _listData; 
		}

		protected override void AddNewData(string undoName, string newName)
		{
			_listData.Add(new SwitchListData());
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
			_listData.Remove(data);
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
		
		}
		#endregion

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
			FindSwSitchables((item, id) =>
			{
				if (_ids.IndexOf(id) == -1)
				{
					_ids.Add(id);
				}
			});

			_ids.Sort();
		}

		private void FindSwSitchables(Action<ISwitchableAuthoring, string> action)
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
	}
}
