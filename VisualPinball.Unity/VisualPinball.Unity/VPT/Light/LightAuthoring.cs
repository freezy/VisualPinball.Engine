#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Light")]
	public class LightAuthoring : ItemAuthoring<Engine.VPT.Light.Light, LightData>
	{
		protected override string[] Children => new[] { "Bulb", "Socket" };

		private UnityEngine.Light _unityLight;

		protected override Engine.VPT.Light.Light GetItem()
		{
			return new Engine.VPT.Light.Light(data);
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
				_unityLight.color = data.Color2.ToUnityColor();
				_unityLight.intensity = data.Intensity / 2f;
				_unityLight.range = data.Falloff * 0.001f;
				// TODO: vpe specific data for height
				_unityLight.transform.localPosition = new Vector3(0f, 0f, 25f);

				// TODO: vpe specific shadow settings
				_unityLight.shadows = LightShadows.Hard;
				_unityLight.shadowBias = 0f;
				_unityLight.shadowNearPlane = 0f;
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector2();
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(data.MeshRadius, data.MeshRadius, data.MeshRadius);
		public override void SetEditorScale(Vector3 scale) => data.MeshRadius = scale.x;
	}
}
