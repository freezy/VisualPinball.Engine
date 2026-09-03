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
using System.IO;
using System.Linq;
using System.Text;

using VisualPinball.Engine.IO.FuturePinball;

namespace VisualPinball.Unity.Editor
{
	public sealed class FptImportReport
	{
		public string SourceFile { get; internal set; }
		public string SourceSha256 { get; internal set; }
		public uint? FileVersion { get; internal set; }
		public int Elements { get; internal set; }
		public int ProceduralElements { get; internal set; }
		public int NativeElements { get; internal set; }
		public int ModelInstances { get; internal set; }
		public int Placeholders { get; internal set; }
		public int SkippedUnresolvedSupport { get; internal set; }
		public int MeshAssets { get; internal set; }
		public int MaterialAssets { get; internal set; }
		public int Colliders { get; internal set; }
		public int ReusedAssets { get; internal set; }
		public int UnresolvedResources { get; internal set; }
		public long ElapsedMilliseconds { get; internal set; }
		public List<string> Warnings { get; } = new List<string>();
		public List<FptRecreationBacklogItem> Backlog { get; } = new List<FptRecreationBacklogItem>();

		internal void Write(string outputDirectory, FuturePinballExtractionManifest manifest)
		{
			Directory.CreateDirectory(outputDirectory);
			File.WriteAllText(Path.Combine(outputDirectory, "import-report.json"), Json(manifest), new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(outputDirectory, "import-report.md"), Markdown(manifest), new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(outputDirectory, "recreation-backlog.json"), BacklogJson(), new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(outputDirectory, "recreation-backlog.md"), BacklogMarkdown(), new UTF8Encoding(false));
		}

		private string Json(FuturePinballExtractionManifest manifest)
		{
			var json = new StringBuilder();
			json.AppendLine("{");
			Property(json, "sourceFile", SourceFile, true);
			Property(json, "sourceSha256", SourceSha256, true);
			Property(json, "fileVersion", FileVersion?.ToString(CultureInfo.InvariantCulture) ?? "null", true, false);
			Property(json, "manifestSchemaVersion", manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "elements", Elements.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "proceduralElements", ProceduralElements.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "nativeElements", NativeElements.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "modelInstances", ModelInstances.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "placeholders", Placeholders.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "skippedUnresolvedSupport", SkippedUnresolvedSupport.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "meshAssets", MeshAssets.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "materialAssets", MaterialAssets.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "colliders", Colliders.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "reusedAssets", ReusedAssets.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "unresolvedResources", UnresolvedResources.ToString(CultureInfo.InvariantCulture), true, false);
			Property(json, "elapsedMilliseconds", ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), true, false);
			json.Append("  \"warnings\": [");
			json.Append(string.Join(", ", Warnings.Select(warning => $"\"{Escape(warning)}\"")));
			json.AppendLine("]");
			json.AppendLine("}");
			return json.ToString();
		}

		private string Markdown(FuturePinballExtractionManifest manifest)
		{
			var text = new StringBuilder();
			text.AppendLine("# Future Pinball Import Report").AppendLine();
			text.AppendLine($"- Source: `{SourceFile}`");
			text.AppendLine($"- SHA-256: `{SourceSha256}`");
			text.AppendLine($"- FPT version: `{FileVersion?.ToString() ?? "unknown"}`");
			text.AppendLine($"- Manifest schema: `{manifest.SchemaVersion}`");
			text.AppendLine($"- Elements: {Elements}");
			text.AppendLine($"- Recreation passes (overlapping): {NativeElements} native VPE counterparts, {ProceduralElements} procedural visuals, {ModelInstances} model instances, {Placeholders} placeholders");
			text.AppendLine($"- Skipped unresolved-support elements: {SkippedUnresolvedSupport}");
			text.AppendLine($"- Assets: {MeshAssets} meshes, {MaterialAssets} materials, {ReusedAssets} reused");
			text.AppendLine($"- VPE colliders: {Colliders}");
			text.AppendLine($"- Unresolved resources: {UnresolvedResources}");
			text.AppendLine($"- Elapsed: {ElapsedMilliseconds} ms");
			text.AppendLine().AppendLine("## Extracted media").AppendLine();
			foreach (var group in manifest.Resources.GroupBy(resource => resource.Category).OrderBy(group => group.Key)) {
				text.AppendLine($"- {group.Key}: {group.Count()} resource(s), {group.Sum(resource => resource.Files.Sum(file => (long)file.Bytes))} bytes");
			}
			if (Warnings.Count > 0) {
				text.AppendLine().AppendLine("## Warnings").AppendLine();
				foreach (var warning in Warnings) text.AppendLine($"- {warning}");
			}
			return text.ToString();
		}

		private string BacklogJson()
		{
			var json = new StringBuilder();
			json.AppendLine("{").AppendLine("  \"schemaVersion\": 1,").AppendLine("  \"items\": [");
			for (var i = 0; i < Backlog.Count; i++) {
				var item = Backlog[i];
				json.AppendLine("    {");
				json.AppendLine($"      \"sourceIndex\": {item.SourceIndex},");
				json.AppendLine($"      \"name\": \"{Escape(item.Name)}\",");
				json.AppendLine($"      \"elementType\": \"{Escape(item.ElementType)}\",");
				json.AppendLine($"      \"currentOutcome\": \"{Escape(item.CurrentOutcome)}\",");
				json.AppendLine($"      \"suggestedCapability\": \"{Escape(item.SuggestedCapability)}\",");
				json.AppendLine($"      \"sourceStream\": \"{Escape(item.SourceStream)}\"");
				json.Append("    }").AppendLine(i + 1 < Backlog.Count ? "," : string.Empty);
			}
			json.AppendLine("  ]").AppendLine("}");
			return json.ToString();
		}

		private string BacklogMarkdown()
		{
			var text = new StringBuilder();
			text.AppendLine("# Future Pinball Recreation Backlog").AppendLine();
			text.AppendLine("Every item below remains traceable to the lossless source bundle and embedded table script.").AppendLine();
			foreach (var item in Backlog.OrderBy(item => item.SourceIndex)) {
				text.AppendLine($"- **{item.Name}** (`{item.ElementType}`, `{item.SourceStream}`): {item.CurrentOutcome}. Suggested: {item.SuggestedCapability}.");
			}
			return text.ToString();
		}

		private static void Property(StringBuilder json, string name, string value, bool comma, bool quote = true)
		{
			json.Append("  \"").Append(Escape(name)).Append("\": ");
			json.Append(quote ? $"\"{Escape(value)}\"" : value);
			json.AppendLine(comma ? "," : string.Empty);
		}

		private static string Escape(string value)
		{
			var escaped = new StringBuilder();
			foreach (var character in value ?? string.Empty) {
				switch (character) {
					case '"': escaped.Append("\\\""); break;
					case '\\': escaped.Append("\\\\"); break;
					case '\b': escaped.Append("\\b"); break;
					case '\f': escaped.Append("\\f"); break;
					case '\n': escaped.Append("\\n"); break;
					case '\r': escaped.Append("\\r"); break;
					case '\t': escaped.Append("\\t"); break;
					default:
						if (character < 0x20) escaped.Append("\\u").Append(((int)character).ToString("x4"));
						else escaped.Append(character);
						break;
				}
			}
			return escaped.ToString();
		}
	}

	public sealed class FptRecreationBacklogItem
	{
		public int SourceIndex { get; internal set; }
		public string Name { get; internal set; }
		public string ElementType { get; internal set; }
		public string CurrentOutcome { get; internal set; }
		public string SuggestedCapability { get; internal set; }
		public string SourceStream { get; internal set; }
	}
}
