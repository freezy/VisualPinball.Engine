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

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Table;
using Light = UnityEngine.Light;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Light")]
	public class LightComponent : MainRenderableComponent<LightData>, ILampDeviceComponent
	{
		#region Data

		[Min(0)]
		[Tooltip("The radius of the bulb mesh")]
		public float BulbSize = 20f;

		public LampStatus State;
		public string BlinkPattern;
		public int BlinkInterval;

		[Min(0)]
		[Tooltip("Time in seconds the lamp takes to reach 100% of intensity.")]
		public float FadeSpeedUp;

		[Min(0)]
		[Tooltip("Time in seconds the lamp takes to turn off.")]
		public float FadeSpeedDown;

		private bool FadeEnabled => FadeSpeedUp > 0f || FadeSpeedDown > 0f;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Light;
		public override string ItemName => "Light";

		public override LightData InstantiateData() => new LightData();

		public override bool HasProceduralMesh => false;

		public override bool OverrideTransform => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<LightData, LightComponent>);
		protected override Type ColliderComponentType { get; } = null;

		public const string LampIdDefault = "default_lamp";
		private const string BulbMeshName = "Light (Bulb)";
		private const string SocketMeshName = "Light (Socket)";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region API

		public IApiLamp GetApi(Player player) => _api ??= new LightApi(gameObject, player, null);
		public IEnumerable<Light> LightSources => GetComponentsInChildren<Light>();

		public Color LampColor => _color;

		public LampStatus LampStatus => State;

		[NonSerialized]
		private LightApi _api;

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineLamp> AvailableLamps => new[] {
			new GamelogicEngineLamp(LampIdDefault),
		};

		public IEnumerable<GamelogicEngineLamp> AvailableDeviceItems => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableLamps;

		#endregion

		#region Transformation

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();

			var vpxPos = (float3)transform.localPosition.TranslateToVpx();

			transform.localPosition = vpxPos.TranslateToWorld();

			// bulb size
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
				if (!mf.sharedMesh) {
					continue;
				}
				switch (mf.sharedMesh.name) {
					case BulbMeshName:
					case SocketMeshName:
						mf.gameObject.transform.localScale = new Vector3(BulbSize, BulbSize, BulbSize);
						break;
				}
			}

			// insert mesh position
			var insertMeshComponent = GetComponentInChildren<LightInsertMeshComponent>();
			if (insertMeshComponent) {
				var t = insertMeshComponent.transform;
				t.localPosition = Physics.TranslateToWorld(-vpxPos.x, -vpxPos.y, insertMeshComponent.PositionZ);
			}
		}

		#endregion

		#region Runtime

		/// <summary>
		/// The current light intensity, between 0 and 1.
		/// </summary>
		private float _value;
		private float _newValue;
		private float _oldValue;
		private float _newValueAt;
		private float _oldValueAt;

		private Color _color;
		private bool _hasLights;
		private readonly List<(Light, float)> _lights = new();
		private readonly List<(Renderer, float)> _materials = new();
		private MaterialPropertyBlock _propBlock;

		public bool Enabled {
			set {
				SetLightIntensity(value ? _value : 0);
				SetMaterialIntensity(value ? _value : 0);
			}
		}

		public Color Color {
			get => _color;
			set {
				_color = value;
				foreach (var (unityLight, _) in _lights) {
					unityLight.color = value;
				}
				foreach (var (mr, intensity) in _materials) {
					mr.GetPropertyBlock(_propBlock);
					RenderPipeline.Current.MaterialConverter.SetEmissiveColor(_propBlock, value * intensity);
					mr.SetPropertyBlock(_propBlock);
				}
			}
		}

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for lamp {name}.");
				return;
			}
			player.Register(GetApi(player), this);

			var lights = GetComponentsInChildren<Light>();
			_value = 0;
			_color = lights.FirstOrDefault()?.color ?? Color.white;

			// remember intensities
			foreach (var unityLight in lights) {
				_lights.Add((unityLight, unityLight.intensity));
				if (FadeEnabled) {
					unityLight.enabled = true;
					unityLight.intensity = 0;

				} else {
					unityLight.enabled = false;
				}
			}

			// remember material emissions
			_propBlock = new MaterialPropertyBlock(); // this is just something we can recycle
			foreach (var mr in GetComponentsInChildren<MeshRenderer>()) {
				var emissiveIntensity = RenderPipeline.Current.MaterialConverter.GetEmissiveIntensity(mr.sharedMaterial);
				if (emissiveIntensity > 0) {
					_materials.Add((mr, emissiveIntensity));
				}
				// todo set to 0 initially
			}

			_hasLights = _lights.Count > 0 || _materials.Count > 0;
		}

		private void Update()
		{
			if (_value == _newValue) {
				return;
			}
			var durationSeconds = _newValueAt - _oldValueAt;
			var position = durationSeconds == 0
				? 1
				: (Time.fixedTime - _oldValueAt) / durationSeconds;
			_value = position >= 1  // done?
				? _newValue
				: math.lerp(_oldValue, _newValue, position);
			SetLightIntensity(_value);
			SetMaterialIntensity(_value);
		}

		public void FadeTo(float value)
		{
			if (!_hasLights) {
				return;
			}
			if (FadeEnabled) {
				_oldValue = _value;
				_oldValueAt = Time.fixedTime;
				_newValue = value;
				_newValueAt = Time.fixedTime + (_value < value
					? FadeSpeedUp * (1 - _value)
					: FadeSpeedDown * _value);

			} else {
				_newValue = value;
				_value = value;
				SetLightIntensity(value);
				SetMaterialIntensity(value);
			}
		}

		public void StartBlinking(float blinkIntensity)
		{
			if (!_hasLights) {
				return;
			}
			StopAllCoroutines();
			StartCoroutine(nameof(Blink), blinkIntensity);
		}

		private IEnumerator Blink(float blinkIntensity)
		{
			// parse blink sequence
			var blinkInterval = BlinkInterval == 0 ? 1000 : BlinkInterval;
			var blinkPattern = BlinkPattern.Trim().Length < 2 ? "10" : BlinkPattern.Trim();
			var sequence = blinkPattern.ToCharArray().Select(c => c == '1').ToArray();

			// step time is stored in ms but we need seconds
			var stepTime = blinkInterval / 1000f;

			while (true) {
				foreach (var on in sequence) {
					yield return Fade(on ? 1 * blinkIntensity : 0);
					var timeFading = on ? FadeSpeedUp : FadeSpeedDown;
					if (timeFading < stepTime) {
						yield return new WaitForSeconds(stepTime - timeFading);
					}
				}
			}
		}

		private IEnumerator Fade(float value)
		{
			var counter = 0f;
			var durationSeconds = _value < value
				? FadeSpeedUp * (1 - _value)
				: FadeSpeedDown * _value;

			if (durationSeconds == 0) {
				_value = value;
				SetLightIntensity(value);
				SetMaterialIntensity(value);

			} else {
				while (counter <= durationSeconds) {
					counter += Time.deltaTime;
					var position = counter / durationSeconds;
					var newValue = Mathf.Lerp(_value, value, position);
					yield return SetIntensity(newValue);
				}
			}
		}

		private IEnumerator SetIntensity(float value)
		{
			_value = value;
			SetLightIntensity(value);
			SetMaterialIntensity(value);
			yield return null;
		}

		private void SetLightIntensity(float value)
		{
			foreach (var (unityLight, intensity) in _lights) {
				if (value > 0) {
					unityLight.intensity = intensity * value;
					unityLight.enabled = true;

				} else {
					unityLight.enabled = false;
				}
			}
		}

		private void SetMaterialIntensity(float value)
		{
			foreach (var (mr, intensity) in _materials) {
				mr.GetPropertyBlock(_propBlock);
				RenderPipeline.Current.MaterialConverter.SetEmissiveIntensity(mr.sharedMaterial, _propBlock, value * intensity);
				mr.SetPropertyBlock(_propBlock);
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(LightData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			var tf = transform;
			tf.localPosition = Physics.TranslateToWorld(data.Center.X, data.Center.Y, 0);
			tf.localScale = Physics.ScaleInvVector;
			tf.localRotation = Quaternion.Euler(new Vector3(-90, 0, 0));
			BulbSize = data.MeshRadius;

			// logical params
			State = (LampStatus)data.State;
			BlinkPattern = data.BlinkPattern;
			BlinkInterval = data.BlinkInterval;
			FadeSpeedUp = data.FadeSpeedUp;
			FadeSpeedDown = data.FadeSpeedDown;

			// insert mesh
			var insertMeshComponent = GetComponentInChildren<LightInsertMeshComponent>();
			if (insertMeshComponent) {
				insertMeshComponent.DragPoints = data.DragPoints;
			}

			// physical params
			var unityLight = GetComponentInChildren<Light>(true);
			if (unityLight) {
				RenderPipeline.Current.LightConverter.UpdateLight(unityLight, data, insertMeshComponent);
			}

			return updatedComponents;
		}


		public override IEnumerable<MonoBehaviour> SetReferencedData(LightData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// surface
			ParentToSurface(data.Surface, data.Center, components);

			// visibility
			if (!data.ShowBulbMesh) {
				foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
					if (!mf.sharedMesh) {
						continue;
					}
					switch (mf.sharedMesh.name) {
						case BulbMeshName:
						case SocketMeshName:
							mf.gameObject.SetActive(false);
							break;
					}
				}
			}

			// insert mesh
			var insertMeshComponent = GetComponentInChildren<LightInsertMeshComponent>();
			if (insertMeshComponent) {
				if (!data.ShowBulbMesh && string.Equals(data.OffImage, table.Data.Image, StringComparison.OrdinalIgnoreCase)) {
					insertMeshComponent.CreateMesh(data, table, textureProvider, materialProvider);
				} else {
					insertMeshComponent.gameObject.SetActive(false);
				}
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override LightData CopyDataTo(LightData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			var pos = (Vector3)transform.localPosition.TranslateToVpx();

			// name and position
			data.Name = name;
			data.Center = pos.ToVertex2Dxy();
			data.MeshRadius = BulbSize;

			// logical params
			data.State = (int)State;
			data.BlinkPattern = BlinkPattern;
			data.BlinkInterval = BlinkInterval;
			data.FadeSpeedUp = FadeSpeedUp;
			data.FadeSpeedDown = FadeSpeedDown;

			// insert mesh
			var insertMeshComponent = GetComponentInChildren<LightInsertMeshComponent>();
			if (insertMeshComponent) {
				data.DragPoints = insertMeshComponent.DragPoints;
			}

			// visibility
			data.ShowBulbMesh = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
				if (!mf.sharedMesh) {
					continue;
				}

				switch (mf.sharedMesh.name) {
					case BulbMeshName:
					case SocketMeshName:
						data.ShowBulbMesh = data.ShowBulbMesh || mf.gameObject.activeInHierarchy;
						break;
				}
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			transform.localPosition = go.transform.localPosition.TranslateToVpx();

			var lightComponent = go.GetComponent<LightComponent>();
			if (lightComponent != null) {
				BulbSize = lightComponent.BulbSize;
				State = lightComponent.State;
				BlinkPattern = lightComponent.BlinkPattern;
				BlinkInterval = lightComponent.BlinkInterval;
				FadeSpeedUp = lightComponent.FadeSpeedUp;
				FadeSpeedDown = lightComponent.FadeSpeedDown;
			}
			UpdateTransforms();
		}

		#endregion
	}
}
