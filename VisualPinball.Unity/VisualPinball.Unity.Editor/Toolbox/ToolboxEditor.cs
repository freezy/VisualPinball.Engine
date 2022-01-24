// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Engine.VPT.MetalWireGuide;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class ToolboxEditor : LockingTableEditorWindow
	{

		/// <summary>
		/// This event is called each time the ToolBoxEditor create a new Item
		/// </summary>
		/// <remarks>
		/// e.g. used by the <see cref="LayerEditor"/> for auto-assigning this item to the first selected layer
		/// </remarks>
		public static event Action<GameObject> ItemCreated;

		[MenuItem("Visual Pinball/Toolbox", false, 201)]
		public static void ShowWindow()
		{
			GetWindow<ToolboxEditor>("Toolbox");
		}

		/// <summary>
		/// Called when the selected table changes
		/// </summary>
		/// <param name="table"></param>
		protected override void SetTable(TableComponent table) { }

		private void OnGUI()
		{
			const IconColor iconColor = IconColor.Gray;
			var iconSize = position.width / 2f - 4.5f;
			var buttonStyle = new GUIStyle(GUI.skin.button) {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageAbove
			};

			if (GUILayout.Button("New Table")) {
				var tableContainer = new FileTableContainer();
				var converter = new VpxSceneConverter(tableContainer);
				var rootGameObj = converter.Convert(false);
				Selection.activeGameObject = rootGameObj;
				Undo.RegisterCreatedObjectUndo(rootGameObj, "New Table");
			}

			if (TableComponent == null) {
				return;
			}

			EditorGUILayout.Space();
			GUILayout.Label(TableComponent.name, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

			GUILayout.BeginHorizontal();

			if (CreateButton("Wall", Icons.Surface(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Surface.GetDefault, "Wall");
			}

			if (CreateButton("Gate", Icons.Gate(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Gate.GetDefault, "New Gate");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Ramp", Icons.Ramp(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Ramp.GetDefault, "New Ramp");
			}

			if (CreateButton("Flipper", Icons.Flipper(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Flipper.GetDefault, "New Flipper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Plunger", Icons.Plunger(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Plunger.GetDefault, "New Plunger");
			}

			if (CreateButton("Bumper", Icons.Bumper(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Bumper.GetDefault, "New Bumper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Spinner", Icons.Spinner(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Spinner.GetDefault, "New Spinner");
			}

			if (CreateButton("Trigger", Icons.Trigger(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Trigger.GetDefault, "New Trigger");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Kicker", Icons.Kicker(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Kicker.GetDefault, "New Kicker");
			}

			if (CreateButton("Light", Icons.Light(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Light.GetDefault, "New Light");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Hit Target", Icons.HitTarget(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(HitTarget.GetHitTarget, "New Hit Target");
			}

			if (CreateButton("Drop Target", Icons.DropTarget(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(HitTarget.GetDropTarget, "New Target");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Rubber", Icons.Rubber(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Rubber.GetDefault, "New Rubber");
			}

			if (CreateButton("Primitive", Icons.Primitive(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Primitive.GetDefault, "New Primitive");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Trough", Icons.Trough(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Trough.GetDefault, "New Trough");
			}

			if (CreateButton("Drop Target\nBank", Icons.DropTargetBank(color: iconColor), iconSize, buttonStyle))
			{
				CreatePrefab<DropTargetBankComponent>("Drop Target Banks", "Prefabs/DropTargetBank");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Slingshot", Icons.Slingshot(color: iconColor), iconSize, buttonStyle)) {
				CreatePrefab<SlingshotComponent>("Slingshots", "Prefabs/Slingshot");
			}

			if (CreateButton("Metal Wire\nGuide", Icons.MetalWireGuide(color: iconColor), iconSize, buttonStyle))
			{
				CreateItem(MetalWireGuide.GetDefault, "New MetalWireGuide");
			}


			GUILayout.EndHorizontal();
		}

		private static bool CreateButton(string label, Texture icon, float iconSize, GUIStyle buttonStyle)
		{
			return GUILayout.Button(
				new GUIContent(label, icon),
				buttonStyle,
				GUILayout.Width(iconSize),
				GUILayout.Height(iconSize)
			);
		}

		private void CreateItem<TItem>(Func<Table, TItem> create, string actionName) where TItem : IItem
		{
			var tableContainer = TableComponent.TableContainer;
			tableContainer.Refresh();
			var item = create(tableContainer.Table);
			Selection.activeGameObject = CreateRenderable(item);
			ItemCreated?.Invoke(Selection.activeGameObject);
			Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, actionName);
		}

		private GameObject CreateRenderable(IItem item)
		{
			var converter = new VpxSceneConverter(TableComponent);
			TableComponent.TableContainer.Refresh();
			return converter.InstantiateAndPersistPrefab(item).GameObject;
		}

		private void CreatePrefab<T>(string groupName, string path) where T : Component
		{
			var converter = new VpxSceneConverter(TableComponent);
			TableComponent.TableContainer.Refresh();

			var parentGo = converter.GetGroupParent(groupName);

			var prefab = Resources.Load<GameObject>(path);
			var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

			if (go) {
				go.name = GetNewPrefabName<T>(go.name);

				go.transform.SetParent(parentGo.transform, false);
			}

			Selection.activeGameObject = go;
		}

		private string GetNewPrefabName<T>(string prefix) where T : Component
		{
			var dict = TableComponent.GetComponentsInChildren<T>()
					.ToDictionary(component => component.name.ToLower(), component => component);

			var n = 0;
			do
			{
				var elementName = $"{prefix}{++n}";
				if (!dict.ContainsKey(elementName.ToLower()))
				{
					return elementName;
				}
			} while (true);
		}
	}
}
