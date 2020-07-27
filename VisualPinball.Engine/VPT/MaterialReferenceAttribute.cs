using System;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Attribute for item data fields that reference vpx materials
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
	public class MaterialReferenceAttribute : Attribute
	{
    }
}
