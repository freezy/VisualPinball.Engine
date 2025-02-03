using System;
using System.IO;

namespace VisualPinball.Unity.Editor.Packaging
{
	/// <summary>
	/// An abstraction for a storage that can contain folders and files.
	/// </summary>
	public interface IVpeStorage : IDisposable
	{
		/// <summary>
		/// Create or reference a new folder in the root of the storage.
		/// </summary>
		/// <param name="name">Name of the new folder</param>
		/// <returns>Reference to the new folder.</returns>
		IPackageFolder AddFolder(string name);

		/// <summary>
		/// Return a reference to an existing folder in the root of the storage.
		/// </summary>
		/// <param name="name">Name of the folder.</param>
		/// <returns>Reference to the existing folder.</returns>
		IPackageFolder GetFolder(string name);

		/// <summary>
		/// Save the storage to a file and close it.
		/// </summary>
		/// <param name="path"></param>
		void SaveAs(string path);

		/// <summary>
		/// Just close the file.
		/// </summary>
		void Close();
	}

	/// <summary>
	/// A folder that lives within the storage.
	/// </summary>
	public interface IPackageFolder
	{
		/// <summary>
		/// Name of the folder.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Create or reference a new folder with this folder as a direct parent.
		/// </summary>
		/// <param name="name">Name of the folder.</param>
		/// <returns>Reference to the new folder.</returns>
		IPackageFolder AddFolder(string name);

		/// <summary>
		/// Create or reference a new file with this folder as a direct parent.
		/// </summary>
		/// <param name="name">Name of the file.</param>
		/// <returns>Reference to the new file.</returns>
		IPackageFile AddFile(string name);

		/// <summary>
		/// Try to retrieve an existing folder with this folder as a direct parent.
		/// </summary>
		/// <param name="name">Name of the folder.</param>
		/// <param name="folder">Reference to the existing folder, or null otherwise</param>
		/// <returns>True if folder exists, false otherwise.</returns>
		bool TryGetFolder(string name, out IPackageFolder folder);

		/// <summary>
		/// Try to retrieve an existing file with this folder as a direct parent.
		/// </summary>
		/// <param name="name">Name of the file.</param>
		/// <param name="file">Reference to the existing file, or null otherwise</param>
		/// <returns>True if file exists, false otherwise.</returns>
		bool TryGetFile(string name, out IPackageFile file);

		/// <summary>
		/// Retrieve an existing folder with this folder as a direct parent.
		/// </summary>
		/// <throws>IllegalArgumentException if folder does not exist.</throws>
		/// <param name="name">Name of the folder.</param>
		/// <returns>Reference to the folder.</returns>
		IPackageFolder GetFolder(string name);

		/// <summary>
		/// Retrieve an existing file with this folder as a direct parent.
		/// </summary>
		/// <throws>IllegalArgumentException if file does not exist.</throws>
		/// <param name="name">Name of the file</param>
		/// <returns>Reference to the file.</returns>
		IPackageFile GetFile(string name);

		/// <summary>
		/// Loop through all folders in this folder by executing an action on each folder.
		/// </summary>
		/// <param name="action">Action to execute.</param>
		void VisitFolders(Action<IPackageFolder> action);

		/// <summary>
		/// Loop through all files in this folder by executing an action on each file.
		/// </summary>
		/// <param name="action">Action to execute.</param>
		void VisitFiles(Action<IPackageFile> action);
	}

	/// <summary>
	/// A file that lives within the storage.
	/// </summary>
	public interface IPackageFile
	{
		/// <summary>
		/// Return an input stream for the file.
		/// </summary>
		/// <returns>A stream that can be written to.</returns>
		Stream AsStream();

		/// <summary>
		/// Write data to the file.
		/// </summary>
		/// <param name="data">Data to write.</param>
		void SetData(byte[] data);

		/// <summary>
		/// Retrieve data from the file.
		/// </summary>
		/// <returns>Data to retrieve.</returns>
		byte[] GetData();
	}
}
