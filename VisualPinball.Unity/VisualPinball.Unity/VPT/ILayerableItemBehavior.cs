﻿namespace VisualPinball.Unity.VPT
{
	/// <summary>
	/// Exposes layer-related data from all ItemBehaviors
	/// </summary>
	public interface ILayerableItemBehavior
	{
		int EditorLayer { get; }
		string EditorLayerName { get; set; }
		bool EditorLayerVisibility { get; set; }
	}
}
