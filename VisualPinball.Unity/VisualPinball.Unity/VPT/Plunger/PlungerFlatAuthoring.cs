using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerFlatAuthoring : PlungerChildAuthoring
	{
		protected override void SetChildEntity(ref PlungerStaticData staticData, Entity entity)
		{
			staticData.FlatEntity = entity;
		}

		protected override IEnumerable<Vertex3DNoTex2> GetVertices(PlungerMeshGenerator meshGenerator, int frame)
		{
			return meshGenerator.BuildFlatVertices(frame);
		}

		protected override void PostConvert(Entity entity, EntityManager dstManager, PlungerMeshGenerator meshGenerator)
		{
			// add mesh data
			var uvBuffer = dstManager.AddBuffer<PlungerUvBufferElement>(entity);
			for (var frame = 0; frame < meshGenerator.NumFrames; frame++) {
				var vertices = meshGenerator.BuildFlatVertices(frame);
				foreach (var v in vertices) {
					uvBuffer.Add(new PlungerUvBufferElement(new float2(v.Tu, v.Tv)));
				}
			}
		}
	}
}
