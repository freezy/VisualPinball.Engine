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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The profile build's instrumentation compiles out of normal builds: every
	/// <see cref="VpeProfiler"/> call disappears, the recorders are inside <c>#if VPE_PROFILE</c>,
	/// and nothing sends performance data to Sentry outside of it.
	/// </summary>
	public class ProfileGatingTests
	{
		private const string Define = "VPE_PROFILE";

		/// <summary>
		/// Files that declare the API, which has to exist in every build.
		/// </summary>
		private static readonly string[] ApiFiles = { "VpeProfiler.cs", "ProfileSection.cs" };

		private static readonly Regex PerfSentryCall = new(@"\bSentrySdk\s*\.\s*(Metrics|Logger)\b|\b(RecordTransaction|StartTransaction)\s*\(");

		[Test]
		public void EveryProfilerMethodCompilesOut()
		{
			var methods = typeof(VpeProfiler).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(m => !m.IsSpecialName) // property getters
				.ToList();
			methods.Should().NotBeEmpty();
			foreach (var method in methods) {
				var conditional = method.GetCustomAttributes<ConditionalAttribute>().Select(a => a.ConditionString);
				conditional.Should().Contain(Define, $"{method.Name} must compile out of normal builds");
				method.ReturnType.Should().Be(typeof(void), $"{method.Name} must return nothing to be conditional");
				method.GetParameters().Should().NotContain(p => p.IsOut || p.ParameterType.IsByRef, $"{method.Name} must have no out or ref parameters");
			}
		}

		[Test]
		public void TheRecordersAreInsideTheDefine()
		{
			var violations = new List<string>();
			foreach (var root in LoggingRulesTests.PackageRoots()) {
				foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
					if (!LoggingRulesTests.IsRuntimeFile(root, file) || !IsProfilingFile(file) || ApiFiles.Contains(Path.GetFileName(file))) {
						continue;
					}
					if (!IsWhollyGated(File.ReadAllLines(file))) {
						violations.Add(file.Substring(root.Length));
					}
				}
			}
			violations.Should().BeEmpty($"profiling code lives in files wrapped in #if {Define}");
		}

		[Test]
		public void NoPerformanceDataGoesToSentryOutsideTheDefine()
		{
			var violations = new List<string>();
			foreach (var root in LoggingRulesTests.PackageRoots()) {
				foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
					if (!LoggingRulesTests.IsRuntimeFile(root, file)) {
						continue;
					}
					foreach (var line in UngatedLines(File.ReadAllLines(file))) {
						violations.Add($"{file.Substring(root.Length)}:{line}");
					}
				}
			}
			violations.Should().BeEmpty($"metrics, logs and transactions only go to Sentry from profile builds (#if {Define})");
		}

		[Test]
		public void TheEngineHasNoCallsToTheProfiler()
		{
			if (VpeProfiler.IsProfileBuild) {
				Assert.Ignore($"{Define} is defined in this project, so the calls are meant to be there.");
			}
			var profiler = typeof(VpeProfiler);
			var assemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.GetName().Name.StartsWith("VisualPinball.", StringComparison.Ordinal) && !a.GetName().Name.EndsWith(".Test", StringComparison.Ordinal))
				.ToList();
			assemblies.Should().Contain(profiler.Assembly);

			var calls = new List<string>();
			foreach (var assembly in assemblies) {
				calls.AddRange(CallsTo(assembly, profiler));
			}
			calls.Should().BeEmpty("[Conditional] removes every call when the define is missing");
		}

		[Test]
		public void TheScannerFindsCalls()
		{
			// a method of this test calling a method of a type we name: the scanner must see it
			CallsTo(typeof(ProfileGatingTests).Assembly, typeof(Probe)).Should().ContainSingle(c => c.Contains(nameof(CallsTheProbe)));
		}

		[TestCase(new[] { "// header", "", "#if VPE_PROFILE", "class A {}", "#endif" }, true)]
		[TestCase(new[] { "#if VPE_PROFILE && UNITY_EDITOR", "class A {}", "#endif", "" }, true)]
		[TestCase(new[] { "using System;", "#if VPE_PROFILE", "class A {}", "#endif" }, false)]
		[TestCase(new[] { "#if VPE_PROFILE", "class A {}", "#endif", "class B {}" }, false)]
		[TestCase(new[] { "#if !VPE_PROFILE", "class A {}", "#endif" }, false)]
		public void ShouldTellAGatedFile(string[] lines, bool gated)
		{
			IsWhollyGated(lines).Should().Be(gated);
		}

		[Test]
		public void ShouldFindUngatedSentryCalls()
		{
			var lines = new[] {
				"SentrySdk.Logger.LogInfo(\"a\");",
				"#if VPE_PROFILE",
				"SentrySdk.Metrics.Gauge(\"b\", 1);",
				"#else",
				"SentrySdk.RecordTransaction(t);",
				"#endif",
				"// SentrySdk.Logger in a comment",
				"#if !VPE_PROFILE",
				"x.StartTransaction(\"c\");",
				"#endif",
			};
			UngatedLines(lines).Should().Equal(1, 5, 9);
		}

		private static bool IsProfilingFile(string file)
		{
			var parts = file.Split('\\', '/');
			return parts.Any(p => p.Equals("Profiling", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// The first and last code lines are an <c>#if VPE_PROFILE</c> and its <c>#endif</c>.
		/// </summary>
		internal static bool IsWhollyGated(string[] lines)
		{
			var code = lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal)).ToList();
			if (code.Count < 2 || !Regex.IsMatch(code[0], $@"^#if\s+\(?\s*{Define}\b") || code[code.Count - 1] != "#endif") {
				return false;
			}
			// the first #if must close on the last line
			var depth = 0;
			for (var i = 0; i < code.Count; i++) {
				if (code[i].StartsWith("#if", StringComparison.Ordinal)) {
					depth++;
				} else if (code[i].StartsWith("#endif", StringComparison.Ordinal)) {
					depth--;
					if (depth == 0 && i < code.Count - 1) {
						return false;
					}
				}
			}
			return depth == 0;
		}

		/// <summary>
		/// The 1-based lines with a Sentry performance call that a normal build compiles.
		/// </summary>
		internal static IEnumerable<int> UngatedLines(string[] lines)
		{
			// per #if level: 1 = only with the define, -1 = only without it, 0 = either
			var regions = new Stack<int>();
			for (var i = 0; i < lines.Length; i++) {
				var trimmed = lines[i].Trim();
				if (trimmed.StartsWith("#if", StringComparison.Ordinal)) {
					regions.Push(Region(trimmed.Substring(3)));
				} else if (trimmed.StartsWith("#elif", StringComparison.Ordinal)) {
					if (regions.Count > 0) {
						regions.Pop();
					}
					regions.Push(0);
				} else if (trimmed.StartsWith("#else", StringComparison.Ordinal)) {
					if (regions.Count > 0) {
						regions.Push(-regions.Pop());
					}
				} else if (trimmed.StartsWith("#endif", StringComparison.Ordinal)) {
					if (regions.Count > 0) {
						regions.Pop();
					}
				} else if (!regions.Contains(1)) {
					var comment = lines[i].IndexOf("//", StringComparison.Ordinal);
					var code = comment >= 0 ? lines[i].Substring(0, comment) : lines[i];
					if (PerfSentryCall.IsMatch(code)) {
						yield return i + 1;
					}
				}
			}
		}

		private static int Region(string condition)
		{
			if (condition.Contains("||")) {
				return 0;
			}
			if (Regex.IsMatch(condition, $@"^\s*\(?\s*{Define}\b")) {
				return 1;
			}
			return Regex.IsMatch(condition, $@"^\s*!\s*{Define}\s*$") ? -1 : 0;
		}

		#region IL

		private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Select(f => (OpCode)f.GetValue(null))
			.GroupBy(o => o.Value)
			.ToDictionary(g => g.Key, g => g.First());

		/// <summary>
		/// Every method of the assembly that calls a method of the given type (of
		/// <see cref="VpeProfiler"/>: one of its conditional methods).
		/// </summary>
		private static IEnumerable<string> CallsTo(Assembly assembly, Type target)
		{
			Type[] types;
			try {
				types = assembly.GetTypes();
			} catch (ReflectionTypeLoadException e) {
				types = e.Types.Where(t => t != null).ToArray();
			}
			const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			foreach (var type in types) {
				if (type == target) {
					continue;
				}
				IEnumerable<MethodBase> methods;
				try {
					methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
				} catch (Exception) {
					continue;
				}
				foreach (var method in methods) {
					if (CallsType(method, target)) {
						yield return $"{assembly.GetName().Name}: {type.FullName}.{method.Name}";
					}
				}
			}
		}

		private static bool CallsType(MethodBase method, Type target)
		{
			byte[] il;
			try {
				il = method.GetMethodBody()?.GetILAsByteArray();
			} catch (Exception) {
				return false;
			}
			if (il == null) {
				return false;
			}
			var i = 0;
			while (i < il.Length) {
				short value = il[i++];
				if (value == 0xFE && i < il.Length) {
					value = (short)(0xFE00 | il[i++]);
				}
				if (!OpCodesByValue.TryGetValue(value, out var op)) {
					return false; // not something we can read, don't guess
				}
				if (op.OperandType == OperandType.InlineMethod) {
					var token = BitConverter.ToInt32(il, i);
					try {
						var callee = method.Module.ResolveMethod(token,
							method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null,
							method.IsGenericMethod ? method.GetGenericArguments() : null);
						// the conditional methods, not e.g. a property that only says whether it's a profile build
						if (callee?.DeclaringType == target && (target != typeof(VpeProfiler) || callee.IsDefined(typeof(ConditionalAttribute), false))) {
							return true;
						}
					} catch (Exception) {
						// a member of an assembly that isn't loaded: not ours
					}
				}
				i += OperandSize(op, il, i);
			}
			return false;
		}

		private static int OperandSize(OpCode op, byte[] il, int at)
		{
			switch (op.OperandType) {
				case OperandType.InlineNone:
					return 0;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					return 1;
				case OperandType.InlineVar:
					return 2;
				case OperandType.InlineI8:
				case OperandType.InlineR:
					return 8;
				case OperandType.InlineSwitch:
					return 4 + BitConverter.ToInt32(il, at) * 4;
				default:
					return 4;
			}
		}

		private static class Probe
		{
			public static void Touch() { }
		}

		private static void CallsTheProbe() => Probe.Touch();

		#endregion
	}
}
