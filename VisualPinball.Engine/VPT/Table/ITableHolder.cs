using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Table
{
	public interface ITableHolder
	{
		Table Table { get; }
		CustomInfoTags CustomInfoTags { get; }
		Dictionary<string, string> TableInfo { get; }

		bool Has<T>(string name) where T : IItem;

		T Get<T>(string name) where T : IItem;

		void Remove<T>(string name) where T : IItem;

		string GetNewName<T>(string prefix) where T : IItem;

		Material GetMaterial(string name);
		Texture GetTexture(string name);

		IEnumerable<IItem> GameItems { get; }
		IEnumerable<IRenderable> Renderables { get; }
		IEnumerable<IItem> NonRenderables { get; }
		IEnumerable<ItemData> ItemDatas { get; }
		Dictionary<string, Collection.Collection> Collections { get; }
		ITableResourceContainer<Texture> Textures { get; }
		ITableResourceContainer<Sound.Sound> Sounds { get; }
		Mappings.Mappings Mappings { get; }
		IEnumerable<ISwitchable> Switchables { get; }
		IEnumerable<ISwitchableDevice> SwitchableDevices { get; }
		IEnumerable<ICoilable> Coilables { get; }
		IEnumerable<ICoilableDevice> CoilableDevices { get; }
		IEnumerable<ILightable> Lightables { get; }

		void Save(string fileName);
	}
}
