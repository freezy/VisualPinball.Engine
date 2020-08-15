using System;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Matches an game item by its name.
	/// </summary>
	public class NameMatchAttribute : ItemMatchAttribute
	{
		public bool IgnoreCase = true;

		private readonly string _name;

		public NameMatchAttribute(string name)
		{
			_name = name;
		}

		public override bool Matches(Engine.VPT.Table.Table table, IRenderable item, RenderObject ro, GameObject obj)
		{
			return IgnoreCase
				? string.Equals(item.Name, _name, StringComparison.CurrentCultureIgnoreCase)
				: item.Name == _name;
		}
	}
}
