using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable, ITimer, IEditable
	{
		public bool IsTimerEnabled { get => Data.IsTimerEnabled; set => Data.IsTimerEnabled = value; }
		public int TimerInterval { get => Data.TimerInterval; set => Data.TimerInterval = value; }
		public bool IsLocked { get => Data.IsLocked; set => Data.IsLocked = value; }
		public int EditorLayer { get => Data.EditorLayer; set => Data.EditorLayer = value; }

		private readonly BumperMeshGenerator _meshGenerator;

		public Bumper(BinaryReader reader, string itemName) : base(new BumperData(reader, itemName))
		{
			_meshGenerator = new BumperMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
