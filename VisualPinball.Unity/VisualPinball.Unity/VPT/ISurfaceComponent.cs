using UnityEngine;

namespace VisualPinball.Unity
{
	public interface ISurfaceComponent
	{
		float Height(Vector2 position);

		Transform transform { get; }
	}
}
