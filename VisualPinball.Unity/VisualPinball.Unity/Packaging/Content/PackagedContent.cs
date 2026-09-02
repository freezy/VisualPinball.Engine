// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VisualPinball.Unity
{
	/// <summary>Stable reference to an inert directory stored in a .vpe package.</summary>
	[Serializable]
	public struct PackagedContentRef
	{
		public string Id;
		public string Kind;
		public string EntryPoint;
		public string ContentHash;

#if UNITY_EDITOR
		[JsonIgnore] public string SourceDirectory;
		[JsonIgnore] public int FileCount;
		[JsonIgnore] public long TotalBytes;
		[JsonIgnore] public string ValidationStatus;
#endif

		public PackagedContentRef(string id, string kind, string entryPoint, string contentHash)
		{
			Id = id;
			Kind = kind;
			EntryPoint = entryPoint;
			ContentHash = contentHash;
#if UNITY_EDITOR
			SourceDirectory = null;
			FileCount = 0;
			TotalBytes = 0;
			ValidationStatus = null;
#endif
		}
	}

	public interface IPackagedContentResolver
	{
		PackagedContentRef AddDirectory(string kind, string sourceRoot, ContentPackOptions options);
		Task<string> ResolveAsync(PackagedContentRef contentRef, IProgress<float> progress, CancellationToken ct);
	}

	/// <summary>
	/// Implemented by components that contribute a source directory at package-write time. The
	/// component must update its serialized <see cref="PackagedContentRef"/> during this callback.
	/// </summary>
	public interface IPackagedContentSource
	{
		void PreparePackagedContent(IPackagedContentResolver resolver);
	}

	/// <summary>
	/// Implemented by restored components that consume content. Readers inject the resolver before
	/// activating a runtime table, so consumers never need access to package or zip internals.
	/// </summary>
	public interface IPackagedContentConsumer
	{
		void SetPackagedContentResolver(IPackagedContentResolver resolver);
	}

	[Serializable]
	public sealed class ContentPackOptions
	{
		public string EntryPoint;
		public string[] IncludeGlobs = Array.Empty<string>();
		public string[] ExcludeGlobs = Array.Empty<string>();
		public int MaxFileCount = PackagedContentLimits.DefaultMaxFileCount;
		public long MaxTotalBytes = PackagedContentLimits.DefaultMaxBundleBytes;

		[JsonIgnore]
		public Action<string> Warning;
	}

	public sealed class PackagedContentCacheOptions
	{
		public string CacheRoot;
		public long CapacityBytes = 4L * 1024 * 1024 * 1024;
		public int MaxFileCount = PackagedContentLimits.DefaultMaxFileCount;
		public long MaxBundleBytes = PackagedContentLimits.DefaultMaxBundleBytes;
	}

	public static class PackagedContentLimits
	{
		public const int DefaultMaxFileCount = 1_000_000;
		public const long DefaultMaxBundleBytes = 16L * 1024 * 1024 * 1024;
	}

	[Serializable]
	public sealed class PackagedContentManifest
	{
		[JsonProperty("format")] public string Format;
		[JsonProperty("version")] public int Version;
		[JsonProperty("kind")] public string Kind;
		[JsonProperty("entryPoint")] public string EntryPoint;
		[JsonProperty("fileCount")] public int FileCount;
		[JsonProperty("totalBytes")] public long TotalBytes;
		[JsonProperty("contentHash")] public string ContentHash;
		[JsonProperty("files")] public List<PackagedContentFile> Files = new();
	}

	[Serializable]
	public sealed class PackagedContentFile
	{
		[JsonProperty("path")] public string Path;
		[JsonProperty("size")] public long Size;
		[JsonProperty("sha256")] public string Sha256;
	}
}
