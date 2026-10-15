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
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Unity.Test
{
	public class DiagnosticsTests
	{
		[TearDown]
		public void RestoreRedaction()
		{
			Redaction.Configure(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName, Environment.OSVersion.Platform == PlatformID.Win32NT);
		}

		[TestCase(@"C:\Users\jane\Documents\VPE Tables\t2.vpe", @"%USERPROFILE%\Documents\VPE Tables\t2.vpe")]
		[TestCase(@"c:\users\JANE\AppData\Local\x", @"%USERPROFILE%\AppData\Local\x")]
		[TestCase(@"C:/Users/jane/roms", @"%USERPROFILE%/roms")]
		[TestCase(@"D:\Users\jane\roms", @"D:\Users\<user>\roms")]
		[TestCase(@"C:\Users\janet\roms", @"C:\Users\janet\roms")]
		[TestCase(@"E:\Pinball\jane\roms", @"E:\Pinball\jane\roms")]
		[TestCase("ROM at \"C:\\Users\\jane\" missing", "ROM at \"%USERPROFILE%\" missing")]
		public void ShouldRedactTheWindowsHomeFolderAndUserName(string input, string expected)
		{
			Redaction.Configure(@"C:\Users\jane", "jane", windows: true);

			Redaction.Redact(input).Should().Be(expected);
		}

		[Test]
		public void ShouldRedactPathsInsideJsonStrings()
		{
			Redaction.Configure(@"C:\Users\jane", "jane", windows: true);
			const string json = "{\n\t\"tableFolder\": \"C:\\\\Users\\\\jane\\\\Documents\\\\Tables\",\n\t\"romFolder\": \"D:\\\\Users\\\\jane\\\\roms\",\n\t\"note\": \"say \\\"hi\\\"\",\n\t\"count\": 3\n}";

			var redacted = Redaction.RedactJson(json);

			redacted.Should().Be("{\n\t\"tableFolder\": \"%USERPROFILE%\\\\Documents\\\\Tables\",\n\t\"romFolder\": \"D:\\\\Users\\\\<user>\\\\roms\",\n\t\"note\": \"say \\\"hi\\\"\",\n\t\"count\": 3\n}");
			UnityEngine.JsonUtility.FromJson<Folders>(redacted).tableFolder.Should().Be(@"%USERPROFILE%\Documents\Tables");
		}

		[Serializable]
		private class Folders
		{
			public string tableFolder;
		}

		[TestCase("/home/jane/.local/share/vpe/logs", "~/.local/share/vpe/logs")]
		[TestCase("/mnt/backup/home/jane/roms", "/mnt/backup/home/<user>/roms")]
		[TestCase("/Users/jane/Library", "/Users/<user>/Library")]
		public void ShouldRedactTheUnixHomeFolderAndUserName(string input, string expected)
		{
			Redaction.Configure("/home/jane", "jane", windows: false);

			Redaction.Redact(input).Should().Be(expected);
		}

		[TestCase("VniKey", true)]
		[TestCase("api_key", true)]
		[TestCase("authToken", true)]
		[TestCase("Password", true)]
		[TestCase("keyBindings", false)]
		[TestCase("romFolder", false)]
		public void ShouldRecognizeSecretSettings(string key, bool secret)
		{
			DiagnosticsWriter.IsSecret(key).Should().Be(secret);
		}

		[Test]
		public void ShouldRedactSecretsOnTheCommandLine()
		{
			var args = new[] { "-screen-width", "1920", "-accessToken", "abc", "-api-key=xyz", "-vpe-log-level", "debug" };

			SystemSpecs.CommandLine(args, editor: false).Should().Be("-screen-width 1920 -accessToken <redacted> -api-key=<redacted> -vpe-log-level debug");
			SystemSpecs.CommandLine(args, editor: true).Should().Be("-vpe-log-level debug");
		}

		[Test]
		public void ShouldNeverWriteTheValueOfASecret()
		{
			var writer = new DiagnosticsWriter()
				.AddSetting("VniKey", "abc123")
				.AddSetting("ApiKey", "")
				.AddSetting("Folder", @"C:\dmd");

			writer.Entries.Select(e => e.Value).Should().Equal("<set>", "<empty>", @"C:\dmd");
		}

		[Test]
		public void ShouldKeepSectionsInAStableOrder()
		{
			DiagnosticsContext.Clear();
			try {
				DiagnosticsContext.Set("Zeta", new DiagnosticsWriter().Add("a", "1"), log: false);
				DiagnosticsContext.Set(DiagnosticsContext.Table, new DiagnosticsWriter().Add("title", "T2"), log: false);
				DiagnosticsContext.Set(DiagnosticsContext.System, new DiagnosticsWriter().Add("os", "x"), log: false);
				DiagnosticsContext.Set(DiagnosticsContext.Table, new DiagnosticsWriter().Add("title", "T3"), log: false);

				DiagnosticsContext.Sections.Select(s => s.Name).Should().Equal(DiagnosticsContext.System, DiagnosticsContext.Table, "Zeta");
				DiagnosticsContext.Get(DiagnosticsContext.Table).Get("title").Should().Be("T3");

			} finally {
				DiagnosticsContext.Clear();
			}
		}

		[Test]
		public void ShouldWriteValidJson()
		{
			var sections = new[] {
				new DiagnosticsContext.Section("Table", new DiagnosticsWriter()
					.Add("title", "Quote \" and backslash \\ and\nnewline")
					.Entries),
				new DiagnosticsContext.Section("Empty", Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>()),
			};

			var json = DiagnosticsContext.ToJson(sections);

			json.Should().Contain(@"""title"": ""Quote \"" and backslash \\ and\nnewline""");
			json.Should().Contain("\"Empty\": {}");
		}

		[Test]
		public void ShouldRedactValuesBeforeEscapingThem()
		{
			Redaction.Configure(@"C:\Users\jane", "jane", windows: true);
			var sections = new[] {
				new DiagnosticsContext.Section("Table", new DiagnosticsWriter().Add("path", @"C:\Users\jane\t2.vpe").Entries),
			};

			DiagnosticsContext.ToJson(sections, Redaction.Redact).Should().Contain(@"""path"": ""%USERPROFILE%\\t2.vpe""");
		}

		[Test]
		public void ShouldStayFailedWhenTheTableStartsAnyway()
		{
			// a file that doesn't exist: no fingerprint worker holding it open
			var file = Path.Combine(Path.GetTempPath(), "vpe-missing-" + Guid.NewGuid().ToString("N") + ".vpe");
			try {
				var attempt = LoadAttempt.Begin(file, "Test");
				attempt.SetPhase(LoadAttempt.PhasePlugins);

				attempt.Fail(new InvalidOperationException("boom"));
				attempt.Succeed();

				attempt.Phase.Should().Be(LoadAttempt.PhaseFailed);
				attempt.FailedPhase.Should().Be(LoadAttempt.PhasePlugins);

			} finally {
				LoadAttempt.End();
			}
		}

		[Test]
		public void ShouldFingerprintSmallAndLargeFiles()
		{
			var small = Path.GetTempFileName();
			var large = Path.GetTempFileName();
			try {
				File.WriteAllBytes(small, new byte[] { 1, 2, 3 });
				var bytes = new byte[3 << 20];
				new Random(42).NextBytes(bytes);
				File.WriteAllBytes(large, bytes);

				var a = LoadAttempt.ComputeFingerprint(small);
				var b = LoadAttempt.ComputeFingerprint(large);

				a.Should().HaveLength(12).And.NotBe(b);
				LoadAttempt.ComputeFingerprint(large).Should().Be(b);

			} finally {
				File.Delete(small);
				File.Delete(large);
			}
		}
	}
}
