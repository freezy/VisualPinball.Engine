using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[ExecuteInEditMode]
	public class FlipperComponent : ItemComponent<Flipper, FlipperData>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public float baseRadius;

		protected override void OnDataSet(FlipperData data)
		{
			baseRadius = data.BaseRadius;
		}

		protected override Flipper GetItem(FlipperData data)
		{
			return new Flipper(data);
		}

		private void OnValidate()
		{

			if (baseRadius != _data.BaseRadius) {
				var table = transform.root.GetComponent<TableComponent>().Table;
				_data.BaseRadius = baseRadius;
				var rog = Item.GetRenderObjects(table, Origin.Original, false);
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
