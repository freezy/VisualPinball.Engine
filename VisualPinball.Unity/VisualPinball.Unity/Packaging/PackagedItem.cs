using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class PackagedItem
	{
		public string Type;
		public Dictionary<string, object> Data;

		public PackagedItem()
		{
		}

		public PackagedItem(Type type, Dictionary<string, object> data)
		{
			Type = type.ToString();
			Data = data;
		}
	}

}
