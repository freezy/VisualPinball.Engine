// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(InsertComponent))]
	[CanEditMultipleObjects]
	public class InsertInspector : UnityEditor.Editor
	{
		private SerializedProperty _shape;
		private SerializedProperty _width;
		private SerializedProperty _height;
		private SerializedProperty _triangleBase;
		private SerializedProperty _triangleApex;
		private SerializedProperty _rectangleCorner;
		private SerializedProperty _sideSegments;
		private SerializedProperty _depth;
		private SerializedProperty _channelWidth;
		private SerializedProperty _channelDepth;
		private SerializedProperty _starburstBursts;
		private SerializedProperty _starburstCenter;
		private SerializedProperty _starburstOffset;
		private SerializedProperty _bottomBevel;
		private SerializedProperty _bottomBevelSegments;
		private SerializedProperty _holes;
		private SerializedProperty _holeSegments;
		private SerializedProperty _holeRadius;
		private SerializedProperty _bakeSettings;
		private SerializedProperty _coverMaterial;
		private SerializedProperty _usePlayfieldNormals;
		private SerializedProperty _coverPlayfield;

		private static readonly int[] TextureSizes = { 512, 1024, 2048, 4096 };
		private static readonly GUIContent[] TextureSizeNames = { new("512"), new("1024"), new("2048"), new("4096") };

		/// <summary>How many holes the "Around" preset spreads out, remembered while the inspector is open.</summary>
		private int _aroundCount = 5;

		private void OnEnable()
		{
			_shape = serializedObject.FindProperty("_shape");
			_width = serializedObject.FindProperty("_width");
			_height = serializedObject.FindProperty("_height");
			_triangleBase = serializedObject.FindProperty("_triangleBase");
			_triangleApex = serializedObject.FindProperty("_triangleApex");
			_rectangleCorner = serializedObject.FindProperty("_rectangleCorner");
			_sideSegments = serializedObject.FindProperty("_sideSegments");
			_depth = serializedObject.FindProperty("_depth");
			_channelWidth = serializedObject.FindProperty("_channelWidth");
			_channelDepth = serializedObject.FindProperty("_channelDepth");
			_starburstBursts = serializedObject.FindProperty("_starburstBursts");
			_starburstCenter = serializedObject.FindProperty("_starburstCenter");
			_starburstOffset = serializedObject.FindProperty("_starburstOffset");
			_bottomBevel = serializedObject.FindProperty("_bottomBevel");
			_bottomBevelSegments = serializedObject.FindProperty("_bottomBevelSegments");
			_holes = serializedObject.FindProperty("_holes");
			_holeSegments = serializedObject.FindProperty("_holeSegments");
			_holeRadius = serializedObject.FindProperty("_holeRadius");
			_bakeSettings = serializedObject.FindProperty("_bakeSettings");
			_coverMaterial = serializedObject.FindProperty("_coverMaterial");
			_usePlayfieldNormals = serializedObject.FindProperty("_usePlayfieldNormals");
			_coverPlayfield = serializedObject.FindProperty("_coverPlayfield");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_shape);
			EditorGUILayout.PropertyField(_width, new GUIContent("Width (VPX)"));
			EditorGUILayout.PropertyField(_height, new GUIContent("Height (VPX)"));

			switch ((InsertShape)_shape.enumValueIndex) {
				case InsertShape.Triangle:
					EditorGUILayout.PropertyField(_triangleBase, new GUIContent("Base Corners"), true);
					EditorGUILayout.PropertyField(_triangleApex, new GUIContent("Apex Corner"), true);
					break;

				case InsertShape.Rectangle:
					EditorGUILayout.PropertyField(_rectangleCorner, new GUIContent("Corners"), true);
					break;
			}

			EditorGUILayout.PropertyField(_sideSegments);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Body", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_depth, new GUIContent("Depth (VPX)"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Channel", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_channelWidth, new GUIContent("Width (VPX)"));
			EditorGUILayout.PropertyField(_channelDepth, new GUIContent("Depth (VPX)"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Bevel", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_bottomBevel, new GUIContent("Bottom Width (VPX)"));
			EditorGUILayout.PropertyField(_bottomBevelSegments, new GUIContent("Bottom Segments"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Starburst", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_starburstBursts, new GUIContent("Bursts"));
			if (_starburstBursts.intValue >= InsertStarburst.MinBursts) {
				EditorGUILayout.PropertyField(_starburstCenter, new GUIContent("Center (VPX)"));
				EditorGUILayout.PropertyField(_starburstOffset, new GUIContent("Offset (VPX)"));
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Holes", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_holeSegments, new GUIContent("Circle Segments"));
			EditorGUILayout.PropertyField(_holeRadius, new GUIContent("New Hole Radius (VPX)"));
			DrawHolePresets();
			EditorGUILayout.PropertyField(_holes, new GUIContent("Holes"), true);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Normal Bake", EditorStyles.boldLabel);
			DrawBakeSettings();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Clear Cover", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_coverMaterial, new GUIContent("Material"));
			EditorGUILayout.PropertyField(_usePlayfieldNormals, new GUIContent("Use Playfield Normals"));
			if (_usePlayfieldNormals.boolValue || _usePlayfieldNormals.hasMultipleDifferentValues) {
				EditorGUILayout.PropertyField(_coverPlayfield, new GUIContent("Playfield Override"));
				if (targets.Length == 1 && ((InsertComponent)target).CoverPlayfield != null) {
					var playfield = ((InsertComponent)target).CoverPlayfield;
					var material = playfield.sharedMaterial;
					using (new EditorGUI.DisabledScope(true)) {
						EditorGUILayout.ObjectField("Detected Surface", playfield, typeof(MeshRenderer), true);
						EditorGUILayout.ObjectField("Source Material", material, typeof(Material), false);
						EditorGUILayout.ObjectField("Source Normal Map", material != null && material.HasProperty("_NormalMap") ? material.GetTexture("_NormalMap") : null, typeof(Texture), false);
					}
				}
			}

			serializedObject.ApplyModifiedProperties();

			DrawBake();
			DrawHybridMaterial();
			if (targets.Length == 1 && ((InsertComponent)target).CoverMaterial == null) {
				using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || Shader.Find("Visual Pinball/Insert Cover") == null)) {
					if (GUILayout.Button("Add Clear Cover")) {
						AddCover((InsertComponent)target);
					}
				}
			}
			DrawStatus();
		}

		public static Material AddCover(InsertComponent insert)
		{
			if (insert.CoverMaterial != null) {
				return insert.CoverMaterial;
			}
			var shader = Shader.Find("Visual Pinball/Insert Cover");
			if (shader == null) {
				throw new System.InvalidOperationException("The HDRP insert cover shader is not installed.");
			}
			if (!AssetDatabase.IsValidFolder("Assets/Inserts")) {
				AssetDatabase.CreateFolder("Assets", "Inserts");
			}
			var folder = insert.Bake != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(insert.Bake)) : null;
			if (string.IsNullOrEmpty(folder)) {
				folder = "Assets/Inserts";
			}
			var material = new Material(shader) { name = $"{insert.name} Cover" };
			AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder.Replace('\\', '/')}/Insert Cover.mat"));
			Undo.RecordObjects(new Object[] { insert, insert.GetComponent<MeshRenderer>() }, "Add Clear Insert Cover");
			insert.SetCoverMaterial(material);
			EditorUtility.SetDirty(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert.GetComponent<MeshRenderer>());
			AssetDatabase.SaveAssetIfDirty(material);
			return material;
		}

		private void DrawHybridMaterial()
		{
			var insert = (InsertComponent)target;
			var data = insert.Bake;
			if (data == null || data.OpticalNormalMap == null || data.OpticalResponseMap == null) {
				return;
			}
			if (data.Material != null && data.Material.HasProperty("_OpticalNormalMap")) {
				EditorGUILayout.HelpBox("Hybrid optics are active. The material controls Max Brightness, facet contrast and reflections. Insert Light controls Intensity, Color and real light spilling onto the table.", MessageType.Info);
				var lighting = insert.GetComponent<InsertLightComponent>();
				if (lighting == null || lighting.SpillLight == null) {
					using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) {
						if (GUILayout.Button("Add Intensity Controls and Spill Light")) {
							AddLighting(insert);
						}
					}
				}
				return;
			}
			using (new EditorGUI.DisabledScope(!insert.IsBakeFresh || InsertBaker.Baking != null || EditorApplication.isPlaying || Shader.Find(InsertBaker.HybridShaderName) == null)) {
				if (GUILayout.Button("Use HDRP Hybrid Material")) {
					InsertBaker.UseHybridMaterial(insert);
				}
			}
		}

		public static InsertLightComponent AddLighting(InsertComponent insert)
		{
			var lighting = insert.GetComponent<InsertLightComponent>() ?? Undo.AddComponent<InsertLightComponent>(insert.gameObject);
			if (lighting.SpillLight == null) {
				var go = new GameObject("Insert Spill Light");
				Undo.RegisterCreatedObjectUndo(go, "Add Insert Spill Light");
				go.transform.SetParent(insert.transform, false);
				var bounds = insert.GetComponent<MeshFilter>().sharedMesh.bounds;
				go.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y + 0.008f, bounds.center.z);
				var light = go.AddComponent<Light>();
				light.type = LightType.Point;
				var converter = RenderPipeline.Current.LightConverter;
				converter.UpdateLight(light, new VisualPinball.Engine.VPT.Light.LightData(), true);
				converter.SetShadow(light, false, false);
				Undo.RecordObject(lighting, "Assign Insert Spill Light");
				lighting.SpillLight = light;
				PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
			}
			lighting.Apply();
			return lighting;
		}

		private void DrawBakeSettings()
		{
			var size = _bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.TextureSize));
			EditorGUILayout.IntPopup(size, TextureSizeNames, TextureSizes, new GUIContent("Texture Size", size.tooltip));
			var padding = _bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Padding));
			EditorGUILayout.IntSlider(padding, 1, 32, new GUIContent("Padding (px)", padding.tooltip));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.EdgeBevel)), new GUIContent("Edge Bevel (VPX)"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.NormalGrainStrength)), new GUIContent("Normal Grain Strength"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.NormalGrainSize)), new GUIContent("Normal Grain Size (VPX)"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Unwrap)), new GUIContent("Unwrap in Blender"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.PreferUvPackMaster)), new GUIContent("Prefer UVPackMaster"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Gpu)), new GUIContent("Bake on GPU"));
			EditorGUILayout.PropertyField(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.HybridOptics)), new GUIContent("Bake Hybrid Optics"));
		}

		/// <summary>
		/// Which Blender bakes, whether the insert's bake still fits it, and the
		/// buttons to bake or drop the bake. One insert at a time.
		/// </summary>
		private void DrawBake()
		{
			var insert = (InsertComponent)target;

			using (new EditorGUILayout.HorizontalScope()) {
				var path = InsertBaker.BlenderPath ?? "";
				var next = EditorGUILayout.DelayedTextField("Blender", path);
				if (GUILayout.Button("...", GUILayout.Width(28f))) {
					var picked = EditorUtility.OpenFilePanel("Blender Executable", Path.GetDirectoryName(path) ?? "",
						Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
					if (!string.IsNullOrEmpty(picked)) {
						next = picked;
					}
				}
				if (next != path) {
					InsertBaker.BlenderPath = next;
				}
			}

			var blender = InsertBaker.Probe();
			if (blender == null) {
				EditorGUILayout.HelpBox("Checking Blender...", MessageType.None);
			} else if (blender.Error != null) {
				EditorGUILayout.HelpBox(blender.Error, MessageType.Error);
			} else {
				EditorGUILayout.HelpBox($"Blender {blender.Blender}, packing with " +
					(blender.UvPackMaster ? "UVPackMaster." : "Blender's packer. Install UVPackMaster 4 for tighter packing."), MessageType.None);
			}

			if (targets.Length > 1) {
				return;
			}

			if (insert.Bake != null) {
				if (insert.IsBakeFresh) {
					EditorGUILayout.HelpBox($"Baked. UVs packed by {insert.Bake.Packer}, covering {insert.Bake.UvFill:P0} of the texture.", MessageType.Info);
				} else {
					EditorGUILayout.HelpBox("The insert geometry has changed. The material is kept as a preview, but its baked textures may no longer line up. Bake Again to update the UVs, normals and optical maps.", MessageType.Warning);
				}
			}
			if (InsertBaker.LastError != null && InsertBaker.LastErrorInsert == insert) {
				EditorGUILayout.HelpBox($"The last bake failed: {InsertBaker.LastError}", MessageType.Error);
				if (InsertBaker.LastLog.Count > 0 && GUILayout.Button("Copy Blender's Log")) {
					EditorGUIUtility.systemCopyBuffer = string.Join(System.Environment.NewLine, InsertBaker.LastLog);
				}
			}

			using (new EditorGUILayout.HorizontalScope()) {
				using (new EditorGUI.DisabledScope(InsertBaker.Baking != null || blender?.Error != null || insert.Error != null || EditorApplication.isPlayingOrWillChangePlaymode)) {
					if (GUILayout.Button(insert.Bake != null ? "Bake Again" : "Bake")) {
						InsertBaker.Bake(insert);
					}
				}
				using (new EditorGUI.DisabledScope(insert.Bake == null)) {
					if (GUILayout.Button("Clear Bake")) {
						Undo.RecordObjects(new Object[] { insert, insert.GetComponent<MeshRenderer>() }, "Clear Insert Bake");
						insert.SetBake(null);
					}
				}
			}
		}

		/// <summary>
		/// Buttons that lay holes out along the raised floor's edge, replacing
		/// whatever holes were there. Each hole can be moved afterwards.
		/// </summary>
		private void DrawHolePresets()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			var inset = outline?.ClampInset(_channelWidth.floatValue) ?? 0f;

			using (new EditorGUI.DisabledScope(outline == null || targets.Length > 1)) {
				using (new EditorGUILayout.HorizontalScope()) {
					if (GUILayout.Button("Corners")) {
						SetHoles(InsertHoles.AtCorners(outline, inset));
					}
					if (GUILayout.Button("Corners + Sides")) {
						SetHoles(InsertHoles.AtCornersAndSides(outline, inset));
					}
				}
				using (new EditorGUILayout.HorizontalScope()) {
					_aroundCount = EditorGUILayout.IntSlider(_aroundCount, 1, 64);
					if (GUILayout.Button("Around", GUILayout.Width(80f))) {
						SetHoles(InsertHoles.Around(outline, inset, _aroundCount));
					}
				}
			}
			// A new hole goes in the middle of this insert's outline, which means
			// nothing to another insert, so it is one at a time like the presets.
			using (new EditorGUI.DisabledScope(targets.Length > 1))
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Add Hole")) {
					var index = _holes.arraySize;
					_holes.InsertArrayElementAtIndex(index);
					var hole = _holes.GetArrayElementAtIndex(index);
					hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value =
						outline != null ? new Vector2(outline.Centroid.x, outline.Centroid.y) : Vector2.zero;
					hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
				}
				var symmetrize = new GUIContent("Symmetrize",
					"Moves holes near the middle line onto it, and sets holes that nearly mirror each other across it to mirror it exactly. " +
					"Near means within 5% of the insert's larger side.");
				using (new EditorGUI.DisabledScope(outline == null)) {
					if (GUILayout.Button(symmetrize)) {
						Symmetrize(0.05f * math.max(outline.Width, outline.Height));
					}
				}
				if (GUILayout.Button("Clear")) {
					_holes.ClearArray();
				}
			}
		}

		private void Symmetrize(float tolerance)
		{
			var holes = new InsertHole[_holes.arraySize];
			for (var i = 0; i < holes.Length; i++) {
				var hole = _holes.GetArrayElementAtIndex(i);
				holes[i] = new InsertHole(hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value,
					hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue);
			}
			var symmetric = InsertHoles.Symmetrize(holes, tolerance);
			for (var i = 0; i < symmetric.Length; i++) {
				_holes.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = symmetric[i].Center;
			}
		}

		private void SetHoles(List<Vector2> centers)
		{
			_holes.ClearArray();
			for (var i = 0; i < centers.Count; i++) {
				_holes.InsertArrayElementAtIndex(i);
				var hole = _holes.GetArrayElementAtIndex(i);
				hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = centers[i];
				hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
			}
		}

		private void DrawStatus()
		{
			var insert = (InsertComponent)target;
			if (insert.Error != null) {
				EditorGUILayout.HelpBox($"{insert.Error}\n\nShowing the last mesh that worked.", MessageType.Error);
			}
			if (insert.CoverWarning != null) {
				EditorGUILayout.HelpBox(insert.CoverWarning, MessageType.Warning);
			}

			foreach (var warning in insert.Warnings) {
				EditorGUILayout.HelpBox(warning, MessageType.Warning);
			}

			if (insert.Outline != null) {
				// The real-world size is the quickest way to catch a value that was
				// typed in the wrong unit.
				var inch = 1f / InsertShapes.VpxUnitsPerInch;
				var millimetre = InsertShapes.MetresPerInch * 1000f * inch;
				EditorGUILayout.HelpBox(
					$"{insert.Outline.Width * inch:0.###} x {insert.Outline.Height * inch:0.###} in " +
					$"({insert.Outline.Width * millimetre:0.#} x {insert.Outline.Height * millimetre:0.#} mm)\n" +
					$"{insert.Outline.Points.Count} outline vertices, {insert.Outline.Perimeter:0.#} VPX units around.",
					MessageType.None);
			}
		}

		private void OnSceneGUI()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			if (outline == null) {
				return;
			}

			var matrix = insert.transform.localToWorldMatrix;
			var points = new Vector3[outline.Points.Count + 1];
			for (var i = 0; i < outline.Points.Count; i++) {
				points[i] = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(outline.Points[i].Position));
			}
			points[^1] = points[0];

			Handles.color = Color.cyan;
			Handles.DrawPolyLine(points);

			// Corner samples and straight-run subdivisions are told apart on sight,
			// which is the quickest way to see what a segment count actually did.
			var size = HandleUtility.GetHandleSize(points[0]) * 0.02f;
			for (var i = 0; i < outline.Points.Count; i++) {
				Handles.color = outline.Points[i].Kind switch {
					InsertOutlinePointKind.Arc => Color.yellow,
					InsertOutlinePointKind.Side => Color.grey,
					_ => Color.red,
				};
				Handles.DotHandleCap(0, points[i], Quaternion.identity, size, EventType.Repaint);
			}

			DrawHoleHandles(insert);
		}

		/// <summary>
		/// A handle on each hole's centre that drags it across the insert's
		/// plane, and one on its rim that sets its radius.
		/// </summary>
		/// <remarks>
		/// Works on the component itself: the serialized object is off limits in
		/// scene GUI. Every change is recorded for undo first.
		/// </remarks>
		private static readonly Color HoleColor = new(1f, 0.6f, 0.1f);

		private static GUIStyle _holeLabel;

		private static GUIStyle HoleLabel => _holeLabel ??= new GUIStyle(EditorStyles.boldLabel) {
			fontSize = 13,
			normal = { textColor = HoleColor },
		};

		private static void DrawHoleHandles(InsertComponent insert)
		{
			var transform = insert.transform;
			var matrix = transform.localToWorldMatrix;
			var up = transform.TransformDirection(InsertMeshGenerator.ToLocal(float2.zero, 1f)).normalized;
			var across = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(1f, 0f))).normalized;
			var along = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(0f, 1f))).normalized;

			for (var i = 0; i < insert.Holes.Count; i++) {
				var hole = insert.Holes[i];
				var center = new float2(hole.Center.x, hole.Center.y);

				var world = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center));
				var rim = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center + new float2(hole.Radius, 0f)));
				var scale = HandleUtility.GetHandleSize(world);

				Handles.color = HoleColor;
				Handles.DrawWireDisc(world, up, Vector3.Distance(world, rim));

				// Numbered as the list numbers its elements, so a hole in the scene
				// can be found in the inspector and the other way round.
				var corner = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center + new float2(0.75f, 0.75f) * hole.Radius));
				Handles.Label(corner, i.ToString(), HoleLabel);

				EditorGUI.BeginChangeCheck();
				var moved = Handles.Slider2D(world, up, across, along, scale * 0.06f, Handles.CircleHandleCap, Vector2.zero);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(moved));
					SetHole(insert, i, new InsertHole(new Vector2(plan.x, plan.y), hole.Radius), "Move Hole");
					continue;
				}

				EditorGUI.BeginChangeCheck();
				var stretched = Handles.Slider(rim, rim - world, scale * 0.04f, Handles.DotHandleCap, 0f);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(stretched));
					var radius = math.max(0.01f, math.distance(plan, center));
					SetHole(insert, i, new InsertHole(hole.Center, radius), "Resize Hole");
				}
			}
		}

		private static void SetHole(InsertComponent insert, int index, InsertHole hole, string action)
		{
			Undo.RecordObject(insert, action);
			insert.SetHole(index, hole);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
		}
	}
}
