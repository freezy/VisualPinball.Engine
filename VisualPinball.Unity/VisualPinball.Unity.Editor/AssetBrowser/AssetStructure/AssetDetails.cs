﻿// Visual Pinball Engine
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

// ReSharper disable PossibleUnintendedReferenceComparison

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What's rendered on the right panel.
	/// </summary>
	public class AssetDetails : VisualElement
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
		private readonly Toggle _replaceSelectedKeepName;
		private readonly Button _addButton;
		private readonly string _addButtonText;

		public new class UxmlFactory : UxmlFactory<AssetDetails, UxmlTraits> { }

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
			_replaceSelectedKeepName = _bodyReadOnly.Q<Toggle>("replace-selected-keep-name");

			// setup events
			_materialVariations.OnSelected += OnVariationSelected;
			_body.Q<ListView>("variations").itemsAdded += OnNewMaterialVariation;
			_bodyReadOnly.Q<Button>("replace-selected").clicked += OnReplaceSelected;
			_addButton.clicked += OnAddSelected;

			Add(_emptyLabel);
			Add(_detailView);

			SetVisibility(_emptyLabel, true);
			SetVisibility(_detailView, false);
		}
		private void OnNewMaterialVariation(IEnumerable<int> ints)
		{
			foreach (var i in ints) {
				_asset.MaterialVariations[i].Name = string.Empty;
				_asset.MaterialVariations[i].Object = null;
				_asset.MaterialVariations[i].Slot = 0;
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
			_header.Q<Label>("library-name").text = asset.Library != null ? asset.Library.Name : "<no library>";

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
				comp.SetEditorPosition(new Vector3(pf.Width / 2, pf.Height / 2, 0));
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

			foreach (var selected in selection) {

				// 1. instantiate prefab
				var go = InstantiateAsset();
				if (go == null) {
					break;
				}

				if (keepName) {
					go.name = selected.name;
				}

				Undo.RegisterCreatedObjectUndo(go, "replace object with asset");
				go.transform.SetParent(selected.transform.parent);

				// if both are vpe components, just copy the data
				if (go.GetComponent(typeof(IMainRenderableComponent)) is IMainRenderableComponent comp) {
					comp.CopyFromObject(selected);

				} else {
					go.transform.localPosition = selected.transform.localPosition;
					go.transform.localRotation = selected.transform.localRotation;
				}
				go.transform.SetSiblingIndex(selected.transform.GetSiblingIndex());
				Undo.DestroyObjectImmediate(selected);

				ApplyVariation(go);

				newSelection.Add(go);
			}

			Selection.objects = newSelection.Select(go => (Object)go).ToArray();
		}

		private GameObject ApplyVariation(GameObject go)
		{
			_materialVariations.SelectedMaterialCombination?.Combination.ApplyMaterial(go);
			return go;
		}

		private void OnVariationSelected(object sender, AssetMaterialCombinationElement el)
		{
			_addButton.text = el == null
				? _addButtonText
				: $"Add {el.Name}";
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
					triangles += mesh.triangles.Length;
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
