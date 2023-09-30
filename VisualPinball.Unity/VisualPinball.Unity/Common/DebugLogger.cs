using System;
using System.IO;

namespace VisualPinball.Unity
{
	public static class DebugLogger
	{
		private const string FileName = @"C:\Temp\physdebug\vpe-2022.log";

		public static void ClearLog()
		{
			if (File.Exists(FileName)) {
				File.Delete(FileName);
			}
		}

		public static void Log(string msg)
		{
			File.AppendAllText(FileName, msg + Environment.NewLine);
		}
	}
}
