using System;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Game;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[ExecuteInEditMode]
	public class FlipperComponent : MonoBehaviour
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public FlipperData FlipperData {
			private get => _flipperData;
			set {
				_flipperData = value;
				_flipper = new Flipper(_flipperData);
				baseRadius = value.BaseRadius;
			}
		}

		private Flipper Flipper => _flipper ?? (_flipper = new Flipper(_flipperData));
		private Flipper _flipper;

		public FlipperData _flipperData;

		public float baseRadius;

		private void OnValidate()
		{
			if (_flipperData == null) {
				throw new InvalidOperationException("_flipperData is null!");
			}

			if (Flipper == null) {
				throw new InvalidOperationException("_flipper is null!");
			}

			if (baseRadius != _flipperData.BaseRadius) {
				var table = transform.root.GetComponent<TableComponent>().Table;
				_flipperData.BaseRadius = baseRadius;
				var rog = _flipper.GetRenderObjects(table, Origin.Original, false);
				var baseGo = transform.Find("Base");
				var baseRo = rog.RenderObjects.FirstOrDefault(ro => ro.Name == "Base");

				var rubberGo = transform.Find("Rubber");
				var rubberRo = rog.RenderObjects.FirstOrDefault(ro => ro.Name == "Rubber");

				var unityBaseMesh = baseGo.GetComponent<MeshFilter>().sharedMesh;
				var unityRubberMesh = rubberGo.GetComponent<MeshFilter>().sharedMesh;
				baseRo.Mesh.ApplyToUnityMesh(unityBaseMesh);
				rubberRo.Mesh.ApplyToUnityMesh(unityRubberMesh);
				Logger.Info("Mesh of {0} updated."/*, rog.Name*/);
			}
		}
	}
}
