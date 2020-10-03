using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.PE;
using ExtremeDumper_Lib.Dumping;

namespace ExtremeDumper_Lib
{
	public class ModuleInfo
	{
		public string Name { get; internal set; }
		public ulong BaseAddress { get; internal set; }
		public ulong BaseSize { get; internal set; }
		public string FileName { get; internal set; }

		public uint ProcessId { get; internal set; }

		public bool IsInMemoryModule { get; internal set; }

		public bool IsClrModule(out ClrModuleInfo clrModuleInfo)
		{
			if (this is ClrModuleInfo info)
			{
				clrModuleInfo = info;
				return true;
			}
			else
			{
				clrModuleInfo = null;
				return false;
			}
		}

		public virtual void DumpModule(string filePath)
		{
			var layout = IsInMemoryModule ? ImageLayout.File : ImageLayout.Memory;
			using (var dumper = DumperFactory.GetDumper(ProcessId, DumperType.Normal))
				dumper.DumpModule((IntPtr)BaseAddress, layout, filePath);
		}

	}

	public class ClrModuleInfo : ModuleInfo
	{
		public string AppDomainName { get; internal set; }
		public string ClrVersion { get; internal set; }

		//public override void DumpModule(string filePath)
		//{
		//	var layout = IsInMemoryModule ? ImageLayout.Memory : ImageLayout.File;
		//	using(var dumper = DumperFactory.GetDumper(ProcessId, DumperType.Normal))
		//		dumper.
		//}
	}
}
