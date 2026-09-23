// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Authoring controls for a hybrid insert and its real spill light. At
	/// runtime a parent VPE Light owns colour and level; this component follows
	/// that renderer state and applies the same brightness to the spill light.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(InsertComponent), typeof(MeshRenderer))]
	[AddComponentMenu("Visual Pinball/Insert Light")]
	public class InsertLightComponent : MonoBehaviour
	{
		[FormerlySerializedAs("Level")]
		[Range(0f, 1f)]
		[Tooltip("Zero is off; one is the material's Max Brightness. Edit here to preview. A parent VPE Light supplies ROM intensity at runtime.")]
		public float Intensity = 1f;

		[Tooltip("Lamp colour. Edit here to preview. A parent VPE Light supplies ROM RGB at runtime; without one this also controls runtime colour.")]
		public Color Color = UnityEngine.Color.white;

		[Header("Environment Spill")]
		[Min(0f)]
		[Tooltip("Spill output in lumens at Max Brightness one and full intensity. Scales with the material's Max Brightness and ROM intensity.")]
		public float SpillLumens = 0.12f;

		[Min(0.001f)]
		[Tooltip("Spill range in metres.")]
		public float SpillRange = 0.065f;

		[Tooltip("A real light above the insert's surface. Its local position remains editable.")]
		public Light SpillLight;

		private static readonly int LuminosityId = Shader.PropertyToID("_InsertLuminosity");
		private static readonly int LevelId = Shader.PropertyToID("_InsertLampLevel");
		private static readonly int ColorId = Shader.PropertyToID("_InsertLampColor");
		private static readonly int EmissionId = Shader.PropertyToID("_EmissiveColor");
		private MeshRenderer _renderer;
		private LightComponent _driver;
		private MaterialPropertyBlock _properties;

		private void OnEnable()
		{
			_renderer = GetComponent<MeshRenderer>();
			_driver = GetComponentInParent<LightComponent>();
			_properties ??= new MaterialPropertyBlock();
			Apply();
		}

		private void OnTransformParentChanged() => _driver = GetComponentInParent<LightComponent>();
		private void LateUpdate() => Apply();

		/// <summary>Apply authoring changes immediately, or follow the current VPE lamp state.</summary>
		public void Apply()
		{
			var material = _renderer != null ? _renderer.sharedMaterial : null;
			if (material == null || !material.HasProperty(LuminosityId)) {
				if (SpillLight != null) {
					SpillLight.enabled = false;
				}
				return;
			}
			_properties ??= new MaterialPropertyBlock();
			_renderer.GetPropertyBlock(_properties, 0);
			var level = math.saturate(Intensity);
			var color = Color.linear;
			if (Application.isPlaying && _driver != null) {
				level = _properties.HasFloat(LevelId) ? math.saturate(_properties.GetFloat(LevelId)) : 0f;
				color = _properties.HasVector(ColorId) ? (Color)_properties.GetVector(ColorId) : UnityEngine.Color.white;
			} else {
				_properties.SetFloat(LevelId, level);
				_properties.SetVector(ColorId, (Vector4)color);
			}
			_renderer.SetPropertyBlock(_properties, 0);
			if (SpillLight == null) {
				return;
			}
			// The material owns its base brightness so its inspector remains live.
			// Match that factor in the spill without overriding it on the renderer.
			var brightness = math.max(0f, material.GetFloat(LuminosityId));
			var tint = material.GetColor(EmissionId);
			var peak = math.max(tint.maxColorComponent, 1e-6f);
			var converter = RenderPipeline.Current.LightConverter;
			converter.SetColor(SpillLight, (color * (tint / peak)).gamma);
			converter.SetRange(SpillLight, math.max(0.001f, SpillRange));
			converter.SetIntensity(SpillLight, math.max(0f, SpillLumens) * brightness * math.max(0f, level));
			SpillLight.enabled = brightness * level * SpillLumens > 0f && color.maxColorComponent > 0f;
		}

		private void OnDisable()
		{
			if (SpillLight != null) {
				SpillLight.enabled = false;
			}
			if (_renderer != null && _renderer.sharedMaterial != null && _properties != null) {
				var material = _renderer.sharedMaterial;
				_renderer.GetPropertyBlock(_properties, 0);
				if (material.HasProperty(LuminosityId)) {
					if (!Application.isPlaying || _driver == null) {
						_properties.SetFloat(LevelId, material.GetFloat(LevelId));
						_properties.SetVector(ColorId, material.GetVector(ColorId));
					}
					_renderer.SetPropertyBlock(_properties, 0);
				}
			}
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			UnityEditor.EditorApplication.delayCall += () => {
				if (this != null && isActiveAndEnabled) {
					Apply();
					UnityEditor.SceneView.RepaintAll();
				}
			};
		}
#endif
	}
}
