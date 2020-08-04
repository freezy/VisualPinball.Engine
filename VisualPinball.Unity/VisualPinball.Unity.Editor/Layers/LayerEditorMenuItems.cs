using UnityEditor;

namespace VisualPinball.Unity.Editor.Layers
{
	public static class LayerEditorMenuItems
	{
		public const string LayerMenuPath = "CONTEXT/LayerEditor";

		[MenuItem(LayerMenuPath + "/New Layer", false, 1)]
		private static void CreateNewLayer(MenuCommand command)
		{
			if (command.context is LayerEditor editor) {
				editor.CreateNewLayer();
			}
		}

		[MenuItem(LayerMenuPath + "/New Layer", true)]
		private static bool CreateNewLayerValidate(MenuCommand command)
		{
			return command.userData == -1;
		}

		[MenuItem(LayerMenuPath + "/Delete Layer", false, 2)]
		private static void DeleteLayer(MenuCommand command)
		{
			if (command.context is LayerEditor editor) {
				editor.DeleteLayer(command.userData);
			}
		}

		[MenuItem(LayerMenuPath + "/Delete Layer", true)]
		private static bool DeleteLayerValidate(MenuCommand command)
		{
			return command.userData != -1;
		}
	}
}
