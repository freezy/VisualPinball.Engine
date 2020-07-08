using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class VisualPinballScript : MonoBehaviour
	{
		public virtual void OnAwake(TableApi table)
		{
			table.Flipper("LeftFlipper").Collide += (sender, args) => {
				Debug.Log("LeftFlipper_Collide " + args.FlipperHit);
			};
			table.Flipper("LeftFlipper").Hit += (sender, args) => {
				Debug.Log("LeftFlipper_Hit");
			};
			table.Flipper("LeftFlipper").Init += (sender, args) => {
				Debug.Log("LeftFlipper_Init");
			};
			table.Flipper("LeftFlipper").LimitBos += (sender, args) => {
				Debug.Log("LeftFlipper_LimitBOS " + args.AngleSpeed);
			};
			table.Flipper("LeftFlipper").LimitEos += (sender, args) => {
				Debug.Log("LeftFlipper_LimitEOS " + args.AngleSpeed);
			};
		}
	}
}
