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

	private static ManagementObject GetDiskForVirtualDisk(ManagementBaseObject vdisk)
	{
		// var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Associators Of {MSFT_VirtualDisk.ObjectID="+vdisk["ObjectID"]+"}");
		// var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Associators Of {MSFT_VirtualDisk} where ClassDefsOnly");
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='"+vdisk["ObjectID"]+"'} where ClassDefsOnly";
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='"+vdisk["ObjectID"]+"'}";
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='{1}\\\\\\\\SERVER2019-1\\\\root/Microsoft/Windows/Storage/Providers_v2\\SPACES_VirtualDisk.ObjectId=\\\"{33fba0cb-bf8f-11ec-9b04-806e6f6e6963}:VD:{3143d14e-abf2-4bba-bc1d-7cf19ae0ba48}{0b274417-075e-4864-8d1f-ae605634c414}\\\"'}";
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='%7b1%7d%5c%5cSERVER2019-1%5croot/Microsoft/Windows/Storage/Providers_v2%5c%5cSPACES_VirtualDisk.ObjectId=%22%7b33fba0cb-bf8f-11ec-9b04-806e6f6e6963%7d:VD:%7b3143d14e-abf2-4bba-bc1d-7cf19ae0ba48%7d%7b0b274417-075e-4864-8d1f-ae605634c414%7d%22'}";
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='%7b1%7d'}"; /* works */
		// var query_string = "Associators Of {MSFT_VirtualDisk.ObjectId='"+Uri.EscapeUriString(vdisk["ObjectID"].ToString())+"'}";
//		var query_string = "Associators Of {MSFT_VirtualDisk.ObjectID='"+Uri.EscapeUriString(vdisk["ObjectID"].ToString())+"'} where ClassDefsOnly"; /* not found */
//		var query_string = "Associators Of {MSFT_VirtualDisk.ObjectId="+Uri.EscapeUriString(vdisk["ObjectID"].ToString())+"} where ClassDefsOnly";

		// var query_string = String.format("select * from MSFT_VirtualDiskToDisk where virtualdisk={0}", vdisk);
		var query_string = "select * from MSFT_VirtualDiskToDisk";

Console.WriteLine(query_string);
		
		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", query_string);
		var res = query.Get();
		ManagementObject[] arr = { null };

		foreach (ManagementObject obj in res) {
			Console.WriteLine("Disk: {0} VirtualDisk: {1}", obj["Disk"], obj["VirtualDisk"]);
		}
/*
		if (res.Count != 1) {
			throw new Exception("Expected Disk object for Virtual Disk with object ID "+vdisk["ObjectID"]+" to exist and be unique, I got "+res.Count+" objects.");
		}
*/
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

		ManagementBaseObject vdisk = (ManagementBaseObject) ret["CreatedVirtualDisk"];
		if (vdisk != null) {
			Console.WriteLine("class is "+vdisk.ClassPath);
			ManagementObject disk = GetDiskForVirtualDisk(vdisk);
		}

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
