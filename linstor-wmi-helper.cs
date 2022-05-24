using System;
using System.Management;

class Program
{
	private static ManagementClass StoragePoolClass;
	private static ManagementClass DiskClass;

	private static ulong GPTOverhead = 130*1024*1024;
	private static ulong PartitionOffset = 129*1024*1024;

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
		string quoted_object_id = vdisk["ObjectId"].
			ToString().Replace(@"\", @"\\").Replace(@"""", @"\""");
		string query_string = @"Associators of {"
                     + @"MSFT_VirtualDisk.ObjectId="""
		     + quoted_object_id
                     + @"""} "
                     + @"Where AssocClass = MSFT_VirtualDiskToDisk";

		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", query_string);
		var res = query.Get();
		ManagementObject[] arr = { null };

		if (res.Count != 1) {
			throw new Exception("Expected Disk object for Virtual Disk with object ID "+vdisk["ObjectID"]+" to exist and be unique, I got "+res.Count+" objects.");
		}
		res.CopyTo(arr, 0);
		return arr[0];
	}

	private static void InitializeWMIClasses()
	{
		StoragePoolClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
		DiskClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_Disk");
	}

	private static void InitializeDisk(ManagementObject disk)
	{
		ManagementBaseObject p = DiskClass.GetMethodParameters("Initialize");
		p["PartitionStyle"] = 2;	/* GPT */
		ManagementBaseObject ret = disk.InvokeMethod("Initialize", p, null);

		Console.WriteLine("retval "+ret["ReturnValue"]);
		Console.WriteLine("status "+ret["ExtendedStatus"]);

		if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't initialize new virtual disk error is "+ret["ReturnValue"]);
		}
	}

	private static void CreatePartition(ManagementObject disk, ulong size, ulong offset)
	{
		ManagementBaseObject p = DiskClass.GetMethodParameters("CreatePartition");
		p["Size"] = size;
		p["Offset"] = offset;
			/* Rest are default values */

		ManagementBaseObject ret = disk.InvokeMethod("CreatePartition", p, null);

		Console.WriteLine("create partition retval "+ret["ReturnValue"]);
		Console.WriteLine("create partition status "+ret["ExtendedStatus"]);

		if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't create partition on new virtual disk error is "+ret["ReturnValue"]);
		}
	}

		/* This calls the WMI method CreateVirtualDisk of the 
		 * MSFT_StoragePool class (pool parameter) in order to
		 * create a virtual disk on this storage pool.
		 */

	private static ManagementBaseObject CreateVirtualDisk(ManagementObject pool, string friendly_name, ulong size, bool thin)
	{
		Console.Write("About to create virtual disk "+friendly_name+" with "+size+" bytes\n");
		ManagementBaseObject p = StoragePoolClass.GetMethodParameters("CreateVirtualDisk");
		p["FriendlyName"] = friendly_name;
		p["Size"] = size + GPTOverhead;
		p["ProvisioningType"] = thin ? 1 : 2;
		p["ResiliencySettingName"] = "Simple"; /* no RAID for now */
		p["Usage"] = 1;
		p["OtherUsageDescription"] = "WinDRBD backing disk";
Console.WriteLine("before invoke ...");
		ManagementBaseObject ret = pool.InvokeMethod("CreateVirtualDisk", p, null);
Console.WriteLine("after invoke ...");
		Console.WriteLine("retval "+ret["ReturnValue"]);
		Console.WriteLine("status "+ret["ExtendedStatus"]);
		Console.WriteLine("virtualdisk "+ret["CreatedVirtualDisk"]);

		if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't create virtual disk error is "+ret["ReturnValue"]);
		}
		return (ManagementBaseObject) ret["CreatedVirtualDisk"];
	}

	private static void CreateVirtualDiskWithPartition(ManagementObject pool, string friendly_name, ulong size, bool thin)
	{
		ManagementBaseObject vdisk = CreateVirtualDisk(pool, friendly_name, size, thin);

		if (vdisk == null)
			throw new Exception("CreateVirtualDisk returned null as object");

		ManagementObject disk = GetDiskForVirtualDisk(vdisk);
		InitializeDisk(disk);
		CreatePartition(disk, size, PartitionOffset);
	}

	public static void Main(string[] args)
	{
		InitializeWMIClasses();
		Console.Write(args.Length+" args");
		if (args.Length < 1) {
			Console.WriteLine("Usage: linstor-helper <storage-pool-friendly-name>");
			return;
		}
		if (args[0] == "virtual-disk") {
			if (args[1] == "create") {
				var the_pool = GetStoragePoolByFriendlyName(args[2]);
				CreateVirtualDiskWithPartition(the_pool, args[3], ulong.Parse(args[4]), args[5] == "thin");
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
