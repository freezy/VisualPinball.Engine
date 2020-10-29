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

using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
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

		[MenuItem("Visual Pinball/Toolbox", false, 100)]
		public static void ShowWindow()
		{
			GetWindow<ToolboxEditor>("Toolbox");
		}

		/// <summary>
		/// Called when the selected table changes
		/// </summary>
		/// <param name="table"></param>
		protected override void SetTable(TableAuthoring table) { }

		private void OnGUI()
		{
			const IconColor iconColor = IconColor.Gray;
			var iconSize = position.width / 2f - 4.5f;
			var buttonStyle = new GUIStyle(GUI.skin.button) {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageAbove
			};

			if (GUILayout.Button("New Table")) {
				const string tableName = "Table1";
				var rootGameObj = new GameObject();
				var table = new Table(new TableData {Name = tableName});
				var converter = rootGameObj.AddComponent<VpxConverter>();
				converter.Convert(tableName, table);
				DestroyImmediate(converter);
				Selection.activeGameObject = rootGameObj;
				Undo.RegisterCreatedObjectUndo(rootGameObj, "New Table");
			}

			if (_tableAuthoring == null) {
				return;
			}

			EditorGUILayout.Space();
			GUILayout.Label(_tableAuthoring.name, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

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

			if (CreateButton("Target", Icons.Target(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(HitTarget.GetDefault, "New Target");
			}

			if (CreateButton("Rubber", Icons.Rubber(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Rubber.GetDefault, "New Rubber");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Primitive", Icons.Primitive(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Primitive.GetDefault, "New Primitive");
			}

			if (CreateButton("Trough", Icons.Trough(color: iconColor), iconSize, buttonStyle)) {
				CreateItem(Trough.GetDefault, "New Trough");
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
			var table = _tableAuthoring.Table;
			var item = create(table);
			table.Add(item, true);
			Selection.activeGameObject = CreateRenderable(item as IRenderable);
			ItemCreated?.Invoke(Selection.activeGameObject);
			Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, actionName);
		}

		private GameObject CreateRenderable(IRenderable renderable)
		{
			var convertedItem = VpxConverter.CreateGameObjects(_tableAuthoring.Table, renderable, GetOrCreateParent(_tableAuthoring, renderable));
			return convertedItem.MainAuthoring.gameObject;
		}

		private static GameObject GetOrCreateParent(Component tb, IItem renderable)
		{
			var parent = string.IsNullOrEmpty(renderable.ItemGroupName)
				? tb.gameObject
				: tb.gameObject.transform.Find(renderable.ItemGroupName)?.gameObject;
			if (parent == null) {
				parent = new GameObject(renderable.ItemGroupName);
				parent.transform.parent = tb.gameObject.transform;
				parent.transform.localPosition = Vector3.zero;
				parent.transform.localRotation = Quaternion.identity;
				parent.transform.localScale = Vector3.one;
			}

			return parent;
		}
	}
}
