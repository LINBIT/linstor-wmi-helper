using System;
using System.Management;

class Program
{
	private static ManagementObjectCollection GetStoragePools()
	{
		ManagementClass m = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
		ManagementBaseObject p = m.GetMethodParameters("GetSupportedSize");
		p["ResiliencySettingName"] = "Simple";

		foreach (ManagementObject o in m.GetInstances())
		{
			Console.WriteLine("Instance "+o["FriendlyName"]);
			ManagementBaseObject r = o.InvokeMethod("GetSupportedSize", p, null);

			Console.WriteLine("Max Size "+r["VirtualDiskSizeMax"]);
			Console.WriteLine("Min Size "+r["VirtualDiskSizeMin"]);
			Console.WriteLine("Divisor "+r["VirtualDiskSizeDivisor"]);
			Console.WriteLine("retval "+r["ReturnValue"]);
			Console.WriteLine("status "+r["ExtendedStatus"]);
			Console.WriteLine("supported sizes "+r["SupportedSizes"]);
		}
		return m.GetInstances();
	}
			
	public static void Main(string[] args)
	{
		GetStoragePools();
	}
}
