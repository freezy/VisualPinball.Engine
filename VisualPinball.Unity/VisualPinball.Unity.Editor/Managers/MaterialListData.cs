namespace VisualPinball.Unity.Editor.Managers
{
	public class MaterialListData : IManagerListData
	{
		[ManagerListColumn]
		public string Name => Material?.Name ?? "";
		[ManagerListColumn(HeaderName = "In Use", Width = 50)]
		public bool InUse;

		public Engine.VPT.Material Material;
	}
}
