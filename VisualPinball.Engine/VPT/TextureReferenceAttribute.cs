using System;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Attribute for item data fields that reference vpx textures
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
	public class TextureReferenceAttribute : Attribute
	{
	}
}
