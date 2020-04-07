using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.HitTest
{
	public class HitTestSystem : JobComponentSystem
	{

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			Entities.ForEach((ref CollisionEventData coll, in BallData ballData) => {

				if (ballData.IsFrozen) {
					return;
				}

				// // search upto current hit time
				// coll.HitTime = hitTime;
				// coll.Obj = null;
				//
				// // always check for playfield and top glass
				// // if (!_table.HasMeshAsPlayfield) {
				// // 	_hitPlayfield.DoHitTest(ball, coll, this);
				// // }
				// //
				// // _hitTopGlass.DoHitTest(ball, coll, this);
				//
				// // swap order of dynamic and static obj checks randomly
				// if (MathF.Random() < 0.5) {
				// 	_hitOcTreeDynamic.HitTestBall(ball, coll, this); // dynamic objects
				// 	_hitOcTree.HitTestBall(ball, coll, this);        // find the hit objects and hit times
				//
				// } else {
				// 	_hitOcTree.HitTestBall(ball, coll, this);        // find the hit objects and hit times
				// 	_hitOcTreeDynamic.HitTestBall(ball, coll, this); // dynamic objects
				// }
				//
				// // this ball's hit time
				// var htz = coll.HitTime;
				//
				// if (htz < 0) {
				// 	// no negative time allowed
				// 	coll.Clear();
				// }
				//
				// if (coll.HasHit) {
				// 	// smaller hit time?
				// 	if (htz <= hitTime) {
				// 		// record actual event time
				// 		hitTime = htz;
				//
				// 		// less than static time interval
				// 		if (htz < PhysicsConstants.StaticTime) {
				//
				// 			if (--staticCnts < 0) {
				// 				staticCnts = 0; // keep from wrapping
				// 				hitTime = PhysicsConstants.StaticTime;
				// 			}
				// 		}
				// 	}
				// }

			}).Run();

			return default;

		}
	}
}
