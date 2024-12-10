using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MechSoundsComponent)), CanEditMultipleObjects]
	public class SoundsComponentInspector : UnityEditor.Editor
	{
		[SerializeField]
		private VisualTreeAsset inspectorXml;

		public override VisualElement CreateInspectorGUI()
		{
			var inspector = new VisualElement();
			var comp = target as MechSoundsComponent;
			if (!comp!.TryGetComponent<ISoundEmitter>(out var _))
				inspector.Add(new HelpBox("Cannot find sound emitter. This component only works with a sound emitter on the same GameObject.", HelpBoxMessageType.Warning));
			inspectorXml.CloneTree(inspector);
			return inspector;
		}
	}
}
