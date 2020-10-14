// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Light")]
	public class LightAuthoring : ItemMainAuthoring<Light, LightData>
	{
		private UnityEngine.Light _unityLight;

		protected override Light InstantiateItem(LightData data) => new Light(data);

		protected override Type MeshAuthoringType { get; } = null;


		public void OnBulbEnabled(bool bulbEnabledBefore, bool bulbEnabledAfter)
		{
			if (bulbEnabledBefore == bulbEnabledAfter) {
				return;
			}

			if (bulbEnabledAfter) {
				LightExtensions.CreateChild<LightBulbMeshAuthoring>(gameObject, LightMeshGenerator.Bulb);
				LightExtensions.CreateChild<LightSocketMeshAuthoring>(gameObject, LightMeshGenerator.Socket);
			} else {
				var bulbMeshAuthoring = GetComponentInChildren<LightBulbMeshAuthoring>();
				if (bulbMeshAuthoring != null) {
					DestroyImmediate(bulbMeshAuthoring.gameObject);
				}
				var socketMeshAuthoring = GetComponentInChildren<LightSocketMeshAuthoring>();
				if (socketMeshAuthoring != null) {
					DestroyImmediate(socketMeshAuthoring.gameObject);
				}
			}
		}

		protected override void ItemDataChanged()
		{
			base.ItemDataChanged();

			if (_unityLight == null) {
				_unityLight = GetComponentInChildren<UnityEngine.Light>(includeInactive: true);
				if (_unityLight == null) {
					var lightObj = new GameObject("Light (Unity)");
					lightObj.layer = VpxConverter.ChildObjectsLayer;
					lightObj.transform.parent = transform;
					lightObj.transform.localPosition = Vector3.zero;
					_unityLight = lightObj.AddComponent<UnityEngine.Light>();
				}
			}

			if (_unityLight != null) {
				// Set color and position
				_unityLight.color = Data.Color2.ToUnityColor();
				_unityLight.intensity = Data.Intensity / 2f;
				_unityLight.range = Data.Falloff * 0.001f;
				// TODO: vpe specific data for height
				_unityLight.transform.localPosition = new Vector3(0f, 0f, 25f);

				// TODO: vpe specific shadow settings
				_unityLight.shadows = LightShadows.Hard;
				_unityLight.shadowBias = 0f;
				_unityLight.shadowNearPlane = 0f;
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector2();
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Data.MeshRadius, Data.MeshRadius, Data.MeshRadius);
		public override void SetEditorScale(Vector3 scale) => Data.MeshRadius = scale.x;
	}
}
