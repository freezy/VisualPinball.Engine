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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public abstract class MeshComponent<TData, TComponent> : SubComponent<TData, TComponent>,
		IMeshComponent, IMeshRendererState
		where TData : ItemData
		where TComponent : MainRenderableComponent<TData>
	{
		public IMainRenderableComponent MainRenderableComponent => MainComponent;

		#region Creation and destruction

		/// <summary>
		/// Whether <see cref="OnDisable"/> hid the renderer, so <see cref="OnEnable"/> only
		/// shows what this component hid. A renderer the author disabled stays disabled when
		/// the scene loads. Serialized so a component saved disabled shows its mesh again
		/// when it is re-enabled after a reload.
		/// </summary>
		[SerializeField, HideInInspector] private bool _hidRenderer;

		/// <summary>
		/// 0 for content saved before <see cref="_hidRenderer"/> existed, which stored a hidden
		/// item as component and renderer both disabled. Older content has no value here and
		/// loads as 0 (no initializer on purpose: Unity keeps it for missing fields). Set as
		/// soon as the component is enabled or disabled under the current rules, so a renderer
		/// the author disabled is not mistaken for one this component hid.
		/// </summary>
		[SerializeField, HideInInspector] private int _rendererStateVersion;

		private const int CurrentRendererState = 1;

		private void Awake()
		{
			// Content saved before _hidRenderer existed: treat the disabled renderer as hidden by
			// this component, so enabling the component shows it again, as it did before.
			if (_rendererStateVersion < CurrentRendererState && !enabled && !_hidRenderer) {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null && !mr.enabled) {
					_hidRenderer = true;
				}
			}
#if UNITY_EDITOR
			EditorAwake();
#endif
		}

		/// <summary>
		/// Content saved before <see cref="_hidRenderer"/> existed also turned the renderer off
		/// when the GameObject was deactivated, and on again when it was activated. Awake can't
		/// tell that apart once the GameObject is active, so the editor calls this when a scene
		/// opens, for components on inactive GameObjects.
		/// </summary>
		void IMeshRendererState.MigrateInactiveLegacyRenderer()
		{
			if (_rendererStateVersion >= CurrentRendererState || _hidRenderer || !enabled || gameObject.activeInHierarchy) {
				return;
			}
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr != null && !mr.enabled) {
				_hidRenderer = true;
				_rendererStateVersion = CurrentRendererState;
			}
		}

		private void OnEnable()
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr != null && _hidRenderer) {
				mr.enabled = true;
			}
			_hidRenderer = false;
			_rendererStateVersion = CurrentRendererState;
		}

		private void OnDisable()
		{
			// Only when the component itself is switched off. Deactivating the GameObject, closing
			// the scene or reloading scripts leave enabled true and the renderer as it is (an
			// inactive GameObject doesn't render anyway).
			if (enabled) {
				_rendererStateVersion = CurrentRendererState;
				return;
			}
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr == null) {
				return;
			}
			if (!mr.enabled) {
				return;   // disabled by the author before the component: this component didn't hide it
			}
#if UNITY_EDITOR
			// Record the renderer in the current Undo step (the inspector checkbox records the
			// component in it), so undoing the toggle shows the mesh again.
			if (!Application.isPlaying && !UnityEditor.Undo.isProcessing) {
				UnityEditor.Undo.RecordObject(mr, "Disable Mesh");
			}
#endif
			mr.enabled = false;
			_hidRenderer = true;
		}

		#endregion

#if UNITY_EDITOR
		[SerializeField] private Vector2 _playfieldDimensions = Vector2.zero;
