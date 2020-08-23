using System;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Patcher
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public abstract class ItemMatchAttribute : Attribute
	{
		/// <summary>
		/// If set, pass the game object with this name as reference to the patcher.
		/// </summary>
		public string Ref;

		public abstract bool Matches(Engine.VPT.Table.Table table, IRenderable item, RenderObject ro, GameObject obj);
	}
}
