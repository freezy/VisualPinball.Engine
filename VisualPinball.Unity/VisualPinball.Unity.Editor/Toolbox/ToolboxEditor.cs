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
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
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
using Light = VisualPinball.Engine.VPT.Light.Light;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class ToolboxEditor : LockingTableEditorWindow
	{
		private Texture2D _bumperIcon;
		private Texture2D _surfaceIcon;
		private Texture2D _rampIcon;
		private Texture2D _flipperIcon;
		private Texture2D _plungerIcon;
		private Texture2D _spinnerIcon;
		private Texture2D _triggerIcon;
		private Texture2D _kickerIcon;
		private Texture2D _targetIcon;
		private Texture2D _rubberIcon;
		private Texture2D _gateIcon;
		private Texture2D _lightIcon;
		private Texture2D _primitiveIcon;

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

		private void OnEnable()
		{
			const string iconPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";
			_bumperIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_bumper.png");
			_surfaceIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_surface.png");
			_rampIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_ramp.png");
			_gateIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_gate.png");
			_flipperIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_flipper.png");
			_plungerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_plunger.png");
			_spinnerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_spinner.png");
			_triggerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_trigger.png");
			_kickerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_kicker.png");
			_targetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_target.png");
			_rubberIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_rubber.png");
			_lightIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_light.png");
			_primitiveIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/icon_primitive.png");
		}

		private void OnGUI()
		{
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

			if (_table == null) {
				GUI.enabled = false;
			}

			GUILayout.Label(_table.name);

			var iconSize = position.width / 2f - 4.5f;
			var buttonStyle = new GUIStyle(GUI.skin.button) {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageAbove
			};

			GUILayout.BeginHorizontal();

			if (CreateButton("Wall", _surfaceIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Surface.GetDefault, "Wall");
			}

			if (CreateButton("Gate", _gateIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Ramp.GetDefault, "New Ramp");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Ramp", _rampIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Ramp.GetDefault, "New Ramp");
			}

			if (CreateButton("Flipper", _flipperIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Flipper.GetDefault, "New Flipper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Plunger", _plungerIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Plunger.GetDefault, "New Plunger");
			}

			if (CreateButton("Bumper", _bumperIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Bumper.GetDefault, "New Bumper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Spinner", _spinnerIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Spinner.GetDefault, "New Spinner");
			}

			if (CreateButton("Trigger", _triggerIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Trigger.GetDefault, "New Trigger");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Kicker", _kickerIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Kicker.GetDefault, "New Kicker");
			}

			if (CreateButton("Light", _lightIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Light.GetDefault, "New Light");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Target", _targetIcon, iconSize, buttonStyle)) {
				CreateItem(_table, HitTarget.GetDefault, "New Target");
			}

			if (CreateButton("Rubber", _rubberIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Rubber.GetDefault, "New Rubber");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Primitive", _primitiveIcon, iconSize, buttonStyle)) {
				CreateItem(_table, Primitive.GetDefault, "New Primitive");
			}

			GUILayout.EndHorizontal();

			GUI.enabled = true;
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

		private static void CreateItem<TItem>(TableAuthoring tableAuth, Func<Table, TItem> create, string actionName) where TItem : IItem
		{
			var table = tableAuth.Table;
			var item = create(table);
			table.Add(item, true);
			Selection.activeGameObject = CreateRenderable(tableAuth, item as IRenderable);
			ItemCreated?.Invoke(Selection.activeGameObject);
			Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, actionName);
		}

		private static GameObject CreateRenderable(TableAuthoring tableAuth, IRenderable renderable)
		{
			var rog = renderable.GetRenderObjects(tableAuth.Table, Origin.Original, false);
			VpxConverter.ConvertRenderObjects(renderable, rog, GetOrCreateParent(tableAuth, rog), tableAuth, out var obj);
			return obj;
		}

		private static GameObject GetOrCreateParent(Component tb, RenderObjectGroup rog)
		{
			var parent = tb.gameObject.transform.Find(rog.Parent)?.gameObject;
			if (parent == null) {
				parent = new GameObject(rog.Parent);
				parent.transform.parent = tb.gameObject.transform;
				parent.transform.localPosition = Vector3.zero;
				parent.transform.localRotation = Quaternion.identity;
				parent.transform.localScale = Vector3.one;
			}

			return parent;
		}
	}
}