#endif

		public virtual void RebuildMeshes()
		{
			UpdateMesh();
		}

		public void ClearMeshVertices()
		{
			var mf = GetComponent<MeshFilter>();
			if (mf && mf.sharedMesh) {
				var mesh = mf.sharedMesh;
				mesh.triangles = null;
				mesh.vertices = Array.Empty<Vector3>();
				mesh.normals = Array.Empty<Vector3>();
				mesh.uv = Array.Empty<Vector2>();
			}
		}

		protected abstract Mesh GetMesh(TData data);

		protected virtual bool IsProcedural => true;

		protected abstract PbrMaterial GetMaterial(TData data, Table table);


		protected Vector2 GetPlayfieldDimensions()
		{
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			// ReSharper disable once RedundantAssignment
			var playfieldDimensions = Vector2.zero;

#if UNITY_EDITOR
			if (playfieldComponent) {
				_playfieldDimensions = new Vector2(playfieldComponent.Width, playfieldComponent.Height);
			}
			playfieldDimensions = _playfieldDimensions;
#endif

			if (playfieldComponent) {
				playfieldDimensions = new Vector2(playfieldComponent.Width, playfieldComponent.Height);
			}

			return playfieldDimensions;
		}

		public void CreateMesh(TData data, Table table, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			CreateMesh(gameObject, GetMesh(data), GetMaterial(data, table), data.GetName(), texProvider, matProvider);
		}

		public static void CreateMesh(GameObject gameObject, Mesh m, PbrMaterial material, string name, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			if (m == null) {
				return;
			}
			var mesh = m.ToUnityMesh($"{name} (Generated)");

			// apply mesh to game object
			var mf = gameObject.GetComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply renderer and material
			if (m.AnimationFrames.Count > 0) { // if number of animations frames are 1, the blend vertices are in the uvs are handle by the lerp shader.
				var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = material.ToUnityMaterial(matProvider, texProvider);
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, m.AnimationDefaultPosition);
			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = material.ToUnityMaterial(matProvider, texProvider);
			}
		}

		private void UpdateMesh()
		{
			var data = MainComponent.InstantiateData();
			MainComponent.UpdateTransforms();
			MainComponent.CopyDataTo(data, null, null, false);
			var mesh = GetMesh(data);
			

			// mesh generator can return null - but in this case the main component
			// will take care of removing the mesh component.
			if (mesh == null || !mesh.IsSet) {
				return;
			}
			var mf = GetComponent<MeshFilter>();

			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				if (!unityMesh) {
					mf.sharedMesh = new UnityEngine.Mesh { name = $"{name} (Updated)" };
					unityMesh = mf.sharedMesh;
				}
				mesh.ApplyToUnityMesh(unityMesh);
				unityMesh.RecalculateBounds();
			}
		}

#if UNITY_EDITOR
		[SerializeField] private int _instanceID;
		private void EditorAwake()
		{
			if (Application.isPlaying) {
				return;
			}

			if (_instanceID == 0 || !IsProcedural) {
				SetInstanceID();
				return;
			}

			if (_instanceID != UnityObjectId.Get(this)) {
				SetInstanceID();

				var mf = GetComponent<MeshFilter>();
				if (mf == null || mf.sharedMesh == null) {
					return;
				}

				// Never rebuild from Awake. Leaving play mode and reloading the domain both restore the
				// scene object by object, and rebuilding here pulls on the owner's drag points while its
				// generated spline child may not exist yet. The owner would then create a blank spline
				// and silently lose its shape. Defer until the hierarchy is whole.
				UnityEditor.EditorApplication.delayCall += RegenerateMesh;
			}
		}

		private void RegenerateMesh()
		{
			UnityEditor.EditorApplication.delayCall -= RegenerateMesh;
			if (!this || Application.isPlaying) {
				return;
			}
			var mf = GetComponent<MeshFilter>();
			if (mf == null || mf.sharedMesh == null) {
				return;
			}
			mf.sharedMesh = null;
			RebuildMeshes();
			Debug.Log($"[{name}] Mesh regenerated.");
		}

		private void SetInstanceID()
		{
			_instanceID = UnityObjectId.Get(this);
			var obj = new UnityEditor.SerializedObject(this);
			obj.FindProperty(nameof(_instanceID)).intValue = _instanceID;
			obj.ApplyModifiedPropertiesWithoutUndo();
		}
#endif

	}
}
#if UNITY_EDITOR

public static class MeshMenu
{
	
	[UnityEditor.MenuItem("GameObject/Pinball/Rebuild Meshes", true, 40)]
	public static bool ApplyChildrenTransformsValidate()
	{
		if (UnityEditor.Selection.gameObjects.Length != 1) {
			return false;
		}
		var comps = UnityEditor.Selection.activeGameObject.GetComponentsInChildren<IMeshComponent>();
		return comps.Length != 0;
	}
}

#endif
