using UnityEngine;

namespace VisualPinball.Unity.VPT
{
	// TODO: rename this interface, its evolved a bit
	public interface IItemDataTransformable
	{
		bool RebuildMeshOnMove { get; }
		bool RebuildMeshOnScale { get; }
		bool MeshDirty { get; set; }
		void RebuildMeshes();

		ItemDataTransformType EditorPositionType { get; }
		Vector3 GetEditorPosition();
		void SetEditorPosition(Vector3 pos);

		ItemDataTransformType EditorRotationType { get; }
		Vector3 GetEditorRotation();
		void SetEditorRotation(Vector3 pos);

		ItemDataTransformType EditorScaleType { get; }
		Vector3 GetEditorScale();
		void SetEditorScale(Vector3 pos);
	}

	public enum ItemDataTransformType
	{
		None,
		OneD,
		TwoD,
		ThreeD,
	}
}
