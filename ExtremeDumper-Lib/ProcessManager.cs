using System;
using System.Collections.Generic;
using System.IO;
using NativeSharp;
using static ExtremeDumper_Lib.Helper.NativeMethods;

namespace ExtremeDumper_Lib {
	public class ProcessManager
	{
		static ProcessManager() 
		{
			Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", string.Empty);
		}

		public static List<ProcessInfo> GetRunningProcesses()
		{
			var processIds = NativeProcess.GetAllProcessIds();
			var moduleEntry = MODULEENTRY32.Default;

			var result = new List<ProcessInfo>(processIds.Length);

			foreach (uint processId in processIds) {
				if (processId == 0)
					continue;
				var snapshotHandle = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
				if (snapshotHandle == INVALID_HANDLE_VALUE)
					continue;
				if (!Module32First(snapshotHandle, ref moduleEntry))
					continue;

				var process = new ProcessInfo(){Id = processId};

				bool isDotNetProcess = false;
				
				process.Is64BitPE = Is64BitPE(moduleEntry.szModule, out bool is64)? is64 : (bool?)null;
				process.ExePath = moduleEntry.szExePath;
				process.ModuleName = moduleEntry.szModule;
				while (Module32Next(snapshotHandle, ref moduleEntry)) {
					string t = moduleEntry.szModule.ToUpperInvariant();
					if (t == "MSCOREE.DLL" || t == "MSCORWKS.DLL" || t == "CLR.DLL" || t == "CORECLR.DLL") {
						isDotNetProcess = true;
						break;
					}
				}

				process.IsDotNetProcess = isDotNetProcess;
				result.Add(process);
			}

			return result;
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
	}
}
