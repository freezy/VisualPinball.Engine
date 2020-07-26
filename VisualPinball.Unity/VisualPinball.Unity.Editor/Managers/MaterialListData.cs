namespace VisualPinball.Unity.Editor.Managers
{
	public class MaterialListData : IManagerListData
	{
		[ManagerListColumn(Order = 0)]
		public string Name => Material?.Name ?? "";
		[ManagerListColumn(Order = 1, HeaderName = "In Use", Width = 50)]
		public bool InUse;

		public Engine.VPT.Material Material;
	}
}
