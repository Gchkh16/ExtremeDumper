using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Principal;
using System.Windows.Forms;
using ExtremeDumper.Helper;
using ExtremeDumper_Lib;
using ExtremeDumper_Lib.Dumping;
using NativeSharp;
using static ExtremeDumper_Lib.Helper.NativeMethods;

namespace ExtremeDumper.Forms {
	internal partial class ProcessesForm : Form {
		private static readonly bool IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
		private DumperType _dumperType = DumperType.Normal;
		private readonly ResourceManager _resources = new ResourceManager(typeof(ProcessesForm));
		private List<ProcessInfo> _processes;
		private static bool _hasSeDebugPrivilege;

		public ProcessesForm() {
			InitializeComponent();
			Text = $"{Application.ProductName} v{Application.ProductVersion} ({(Environment.Is64BitProcess ? "x64" : "x86")}{(IsAdministrator ? _resources.GetString("StrAdministrator") : string.Empty)})";
			typeof(ListView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, lvwProcesses, new object[] { true });
			lvwProcesses.ListViewItemSorter = new ListViewItemSorter(lvwProcesses, new List<TypeCode> {
				TypeCode.String,
				TypeCode.Int32,
				TypeCode.String
			});
			for (var dumperType = DumperType.Normal; dumperType <= DumperType.Normal; dumperType++) {
				var item = new ToolStripMenuItem(dumperType.ToString());
				var currentDumperType = dumperType;
				item.Click += (object sender, EventArgs e) => SwitchDumperType(currentDumperType);
				mnuDumperType.DropDownItems.Add(item);
			}
			SwitchDumperType(DumperType.Normal);
			RefreshProcessList();
		}

		#region Events
		private void mnuDebugPrivilege_Click(object sender, EventArgs e) {
			if (_hasSeDebugPrivilege)
				return;

			if (!IsAdministrator) {
				MessageBoxStub.Show(_resources.GetString("StrRunAsAdmin") + Application.ProductName, MessageBoxIcon.Error);
				return;
			}
			try {
				Process.EnterDebugMode();
				_hasSeDebugPrivilege = true;
				mnuDebugPrivilege.Checked = true;
				mnuDebugPrivilege.Enabled = false;
				Text = Text.Substring(0, Text.Length - 1) + ", SeDebugPrivilege)";
				MessageBoxStub.Show(_resources.GetString("StrSuccess"), MessageBoxIcon.Information);
			}
			catch {
				MessageBoxStub.Show(_resources.GetString("StrFailed"), MessageBoxIcon.Error);
			}
		}

		private void lvwProcesses_Resize(object sender, EventArgs e) {
			lvwProcesses.AutoResizeColumns(true);
		}

		private void mnuDumpProcess_Click(object sender, EventArgs e) {
			if (lvwProcesses.SelectedIndices.Count == 0)
				return;

			uint processId = uint.Parse(lvwProcesses.GetFirstSelectedSubItem(chProcessId.Index).Text);
			using (var process = NativeProcess.Open(processId))
				fbdlgDumped.SelectedPath = Path.GetDirectoryName(process.ImagePath);
			if (fbdlgDumped.ShowDialog() != DialogResult.OK)
				return;
			DumpProcess(processId, Path.Combine(fbdlgDumped.SelectedPath, "Dumps"));
		}

		private void mnuViewModules_Click(object sender, EventArgs e) {
			if (lvwProcesses.SelectedIndices.Count == 0)
				return;

			

			var processNameItem = lvwProcesses.GetFirstSelectedSubItem(chProcessName.Index);
			if (Environment.Is64BitProcess && processNameItem.BackColor == Cache.DotNetColor && processNameItem.Text.EndsWith(_resources.GetString("Str32Bit"), StringComparison.Ordinal)) {
				MessageBoxStub.Show(_resources.GetString("StrViewModulesSwitchTo32Bit"), MessageBoxIcon.Error);
			}
			else {
				var modulesForm = new ModulesForm(_processes[lvwProcesses.SelectedIndices[0]], _dumperType);
				modulesForm.Show();
			}
		}

		private void mnuRefreshProcessList_Click(object sender, EventArgs e) {
			RefreshProcessList();
		}

		private void mnuOnlyDotNetProcess_Click(object sender, EventArgs e) {
			RefreshProcessList();
		}

		private void mnuInjectDll_Click(object sender, EventArgs e) {
			if (lvwProcesses.SelectedIndices.Count == 0)
				return;

			var injectingForm = new InjectingForm(uint.Parse(lvwProcesses.GetFirstSelectedSubItem(chProcessId.Index).Text));
			injectingForm.Show();
		}

		private void mnuGotoLocation_Click(object sender, EventArgs e) {
			if (lvwProcesses.SelectedIndices.Count == 0)
				return;

			Process.Start("explorer.exe", @"/select, " + lvwProcesses.GetFirstSelectedSubItem(chProcessPath.Index).Text);
		}
		#endregion

		private void SwitchDumperType(DumperType dumperType) {
			string name = dumperType.ToString();
			foreach (ToolStripMenuItem item in mnuDumperType.DropDownItems)
				item.Checked = item.Text == name;
			_dumperType = dumperType;
		}

		private void RefreshProcessList() {
			lvwProcesses.Items.Clear();


			_processes = ProcessManager.GetRunningProcesses();


			foreach (var processInfo in _processes)
			{
				var listViewItem = new ListViewItem(processInfo.ModuleName);
				listViewItem.SubItems.Add(processInfo.Id.ToString());
				listViewItem.SubItems.Add(processInfo.ExePath);
				if(Cache.Is64BitProcess && processInfo.Is64BitPE != null && !processInfo.Is64BitPE.Value) listViewItem.Text += _resources.GetString("Str32Bit");
				if (processInfo.IsDotNetProcess) listViewItem.BackColor = Cache.DotNetColor;
				lvwProcesses.Items.Add(listViewItem);
			}
			lvwProcesses.AutoResizeColumns(false);
		}

		private static bool Is64BitPE(string filePath, out bool is64) {
			try {
				using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new BinaryReader(stream)) {
					reader.BaseStream.Position = 0x3C;
					uint peOffset = reader.ReadUInt32();
					reader.BaseStream.Position = peOffset + 0x4;
					ushort machine = reader.ReadUInt16();
					if (machine != 0x14C && machine != 0x8664)
						throw new InvalidDataException();
					is64 = machine == 0x8664;
				}
				return true;
			}
			catch {
				is64 = false;
				return false;
			}
		}

		private void DumpProcess(uint processId, string directoryPath) {
			if (!Directory.Exists(directoryPath))
				Directory.CreateDirectory(directoryPath);
			using (var dumper = DumperFactory.GetDumper(processId, _dumperType))
				MessageBoxStub.Show($"{dumper.DumpProcess(directoryPath)} {_resources.GetString("StrDumpFilesSuccess")}{Environment.NewLine}{directoryPath}", MessageBoxIcon.Information);
		}
	}
}
