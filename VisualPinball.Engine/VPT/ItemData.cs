// ReSharper disable UnassignedField.Global

using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Data classes reflects what's in the VPX file.<p/>
	///
	/// Every playfield item has its own data class. They can currently
	/// only read data.
	/// </summary>
	public class ItemData : BiffData
	{
		[BiffString("NAME", IsWideString = true)]
		public string Name;

		public readonly string StorageName;

		public ItemData(string storageName)
		{
			StorageName = storageName;
		}
	}
}
