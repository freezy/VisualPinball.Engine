// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using Unity.Mathematics;
using UnityEngine;

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
		[Min(0f)]
		[Tooltip("Brightness multiplier for both the insert and its spill light. One is the material's nominal emission.")]
		public float Brightness = 1f;

		[Range(0f, 1f)]
		[Tooltip("Preview level. A parent VPE Light supplies the level at runtime; without one this also controls runtime dimming.")]
		public float Level = 1f;

		[Tooltip("Preview colour. A parent VPE Light supplies RGB at runtime; without one this also controls runtime colour.")]
		public Color Color = UnityEngine.Color.white;

		[Min(0f)]
		[Tooltip("Spill light output in lumens at brightness and level one. Tune separately from the lens's nominal HDR emission.")]
		public float SpillLumens = 0.12f;

		[Min(0.001f)]
		[Tooltip("Spill range in metres.")]
		public float SpillRange = 0.065f;

		[Tooltip("A real light above the insert's surface. Its local position remains editable.")]
		public Light SpillLight;

		private static readonly int BrightnessId = Shader.PropertyToID("_InsertBrightness");
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
			if (material == null || !material.HasProperty(BrightnessId)) {
				if (SpillLight != null) {
					SpillLight.enabled = false;
				}
				return;
			}
			_properties ??= new MaterialPropertyBlock();
			_renderer.GetPropertyBlock(_properties);
			var level = math.saturate(Level);
			var color = Color.linear;
			if (Application.isPlaying && _driver != null) {
				level = _properties.HasFloat(LevelId) ? _properties.GetFloat(LevelId) : 0f;
				color = _properties.HasVector(ColorId) ? (Color)_properties.GetVector(ColorId) : UnityEngine.Color.white;
			} else {
				_properties.SetFloat(LevelId, level);
				_properties.SetVector(ColorId, (Vector4)color);
			}
			var brightness = math.max(0f, Brightness);
			_properties.SetFloat(BrightnessId, brightness);
			_renderer.SetPropertyBlock(_properties);
			if (SpillLight == null) {
				return;
			}
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
				_renderer.GetPropertyBlock(_properties);
				if (material.HasProperty(BrightnessId)) {
					_properties.SetFloat(BrightnessId, material.GetFloat(BrightnessId));
					if (!Application.isPlaying || _driver == null) {
						_properties.SetFloat(LevelId, material.GetFloat(LevelId));
						_properties.SetVector(ColorId, material.GetVector(ColorId));
					}
					_renderer.SetPropertyBlock(_properties);
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
