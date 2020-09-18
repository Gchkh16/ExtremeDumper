using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtremeDumper_Lib
{
	public class ModuleInfo
	{
		public string Name { get; internal set; }
		public ulong BaseAddress { get; internal set; }
		public ulong BaseSize { get; internal set; }
		public string FileName { get; internal set; }

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
				return true;
			}
		}

	}

	public class ClrModuleInfo : ModuleInfo
	{
		public string AppDomainName { get; internal set; }
		public string ClrVersion { get; internal set; }
		public bool IsInMemoryModule { get; internal set; }
	}
}
