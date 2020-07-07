using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class VisualPinballScript : MonoBehaviour
	{
		public virtual void OnAwake(TableApi table)
		{
			table.Flipper("LeftFlipper").LimitBos += (sender, args) => {
				Debug.Log("Left flipper BOS");
			};

			table.Flipper("LeftFlipper").LimitEos += (sender, args) => {
				Debug.Log("Left flipper EOS");
			};
		}
	}
}
