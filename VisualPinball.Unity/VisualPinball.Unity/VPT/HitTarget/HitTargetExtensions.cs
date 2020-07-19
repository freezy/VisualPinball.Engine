﻿using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.VPT.Trigger;

namespace VisualPinball.Unity.VPT.HitTarget
{
	public static class HitTargetExtensions
	{
		public static HitTargetBehavior SetupGameObject(this Engine.VPT.HitTarget.HitTarget hitTarget, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<HitTargetBehavior>().SetData(hitTarget.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as HitTargetBehavior;
		}
	}
}
