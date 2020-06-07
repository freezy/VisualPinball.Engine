﻿using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Spinner
{
	public static class SpinnerExtensions
	{
		public static SpinnerBehavior SetupGameObject(this Engine.VPT.Spinner.Spinner spinner, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SpinnerBehavior>().SetData(spinner.Data);
			obj.AddComponent<ConvertToEntity>();

			var wire = obj.transform.Find("Plate").gameObject;
			wire.AddComponent<SpinnerPlateBehavior>().SetData(spinner.Data, "Plate");

			return ic as SpinnerBehavior;
		}
	}
}
