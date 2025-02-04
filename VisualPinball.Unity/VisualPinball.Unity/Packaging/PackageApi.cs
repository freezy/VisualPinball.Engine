// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IO;

namespace VisualPinball.Unity.Editor.Packaging
{
	/// <summary>
	/// Here we decide which implementation to use for storing and packing data.
	/// </summary>
	public static class PackageApi
	{
		public static readonly IStorageManager StorageManager = new SharpZipStorageManager();
		// public static IStorageManager StorageManager => new OpenMcdfStorageManager();

		public static readonly IDataPacker Packer = new MemoryPackDataPacker();
		// public static readonly IDataPacker Packer = new JsonPacker();
	}

	/// <summary>
	/// A manager for creating and opening storages.
	/// </summary>
	public interface IStorageManager
	{
		/// <summary>
		/// Creates a new storage instance for writing to the file.
		/// </summary>
		/// <param name="path">Path to the file.</param>
		/// <returns>Instance of the storage.</returns>
		IPackageStorage CreateStorage(string path);

		/// <summary>
		/// Opens an existing storage instance for reading from the file.
		/// </summary>
		/// <param name="path">Path to the file.</param>
		/// <returns>Instance of the storage.</returns>
		IPackageStorage OpenStorage(string path);
	}

	/// <summary>
	/// An abstraction for a storage that can contain folders and files.
	/// </summary>
	public interface IPackageStorage : IDisposable
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
		/// Close the file.
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
		/// <param name="ext">Extensions of the file, dot included.</param>
		/// <returns>Reference to the new file.</returns>
		IPackageFile AddFile(string name, string ext = null);

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
		/// <param name="ext">Extensions of the file, dot included.</param>
		/// <returns>True if file exists, false otherwise.</returns>
		bool TryGetFile(string name, out IPackageFile file, string ext = null);

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
		/// <param name="ext">Extensions of the file, dot included.</param>
		/// <returns>Reference to the file.</returns>
		IPackageFile GetFile(string name, string ext = null);

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

	/// <summary>
	/// The data packer is the part that converts object data into a byte array and vice versa.
	/// </summary>
	public interface IDataPacker
	{
		/// <summary>
		/// Convert an object to a byte array.
		/// </summary>
		/// <param name="obj">Object to serialize</param>
		/// <returns>Serialized data.</returns>
		public byte[] Pack<T>(T obj);

		/// <summary>
		/// Convert a byte array to an object.
		/// </summary>
		/// <param name="data">Serialized data</param>
		/// <typeparam name="T">Type of the object to deserialize</typeparam>
		/// <returns>Deserialized object.</returns>
		public T Unpack<T>(byte[] data);

		public string FileExtension { get; }
	}
}
