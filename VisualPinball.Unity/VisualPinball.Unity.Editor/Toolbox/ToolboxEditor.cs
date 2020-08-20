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
	public class ToolboxEditor : EditorWindow
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

		private static TableAuthoring TableAuthoring => FindObjectOfType<TableAuthoring>();

		private static Table Table {
			get {
				var tb = TableAuthoring;
				return tb == null ? null : tb.Item;
			}
		}

		[MenuItem("Visual Pinball/Toolbox", false, 100)]
		public static void ShowWindow()
		{
			GetWindow<ToolboxEditor>("Toolbox");
		}

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
			var existingTable = FindObjectOfType<TableAuthoring>();
			if (existingTable == null && GUILayout.Button("New Table")) {
				const string tableName = "Table1";
				var rootGameObj = new GameObject();
				var table = new Table(new TableData {Name = tableName});
				var converter = rootGameObj.AddComponent<VpxConverter>();
				converter.Convert(tableName, table);
				DestroyImmediate(converter);
				Selection.activeGameObject = rootGameObj;
				Undo.RegisterCreatedObjectUndo(rootGameObj, "New Table");
			}

			if (TableAuthoring == null) {
				GUI.enabled = false;
			}

			var iconSize = position.width / 2f - 4.5f;
			var buttonStyle = new GUIStyle(GUI.skin.button) {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageAbove
			};

			GUILayout.BeginHorizontal();

			if (CreateButton("Wall", _surfaceIcon, iconSize, buttonStyle)) {
				CreateItem(Surface.GetDefault, "Wall");
			}

			if (CreateButton("Gate", _gateIcon, iconSize, buttonStyle)) {
				CreateItem(Ramp.GetDefault, "New Ramp");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Ramp", _rampIcon, iconSize, buttonStyle)) {
				CreateItem(Ramp.GetDefault, "New Ramp");
			}

			if (CreateButton("Flipper", _flipperIcon, iconSize, buttonStyle)) {
				CreateItem(Flipper.GetDefault, "New Flipper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Plunger", _plungerIcon, iconSize, buttonStyle)) {
				CreateItem(Plunger.GetDefault, "New Plunger");
			}

			if (CreateButton("Bumper", _bumperIcon, iconSize, buttonStyle)) {
				CreateItem(Bumper.GetDefault, "New Bumper");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Spinner", _spinnerIcon, iconSize, buttonStyle)) {
				CreateItem(Spinner.GetDefault, "New Spinner");
			}

			if (CreateButton("Trigger", _triggerIcon, iconSize, buttonStyle)) {
				CreateItem(Trigger.GetDefault, "New Trigger");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Kicker", _kickerIcon, iconSize, buttonStyle)) {
				CreateItem(Kicker.GetDefault, "New Kicker");
			}

			if (CreateButton("Light", _lightIcon, iconSize, buttonStyle)) {
				CreateItem(Light.GetDefault, "New Light");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Target", _targetIcon, iconSize, buttonStyle)) {
				CreateItem(HitTarget.GetDefault, "New Target");
			}

			if (CreateButton("Rubber", _rubberIcon, iconSize, buttonStyle)) {
				CreateItem(Rubber.GetDefault, "New Rubber");
			}

			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();

			if (CreateButton("Primitive", _primitiveIcon, iconSize, buttonStyle)) {
				CreateItem(Primitive.GetDefault, "New Primitive");
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

		private static void CreateItem<TItem>(Func<Table, TItem> create, string actionName) where TItem : IItem
		{
			var table = Table;
			var item = create(table);
			table.Add(item, true);
			Selection.activeGameObject = CreateRenderable(table, item as IRenderable);
			ItemCreated?.Invoke(Selection.activeGameObject);
			Undo.RegisterCreatedObjectUndo(Selection.activeGameObject, actionName);
		}

		private static GameObject CreateRenderable(Table table, IRenderable renderable)
		{
			var tb = TableAuthoring;
			var rog = renderable.GetRenderObjects(table, Origin.Original, false);
			VpxConverter.ConvertRenderObjects(renderable, rog, GetOrCreateParent(tb, rog), tb, out var obj);
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
