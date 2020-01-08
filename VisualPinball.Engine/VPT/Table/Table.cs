using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The root object for everything table related. <p/>
	///
	/// A table contains all the playfield elements, as well as a set of
	/// global data.
	/// </summary>
	public class Table : Item<TableData>, IRenderable
	{
		public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Bumper.Bumper> Bumpers = new Dictionary<string, VisualPinball.Engine.VPT.Bumper.Bumper>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Light.Light> Lights = new Dictionary<string, VisualPinball.Engine.VPT.Light.Light>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive> Primitives = new Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive>();

		public IRenderable[] Renderables => new IRenderable[] { this }
			.Concat(Bumpers.Values)
			//.Concat(Lights.Values)
			.Concat(Primitives.Values)
			.ToArray();

		private readonly TableMeshGenerator _meshGenerator;

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <returns>The parsed table</returns>
		public static Table Load(string filename)
		{
			return TableLoader.Load(filename);
		}

		public Table(BinaryReader reader) : base(new TableData(reader))
		{
			_meshGenerator = new TableMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table table)
		{
			return new[] { _meshGenerator.Playfield };
		}

		public Material GetMaterial(string name)
		{
			return Data.Materials == null || name == null
				? null
				: Data.Materials.FirstOrDefault(m => m.Name == name);
		}

		public Texture GetTexture(string name)
		{
			return name == null
				? null
				: Textures.ContainsKey(name.ToLower()) ? Textures[name.ToLower()] : null;
		}

		public float GetScaleZ()
		{
			return Data.BgScaleZ?[Data.BgCurrentSet] ?? 1.0f;
		}

		internal void SetupPlayfieldMesh()
		{
			if (Primitives.ContainsKey("playfield_mesh")) {
				_meshGenerator.SetFromPrimitive(this, Primitives["playfield_mesh"]);
				Primitives.Remove("playfield_mesh");
			} else {
				_meshGenerator.SetFromTableDimensions(this);
			}
		}

		public float GetSurfaceHeight(string surfaceName, float x, float y)
		{
			if (surfaceName == null) {
				return Data.TableHeight;
			}

			// if (Surfaces.ContainsKey[surfaceName]) {
			// 	return Data.TableHeight + Surfaces[surfaceName].Data.HeightTop;
			// }
			//
			// if (Surfaces[surfaceName]) {
			// 	return Data.TableHeight + Surfaces[surfaceName].GetSurfaceHeight(x, y, this);
			// }

			//logger().warn('[Table.getSurfaceHeight] Unknown surface %s.', surface);
			return Data.TableHeight;
		}
	}
}
