// Visual Pinball Engine
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

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this light is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

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

		public override bool OverrideTransform => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<LightData, LightComponent>);
		protected override Type ColliderComponentType { get; } = null;

		public const string LampIdDefault = "default_lamp";
		private const string BulbMeshName = "Light (Bulb)";
		private const string SocketMeshName = "Light (Socket)";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region API

		public IApiLamp GetApi(Player player) => _api ??= new LightApi(gameObject, player);
		public IEnumerable<Light> LightSources => GetComponentsInChildren<Light>();

		public Color LampColor {
			get {
				var src = GetComponentInChildren<Light>();
				return Color.magenta; //src == null ? Color.white : src.color;
			}
		}

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

			var localPos = transform.localPosition;

			// position
			localPos.z = Surface != null
				? Surface.Height(((float3)localPos).xy) + localPos.z
				: PlayfieldHeight + localPos.z;
			transform.localPosition = localPos;

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
				var pos = t.localPosition;
				t.localPosition = new Vector3(-localPos.x, -localPos.y, insertMeshComponent.PositionZ);
			}
		}

		#endregion

		#region Runtime

		private bool _hasLights;
		private Light[] _unityLights;
		private readonly List<(Renderer, Color, float)> _fullEmissions = new List<(Renderer, Color, float)>();
		private float _fullIntensity;
		private MaterialPropertyBlock _propBlock;

		public bool Enabled {
			set {
				StopAllCoroutines();
				foreach (var unityLight in _unityLights) {
					unityLight.enabled = value;
				}
			}
		}

		public Color Color {
			get => _unityLights[0].color;
			set {
				foreach (var unityLight in _unityLights) {
					unityLight.color = value;

					// todo handle insert material color
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

			player.RegisterLamp(this);
			_unityLights = GetComponentsInChildren<Light>();
			_hasLights = _unityLights.Length > 0;

			// remember intensity
			if (_hasLights) {
				_fullIntensity = _unityLights[0].intensity;
			}
			// enable at 0
			foreach (var unityLight in _unityLights) {
				if (FadeEnabled) {
					unityLight.enabled = true;
					unityLight.intensity = 0;

				} else {
					unityLight.enabled = false;
				}
			}

			// emissive materials
			_propBlock = new MaterialPropertyBlock();
			foreach (var mr in GetComponentsInChildren<MeshRenderer>()) {
				var emissiveColor = RenderPipeline.Current.MaterialConverter.GetEmissiveColor(mr.sharedMaterial);
				if (emissiveColor?.a > 10f) {
					_fullEmissions.Add((mr, (Color)emissiveColor, 0));
				}
			}
		}

		public void FadeTo(float value)
		{
			if (!_hasLights) {
				return;
			}
			if (FadeEnabled) {
				StopAllCoroutines();
				StartCoroutine(nameof(Fade), value);

			} else {
				foreach (var unityLight in _unityLights) {
					if (value > 0) {
						unityLight.intensity = value * _fullIntensity;
						unityLight.enabled = true;

					} else {
						unityLight.enabled = false;
					}
				}

				SetEmissions(value);
			}
		}

		public void StartBlinking()
		{
			if (!_hasLights) {
				return;
			}
			StopAllCoroutines();
			StartCoroutine(nameof(Blink));
		}

		private IEnumerator Blink()
		{
			// parse blink sequence
			var blinkInterval = BlinkInterval == 0 ? 1000 : BlinkInterval;
			var blinkPattern = BlinkPattern.Trim().Length < 2 ? "10" : BlinkPattern.Trim();
			var sequence = blinkPattern.ToCharArray().Select(c => c == '1').ToArray();

			// step time is stored in ms but we need seconds
			var stepTime = blinkInterval / 1000f;

			while (true) {
				foreach (var on in sequence) {
					yield return Fade(on ? 1 : 0);
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

			var a = _unityLights[0].intensity;
			var b = _fullIntensity * value;
			var duration = a < b
				? FadeSpeedUp * (_fullIntensity - a) / _fullIntensity
				: FadeSpeedDown * (1 - (_fullIntensity - a) / _fullIntensity);

			if (duration == 0) {
				foreach (var unityLight in _unityLights) {
					unityLight.intensity = b;
				}
				SetEmissions(value);

			} else {
				while (counter <= duration) {
					counter += Time.deltaTime;
					var position = counter / duration;
					foreach (var unityLight in _unityLights) {
						unityLight.intensity = Mathf.Lerp(a, b, position);
					}
					yield return FadeEmissions(value, position);
				}
			}
		}

		/// <summary>
		/// Sets the material emissions as a LERP between the current emission and
		/// a value for a given position.
		/// </summary>
		/// <param name="value">Value, between 0 and 1. End position of LERP is this value times full emission.</param>
		/// <param name="position">LERP position</param>
		private IEnumerator FadeEmissions(float value, float position)
		{
			for (var i = 0; i < _fullEmissions.Count; i++) {
				var (mr, color, lastValue) = _fullEmissions[i];
				mr.GetPropertyBlock(_propBlock);
				var emission = Mathf.Lerp(lastValue, value, position);
				RenderPipeline.Current.MaterialConverter.SetEmissiveColor(_propBlock, emission * color * 0.05f);
				_fullEmissions[i] = (mr, color, emission);
				mr.SetPropertyBlock(_propBlock);
			}
			yield return null;
		}

		private void SetEmissions(float value)
		{
			foreach (var (mr, color, lastValue) in _fullEmissions) {
				mr.GetPropertyBlock(_propBlock);
				RenderPipeline.Current.MaterialConverter.SetEmissiveColor(_propBlock, value * color * 0.05f);
				mr.SetPropertyBlock(_propBlock);
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(LightData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			transform.localPosition = new Vector3(data.Center.X, data.Center.Y, 0);
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
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);

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
			var pos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Center = pos.ToVertex2Dxy();
			data.Surface = Surface != null ? Surface.name : string.Empty;
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

		#endregion
	}
}
