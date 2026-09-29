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
	[PackAs("InsertLight")]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(InsertComponent), typeof(MeshRenderer))]
	[AddComponentMenu("Visual Pinball/Insert Light")]
	public class InsertLightComponent : MonoBehaviour, ILampDeviceComponent, IPackable
	{
		[FormerlySerializedAs("Level")]
		[Range(0f, 1f)]
		[Tooltip("Zero is off; one is the material's Max Brightness. Edit here to preview; map this component in Lamp Manager for ROM control.")]
		public float Intensity = 1f;

		[Tooltip("Lamp colour. Edit here to preview; RGB lamp mappings update it at runtime.")]
		public Color Color = UnityEngine.Color.white;

		/// <summary>A bulb of the material's beyond the first: its own colour and level.</summary>
		[Serializable]
		public class Bulb
		{
			[Range(0f, 1f)]
			[Tooltip("This bulb's intensity, zero to one. Edit here to preview.")]
			public float Intensity = 1f;

			[Tooltip("This bulb's colour. Edit here to preview.")]
			public Color Color = UnityEngine.Color.white;
		}

		[Tooltip("The material's second, third and fourth bulb, where it has them: each with a colour and intensity of its own, driven by its own lamp mapping. The first bulb is Intensity and Color above.")]
		public Bulb[] ExtraBulbs = { new(), new(), new() };

		/// <summary>How many bulbs the material can have: the first and three more.</summary>
		public const int MaxBulbs = 4;

		[Tooltip("What to call each bulb in Lamp Manager, such as Left, Middle and Right, so the right lamp goes to the right bulb. Empty names show as Bulb 1 to Bulb 4.")]
		public string[] BulbNames = new string[MaxBulbs];

		/// <summary>
		/// The device item of a bulb beyond the first, its one-based number
		/// appended; the first bulb's is the default lamp id, so a mapping made
		/// before the bulbs came keeps driving it.
		/// </summary>
		public const string BulbIdPrefix = "bulb_";

		[Tooltip("Turns the hue and saturation of the body and of the clear cover towards the lamp's while the lamp is lit, as far as it is lit, never their brightness: a grey cover milks a coloured lamp otherwise. A white lamp changes nothing, and a lamp that is off leaves both their own colour.")]
		public bool TintWithLamp;

		[Range(0f, 1f)]
		[Tooltip("How much of the lamp's hue the body and the cover take at full intensity.")]
		public float LampTint = 1f;

		/// <summary>The defaults a point-light spill was made with, before spills were spots.</summary>
		public const float PointSpillLumens = 0.12f;
		public const float PointSpillRange = 0.065f;
		public const float SpotSpillLumens = 1.5f;
		public const float SpotSpillRange = 0.1f;

		[Header("Environment Spill")]
		[Min(0f)]
		[Tooltip("Spill output per bulb in lumens at Max Brightness one and full intensity. Active bulbs add their output and mix their colours.")]
		public float SpillLumens = SpotSpillLumens;

		[Min(0.001f)]
		[Tooltip("Spill range in metres.")]
		public float SpillRange = SpotSpillRange;

		[Tooltip("A real light driven with all active bulbs: a wide spot just under the insert, pointing up through it at the ball and whatever else is above. Its transform remains editable.")]
		public Light SpillLight;

		public byte[] Pack() => InsertLightPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> InsertLightPackable.PackReferences(this, refs);

		public void Unpack(byte[] bytes) => InsertLightPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files)
			=> InsertLightPackable.UnpackReferences(bytes, this, refs);

		private static readonly int LuminosityId = Shader.PropertyToID("_InsertLuminosity");
		private static readonly int LevelId = Shader.PropertyToID("_InsertLampLevel");
		private static readonly int ColorId = Shader.PropertyToID("_InsertLampColor");
		private static readonly int TintId = Shader.PropertyToID("_InsertLampTint");
		private static readonly int[] BulbColorIds = { Shader.PropertyToID("_InsertLampColorB"), Shader.PropertyToID("_InsertLampColorC"), Shader.PropertyToID("_InsertLampColorD") };
		private static readonly int[] LampIds = { Shader.PropertyToID("_OpticalLamp"), Shader.PropertyToID("_OpticalLampB"), Shader.PropertyToID("_OpticalLampC"), Shader.PropertyToID("_OpticalLampD") };
		private static readonly int SpreadId = Shader.PropertyToID("_OpticalSpread");
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
		/// <summary>
		/// One lamp per bulb the material has: an insert with one bulb is one
		/// lamp, as any light, and one with more offers Bulb 1 to Bulb 4 as the
		/// device items to map, each with a lamp of its own.
		/// </summary>
		public IEnumerable<GamelogicEngineLamp> AvailableLamps {
			get {
				var count = BulbCount;
				if (count <= 1) {
					return new[] { new GamelogicEngineLamp(LightComponent.LampIdDefault) };
				}
				var lamps = new GamelogicEngineLamp[count];
				for (var i = 0; i < count; i++) {
					lamps[i] = new GamelogicEngineLamp(BulbId(i)) { Description = BulbLabel(i) };
				}
				return lamps;
			}
		}

		/// <summary>The bulb's name, or Bulb N where it has none.</summary>
		public string BulbLabel(int bulb)
		{
			var name = BulbNames != null && bulb >= 0 && bulb < BulbNames.Length ? BulbNames[bulb] : null;
			return string.IsNullOrWhiteSpace(name) ? $"Bulb {bulb + 1}" : name.Trim();
		}

		/// <summary>Where the material puts the bulb, in insert space, in millimetres.</summary>
		public Vector3 BulbPosition(int bulb)
		{
			var material = BulbMaterial();
			if (material == null || bulb < 0 || bulb >= LampIds.Length || !material.HasProperty(LampIds[bulb])) {
				return Vector3.zero;
			}
			var lamp = material.GetVector(LampIds[bulb]);
			return new Vector3(lamp.x, lamp.y, lamp.z) * 1000f;
		}

		/// <summary>Changes the material's active bulb count, preserving disabled bulbs' places.</summary>
		public bool SetBulbCount(int count)
		{
			var material = BulbMaterial();
			if (material == null || !material.HasProperty(LuminosityId)) {
				return false;
			}
			count = math.clamp(count, 1, MaxBulbs);
			var first = material.GetVector(LampIds[0]);
			var radius = math.max(first.w, 0.0001f);
			if (first.w <= 0f) {
				first.w = radius;
				material.SetVector(LampIds[0], first);
			}
			for (var i = 1; i < LampIds.Length; i++) {
				if (!material.HasProperty(LampIds[i])) {
					continue;
				}
				var value = material.GetVector(LampIds[i]);
				if (i < count) {
					if (value.x == 0f && value.y == 0f && value.z == 0f) {
						var previous = material.GetVector(LampIds[i - 1]);
						value.x = previous.x;
						value.y = previous.y;
						value.z = previous.z + radius * 2.5f;
					}
					value.w = radius;
				} else {
					value.w = 0f;
				}
				material.SetVector(LampIds[i], value);
			}
			Apply();
			return true;
		}

		/// <summary>Moves one virtual bulb in the material, in insert-local millimetres.</summary>
		public bool SetBulbPosition(int bulb, Vector3 millimetres)
		{
			var material = BulbMaterial();
			if (material == null || bulb < 0 || bulb >= LampIds.Length || !material.HasProperty(LampIds[bulb])) {
				return false;
			}
			var value = material.GetVector(LampIds[bulb]);
			value.x = millimetres.x * 0.001f;
			value.y = millimetres.y * 0.001f;
			value.z = millimetres.z * 0.001f;
			material.SetVector(LampIds[bulb], value);
			Apply();
			return true;
		}

		private Material BulbMaterial()
		{
			var renderer = _renderer != null ? _renderer : GetComponent<MeshRenderer>();
			return renderer != null ? renderer.sharedMaterial : null;
		}

		/// <summary>
		/// How many bulbs the material has switched on: the first and those
		/// beyond it up to the highest with a radius, since the shader lights
		/// every one that has one, whatever a revert left in between.
		/// </summary>
		public int BulbCount {
			get {
				var material = BulbMaterial();
				if (material == null) {
					return 1;
				}
				var count = 1;
				for (var i = 1; i < LampIds.Length; i++) {
					if (material.HasProperty(LampIds[i]) && material.GetVector(LampIds[i]).w > 0f) {
						count = i + 1;
					}
				}
				return count;
			}
		}

		/// <summary>The device item naming the bulb at that index.</summary>
		public static string BulbId(int bulb) => bulb <= 0 ? LightComponent.LampIdDefault : BulbIdPrefix + (bulb + 1);

		/// <summary>
		/// The bulb a device item names: the first for the default lamp, for
		/// no item at all and for anything unknown.
		/// </summary>
		public static int BulbIndex(string deviceItem)
		{
			if (!string.IsNullOrEmpty(deviceItem) && deviceItem.StartsWith(BulbIdPrefix)
				&& int.TryParse(deviceItem.Substring(BulbIdPrefix.Length), out var number)) {
				return math.clamp(number - 1, 0, MaxBulbs - 1);
			}
			return 0;
		}
		public IEnumerable<GamelogicEngineLamp> AvailableDeviceItems => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableLamps;

		// Once this device receives an event, a parent Light must not overwrite it.
		// The first bulb is the one a parent Light drives; the others are owned
		// on their own, so mapping only a second bulb leaves the first to the
		// parent.
		internal bool IsControlledByApi { get; private set; }
		internal bool AreExtraBulbsControlledByApi { get; private set; }

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
			AreExtraBulbsControlledByApi = false;
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

		/// <summary>
		/// Drives one bulb: the first is the lamp itself, the others their own
		/// entry, which a blinking status leaves on, since only the first bulb
		/// blinks.
		/// </summary>
		internal void SetBulbIntensity(int bulb, float value)
		{
			if (bulb <= 0) {
				SetLampIntensity(value);
				return;
			}
			AreExtraBulbsControlledByApi = true;
			ExtraBulb(bulb).Intensity = math.saturate(value);
			Apply();
		}

		internal void SetBulbColor(int bulb, Color value)
		{
			if (bulb <= 0) {
				SetLampColor(value);
				return;
			}
			AreExtraBulbsControlledByApi = true;
			var extra = ExtraBulb(bulb);
			extra.Color = new Color(value.r, value.g, value.b, extra.Color.a);
			Apply();
		}

		internal void SetBulbStatus(int bulb, LampStatus status)
		{
			if (bulb <= 0) {
				SetLampStatus(status);
				return;
			}
			SetBulbIntensity(bulb, status == LampStatus.Off ? 0f : 1f);
		}

		/// <summary>The entry of a bulb beyond the first, the array made whole if it was cut short.</summary>
		private Bulb ExtraBulb(int bulb)
		{
			var index = math.clamp(bulb, 1, MaxBulbs - 1) - 1;
			if (ExtraBulbs == null || ExtraBulbs.Length < MaxBulbs - 1) {
				var bulbs = new Bulb[MaxBulbs - 1];
				for (var i = 0; i < bulbs.Length; i++) {
					bulbs[i] = ExtraBulbs != null && i < ExtraBulbs.Length && ExtraBulbs[i] != null ? ExtraBulbs[i] : new Bulb();
				}
				ExtraBulbs = bulbs;
			}
			return ExtraBulbs[index] ??= new Bulb();
		}

		/// <summary>Immediate on/off preview used by Lamp Manager outside play mode.</summary>
		public bool Enabled {
			set {
				_blinking = false;
				Intensity = value ? 1f : 0f;
				Apply();
			}
		}

		/// <summary>The same preview for the bulb a device item names.</summary>
		public void SetBulbEnabled(string deviceItem, bool enabled)
		{
			var bulb = BulbIndex(deviceItem);
			if (bulb == 0) {
				Enabled = enabled;
				return;
			}
			ExtraBulb(bulb).Intensity = enabled ? 1f : 0f;
			Apply();
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
				TintCover(0f, UnityEngine.Color.white, tint: false);
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
			var spillLevel = level;
			var spillColor = color * level;
			for (var i = 0; i < BulbColorIds.Length; i++) {
				var bulb = ExtraBulbs != null && i < ExtraBulbs.Length ? ExtraBulbs[i] : null;
				var drive = bulb != null ? bulb.Color.linear : UnityEngine.Color.white;
				var bulbLevel = bulb != null ? math.saturate(bulb.Intensity) : 0f;
				_properties.SetVector(BulbColorIds[i], new Vector4(drive.r, drive.g, drive.b, bulbLevel));
				// Match the shader's active slots; disabled bulbs retain their authoring state.
				if (SpillLight != null && material.HasProperty(LampIds[i + 1]) && material.GetVector(LampIds[i + 1]).w > 0f) {
					spillLevel += bulbLevel;
					spillColor += drive * bulbLevel;
				}
			}
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
			// Average the linear colours and add levels so one shared light emits their sum.
			spillColor = spillLevel > 0f ? spillColor / spillLevel : color;
			converter.SetColor(SpillLight, (spillColor * (tint / peak)).gamma);
			converter.SetRange(SpillLight, math.max(0.001f, SpillRange));
			converter.SetIntensity(SpillLight, math.max(0f, SpillLumens) * brightness * spillLevel);
			SpillLight.enabled = brightness * spillLevel * SpillLumens > 0f && spillColor.maxColorComponent > 0f;
		}

		/// <summary>
		/// Hands the lamp's level and colour, and how much of its hue to take,
		/// to the clear cover in the renderer's second slot, through that slot's
		/// own property block, which the insert's playfield-normal projection
		/// shares: both read the block before writing it.
		/// </summary>
		private void TintCover(float level, Color color, bool tint = true)
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
			_coverProperties.SetFloat(TintId, tint && TintWithLamp ? math.saturate(LampTint) : 0f);
			// The body's bulbs, where they sit, their spread and how the others
			// are driven, so the haze can take the nearest one's colour.
			var body = _materials[0];
			for (var i = 0; i < LampIds.Length; i++) {
				_coverProperties.SetVector(LampIds[i], body != null && body.HasProperty(LampIds[i]) ? body.GetVector(LampIds[i]) : Vector4.zero);
			}
			_coverProperties.SetFloat(SpreadId, body != null && body.HasProperty(SpreadId) ? body.GetFloat(SpreadId) : 0f);
			for (var i = 0; i < BulbColorIds.Length; i++) {
				_coverProperties.SetVector(BulbColorIds[i], _properties.HasVector(BulbColorIds[i]) ? _properties.GetVector(BulbColorIds[i]) : new Vector4(1f, 1f, 1f, 0f));
			}
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
					// Each bulb the game drove goes off; the others go back to the material's own.
					if (IsControlledByApi) {
						_properties.SetFloat(LevelId, 0f);
					} else if (!Application.isPlaying || _driver == null) {
						_properties.SetFloat(LevelId, material.GetFloat(LevelId));
						_properties.SetVector(ColorId, material.GetVector(ColorId));
					}
					if (AreExtraBulbsControlledByApi) {
						foreach (var id in BulbColorIds) {
							var drive = _properties.HasVector(id) ? _properties.GetVector(id) : new Vector4(1f, 1f, 1f, 0f);
							drive.w = 0f;
							_properties.SetVector(id, drive);
						}
					} else if (!Application.isPlaying || _driver == null) {
						foreach (var id in BulbColorIds) {
							if (material.HasProperty(id)) {
								_properties.SetVector(id, material.GetVector(id));
							}
						}
					}
					_renderer.SetPropertyBlock(_properties, 0);
				}
			}
			// The cover does not stay tinted by a light that is gone, whatever
			// drives the body still holds; its projection properties in the same
			// block are left as they are.
			if (_renderer != null) {
				TintCover(0f, UnityEngine.Color.white, tint: false);
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
