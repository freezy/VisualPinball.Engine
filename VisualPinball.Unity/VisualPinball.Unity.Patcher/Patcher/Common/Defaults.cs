using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher.Item;
using VisualPinball.Unity.Patcher.Matcher.Table;

namespace VisualPinball.Unity.Patcher.Patcher.Common
{
	[AnyMatch]
	public class Defaults
	{
		[NameMatch("BallShadow1")]
		[NameMatch("BallShadow2")]
		[NameMatch("BallShadow3")]
		[NameMatch("BallShadow4")]
		[NameMatch("BallShadow5")]
		[NameMatch("BallShadow6")]
		[NameMatch("BallShadow7")]
		public void RemoveBallShadow(GameObject gameObject)
		{
			gameObject.GetComponent<MeshRenderer>().enabled = false;
		}

		[NameMatch("FlipperLSh")]
		[NameMatch("FlipperRSh")]
		public void RemoveFlipperShadow(GameObject gameObject)
		{
			gameObject.GetComponent<MeshRenderer>().enabled = false;
		}
	}
}
