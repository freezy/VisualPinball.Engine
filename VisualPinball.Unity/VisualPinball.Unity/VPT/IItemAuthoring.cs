using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The non-typed version of ItemAuthoring.
	/// </summary>
	public interface IItemAuthoring
	{
		ItemDataTransformType EditorPositionType { get; }
		Vector3 GetEditorPosition();
		void SetEditorPosition(Vector3 pos);

		ItemDataTransformType EditorRotationType { get; }
		Vector3 GetEditorRotation();
		void SetEditorRotation(Vector3 rot);

		ItemDataTransformType EditorScaleType { get; }
		Vector3 GetEditorScale();
		void SetEditorScale(Vector3 rot);
	}
}
