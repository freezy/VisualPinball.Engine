using System;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Managers
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
	public class ManagerListColumnAttribute : Attribute
	{
		public string HeaderName;
		public TextAlignment HeaderAlignment;
		public int Width = 300;
	}
}
