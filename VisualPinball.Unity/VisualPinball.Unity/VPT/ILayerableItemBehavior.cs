namespace VisualPinball.Unity.VPT
{
	/// <summary>
	/// Expose Layer related date from all ItemBehaviors
	/// </summary>
	public interface ILayerableItemBehavior
	{
		int EditorLayer { get; set; }
		string EditorLayerName { get; set; }
		bool EditorLayerVisibility { get; set; }
	}
}
