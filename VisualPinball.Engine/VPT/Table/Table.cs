using System.Collections.Generic;
using System.IO;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The root object for everything table related. <p/>
	///
	/// A table contains all the playfield elements, as well as a set of
	/// global data.
	/// </summary>
	public class Table
	{
		public readonly TableData Data;

		public readonly Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive> Primitives = new Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive>();

		public Table(BinaryReader reader)
		{
			Data = new TableData(reader);
		}

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <returns>The parsed table</returns>
		public static Table Load(string filename)
		{
			return TableLoader.Load(filename);
		}
	}
}
