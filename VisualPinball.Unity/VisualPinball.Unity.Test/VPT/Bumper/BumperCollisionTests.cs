using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Test
{
	public class BumperCollisionTests
	{
		private Table _table;

		[SetUp]
		public void Setup()
		{
			_table = Table.Load(VpxPath.Bumper);
		}

		[Test]
		public void TestSomething()
		{
			
		}
	}
}
