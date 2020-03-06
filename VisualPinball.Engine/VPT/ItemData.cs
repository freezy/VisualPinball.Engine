// ReSharper disable UnassignedField.Global

using System;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Data classes reflects what's in the VPX file.<p/>
	///
	/// Every playfield item has its own data class. They can currently
	/// only read data.
	/// </summary>
	[Serializable]
	public abstract class ItemData : BiffData
	{
		[BiffBool("LOCK", Pos = 1000)]
		public bool IsLocked;

		[BiffInt("LAYR", Pos = 1001)]
		public int EditorLayer;

		public abstract string GetName();

		protected ItemData(string storageName) : base(storageName) { }
	}

	public interface IPhysicalData
	{
		float GetElasticity();
		float GetElasticityFalloff();
		float GetFriction();
		float GetScatter();
		bool GetOverwritePhysics();
		bool GetIsCollidable();
		string GetPhysicsMaterial();
	}
}
