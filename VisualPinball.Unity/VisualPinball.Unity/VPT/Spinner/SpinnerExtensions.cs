using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class SpinnerExtensions
	{
		public static SpinnerAuthoring SetupGameObject(this Engine.VPT.Spinner.Spinner spinner, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SpinnerAuthoring>().SetItem(spinner);
			obj.AddComponent<ConvertToEntity>();

			var wire = obj.transform.Find("Plate").gameObject;
			wire.AddComponent<SpinnerPlateAuthoring>().SetItem(spinner, "Plate");

			return ic as SpinnerAuthoring;
		}
	}
}
