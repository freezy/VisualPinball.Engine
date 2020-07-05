using System;

namespace VisualPinball.Unity.VPT
{
	internal interface IApiInitializable
	{
		void OnInit();
	}

	internal interface IApiHittable
	{
		void OnHit();
	}
}
