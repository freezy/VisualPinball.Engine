using UnityEngine;

namespace VisualPinball.Unity
{
	public interface IItemColliderAuthoring
	{
		bool IsSubComponent { get; }

		void SetEditorPosition(Vector3 pos);
	}
}
