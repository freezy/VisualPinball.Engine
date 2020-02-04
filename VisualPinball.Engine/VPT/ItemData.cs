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
	public abstract class ItemData : BiffData
	{
		public abstract string Name { get; set; }

		[BiffBool("LOCK", Pos = 1000)]
		public bool IsLocked;

		[BiffInt("LAYR", Pos = 1001)]
		public int EditorLayer;

		protected ItemData(string storageName) : base(storageName) { }
	}
}
