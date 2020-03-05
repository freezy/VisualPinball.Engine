using System;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Patcher.Matcher.Item
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public abstract class ItemMatchAttribute : Attribute
	{
		public abstract bool Matches(Engine.VPT.Table.Table table, IRenderable item, RenderObject ro, GameObject obj);
	}
}
