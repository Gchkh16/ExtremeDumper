using System;
using System.Drawing;

namespace ExtremeDumper.Helper {
	internal static class Cache {
		public static readonly bool Is64BitProcess = IntPtr.Size == 8;

		public static readonly Color DotNetColor = Color.YellowGreen;
	}
}
