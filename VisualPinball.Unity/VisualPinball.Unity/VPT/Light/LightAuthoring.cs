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
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Light")]
	public class LightAuthoring : ItemMainRenderableAuthoring<Light, LightData>, ILampAuthoring
	{
		public ILightable Lightable => Item;

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

		private UnityEngine.Light _unityLight;
		private float _fullIntensity;

		protected override Light InstantiateItem(LightData data) => new Light(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Light, LightData, LightAuthoring>);
		protected override Type ColliderAuthoringType { get; } = null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override IEnumerable<Type> ValidParents => LightBulbMeshAuthoring.ValidParentTypes
			.Concat(LightSocketMeshAuthoring.ValidParentTypes)
			.Distinct();

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for lamp {Name}.");
				return;
			}

			player.RegisterLamp(Item, gameObject);
			_unityLight = GetComponentInChildren<UnityEngine.Light>();
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
			var sequence = Data.BlinkPattern.ToCharArray().Select(c => c == '1').ToArray();

			// step time is stored in ms but we need seconds
			var stepTime = Data.BlinkInterval / 1000f;

			while (true) {
				foreach (var on in sequence) {
					yield return Fade(on ? 1 : 0);
					var timeFading = on ? Data.FadeSpeedUp : Data.FadeSpeedDown;
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
				? _data.FadeSpeedUp * (_fullIntensity - a) / _fullIntensity
				: _data.FadeSpeedDown * (1 - (_fullIntensity - a) / _fullIntensity);

			while (counter < duration) {
				counter += Time.deltaTime;
				_unityLight.intensity = Mathf.Lerp(a, b, counter / duration);
				yield return null;
			}
		}

		public override void SetData(LightData data, IMaterialProvider materialProvider, Dictionary<string, IItemMainAuthoring> components)
		{
		}

		public override void CopyDataTo(LightData data)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Center = localPos.ToVertex2Dxy();

			// update visibility
			data.ShowBulbMesh = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case LightBulbMeshAuthoring bulbMeshAuthoring:
						data.ShowBulbMesh = data.ShowBulbMesh || bulbMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case LightSocketMeshAuthoring socketMeshAuthoring:
						data.ShowBulbMesh = data.ShowBulbMesh || socketMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}
		}

		public void OnBulbEnabled(bool bulbEnabledBefore, bool bulbEnabledAfter)
		{
			if (bulbEnabledBefore == bulbEnabledAfter) {
				return;
			}

			var convertedItem = new ConvertedItem<Light, LightData, LightAuthoring>(gameObject);
			if (bulbEnabledAfter) {
				convertedItem.AddMeshAuthoring<LightBulbMeshAuthoring>(LightMeshGenerator.Bulb);
				convertedItem.AddMeshAuthoring<LightSocketMeshAuthoring>(LightMeshGenerator.Socket);

			} else {
				convertedItem.Destroy<LightBulbMeshAuthoring>();
				convertedItem.Destroy<LightSocketMeshAuthoring>();
			}
		}

		public override void ItemDataChanged()
		{
			base.ItemDataChanged();

			if (_unityLight == null) {
				_unityLight = GetComponentInChildren<UnityEngine.Light>(includeInactive: true);
				if (_unityLight == null) {
					var lightObj = new GameObject("Light (Unity)") {
						layer = SceneTableContainer.ChildObjectsLayer
					};
					lightObj.transform.parent = transform;
					lightObj.transform.localPosition = Vector3.zero;
					_unityLight = lightObj.AddComponent<UnityEngine.Light>();
				}
			}
			RenderPipeline.Current.LightConverter.UpdateLight(_unityLight, Data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Data.MeshRadius, Data.MeshRadius, Data.MeshRadius);
		public override void SetEditorScale(Vector3 scale) => Data.MeshRadius = scale.x;
	}
}
