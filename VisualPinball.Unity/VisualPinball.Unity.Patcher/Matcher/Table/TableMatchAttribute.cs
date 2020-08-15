using System;

namespace VisualPinball.Unity.Patcher
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public abstract class TableMatchAttribute : Attribute
	{
		public abstract bool Matches(Engine.VPT.Table.Table table, string fileName);
	}
}
