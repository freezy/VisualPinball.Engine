using UnityEngine;

namespace VisualPinball.Unity.VPT
{
	public interface IEditableItemBehavior
	{
		bool IsLocked { get; set; }
		bool MeshDirty { get; set; }
		string[] UsedMaterials { get; }

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

		// Called by the material editor when a rename occurs to give each item a chance
		// to update its fields for the new name
		void HandleMaterialRenamed(string undoName, string oldName, string newName);
		void HandleTextureRenamed(string undoName, string oldName, string newName);
	}

	public enum ItemDataTransformType
	{
		None,
		OneD,
		TwoD,
		ThreeD,
	}
}
