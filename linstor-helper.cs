using System;
using System.Management;

class Program
{
	private static ManagementClass StoragePoolClass;

	private static ManagementObjectCollection GetStoragePools()
	{
		return StoragePoolClass.GetInstances();
	}

	private static ManagementObjectCollection GetStoragePoolByFriendlyName(String name)
	{
		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Select * From MSFT_StoragePool Where FriendlyName = '"+name+"'");
		var res = query.Get();
		if (res.Count != 1) {
			throw new Exception("Expected Storage Pool with friendly name "+name+" to exist and be unique, I got "+res.Count+" objects.");
		}
		return res;
	}

	private static void InitializeWMIClasses()
	{
		StoragePoolClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
	}

	public static void Main(string[] args)
	{
		InitializeWMIClasses();
		// var pools = GetStoragePools();
		Console.Write(args.Length+" args");
		if (args.Length < 1) {
			Console.WriteLine("Usage: linstor-helper <storage-pool-friendly-name>");
			return;
		}
		var pools = GetStoragePoolByFriendlyName("WinDRBDTestPool");
//		var pools = GetStoragePoolByFriendlyName("WinDRBDTestPool1");

		ManagementBaseObject p = StoragePoolClass.GetMethodParameters("GetSupportedSize");
		p["ResiliencySettingName"] = "Simple";

		foreach (ManagementObject o in pools)
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
	}
}
