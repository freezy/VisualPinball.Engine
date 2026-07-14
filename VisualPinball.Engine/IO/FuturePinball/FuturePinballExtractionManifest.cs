// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public sealed class FuturePinballExtractionManifest
	{
		public int SchemaVersion { get; internal set; } = 1;
		public string SourceFileName { get; internal set; }
		public long SourceBytes { get; internal set; }
		public string SourceSha256 { get; internal set; }
		public string OriginalFile { get; internal set; }
		public uint? FileVersion { get; internal set; }
		public int CompoundEntryCount { get; internal set; }
		public FuturePinballManifestCounts Counts { get; internal set; }
		public IReadOnlyList<FuturePinballManifestStream> Streams { get; internal set; } = Array.Empty<FuturePinballManifestStream>();
		public IReadOnlyList<FuturePinballManifestResource> Resources { get; internal set; } = Array.Empty<FuturePinballManifestResource>();
		public IReadOnlyList<string> Issues { get; internal set; } = Array.Empty<string>();
	}

	public sealed class FuturePinballManifestCounts
	{
		public int Elements { get; internal set; }
		public int Images { get; internal set; }
		public int Sounds { get; internal set; }
		public int Music { get; internal set; }
		public int PinModels { get; internal set; }
		public int DmdFonts { get; internal set; }
		public int ImageLists { get; internal set; }
		public int LightLists { get; internal set; }
		public int OpaqueRecords { get; internal set; }
		public int UnresolvedLinkedResources { get; internal set; }
	}

	public sealed class FuturePinballManifestStream
	{
		public string Name { get; internal set; }
		public string Kind { get; internal set; }
		public int? SourceIndex { get; internal set; }
		public uint? ElementTypeId { get; internal set; }
		public string ElementType { get; internal set; }
		public int Bytes { get; internal set; }
		public string Sha256 { get; internal set; }
		public string RawFile { get; internal set; }
		public IReadOnlyList<FuturePinballManifestRecord> Records { get; internal set; } = Array.Empty<FuturePinballManifestRecord>();
	}

	public sealed class FuturePinballManifestRecord
	{
		public int Offset { get; internal set; }
		public uint StoredLength { get; internal set; }
		public int ConsumedLength { get; internal set; }
		public string OriginalTag { get; internal set; }
		public string CanonicalTag { get; internal set; }
		public string Name { get; internal set; }
		public string ValueKind { get; internal set; }
		public string Value { get; internal set; }
		public int PayloadBytes { get; internal set; }
		public string PayloadSha256 { get; internal set; }
		public string OpaqueFile { get; internal set; }
	}

	public sealed class FuturePinballManifestResource
	{
		public string Category { get; internal set; }
		public int? SourceIndex { get; internal set; }
		public string SourceStream { get; internal set; }
		public string LogicalName { get; internal set; }
		public int? DeclaredType { get; internal set; }
		public bool Linked { get; internal set; }
		public string LinkedPath { get; internal set; }
		public string ResolutionStatus { get; internal set; }
		public string ResolvedLibrary { get; internal set; }
		public string ResolvedLibraryEntry { get; internal set; }
		public string DetectedFormat { get; internal set; }
		public IReadOnlyList<FuturePinballManifestFile> Files { get; internal set; } = Array.Empty<FuturePinballManifestFile>();
		public IReadOnlyList<string> Items { get; internal set; } = Array.Empty<string>();
		public IReadOnlyList<string> Referrers { get; internal set; } = Array.Empty<string>();
	}

	public sealed class FuturePinballManifestFile
	{
		public string Role { get; internal set; }
		public string Path { get; internal set; }
		public int Bytes { get; internal set; }
		public string Sha256 { get; internal set; }
	}

	internal static class FuturePinballManifestJson
	{
		public static string Serialize(FuturePinballExtractionManifest manifest)
		{
			var json = new StringBuilder(1024 * 32);
			json.Append("{\n");
			Property(json, 1, "schemaVersion", manifest.SchemaVersion, true);
			Property(json, 1, "sourceFileName", manifest.SourceFileName, true);
			Property(json, 1, "sourceBytes", manifest.SourceBytes, true);
			Property(json, 1, "sourceSha256", manifest.SourceSha256, true);
			Property(json, 1, "originalFile", manifest.OriginalFile, true);
			NullableProperty(json, 1, "fileVersion", manifest.FileVersion, true);
			Property(json, 1, "compoundEntryCount", manifest.CompoundEntryCount, true);
			Indent(json, 1).Append("\"counts\": ");
			WriteCounts(json, manifest.Counts);
			json.Append(",\n");
			Indent(json, 1).Append("\"streams\": [\n");
			for (var i = 0; i < manifest.Streams.Count; i++) {
				WriteStream(json, manifest.Streams[i], 2);
				json.Append(i + 1 == manifest.Streams.Count ? "\n" : ",\n");
			}
			Indent(json, 1).Append("],\n");
			Indent(json, 1).Append("\"resources\": [\n");
			for (var i = 0; i < manifest.Resources.Count; i++) {
				WriteResource(json, manifest.Resources[i], 2);
				json.Append(i + 1 == manifest.Resources.Count ? "\n" : ",\n");
			}
			Indent(json, 1).Append("],\n");
			Indent(json, 1).Append("\"issues\": ");
			WriteStrings(json, manifest.Issues);
			json.Append("\n}\n");
			return json.ToString();
		}

		public static string SerializeTableData(FuturePinballManifestStream tableData)
		{
			var json = new StringBuilder();
			WriteStream(json, tableData, 0);
			return json.Append('\n').ToString();
		}

		public static string SerializeElements(IReadOnlyList<FuturePinballManifestStream> elements)
		{
			var json = new StringBuilder("[\n");
			for (var i = 0; i < elements.Count; i++) {
				WriteStream(json, elements[i], 1);
				json.Append(i + 1 == elements.Count ? "\n" : ",\n");
			}
			return json.Append("]\n").ToString();
		}

		private static void WriteCounts(StringBuilder json, FuturePinballManifestCounts counts)
		{
			json.Append("{ ");
			InlineProperty(json, "elements", counts.Elements, true);
			InlineProperty(json, "images", counts.Images, true);
			InlineProperty(json, "sounds", counts.Sounds, true);
			InlineProperty(json, "music", counts.Music, true);
			InlineProperty(json, "pinModels", counts.PinModels, true);
			InlineProperty(json, "dmdFonts", counts.DmdFonts, true);
			InlineProperty(json, "imageLists", counts.ImageLists, true);
			InlineProperty(json, "lightLists", counts.LightLists, true);
			InlineProperty(json, "opaqueRecords", counts.OpaqueRecords, true);
			InlineProperty(json, "unresolvedLinkedResources", counts.UnresolvedLinkedResources, false);
			json.Append(" }");
		}

		private static void WriteStream(StringBuilder json, FuturePinballManifestStream stream, int indent)
		{
			Indent(json, indent).Append("{\n");
			Property(json, indent + 1, "name", stream.Name, true);
			Property(json, indent + 1, "kind", stream.Kind, true);
			NullableProperty(json, indent + 1, "sourceIndex", stream.SourceIndex, true);
			NullableProperty(json, indent + 1, "elementTypeId", stream.ElementTypeId, true);
			Property(json, indent + 1, "elementType", stream.ElementType, true);
			Property(json, indent + 1, "bytes", stream.Bytes, true);
			Property(json, indent + 1, "sha256", stream.Sha256, true);
			Property(json, indent + 1, "rawFile", stream.RawFile, true);
			Indent(json, indent + 1).Append("\"records\": [\n");
			for (var i = 0; i < stream.Records.Count; i++) {
				WriteRecord(json, stream.Records[i], indent + 2);
				json.Append(i + 1 == stream.Records.Count ? "\n" : ",\n");
			}
			Indent(json, indent + 1).Append("]\n");
			Indent(json, indent).Append('}');
		}

		private static void WriteRecord(StringBuilder json, FuturePinballManifestRecord record, int indent)
		{
			Indent(json, indent).Append("{ ");
			InlineProperty(json, "offset", record.Offset, true);
			InlineProperty(json, "storedLength", record.StoredLength, true);
			InlineProperty(json, "consumedLength", record.ConsumedLength, true);
			InlineProperty(json, "originalTag", record.OriginalTag, true);
			InlineProperty(json, "canonicalTag", record.CanonicalTag, true);
			InlineProperty(json, "name", record.Name, true);
			InlineProperty(json, "valueKind", record.ValueKind, true);
			InlineProperty(json, "value", record.Value, true);
			InlineProperty(json, "payloadBytes", record.PayloadBytes, true);
			InlineProperty(json, "payloadSha256", record.PayloadSha256, true);
			InlineProperty(json, "opaqueFile", record.OpaqueFile, false);
			json.Append(" }");
		}

		private static void WriteResource(StringBuilder json, FuturePinballManifestResource resource, int indent)
		{
			Indent(json, indent).Append("{\n");
			Property(json, indent + 1, "category", resource.Category, true);
			NullableProperty(json, indent + 1, "sourceIndex", resource.SourceIndex, true);
			Property(json, indent + 1, "sourceStream", resource.SourceStream, true);
			Property(json, indent + 1, "logicalName", resource.LogicalName, true);
			NullableProperty(json, indent + 1, "declaredType", resource.DeclaredType, true);
			Property(json, indent + 1, "linked", resource.Linked, true);
			Property(json, indent + 1, "linkedPath", resource.LinkedPath, true);
			Property(json, indent + 1, "resolutionStatus", resource.ResolutionStatus, true);
			Property(json, indent + 1, "resolvedLibrary", resource.ResolvedLibrary, true);
			Property(json, indent + 1, "resolvedLibraryEntry", resource.ResolvedLibraryEntry, true);
			Property(json, indent + 1, "detectedFormat", resource.DetectedFormat, true);
			Indent(json, indent + 1).Append("\"files\": [\n");
			for (var i = 0; i < resource.Files.Count; i++) {
				var file = resource.Files[i];
				Indent(json, indent + 2).Append("{ ");
				InlineProperty(json, "role", file.Role, true);
				InlineProperty(json, "path", file.Path, true);
				InlineProperty(json, "bytes", file.Bytes, true);
				InlineProperty(json, "sha256", file.Sha256, false);
				json.Append(i + 1 == resource.Files.Count ? " }\n" : " },\n");
			}
			Indent(json, indent + 1).Append("],\n");
			Indent(json, indent + 1).Append("\"items\": ");
			WriteStrings(json, resource.Items);
			json.Append(",\n");
			Indent(json, indent + 1).Append("\"referrers\": ");
			WriteStrings(json, resource.Referrers);
			json.Append('\n');
			Indent(json, indent).Append('}');
		}

		private static void WriteStrings(StringBuilder json, IReadOnlyList<string> values)
		{
			json.Append('[');
			for (var i = 0; i < values.Count; i++) {
				if (i > 0) json.Append(", ");
				String(json, values[i]);
			}
			json.Append(']');
		}

		private static void Property(StringBuilder json, int indent, string name, string value, bool comma)
		{
			Indent(json, indent);
			String(json, name).Append(": ");
			String(json, value);
			json.Append(comma ? ",\n" : "\n");
		}

		private static void Property(StringBuilder json, int indent, string name, long value, bool comma)
		{
			Indent(json, indent);
			String(json, name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture));
			json.Append(comma ? ",\n" : "\n");
		}

		private static void Property(StringBuilder json, int indent, string name, bool value, bool comma)
		{
			Indent(json, indent);
			String(json, name).Append(": ").Append(value ? "true" : "false");
			json.Append(comma ? ",\n" : "\n");
		}

		private static void NullableProperty<T>(StringBuilder json, int indent, string name, T? value, bool comma) where T : struct
		{
			Indent(json, indent);
			String(json, name).Append(": ");
			json.Append(value.HasValue ? Convert.ToString(value.Value, CultureInfo.InvariantCulture) : "null");
			json.Append(comma ? ",\n" : "\n");
		}

		private static void InlineProperty(StringBuilder json, string name, string value, bool comma)
		{
			String(json, name).Append(": ");
			String(json, value);
			if (comma) json.Append(", ");
		}

		private static void InlineProperty(StringBuilder json, string name, long value, bool comma)
		{
			String(json, name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture));
			if (comma) json.Append(", ");
		}

		private static StringBuilder String(StringBuilder json, string value)
		{
			if (value == null) return json.Append("null");
			json.Append('"');
			foreach (var character in value) {
				switch (character) {
					case '"': json.Append("\\\""); break;
					case '\\': json.Append("\\\\"); break;
					case '\b': json.Append("\\b"); break;
					case '\f': json.Append("\\f"); break;
					case '\n': json.Append("\\n"); break;
					case '\r': json.Append("\\r"); break;
					case '\t': json.Append("\\t"); break;
					default:
						if (character < 0x20) json.Append("\\u").Append(((int)character).ToString("x4"));
						else json.Append(character);
						break;
				}
			}
			return json.Append('"');
		}

		private static StringBuilder Indent(StringBuilder json, int indent)
		{
			return json.Append(' ', indent * 2);
		}
	}
}
