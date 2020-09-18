using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using ExtremeDumper.Helper;
using ExtremeDumper_Lib;
using ExtremeDumper_Lib.Dumping;
using Microsoft.Diagnostics.Runtime;
using NativeSharp;
using static ExtremeDumper_Lib.Helper.NativeMethods;
using ImageLayout = dnlib.PE.ImageLayout;

namespace ExtremeDumper.Forms
{
	internal unsafe partial class ModulesForm : Form
	{
		private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
		private readonly NativeProcess _process;
		private readonly ProcessInfo _processInfo;
		private readonly DumperType _dumperType;
		private readonly ResourceManager _resources = new ResourceManager(typeof(ModulesForm));

		public ModulesForm(ProcessInfo processInfo, DumperType dumperType)
		{
			InitializeComponent();
			_processInfo = processInfo;
			_process = NativeProcess.Open(processInfo.Id);
			if (_process == NativeProcess.InvalidProcess)
				throw new InvalidOperationException();
			_dumperType = dumperType;
			Text = $"{_resources.GetString("StrModules")} {processInfo.ModuleName}(ID={processInfo.Id})";
			typeof(ListView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, lvwModules, new object[] { true });
			lvwModules.ListViewItemSorter = new ListViewItemSorter(lvwModules, new List<TypeCode> { TypeCode.String, TypeCode.String, TypeCode.String, TypeCode.UInt64, TypeCode.Int32, TypeCode.String })
			{
				AllowHexLeading = true
			};
			RefreshModuleList();
		}

		#region Events
		private void lvwModules_Resize(object sender, EventArgs e)
		{
			lvwModules.AutoResizeColumns(true);
		}

		private void mnuDumpModule_Click(object sender, EventArgs e)
		{
			if (lvwModules.SelectedIndices.Count == 0)
				return;

			string filePath = EnsureValidFileName(lvwModules.GetFirstSelectedSubItem(chModuleName.Index).Text);
			if (filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
				filePath = PathInsertPostfix(filePath, ".dump");
			else
				filePath += ".dump.dll";
			sfdlgDumped.FileName = filePath;
			sfdlgDumped.InitialDirectory = Path.GetDirectoryName(_process.GetMainModule().ImagePath);
			if (sfdlgDumped.ShowDialog() != DialogResult.OK)
				return;
			var moduleHandle = (IntPtr)ulong.Parse(lvwModules.GetFirstSelectedSubItem(chModuleHandle.Index).Text.Substring(2), NumberStyles.HexNumber, null);
			DumpModule(moduleHandle, lvwModules.GetFirstSelectedSubItem(chModulePath.Index).Text == "InMemory" ? ImageLayout.File : ImageLayout.Memory, sfdlgDumped.FileName);
		}

		private void mnuRefreshModuleList_Click(object sender, EventArgs e)
		{
			RefreshModuleList();
		}

		private void mnuViewFunctions_Click(object sender, EventArgs e)
		{
			if (lvwModules.SelectedIndices.Count == 0)
				return;

			var functionsForm = new FunctionsForm(_process.UnsafeGetModule((void*)ulong.Parse(lvwModules.GetFirstSelectedSubItem(chModuleHandle.Index).Text.Substring(2), NumberStyles.HexNumber, null)));
			functionsForm.Show();
		}

		private void mnuOnlyDotNetModule_Click(object sender, EventArgs e)
		{
			RefreshModuleList();
		}

		private void mnuGotoLocation_Click(object sender, EventArgs e)
		{
			if (lvwModules.SelectedIndices.Count == 0)
				return;

			string filePath = lvwModules.GetFirstSelectedSubItem(chModulePath.Index).Text;
			if (filePath != "InMemory")
				Process.Start("explorer.exe", @"/select, " + filePath);
		}
		#endregion

		private void RefreshModuleList()
		{
			lvwModules.Items.Clear();
			ListViewItem listViewItem;

			foreach (var moduleInfo in _processInfo.GetModules())
			{
				bool isClrModule = moduleInfo.IsClrModule(out var clrInfo);
				if(!isClrModule && mnuOnlyDotNetModule.Checked) continue;
				listViewItem = new ListViewItem(moduleInfo.Name);
				listViewItem.SubItems.Add(isClrModule ? clrInfo.AppDomainName : string.Empty);
				listViewItem.SubItems.Add(isClrModule ? clrInfo.ClrVersion : string.Empty);
				listViewItem.SubItems.Add("0x" + moduleInfo.BaseAddress.ToString(Cache.Is64BitProcess ? "X16" : "X8"));
				listViewItem.SubItems.Add("0x" + moduleInfo.BaseSize.ToString("X8"));
				listViewItem.SubItems.Add(moduleInfo.FileName);
				if (isClrModule) listViewItem.BackColor = Cache.DotNetColor;
			}

			lvwModules.AutoResizeColumns(false);
		}

		private static string EnsureValidFileName(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
				return string.Empty;

			var newFileName = new StringBuilder(fileName.Length);
			foreach (char chr in fileName)
			{
				if (!InvalidFileNameChars.Contains(chr))
					newFileName.Append(chr);
			}
			return newFileName.ToString();
		}

		private void DumpModule(IntPtr moduleHandle, ImageLayout imageLayout, string filePath)
		{
			bool result;
			using (var dumper = DumperFactory.GetDumper(_process.Id, _dumperType))
				result = dumper.DumpModule(moduleHandle, imageLayout, filePath);
			MessageBoxStub.Show(result ? $"{_resources.GetString("StrDumpModuleSuccessfully")}{Environment.NewLine}{filePath}" : _resources.GetString("StrFailToDumpModule"), result ? MessageBoxIcon.Information : MessageBoxIcon.Error);
		}

		private static string PathInsertPostfix(string path, string postfix)
		{
			return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + postfix + Path.GetExtension(path));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				components?.Dispose();
				_process.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
