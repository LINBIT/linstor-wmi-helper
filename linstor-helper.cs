using System;
using System.Management;

class Program
{
	private static ManagementClass StoragePoolClass;

	private static ManagementObjectCollection GetStoragePools()
	{
		return StoragePoolClass.GetInstances();
	}

	private static ManagementObject GetStoragePoolByFriendlyName(String name)
	{
		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Select * From MSFT_StoragePool Where FriendlyName = '"+name+"'");
		var res = query.Get();
		ManagementObject[] arr = { null };

		if (res.Count != 1) {
			throw new Exception("Expected Storage Pool with friendly name "+name+" to exist and be unique, I got "+res.Count+" objects.");
		}
		res.CopyTo(arr, 0);
		return arr[0];
	}

	private static void InitializeWMIClasses()
	{
		StoragePoolClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
	}

		/* This calls the WMI method CreateVirtualDisk of the 
		 * MSFT_StoragePool class (pool parameter) in order to
		 * create a virtual disk on this storage pool.
		 */

	private static ManagementObject CreateVirtualDisk(ManagementObject pool, string friendly_name, ulong size, bool thin)
	{
		Console.Write("About to create virtual disk "+friendly_name+" with "+size+" bytes\n");
		ManagementBaseObject p = StoragePoolClass.GetMethodParameters("CreateVirtualDisk");
		p["FriendlyName"] = friendly_name;
		p["Size"] = size;
		p["ProvisioningType"] = thin ? 1 : 2;
		p["ResiliencySettingName"] = "Simple"; /* no RAID for now */
		p["Usage"] = 1;
		p["OtherUsageDescription"] = "WinDRBD backing disk";
		ManagementBaseObject ret = pool.InvokeMethod("CreateVirtualDisk", p, null);
		Console.WriteLine("retval "+ret["ReturnValue"]);
		Console.WriteLine("status "+ret["ExtendedStatus"]);
		Console.WriteLine("virtualdisk "+ret["CreatedVirtualDisk"]);

		return null;
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
		if (args[0] == "virtual-disk") {
			if (args[1] == "create") {
				var the_pool = GetStoragePoolByFriendlyName(args[2]);
				var disk = CreateVirtualDisk(the_pool, args[3], ulong.Parse(args[4]), args[5] == "thin");
				return;
			}
		}

		var pool = GetStoragePoolByFriendlyName(args[0]);

		ManagementBaseObject p = StoragePoolClass.GetMethodParameters("GetSupportedSize");
		p["ResiliencySettingName"] = "Simple";

		Console.WriteLine("Instance "+pool["FriendlyName"]);
		ManagementBaseObject r = pool.InvokeMethod("GetSupportedSize", p, null);

		Console.WriteLine("Max Size "+r["VirtualDiskSizeMax"]);
		Console.WriteLine("Min Size "+r["VirtualDiskSizeMin"]);
		Console.WriteLine("Divisor "+r["VirtualDiskSizeDivisor"]);
		Console.WriteLine("retval "+r["ReturnValue"]);
		Console.WriteLine("status "+r["ExtendedStatus"]);
		Console.WriteLine("supported sizes "+r["SupportedSizes"]);
	}
}
