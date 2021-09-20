// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using FluentAssertions;
using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveMeshTests : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public PrimitiveMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.Primitive);
			_obj = LoadObjFixture(ObjPath.Primitive);
		}

		[Test]
		public void ShouldGenerateImportedMesh()
		{
			var p = _tc.Primitive("Books");
			var bookMesh = ((IRenderable)p).GetMesh(null, _tc.Table);
			AssertObjMesh(_obj, bookMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateACube()
		{
			var p = _tc.Primitive("Cube");
			var cubeMesh = ((IRenderable)p).GetMesh(null, _tc.Table);
			AssertObjMesh(_obj, cubeMesh);
		}

		[Test]
		public void ShouldGenerateATriangle()
		{
			var p = _tc.Primitive("Triangle");
			var triangleMesh = ((IRenderable)p).GetMesh(null, _tc.Table);
			AssertObjMesh(_obj, triangleMesh);
		}

		[Test]
		public void ShouldProvideCorrectTransformationMatrices()
		{
			var m = (_tc.Primitive("Primitive1") as IRenderable).TransformationMatrix(_tc.Table, Origin.Original);

			m.GetScaling().X.Should().Be(100f);
			m.GetScaling().Y.Should().Be(100f);
			m.GetScaling().Z.Should().Be(100f);

			m.GetTranslation().X.Should().Be(505f);
			m.GetTranslation().Y.Should().Be(1305f);
			m.GetTranslation().Z.Should().Be(_tc.Table.TableHeight);
		}

		[Test]
		public void ShouldGenerateACompressedMesh()
		{
			var th = FileTableContainer.Load(VpxPath.PrimitiveCompressed);
			var obj = LoadObjFixture(ObjPath.PrimitiveCompressed);
			var p = th.Primitive("compressed");

			var compressedMesh = ((IRenderable)p).GetMesh(null, th.Table);
			AssertObjMesh(obj, compressedMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateAnAnimatedMesh() {
			var table = FileTableContainer.Load(VpxPath.PrimitiveAnimated);

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
