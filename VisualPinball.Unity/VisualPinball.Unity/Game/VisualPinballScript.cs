using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class VisualPinballScript : MonoBehaviour
	{
		public virtual void OnAwake(TableApi table)
		{
			table.Gate("Plate").LimitBos += (sender, args) => {
				Debug.Log("Plate BOS at " + args.AngleSpeed);
			};

			table.Gate("Plate").LimitEos += (sender, args) => {
				Debug.Log("Plate EOS at " + args.AngleSpeed);
			};

			table.Gate("Plate").Init += (sender, args) => {
				Debug.Log("Plate Init");
			};

			table.Gate("Plate").Hit += (sender, args) => {
				Debug.Log("Plate Hit");
			};
		}
	}
}
