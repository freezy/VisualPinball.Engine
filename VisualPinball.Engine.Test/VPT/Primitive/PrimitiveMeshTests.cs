using System.Linq;
using FluentAssertions;
using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public PrimitiveMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Primitive);
			_obj = LoadObjFixture(ObjPath.Primitive);
		}

		[Test]
		public void ShouldGenerateImportedMesh()
		{
			var bookMesh = _table.Primitive("Books").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, bookMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateACube()
		{
			var cubeMesh = _table.Primitive("Cube").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, cubeMesh);
		}

		[Test]
		public void ShouldGenerateATriangle()
		{
			var triangleMesh = _table.Primitive("Triangle").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, triangleMesh);
		}

		[Test]
		public void ShouldProvideCorrectTransformationMatrices()
		{
			var rog = _table.Primitive("Primitive1").GetRenderObjects(_table, Origin.Original, false);

			rog.TransformationMatrix.GetScaling().X.Should().Be(100f);
			rog.TransformationMatrix.GetScaling().Y.Should().Be(100f);
			rog.TransformationMatrix.GetScaling().Z.Should().Be(100f);

			rog.TransformationMatrix.GetTranslation().X.Should().Be(505f);
			rog.TransformationMatrix.GetTranslation().Y.Should().Be(1305f);
			rog.TransformationMatrix.GetTranslation().Z.Should().Be(_table.TableHeight);
		}

		[Test]
		public void ShouldGenerateACompressedMesh()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.PrimitiveCompressed);
			var obj = LoadObjFixture(ObjPath.PrimitiveCompressed);

			var compressedMesh = table.Primitive("compressed").GetRenderObjects(table).RenderObjects[0].Mesh;
			AssertObjMesh(obj, compressedMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateAnAnimatedMesh() {
			var table = Engine.VPT.Table.Table.Load(VpxPath.PrimitiveAnimated);

			var animatedPrimitive = table.Primitive("AnimatedPrimitive");
			var mesh = animatedPrimitive.GetMesh();

			for (var i = 0; i < 7; i++) {
				var obj = LoadObjFixture(ObjPath.PrimitiveAnimated[i]);
				var frame = mesh.AnimationFrames[i];
				var frameMesh = new Mesh(
					frame.Select(v => v.ToVertex3DNoTex2()).ToArray(),
					mesh.Indices
				);

				AssertObjMesh(obj, frameMesh, "AnimatedPrimitive", switchZ: true);
			}
		}
	}
}
