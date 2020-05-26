using UnityEditor;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Inspectors
{
	public abstract class ItemInspector : UnityEditor.Editor
    {
		protected void ItemDataField(string label, ref float field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				string undoLabel = "Edit " + label;
				if (dirtyMesh) {
					// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
					var item = (target as IItemDataTransformable);
					if (item != null) {
						item.MeshDirty = true;
						Undo.RecordObject(this, undoLabel);
					}
				}
				Undo.RecordObject(target, undoLabel);
				field = val;
			}
		}
	}
}
