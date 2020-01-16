namespace VisualPinball.Unity.Patcher.Matcher.Table
{
	/// <summary>
	/// Matches all tables.
	/// </summary>
	public class AnyMatchAttribute : TableMatchAttribute
	{
		public override bool Matches(Engine.VPT.Table.Table table, string fileName)
		{
			return true;
		}
	}
}
