// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A mappable lamp device controlling a hybrid insert and its real spill light.
	/// An unmapped insert can also follow a parent VPE Light for existing tables.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(InsertComponent), typeof(MeshRenderer))]
	[AddComponentMenu("Visual Pinball/Insert Light")]
	public class InsertLightComponent : MonoBehaviour, ILampDeviceComponent
	{
		[FormerlySerializedAs("Level")]
		[Range(0f, 1f)]
		[Tooltip("Zero is off; one is the material's Max Brightness. Edit here to preview; map this component in Lamp Manager for ROM control.")]
		public float Intensity = 1f;

		[Tooltip("Lamp colour. Edit here to preview; RGB lamp mappings update it at runtime.")]
		public Color Color = UnityEngine.Color.white;

		[Tooltip("Turns the hue and saturation of the body and of the clear cover towards the lamp's while the lamp is lit, as far as it is lit, never their brightness: a grey cover milks a coloured lamp otherwise. A white lamp changes nothing, and a lamp that is off leaves both their own colour.")]
		public bool TintWithLamp;

		[Range(0f, 1f)]
		[Tooltip("How much of the lamp's hue the body and the cover take at full intensity.")]
		public float LampTint = 1f;

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
		private static readonly int TintId = Shader.PropertyToID("_InsertLampTint");
		private static readonly int EmissionId = Shader.PropertyToID("_EmissiveColor");
		private MeshRenderer _renderer;
		private LightComponent _driver;
		private MaterialPropertyBlock _properties;
		private MaterialPropertyBlock _coverProperties;
		private readonly List<Material> _materials = new();
		[NonSerialized] private InsertLightApi _api;
		private bool _blinking;
		private float _blinkStarted;

		public IApiLamp GetApi(Player player) => _api ??= new InsertLightApi(this);
		public IEnumerable<Light> LightSources => SpillLight != null ? new[] { SpillLight } : Array.Empty<Light>();
		public Color LampColor => Color;
		public LampStatus LampStatus => _blinking ? LampStatus.Blinking : Intensity > 0f ? LampStatus.On : LampStatus.Off;
		public IEnumerable<GamelogicEngineLamp> AvailableLamps => new[] { new GamelogicEngineLamp(LightComponent.LampIdDefault) };
		public IEnumerable<GamelogicEngineLamp> AvailableDeviceItems => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableLamps;

		// Once this device receives an event, a parent Light must not overwrite it.
		internal bool IsControlledByApi { get; private set; }

		private void Awake() => RegisterWithPlayer();

		/// <summary>
		/// Registers the lamp with the player, as LightComponent does in Awake, so
		/// the API is there when LampPlayer maps lamps. This component executes
		/// always, so its Awake may have run in edit mode only, and a script
		/// reload since then has dropped what it registered: Enter Play Mode
		/// without a scene reload does not call Awake again, hence OnEnable, which
		/// a reload does call, registers too, and LampPlayer takes a mapped device
		/// that still missed both.
		/// </summary>
		private void RegisterWithPlayer()
		{
			var player = GetComponentInParent<Player>();
			if (player != null && player.Lamp(this) == null) {
				player.Register(GetApi(player), this);
			}
		}

		internal void ReleaseApiControl()
		{
			IsControlledByApi = false;
			_blinking = false;
		}

		internal void SetLampIntensity(float value)
		{
			IsControlledByApi = true;
			_blinking = false;
			Intensity = math.saturate(value);
			Apply();
		}

		internal void SetLampColor(Color value)
		{
			IsControlledByApi = true;
			Color = new Color(value.r, value.g, value.b, Color.a);
			Apply();
		}

		internal void SetLampStatus(LampStatus status)
		{
			SetLampIntensity(status == LampStatus.Off ? 0f : 1f);
			_blinking = status == LampStatus.Blinking;
			_blinkStarted = Time.time;
		}

		/// <summary>Immediate on/off preview used by Lamp Manager outside play mode.</summary>
		public bool Enabled {
			set {
				_blinking = false;
				Intensity = value ? 1f : 0f;
				Apply();
			}
		}

		private void OnEnable()
		{
			RegisterWithPlayer();
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
			if (!isActiveAndEnabled) { return; }
			var material = _renderer != null ? _renderer.sharedMaterial : null;
			if (material == null || !material.HasProperty(LuminosityId)) {
				if (SpillLight != null) {
					SpillLight.enabled = false;
				}
				// A body that stopped being the hybrid one takes its tint off the cover too.
				TintCover(0f, UnityEngine.Color.white);
				return;
			}
			_properties ??= new MaterialPropertyBlock();
			_renderer.GetPropertyBlock(_properties, 0);
			var level = math.saturate(Intensity);
			var color = Color.linear;
			if (Application.isPlaying && _driver != null && !IsControlledByApi) {
				level = _properties.HasFloat(LevelId) ? math.saturate(_properties.GetFloat(LevelId)) : 0f;
				color = _properties.HasVector(ColorId) ? (Color)_properties.GetVector(ColorId) : UnityEngine.Color.white;
			} else {
				if (_blinking && Application.isPlaying) {
					// Match the default VPE blink interval: one second per on/off step.
					level *= (int)(Time.time - _blinkStarted) % 2 == 0 ? 1f : 0f;
				}
				_properties.SetFloat(LevelId, level);
				_properties.SetVector(ColorId, (Vector4)color);
			}
			_properties.SetFloat(TintId, TintWithLamp ? math.saturate(LampTint) : 0f);
			_renderer.SetPropertyBlock(_properties, 0);
			TintCover(level, color);
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

		/// <summary>
		/// Hands the lamp's level and colour, and how much of its hue to take,
		/// to the clear cover in the renderer's second slot, through that slot's
		/// own property block, which the insert's playfield-normal projection
		/// shares: both read the block before writing it.
		/// </summary>
		private void TintCover(float level, Color color)
		{
			// Into a list: the array property copies on every read, every frame.
			_renderer.GetSharedMaterials(_materials);
			if (_materials.Count < 2 || _materials[1] == null || !_materials[1].HasProperty(TintId)) {
				return;
			}
			_coverProperties ??= new MaterialPropertyBlock();
			_renderer.GetPropertyBlock(_coverProperties, 1);
			_coverProperties.SetFloat(LevelId, level);
			_coverProperties.SetVector(ColorId, (Vector4)color);
			_coverProperties.SetFloat(TintId, TintWithLamp ? math.saturate(LampTint) : 0f);
			_renderer.SetPropertyBlock(_coverProperties, 1);
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
					if (IsControlledByApi) {
						_properties.SetFloat(LevelId, 0f);
					} else if (!Application.isPlaying || _driver == null) {
						_properties.SetFloat(LevelId, material.GetFloat(LevelId));
						_properties.SetVector(ColorId, material.GetVector(ColorId));
					}
					_renderer.SetPropertyBlock(_properties, 0);
				}
			}
			// The cover does not stay tinted by a light that is gone; its
			// projection properties in the same block are left as they are.
			if (_renderer != null) {
				TintCover(0f, UnityEngine.Color.white);
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
