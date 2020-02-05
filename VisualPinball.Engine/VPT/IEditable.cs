namespace VisualPinball.Engine.VPT
{
	public interface IEditable
	{
		bool IsLocked { get; set; }
		int EditorLayer { get; set; }
	}
}
