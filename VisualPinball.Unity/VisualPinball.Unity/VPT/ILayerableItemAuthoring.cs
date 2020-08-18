namespace VisualPinball.Unity
{
	/// <summary>
	/// Exposes layer-related data from all ItemBehaviors
	/// </summary>
	public interface ILayerableItemAuthoring
	{
		int EditorLayer { get; }
		string EditorLayerName { get; set; }
		bool EditorLayerVisibility { get; set; }
	}
}
