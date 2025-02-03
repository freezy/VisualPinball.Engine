using System;
using System.IO;

namespace VisualPinball.Unity.Editor.Packaging
{
	public interface IVpeStorage : IDisposable
	{
		IPackageFolder AddFolder(string name);
		IPackageFolder GetFolder(string name);
		void SaveAs(string path);
		void Close();
	}

	public interface IPackageFolder
	{
		string Name { get; }
		IPackageFile AddFile(string name);
		IPackageFolder AddFolder(string name);
		bool TryGetFolder(string name, out IPackageFolder storage);
		IPackageFile GetFile(string name);
		IPackageFolder GetFolder(string name);
		void VisitFiles(Action<IPackageFile> action);
		void VisitFolders(Action<IPackageFolder> action);
		bool TryGetFile(string name, out IPackageFile stream);
	}

	public interface IPackageFile
	{
		Stream AsStream();
		void SetData(byte[] data);
		byte[] GetData();
	}
}
