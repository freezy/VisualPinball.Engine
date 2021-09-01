// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Light")]
	public class LightAuthoring : ItemMainRenderableAuthoring<LightData>, ILampDeviceAuthoring
	{
		#region Data

		public Vector3 Position;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this light is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		[Min(0)]
		[Tooltip("The radius of the bulb mesh")]
		public float BulbSize = 20f;

		public int State;
		public string BlinkPattern;
		public int BlinkInterval;

		public float FadeSpeedUp;
		public float FadeSpeedDown;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Light;
		public override string ItemName => "Light";

		public override IEnumerable<Type> ValidParents => Type.EmptyTypes;

		public override LightData InstantiateData() => new LightData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<LightData, LightAuthoring>);
		protected override Type ColliderAuthoringType { get; } = null;

		public const string LampIdDefault = "default_lamp";
		private const string BulbMeshName = "Light (Bulb)";
		private const string SocketMeshName = "Light (Socket)";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineLamp> AvailableLamps => new[] {
			new GamelogicEngineLamp(LampIdDefault),
		};

		public IEnumerable<GamelogicEngineLamp> AvailableDeviceItems => AvailableLamps;

		#endregion

		#region Transformation

		public override void UpdateTransforms()
		{
			// position
			transform.localPosition = Surface != null
				? new Vector3(Position.x, Position.y, Surface.Height(Position) + Position.z)
				: new Vector3(Position.x, Position.y, PlayfieldHeight + Position.z);

			// bulb size
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
				switch (mf.sharedMesh.name) {
					case BulbMeshName:
					case SocketMeshName:
						mf.gameObject.transform.localScale = new Vector3(BulbSize, BulbSize, BulbSize);
						break;
				}
			}
		}

		#endregion

		#region Runtime

		private UnityEngine.Light _unityLight;
		private float _fullIntensity;

		public bool Enabled {
			set {
				StopAllCoroutines();
				_unityLight.enabled = value;
			}
		}

		public Color Color {
			get => _unityLight.color;
			set => _unityLight.color = value;
		}

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for lamp {name}.");
				return;
			}

			player.RegisterLamp(this);
			_unityLight = GetComponent<UnityEngine.Light>();
			_fullIntensity = _unityLight.intensity;
		}

		public void FadeTo(float seconds, float value)
		{
			StopAllCoroutines();
			StartCoroutine(nameof(Fade), value);
		}

		public void StartBlinking()
		{
			StopAllCoroutines();
			StartCoroutine(nameof(Blink));
		}

		private IEnumerator Blink()
		{
			// parse blink sequence
			var sequence = BlinkPattern.ToCharArray().Select(c => c == '1').ToArray();

			// step time is stored in ms but we need seconds
			var stepTime = BlinkInterval / 1000f;

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

			var a = _unityLight.intensity;
			var b = _fullIntensity * value;
			var duration = a < b
				? FadeSpeedUp * (_fullIntensity - a) / _fullIntensity
				: FadeSpeedDown * (1 - (_fullIntensity - a) / _fullIntensity);

			while (counter < duration) {
				counter += Time.deltaTime;
				_unityLight.intensity = Mathf.Lerp(a, b, counter / duration);
				yield return null;
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(LightData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = new Vector3(data.Center.X, data.Center.Y, 0);
			BulbSize = data.MeshRadius;

			// logical params
			State = data.State;
			BlinkPattern = data.BlinkPattern;
			BlinkInterval = data.BlinkInterval;
			FadeSpeedUp = data.FadeSpeedUp;
			FadeSpeedDown = data.FadeSpeedDown;

			// physical params
			var unityLight = GetComponentInChildren<UnityEngine.Light>(true);
			if (unityLight) {
				RenderPipeline.Current.LightConverter.UpdateLight(unityLight, data);
			}

			// visibility
			if (!data.ShowBulbMesh) {
				foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
					switch (mf.sharedMesh.name) {
						case BulbMeshName:
						case SocketMeshName:
							mf.gameObject.SetActive(false);
							break;
					}
				}
			}

			return updatedComponents;
		}


		public override IEnumerable<MonoBehaviour> SetReferencedData(LightData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			return Array.Empty<MonoBehaviour>();
		}

		public override LightData CopyDataTo(LightData data, string[] materialNames, string[] textureNames,
			bool forExport)
		{
			// name and position
			data.Name = name;
			data.Center = Position.ToVertex2Dxy();
			data.Surface = Surface != null ? Surface.name : string.Empty;
			data.MeshRadius = BulbSize;

			// logical params
			data.State = State;
			data.BlinkPattern = BlinkPattern;
			data.BlinkInterval = BlinkInterval;
			data.FadeSpeedUp = FadeSpeedUp;
			data.FadeSpeedDown = FadeSpeedDown;

			// visibility
			data.ShowBulbMesh = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
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

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => Position;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(BulbSize, BulbSize, BulbSize);
		public override void SetEditorScale(Vector3 scale) => BulbSize = scale.x;

		#endregion
	}
}
