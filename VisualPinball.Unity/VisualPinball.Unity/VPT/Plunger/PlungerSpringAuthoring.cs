using System.Collections.Generic;
using Unity.Entities;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerSpringAuthoring : PlungerChildAuthoring
	{
		protected override void SetChildEntity(ref PlungerStaticData staticData, Entity entity)
		{
			staticData.SpringEntity = entity;
		}

		protected override IEnumerable<Vertex3DNoTex2> GetVertices(PlungerMeshGenerator meshGenerator, int frame)
		{
			return meshGenerator.BuildSpringVertices(frame);
		}
	}
}
