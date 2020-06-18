using UnityEngine;

namespace VisualPinball.Unity.VPT
{
	public interface IEditableItemBehavior
	{
		bool MeshDirty { get; set; }
		void RebuildMeshes();

		// the following interfaces allow each item behavior to define which axes should
		// be shown on the scene view gizmo, the gizmo itself will use the associated
		// get and set methods, which are expected to update item data directly
		ItemDataTransformType EditorPositionType { get; }
		Vector3 GetEditorPosition();
		void SetEditorPosition(Vector3 pos);

		ItemDataTransformType EditorRotationType { get; }
		Vector3 GetEditorRotation();
		void SetEditorRotation(Vector3 pos);

		ItemDataTransformType EditorScaleType { get; }
		Vector3 GetEditorScale();
		void SetEditorScale(Vector3 pos);

		bool IsLocked { get; set; }
	}

	public enum ItemDataTransformType
	{
		None,
		OneD,
		TwoD,
		ThreeD,
	}
}
