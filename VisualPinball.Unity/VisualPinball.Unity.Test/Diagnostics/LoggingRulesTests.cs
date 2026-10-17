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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Enforces the logging rules of VpeLog's header on the runtime code of the VPE packages
	/// that follow them, when this project has them as local (file:) packages.
	/// </summary>
	public class LoggingRulesTests
	{
		/// <summary>
		/// Packages whose runtime code has been brought in line with the rules.
		/// </summary>
		private static readonly string[] Packages = {
			"org.visualpinball.engine.unity",
			"org.visualpinball.engine.unity.hdrp",
			"org.visualpinball.engine.pinmame",
			"org.visualpinball.engine.kiki",
			"org.visualpinball.engine.missionpinball",
			"org.visualpinball.engine.proc",
			"org.visualpinball.engine.dmd",
			"org.visualpinball.engine.pinup",
			"org.visualpinball.engine.b2sbackglass",
			"org.visualpinball.engine.player",
		};

		/// <summary>
		/// A comment on a line that breaks a rule on purpose, e.g. logging that logging failed.
		/// </summary>
		private const string AllowMarker = "logging-rules: allow";

		private static readonly Regex UnityLogCall = new(@"\bDebug\s*\.\s*Log(Warning|Error|Exception|Format|Assertion)?\s*\(");
		private static readonly Regex LoggerCall = new(@"\b(Logger|_logger|logger)\s*\.\s*(Trace|Debug|Info|Warn|Error|Fatal)\s*\(");
		private static readonly Regex ExceptionInInterpolation = new(@"\$@?""(?:[^""\\]|\\.)*?\{\s*(e|ex|exception|error|err)(\.Message|\.ToString\(\))?\s*\}");
		private static readonly Regex ExceptionMessage = new(@"\b(e|ex|exception|error|err)\s*\.\s*(Message|ToString\s*\(\s*\))");

		/// <summary>
		/// What a rule looks at: one call, with the logger level and the full argument text.
		/// </summary>
		internal readonly struct Call
		{
			public readonly string Level;
			public readonly string Arguments;

			public Call(string level, string arguments)
			{
				Level = level;
				Arguments = arguments;
			}
		}

		internal static bool IsUnityLog(Call call) => call.Level == "Unity";

		internal static bool IsInterpolatedError(Call call) =>
			(call.Level == "Error" || call.Level == "Fatal") && Regex.IsMatch(call.Arguments, @"\$@?""|@\$""");

		internal static bool IsStringifiedException(Call call) =>
			call.Level != "Unity" && (ExceptionInInterpolation.IsMatch(call.Arguments) || ExceptionMessage.IsMatch(call.Arguments));

		[Test]
		public void ShouldNotUseUnityLoggingInRuntimeCode()
		{
			Violations(IsUnityLog).Should().BeEmpty("runtime code logs through NLog, UnityEngine.Debug.Log* only inside #if UNITY_EDITOR");
		}

		[Test]
		public void ShouldUseTemplatesForErrors()
		{
			Violations(IsInterpolatedError).Should().BeEmpty("errors are grouped by their message template, so Error and Fatal never take an interpolated string");
		}

		[Test]
		public void ShouldPassExceptionsAsExceptions()
		{
			Violations(IsStringifiedException).Should().BeEmpty("exceptions are passed as the exception argument, so their stack trace is kept");
		}

		[Test]
		public void ShouldSkipEditorOnlyAndCommentedCode()
		{
			var source = string.Join("\n",
				"// Debug.Log(\"in a comment\");",
				"/* Debug.Log(\"in a block",
				"   comment\"); */",
				"#if UNITY_EDITOR",
				"Debug.Log(\"editor only\");",
				"#else",
				"Debug.Log(\"player\");",
				"#endif",
				"#if !UNITY_EDITOR",
				"Debug.LogWarning(\"player only\");",
				"#endif",
				"#if !UNITY_EDITOR && FEATURE",
				"x();",
				"#else",
				"Debug.Log(\"editor, or a player without the feature\");",
				"#endif",
				"Debug.Log(\"allowed\"); // logging-rules: allow");

			Scan(source, IsUnityLog).Select(v => v.line).Should().Equal(7, 10, 15);
		}

		[Test]
		public void ShouldFindViolationsAcrossLines()
		{
			var source = string.Join("\n",
				"Logger.Error(",
				"	$\"Cannot load {path}\");",
				"Logger.Error(e, \"template {X}\", x);",
				"Logger.Warn(ex.Message);",
				"Logger.Info($\"failed: {ex}\");",
				"Logger.Warn(e, \"Failed reading {File}\", file);",
				"Logger.Error(e,",
				"	\"Cannot do {Thing} because of {Reason}\",",
				"	thing, ex.Message);",
				"Logger.Warn($\"Failed reading \\\"{path}\\\": {ex}\");",
				"Logger.Warn(\"Invalid {Delimiter}: {Reason}\", ')', ex.Message);");

			Scan(source, IsInterpolatedError).Select(v => v.line).Should().Equal(1);
			Scan(source, IsStringifiedException).Select(v => v.line).Should().Equal(4, 5, 7, 10, 11);
		}

		private static List<string> Violations(Func<Call, bool> rule)
		{
			var violations = new List<string>();
			foreach (var root in PackageRoots()) {
				foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
					if (!IsRuntimeFile(root, file)) {
						continue;
					}
					foreach (var (line, text) in Scan(File.ReadAllText(file), rule)) {
						violations.Add($"{file.Substring(root.Length).TrimStart('\\', '/')}:{line}: {text.Trim()}");
					}
				}
			}
			return violations;
		}

		internal static IEnumerable<string> PackageRoots()
		{
			return PackageInfo.GetAllRegisteredPackages()
				.Where(p => Packages.Contains(p.name) && p.source == PackageSource.Local)
				.Select(p => p.resolvedPath);
		}

		internal static bool IsRuntimeFile(string root, string file)
		{
			var parts = file.Substring(root.Length).Split('\\', '/');
			return !parts.Any(p =>
				p.EndsWith("~", StringComparison.Ordinal)
				|| p.StartsWith(".", StringComparison.Ordinal)
				|| p.Equals("Editor", StringComparison.OrdinalIgnoreCase)
				|| p.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase)
				|| p.Equals("Test", StringComparison.OrdinalIgnoreCase)
				|| p.Equals("Tests", StringComparison.OrdinalIgnoreCase)
				|| p.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
				|| p.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
				|| p.Equals("Samples", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// The calls breaking the rule, with the line they start on, outside comments and
		/// outside code compiled only in the editor. Calls may span several lines.
		/// </summary>
		internal static IEnumerable<(int line, string text)> Scan(string source, Func<Call, bool> rule)
		{
			var (code, lineStarts, rawLines) = Preprocess(source);
			var found = new List<(int line, string text)>();
			foreach (var (regex, isUnity) in new[] { (UnityLogCall, true), (LoggerCall, false) }) {
				foreach (Match match in regex.Matches(code)) {
					var arguments = Arguments(code, match.Index + match.Length);
					var call = new Call(isUnity ? "Unity" : match.Groups[2].Value, arguments);
					if (!rule(call)) {
						continue;
					}
					var line = LineOf(lineStarts, match.Index);
					if (rawLines[line - 1].Contains(AllowMarker)) {
						continue;
					}
					found.Add((line, rawLines[line - 1]));
				}
			}
			return found.OrderBy(f => f.line);
		}

		/// <summary>
		/// The source with comments, editor-only code and preprocessor lines blanked out, line
		/// by line, so positions still map to the original lines.
		/// </summary>
		private static (string code, List<int> lineStarts, string[] rawLines) Preprocess(string source)
		{
			// per #if level: 1 = editor only, -1 = player only, 0 = unknown
			var regions = new Stack<int>();
			var inBlockComment = false;
			var rawLines = source.Replace("\r\n", "\n").Split('\n');
			var sb = new StringBuilder();
			var lineStarts = new List<int>();
			foreach (var line in rawLines) {
				lineStarts.Add(sb.Length);
				var trimmed = line.Trim();
				if (trimmed.StartsWith("#if")) {
					regions.Push(Region(trimmed.Substring(3)));
				} else if (trimmed.StartsWith("#elif")) {
					if (regions.Count > 0) {
						regions.Pop();
					}
					regions.Push(Region(trimmed.Substring(5)));
				} else if (trimmed.StartsWith("#else")) {
					if (regions.Count > 0) {
						// the else of an editor-only block is player code and vice versa
						regions.Push(-regions.Pop());
					}
				} else if (trimmed.StartsWith("#endif")) {
					if (regions.Count > 0) {
						regions.Pop();
					}
				} else {
					var code = StripComments(line, ref inBlockComment);
					if (!regions.Contains(1)) {
						sb.Append(code);
					}
				}
				sb.Append('\n');
			}
			return (sb.ToString(), lineStarts, rawLines);
		}

		/// <summary>
		/// The text from after an opening parenthesis to its match, ignoring parentheses in strings.
		/// </summary>
		private static string Arguments(string code, int start)
		{
			var depth = 1;
			var inString = false;
			var i = start;
			for (; i < code.Length && depth > 0; i++) {
				var c = code[i];
				if (!inString && c == '\'' && i + 2 < code.Length) {
					// a character literal like ')' or '\'' must not count as a parenthesis
					var end = code.IndexOf('\'', i + (code[i + 1] == '\\' ? 3 : 2));
					if (end > i) {
						i = end;
						continue;
					}
				}
				if (c == '"' && code[i - 1] != '\\') {
					inString = !inString;
				} else if (!inString && c == '(') {
					depth++;
				} else if (!inString && c == ')') {
					depth--;
				}
			}
			return code.Substring(start, System.Math.Max(0, i - 1 - start));
		}

		private static int LineOf(List<int> lineStarts, int index)
		{
			var line = lineStarts.BinarySearch(index);
			return line >= 0 ? line + 1 : ~line;
		}

		private static int Region(string condition)
		{
			if (condition.Contains("||")) {
				return 0;
			}
			// "#if UNITY_EDITOR", "#if UNITY_EDITOR && FOO": editor only. Its #else becomes
			// player code, which gets scanned, the safe side for an unknown branch too.
			if (Regex.IsMatch(condition, @"^\s*\(?\s*UNITY_EDITOR\b")) {
				return 1;
			}
			// "#if !UNITY_EDITOR" alone: player only, its #else editor only. With more
			// conditions, the #else may run in a player too, so it stays unknown.
			if (Regex.IsMatch(condition, @"^\s*!\s*UNITY_EDITOR\s*$")) {
				return -1;
			}
			return 0;
		}

		private static string StripComments(string line, ref bool inBlockComment)
		{
			var result = new StringBuilder();
			var inString = false;
			for (var i = 0; i < line.Length; i++) {
				if (inBlockComment) {
					if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '/') {
						inBlockComment = false;
						i++;
					}
					continue;
				}
				if (!inString && line[i] == '/' && i + 1 < line.Length) {
					if (line[i + 1] == '/') {
						break;
					}
					if (line[i + 1] == '*') {
						inBlockComment = true;
						i++;
						continue;
					}
				}
				if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) {
					inString = !inString;
				}
				result.Append(line[i]);
			}
			return result.ToString();
		}
	}
}
