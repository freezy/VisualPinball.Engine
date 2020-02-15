using System;

namespace VisualPinball.Engine.IO
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BiffIgnoreAttribute : Attribute
	{
		public readonly string Name;

		public BiffIgnoreAttribute(string name)
		{
			Name = name;
		}
	}
}
