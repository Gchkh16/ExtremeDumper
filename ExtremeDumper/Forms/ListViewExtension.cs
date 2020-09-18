using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ExtremeDumper_Lib.Helper.NativeMethods;

namespace ExtremeDumper.Forms {
	internal static class ListViewExtension {
		public static void AutoResizeColumns(this ListView listView, bool onlyLastColumn) {
			if (listView is null)
				throw new ArgumentNullException(nameof(listView));

			var scrollBarInfo = SCROLLBARINFO.Default;
			GetScrollBarInfo(listView.Handle, OBJID_VSCROLL, ref scrollBarInfo);
			int sumWidths = scrollBarInfo.dxyLineButton;
			if (onlyLastColumn) {
				foreach (ColumnHeader columnHeader in listView.Columns)
					sumWidths += columnHeader.Width;
				listView.Columns[listView.Columns.Count - 1].Width += listView.Width - sumWidths - 4;
			}
			else {
				int[] minWidths = new int[listView.Columns.Count];
				using (var g = listView.CreateGraphics()) {
					for (int i = 0; i < minWidths.Length; i++)
						minWidths[i] = (int)g.MeasureString(listView.Columns[i].Text, listView.Font).Width + 10;
				}
				listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
				for (int i = 0; i < minWidths.Length; i++) {
					if (listView.Columns[i].Width < minWidths[i])
						listView.Columns[i].Width = minWidths[i];
					sumWidths += listView.Columns[i].Width;
				}
				listView.Columns[minWidths.Length - 1].Width += listView.Width - sumWidths;
			}
		}

		public static ListViewItem.ListViewSubItem GetFirstSelectedSubItem(this ListView listView, int index) {
			if (listView is null)
				throw new ArgumentNullException(nameof(listView));

			return listView.SelectedItems[0].SubItems[index];
		}


		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct SCROLLBARINFO {
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(SCROLLBARINFO));
			public static SCROLLBARINFO Default = new SCROLLBARINFO { cbSize = UnmanagedSize };

			public uint cbSize;
			public Rectangle rcScrollBar;
			public int dxyLineButton;
			public int xyThumbTop;
			public int xyThumbBottom;
			public int reserved;
			public fixed uint rgstate[6];
		}


		[DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetScrollBarInfo", ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetScrollBarInfo(IntPtr hwnd, int idObject, ref SCROLLBARINFO psbi);

	}
}
