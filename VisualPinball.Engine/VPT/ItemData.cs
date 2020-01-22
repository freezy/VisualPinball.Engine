// ReSharper disable UnassignedField.Global

using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Data classes reflects what's in the VPX file.<p/>
	///
	/// Every playfield item has its own data class. They can currently
	/// only read data.
	/// </summary>
	public abstract class ItemData : BiffData
	{
		public readonly string StorageName;

		public abstract string Name { get; set; }

		protected ItemData(string storageName)
		{
			StorageName = storageName;
		}
	}
}
