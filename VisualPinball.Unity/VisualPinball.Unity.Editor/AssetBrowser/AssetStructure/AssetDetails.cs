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

// ReSharper disable PossibleUnintendedReferenceComparison

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What's rendered on the right panel.
	/// </summary>
	[UxmlElement]
	public partial class AssetDetails : VisualElement
	{
		public Asset Asset {
			get => _asset;
			set {
				if (_asset == value) {
					return;
				}
				// toggle empty label
				if (value != null && _asset == null) {
					SetVisibility(_emptyLabel, false);
					SetVisibility(_detailView, true);
				}
				if (value == null && _asset != null) {
					SetVisibility(_emptyLabel, true);
					SetVisibility(_detailView, false);
				}
				_asset = value;
				if (value == null) {
					return;
				}
				var so = new SerializedObject(_asset);
				_bodyReadOnly.Bind(so);
				_body.Bind(so);
				BindReadOnly(_asset);
				Bind(_asset);
			}
		}

		public bool HasAsset => _asset != null;

		private Asset _asset;

		private readonly TemplateContainer _header;
		private readonly TemplateContainer _body;
		private readonly TemplateContainer _bodyReadOnly;
		private readonly TemplateContainer _footer;

		private readonly VisualElement _detailView;
		private readonly Label _emptyLabel;

		private readonly AssetMaterialVariationsElement _materialVariations;
		private readonly AssetDecalVariationsElement _decalVariations;
		private readonly Toggle _replaceSelectedKeepName;
		private readonly Button _addButton;
		private readonly string _addButtonText;
		private AssetMaterialCombinationElement _selectedMaterialElement;

		public AssetDetails()
		{
			var header = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetDetails_Header.uxml");
			_header = header.CloneTree();
			var body = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetDetails_Body.uxml");
			_body = body.CloneTree();
			var bodyReadOnly = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetDetails_Body_ReadOnly.uxml");
			_bodyReadOnly = bodyReadOnly.CloneTree();
			var footer = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetDetails_Footer.uxml");
			_footer = footer.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetDetails.uss");
			styleSheets.Add(styleSheet);

			_detailView = new VisualElement();
			_detailView.Add(_header);
			_detailView.Add(_bodyReadOnly);
			_detailView.Add(_body);
			_detailView.Add(_footer);

			_emptyLabel = new Label("No item selected.") {
				style = {
					alignSelf = Align.Center,
					marginTop = 24,
					marginBottom = 24,
					unityFontStyleAndWeight = FontStyle.Italic
				}
			};
			_addButton = _bodyReadOnly.Q<Button>("add-selected");
			_addButtonText = _addButton.text;
			_materialVariations = _bodyReadOnly.Q<AssetMaterialVariationsElement>("material-variations");
			_decalVariations = _bodyReadOnly.Q<AssetDecalVariationsElement>("decal-variations");
			_replaceSelectedKeepName = _bodyReadOnly.Q<Toggle>("replace-selected-keep-name");

			// setup events
			_materialVariations.OnSelected += OnVariationSelected;
			_decalVariations.OnSelected += OnDecalVariationSelected;
			_body.Q<ListView>("variations").itemsAdded += OnNewMaterialVariation;
			_bodyReadOnly.Q<Button>("replace-selected").clicked += OnReplaceSelected;
			_addButton.clicked += OnAddSelected;
			RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
			RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);

			Add(_emptyLabel);
			Add(_detailView);

			SetVisibility(_emptyLabel, true);
			SetVisibility(_detailView, false);
		}
		private void OnNewMaterialVariation(IEnumerable<int> ints)
		{
			foreach (var i in ints) {
				_asset.MaterialVariations[i].Name = string.Empty;
				_asset.MaterialVariations[i].Target = new AssetMaterialTarget();
				_asset.MaterialVariations[i].Overrides = new List<AssetMaterialOverride>();
			}
		}

		#region Bindings

		public void Refresh()
		{
			if (_asset == null) {
				return;
			}
			BindReadOnly(_asset);
			Bind(_asset);
		}

		public void Bind(Asset asset)
		{
			_header.Q<Label>("title").text = asset.Name;
			_header.Q<Image>("library-icon").image = Icons.AssetLibrary(IconSize.Small);

			if (asset.ThumbCameraPos == default) {
				asset.ThumbCameraPos = new Vector3(0, asset.ThumbCameraHeight, 0);
			}

			var libraries = asset.Libraries;
			switch (libraries.Length) {
				case 0:
					_header.Q<Label>("library-name").text = "<no library>";
					break;
				case > 0:
					if (libraries.Length == 1 && asset.Library != libraries[0]) {
						_header.Q<Label>("library-name").text = $"{libraries[0].Name} (⚠️ \u2260{asset.Library.Name})";
					} else {
						_header.Q<Label>("library-name").text = string.Join(", ", libraries.Select(l => l.Name));
					}
					break;
				default:
					_header.Q<Label>("library-name").text = asset.Library != null ? asset.Library.Name : "<no library>";
					break;
			}

			_header.Q<Image>("category-icon").image = EditorGUIUtility.IconContent("d_Folder Icon").image;
			_header.Q<Label>("category-name").text = asset.Category?.Name ?? "<no category>";
			_header.Q<Image>("date-icon").image = Icons.Calendar(IconSize.Small);
			_header.Q<Label>("date-value").text = asset.AddedAt.ToLongDateString();

			_header.Q<PreviewEditorElement>("preview").Object = asset.Object;
			_body.Q<PresetDropdownElement>("thumb-camera-preset").SetValue(asset.ThumbCameraPreset);
			BindInfo(asset);

			SetVisibility(_bodyReadOnly, asset.Library.IsLocked);
			SetVisibility(_body, !asset.Library.IsLocked);

			var description = _header.Q<Label>("description");
			description.text = asset.Description;
			SetVisibility(description, asset.Library.IsLocked && !string.IsNullOrEmpty(asset.Description));

			BindBackgroundObjects();
		}

		private void BindBackgroundObjects()
		{
			var bgo = _body.Q<ObjectDropdownElement>("environment-field");
			var bgParent = SceneManager.GetActiveScene().GetRootGameObjects()
					.FirstOrDefault(go => go.name == "Environment");
			
			if (bgParent == null) {
				bgo.visible = false;
				return;
			}
			bgo.visible = true;
			// populate first, then resolve by name like the thumb generator does (objects can be nested).
			bgo.AddObjectsToDropdown<MeshRenderer>(bgParent, true);
			bgo.SetValue(_asset?.EnvironmentGameObjectName);
			bgo.RegisterValueChangedCallback(OnThumbEnvironmentChanged);
		}
		
		private void OnThumbEnvironmentChanged(Object obj)
		{
			// not bound to a serialized property, so record and dirty manually, otherwise it won't get saved.
			Undo.RecordObject(_asset, "change thumbnail environment");
			_asset.EnvironmentGameObjectName = obj.name;
			EditorUtility.SetDirty(_asset);
		}

		private void OnUndoRedo()
		{
			// the environment dropdown isn't bound, so sync it manually.
			if (_asset != null) {
				_body.Q<ObjectDropdownElement>("environment-field").SetValue(_asset.EnvironmentGameObjectName);
			}
		}

		private void BindInfo(Asset asset)
		{
			// info
			if (asset.Object is GameObject go) {
				SetVisibility(_footer.Q<Foldout>("footer-info"), true);
				var (meshes, subMeshes, vertices, triangles, uvs, materials) = CountVertices(go);
				const string separator = ", ";
				_footer.Q<Label>("info-geo-stats").text =
					vertices + (vertices == 1 ? " vertex" : " vertices") + separator +
					triangles + " triangle" + (triangles == 1 ? "" : "s") + separator +
					uvs + " uv" + (uvs == 1 ? "" : "s") + separator +
					meshes + " mesh" + (meshes == 1 ? "" : "es") + separator +
					subMeshes + " sub mesh" + (subMeshes == 1 ? "" : "es") + separator +
					materials + " material" + (materials == 1 ? "" : "s");

			} else {
				SetVisibility(_footer.Q<Foldout>("footer-info"), false);
			}
		}

		private void BindReadOnly(Asset asset)
		{
			// material variations
			_materialVariations.SetValue(asset);
			_decalVariations.SetValue(asset, null);
			_addButton.text = _addButtonText;

			// tags
			var tags = _bodyReadOnly.Q<Foldout>("tags-foldout");
			if (asset.Tags is { Count: > 0 }) {
				var container = tags.Q<VisualElement>("tags-container");
				container.Clear();
				foreach (var tag in asset.Tags) {
					container.Add(new AssetTagElement(tag));
				}
				SetVisibility(tags, true);
			} else {
				SetVisibility(tags, false);
			}

			// attributes
			var attributes = _bodyReadOnly.Q<Foldout>("attributes-container");
			if (asset.Attributes is { Count: > 0 }) {
				attributes.Clear();
				foreach (var attribute in asset.Attributes) {
					attributes.Add(new AssetAttributeElement(attribute));
				}
				SetVisibility(attributes, true);
			} else {
				SetVisibility(attributes, false);
			}

			// links
			var links = _bodyReadOnly.Q<Foldout>("links-foldout");
			if (asset.Links is { Count: > 0 }) {
				var container = links.Q<VisualElement>("links-container");
				container.Clear();
				foreach (var link in asset.Links) {
					container.Add(new AssetLinkElement(link));
				}
				SetVisibility(links, true);
			} else {
				SetVisibility(links, false);
			}

			// quality
			var qualityContainer = _bodyReadOnly.Q<VisualElement>("quality-container");
			qualityContainer.Clear();
			qualityContainer.Add(new AssetQualityElement(asset.Quality));

		}

		#endregion

		#region Actions

		private void OnAddSelected()
		{
			// find parent
			var (pf, parentTransform) = FindPlayfieldAndParent();

			// instantiate
			var go = InstantiateAsset(parentTransform);

			// move to the middle of the playfield
			if (pf != null && go.GetComponent(typeof(IMainRenderableComponent)) is IMainRenderableComponent comp) {
				go.transform.localPosition = new Vector3(Physics.ScaleToWorld(pf.Width / 2), 0, -Physics.ScaleToWorld(pf.Height / 2));
				comp.UpdateTransforms();
			}

			ApplyVariation(go);

			Selection.objects = new Object[] { go };
		}

		private void OnReplaceSelected()
		{
			var selection = Selection.gameObjects;
			var newSelection = new List<GameObject>();
			var keepName = _replaceSelectedKeepName.value;
			var replacements = new Dictionary<Object, Object>();
			var replaced = new List<GameObject>();

			Undo.IncrementCurrentGroup();
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Replace object with asset");

			foreach (var selected in selection) {

				// instantiate prefab
				var go = InstantiateAsset();
				if (go == null) {
					break;
				}

				// rename if desired
				if (keepName) {
					go.name = selected.name;
				}

				// enable undo
				Undo.RegisterCreatedObjectUndo(go, "Replace object with asset");
				go.transform.SetParent(selected.transform.parent);

				// if both are vpe components, copy the data
				if (go.GetComponent(typeof(IMainRenderableComponent)) is IMainRenderableComponent comp) {
					comp.CopyFromObject(selected);
				}
				go.transform.localPosition = selected.transform.localPosition;
				go.transform.localRotation = selected.transform.localRotation;
				go.transform.localScale = selected.transform.localScale;
				go.transform.SetSiblingIndex(selected.transform.GetSiblingIndex());

				MapReplacements(selected, go, replacements);
				replaced.Add(selected);

				// apply variation materials
				ApplyVariation(go);

				newSelection.Add(go);
			}

			// point whatever referenced the replaced objects at their replacements, while both still exist.
			UpdateReferences(replacements, replaced);
			foreach (var selected in replaced) {
				Undo.DestroyObjectImmediate(selected);
			}
			Undo.CollapseUndoOperations(undoGroup);

			Selection.objects = newSelection.Select(go => (Object)go).ToArray();
		}

		/// <summary>
		/// Pairs every object of the replaced hierarchy with its counterpart in the
		/// new one: game objects by their path below the root, and their components
		/// by type, in the order they come on the game object.
		/// </summary>
		private static void MapReplacements(GameObject srcRoot, GameObject destRoot, Dictionary<Object, Object> replacements)
		{
			foreach (var srcTransform in srcRoot.GetComponentsInChildren<Transform>(true)) {
				var path = srcTransform == srcRoot.transform ? null : AnimationUtility.CalculateTransformPath(srcTransform, srcRoot.transform);
				var destTransform = path == null ? destRoot.transform : destRoot.transform.Find(path);
				if (destTransform == null) {
					continue;
				}
				replacements[srcTransform.gameObject] = destTransform.gameObject;
				foreach (var group in srcTransform.GetComponents<Component>().Where(c => c != null).GroupBy(c => c.GetType())) {
					var destComponents = destTransform.GetComponents(group.Key);
					var i = 0;
					foreach (var srcComponent in group) {
						if (i < destComponents.Length) {
							replacements[srcComponent] = destComponents[i];
						}
						i++;
					}
				}
			}
		}

		/// <summary>
		/// Goes through every component in the open scenes, and the prefab being
		/// edited, and points its references to replaced objects at their
		/// replacements, undoably. That covers the table's switch, coil, lamp and
		/// wire mappings as well as any other component. References to replaced
		/// objects without a counterpart are logged, since they'll be missing.
		/// </summary>
		private static void UpdateReferences(Dictionary<Object, Object> replacements, List<GameObject> replaced)
		{
			var doomed = new HashSet<GameObject>(replaced.SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject));
			var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(scene => scene.isLoaded).ToList();
			var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (prefabStage != null) {
				scenes.Add(prefabStage.scene);
			}

			var updated = 0;
			var lost = new List<string>();
			foreach (var component in scenes.SelectMany(scene => scene.GetRootGameObjects()).SelectMany(root => root.GetComponentsInChildren<Component>(true))) {
				// a transform's references are the hierarchy itself, which mustn't be rewired.
				if (component == null || component is Transform || doomed.Contains(component.gameObject)) {
					continue;
				}
				var so = new SerializedObject(component);
				var property = so.GetIterator();
				var changed = false;
				while (property.Next(true)) {
					if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) {
						continue;
					}
					var target = property.objectReferenceValue;
					if (replacements.TryGetValue(target, out var replacement)) {
						property.objectReferenceValue = replacement;
						changed = true;
						updated++;
					} else if (target is GameObject go && doomed.Contains(go) || target is Component c && doomed.Contains(c.gameObject)) {
						lost.Add($"{component.name}.{component.GetType().Name}.{property.propertyPath} -> {target.name} ({target.GetType().Name})");
					}
				}
				if (changed) {
					so.ApplyModifiedProperties();
				}
			}

			if (updated > 0) {
				Debug.Log($"Replace: pointed {updated} reference(s) at the new objects.");
			}
			if (lost.Count > 0) {
				Debug.LogWarning($"Replace: {lost.Count} reference(s) point at objects the new asset has no counterpart for, and will be missing:\n{string.Join("\n", lost)}");
			}
		}

		private void ApplyVariation(GameObject go)
		{
			if (_decalVariations.SelectedMaterialCombination != null) {
				_decalVariations.SelectedMaterialCombination.Combination.ApplyMaterial(go);
			} else {
				_materialVariations.SelectedMaterialCombination?.Combination.ApplyMaterial(go);
			}
		}

		private void OnVariationSelected(object sender, AssetMaterialCombinationElement el)
		{
			_addButton.text = el == null
				? _addButtonText
				: $"Add {el.Q<Label>()?.text ?? "selected"}";
			_selectedMaterialElement = el;
			_decalVariations.SetValue(Asset, el?.Combination);
		}

		private void OnDecalVariationSelected(object sender, AssetMaterialCombinationElement decalEl)
		{
			if (decalEl == null) {
				OnVariationSelected(null, _selectedMaterialElement);
			} else {
				_addButton.text = $"Add {decalEl.Q<Label>()?.text ?? "decal variation"}";
			}
		}

		#endregion

		#region Tools

		private GameObject InstantiateAsset(Transform parentGo = null)
		{
			var prefab = _asset.Object;
			var prefabType = PrefabUtility.GetPrefabAssetType(prefab);
			GameObject go;
			if (prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant) {
				go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentGo);

			} else {
				go = Object.Instantiate(prefab, parentGo) as GameObject;
			}

			if (go != null) {
				go.name = prefab.name;

				// unpack?
				if (_asset.UnpackPrefab) {
					PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
				}

			} else {
				Debug.LogError("Error instantiating prefab.");
			}

			Undo.RegisterCreatedObjectUndo(go, "add asset to scene");

			return go;
		}

		private static (PlayfieldComponent, Transform) FindPlayfieldAndParent()
		{
			// first, check selection in hierarchy.
			if (Selection.activeGameObject != null) {
				var pf = Selection.activeGameObject.GetComponentInParent<PlayfieldComponent>();
				if (pf != null) {
					return Selection.activeGameObject.GetComponent<PlayfieldComponent>() != null
						? (pf, Selection.activeGameObject.transform)
						: (pf, Selection.activeGameObject.transform.parent.transform);
				}
			}

			// if nothing selected, put it under the playfield.
			if (TableSelector.Instance.SelectedTable != null) {
				var pf = TableSelector.Instance.SelectedTable.GetComponentInChildren<PlayfieldComponent>();
				if (pf != null) {
					return (pf, pf.transform);
				}
				Debug.LogError("Cannot find playfield. You'll need to have a playfield component so the asset can be scaled correctly.");
			} else {
				Debug.LogError("No table found. You'll need to have a table with a playfield so the asset can be scaled correctly.");
			}
			return (null, null);
		}

		private static (int, int, int, int, int, int) CountVertices(GameObject go)
		{
			var vertices = 0;
			var triangles = 0;
			var uvs = 0;
			var meshes = 0;
			var subMeshes = 0;
			var materials = 0;
			foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) {
				var mesh = mf.sharedMesh;
				if (mesh != null) {
					meshes++;
					vertices += mesh.vertexCount;
					triangles += mesh.triangles.Length / 3;
					uvs += mesh.uv.Length;
					subMeshes += mesh.subMeshCount;
				}

				var mr = mf.gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					materials += mr.sharedMaterials.Length;
				}
			}
			return (meshes, subMeshes, vertices, triangles, uvs, materials);
		}

		private static void SetVisibility(VisualElement element, bool isVisible)
		{
			switch (isVisible) {
				case false when !element.ClassListContains("hidden"):
					element.AddToClassList("hidden");
					break;
				case true when element.ClassListContains("hidden"):
					element.RemoveFromClassList("hidden");
					break;
			}
		}

		#endregion
	}
}
