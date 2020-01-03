using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The root object for everything table related. <p/>
	///
	/// A table contains all the playfield elements, as well as a set of
	/// global data.
	/// </summary>
	public class Table : Item<TableData>
	{
		public Material[] Materials => Data.Materials;
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive> Primitives = new Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive>();
		public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();

		public Table(BinaryReader reader) : base(new TableData(reader)) { }

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <returns>The parsed table</returns>
		public static Table Load(string filename)
		{
			return TableLoader.Load(filename);
		}

		public Material GetMaterial(string name)
		{
			return Data.Materials.FirstOrDefault(m => m.Name == name);
		}

		public float GetScaleZ()
		{
			return Data.BgScaleZ?[Data.BgCurrentSet] ?? 1.0f;
		}
	}
}
