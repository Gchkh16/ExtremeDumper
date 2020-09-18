using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtremeDumper_Lib.Helper;
using Microsoft.Diagnostics.Runtime;

namespace ExtremeDumper_Lib
{
	public class ProcessInfo
	{
		public string ModuleName { get; set; }
		public uint Id { get; internal set; }
		public bool IsDotNetProcess { get; internal set; }
		public bool? Is64BitPE { get; internal set; }
		public string ExePath { get; internal set; }

		public List<ModuleInfo> GetModules()
		{
			return GetNativeModules().Concat(GetClrModules()).ToList();
		}

		public IEnumerable<ModuleInfo> GetNativeModules()
		{
			var moduleEntry32 = NativeMethods.MODULEENTRY32.Default;
			var snapshotHandle = NativeMethods.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, Id);
			if (snapshotHandle == NativeMethods.INVALID_HANDLE_VALUE)
				yield break;
			if (!NativeMethods.Module32First(snapshotHandle, ref moduleEntry32))
				yield break;
			do
			{
				var module = new ModuleInfo { Name = moduleEntry32.szModule, FileName = moduleEntry32.szExePath, BaseAddress = (ulong)moduleEntry32.modBaseAddr.ToInt64(), BaseSize = moduleEntry32.modBaseSize };
				yield return module;
			} while (NativeMethods.Module32Next(snapshotHandle, ref moduleEntry32));
		}

		public IEnumerable<ModuleInfo> GetClrModules()
		{
			using (var dataTarget = DataTarget.AttachToProcess((int)Id, 1000, AttachFlag.Passive))
			{
				foreach (var clrModule in dataTarget.ClrVersions.Select(t => t.CreateRuntime()).SelectMany(t => t.AppDomains).SelectMany(t => t.Modules))
				{
					string name = clrModule.Name;
					bool inMemory;
					if (!string.IsNullOrEmpty(name))
					{
						inMemory = name.Contains(",");
					}
					else
					{
						name = "EmptyName";
						inMemory = true;
					}
					string moduleName = !inMemory ? Path.GetFileName(name) : name.Split(',')[0];

					var module = new ClrModuleInfo
					{
						Name = moduleName,
						AppDomainName = string.Join(", ", clrModule.AppDomains.Select(t => t.Name)),
						ClrVersion = clrModule.Runtime.ClrInfo.Version.ToString(),
						BaseAddress = clrModule.ImageBase,
						BaseSize = clrModule.Size,
						IsInMemoryModule = inMemory,
						FileName = name
					};

					yield return module;
				}
			}
		}


		public const uint TH32CS_SNAPMODULE = 0x00000008;

		public const uint TH32CS_SNAPMODULE32 = 0x00000010;
	}
}
