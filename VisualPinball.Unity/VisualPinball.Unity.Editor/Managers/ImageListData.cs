namespace VisualPinball.Unity.Editor.Managers
{
	public class ImageListData : IManagerListData
	{
		[ManagerListColumn]
		public string Name => Texture?.Name ?? "";
		[ManagerListColumn(HeaderName = "In Use", Width = 50)]
		public bool InUse;

		public Engine.VPT.Texture Texture;
	}
}
