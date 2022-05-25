using System;
using System.Management;

class LinstorWMIHelper
{
	private static ManagementClass StoragePoolClass;
	private static ManagementClass DiskClass;
	private static ManagementClass VirtualDiskClass;
	private static ManagementClass PartitionClass;

	private static ulong GPTOverhead = 130*1024*1024;
	private static ulong PartitionOffset = 129*1024*1024;

	private static ManagementObjectCollection GetStoragePools()
	{
		return StoragePoolClass.GetInstances();
	}

	private static ManagementObject GetObjectByFriendlyName(String name, String classname)
	{
		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Select * From "+classname+" Where FriendlyName = '"+name+"'");
		var res = query.Get();
		ManagementObject[] arr = { null };

		if (res.Count != 1) {
			throw new Exception("Expected "+classname+" with friendly name "+name+" to exist and be unique, I got "+res.Count+" objects.");
		}
		res.CopyTo(arr, 0);
		return arr[0];
	}

	private static ManagementObjectCollection GetObjectsByPattern(String pattern, String classname)
	{
		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "Select * From "+classname+" Where FriendlyName LIKE '"+pattern+"'");
		return query.Get();
	}

	private static ManagementObject GetStoragePoolByFriendlyName(String name)
	{
		return GetObjectByFriendlyName(name, "MSFT_StoragePool");
	}

	private static ManagementObject GetVirtualDiskByFriendlyName(String name)
	{
		return GetObjectByFriendlyName(name, "MSFT_VirtualDisk");
	}

	private static ManagementObjectCollection GetVirtualDisksByPattern(String pattern)
	{
		return GetObjectsByPattern(pattern, "MSFT_VirtualDisk");
	}

	private static ManagementObject GetAssociatedObject(ManagementBaseObject obj, String assoc_class)
	{
		string quoted_object_id = obj["ObjectId"].
			ToString().Replace(@"\", @"\\").Replace(@"""", @"\""");
		string query_string = @"Associators of {"
                     + obj.ClassPath + @".ObjectId="""
		     + quoted_object_id
                     + @"""} "
                     + @"Where AssocClass = "+assoc_class;

		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", query_string);
		var res = query.Get();
		ManagementObject[] arr = { null };

		if (res.Count != 1) {
			throw new Exception("Expected "+assoc_class+" object for "+obj.ClassPath+" with object ID "+obj["ObjectID"]+" to exist and be unique, I got "+res.Count+" objects.");
		}
		res.CopyTo(arr, 0);
		return arr[0];
	}

	private static ManagementObjectCollection GetPartitionsForDisk(ManagementBaseObject disk, String assoc_class)
	{
		string quoted_object_id = disk["ObjectId"].
			ToString().Replace(@"\", @"\\").Replace(@"""", @"\""");
		string query_string = @"Associators of {"
                     + disk.ClassPath + @".ObjectId="""
		     + quoted_object_id
                     + @"""} "
                     + @"Where AssocClass = "+assoc_class;

		var query = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", query_string);
		return query.Get();
	}

	private static ManagementObject GetDiskForVirtualDisk(ManagementBaseObject vdisk)
	{
		return GetAssociatedObject(vdisk, "MSFT_VirtualDiskToDisk");
	}

	private static ManagementObject GetStoragePoolForVirtualDisk(ManagementBaseObject vdisk)
	{
		return GetAssociatedObject(vdisk, "MSFT_StoragePoolToVirtualDisk");
	}

	private static void InitializeWMIClasses()
	{
		StoragePoolClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
		DiskClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_Disk");
		VirtualDiskClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_VirtualDisk");
		PartitionClass = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_Partition");
	}

	private static void InitializeDisk(ManagementObject disk)
	{
		ManagementBaseObject p = DiskClass.GetMethodParameters("Initialize");
		p["PartitionStyle"] = 2;	/* GPT */
		ManagementBaseObject ret = disk.InvokeMethod("Initialize", p, null);

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
		p["Size"] = size;
		p["ProvisioningType"] = thin ? 1 : 2;
		p["ResiliencySettingName"] = "Simple"; /* no RAID for now */
		p["Usage"] = 1;
		p["OtherUsageDescription"] = "WinDRBD backing disk";
		ManagementBaseObject ret = pool.InvokeMethod("CreateVirtualDisk", p, null);

		if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't create virtual disk error is "+ret["ReturnValue"]);
		}
		return (ManagementBaseObject) ret["CreatedVirtualDisk"];
	}

	private static void CreateVirtualDiskWithPartition(ManagementObject pool, string friendly_name, ulong size, bool thin)
	{
		ManagementBaseObject vdisk = CreateVirtualDisk(pool, friendly_name, size + GPTOverhead, thin);

		if (vdisk == null)
			throw new Exception("CreateVirtualDisk returned null as object");

		ManagementObject disk = GetDiskForVirtualDisk(vdisk);
		InitializeDisk(disk);
		CreatePartition(disk, size, PartitionOffset);
	}

	private static ManagementObject GetDataPartition(ManagementBaseObject vdisk)
	{
		ManagementBaseObject disk = GetDiskForVirtualDisk(vdisk);
		ManagementObjectCollection partitions = GetPartitionsForDisk(disk, "MSFT_DiskToPartition");
		ManagementBaseObject partition2 = null;

		foreach (var p in partitions) {
			if (int.Parse(p["PartitionNumber"].ToString()) == 2) {
				partition2 = p;
				break;
			}
		}
		return (ManagementObject) partition2;
	}

	private static void PrintVirtualDiskInfo(String pattern)
	{
		var vdisks = GetVirtualDisksByPattern(pattern);
		foreach (var vdisk in vdisks) {
			ManagementBaseObject pool = GetStoragePoolForVirtualDisk(vdisk);
			var partition2 = GetDataPartition(vdisk);

			if (partition2 != null) {
				Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", 
					vdisk["Size"].ToString(),
					vdisk["FriendlyName"].ToString(),
					pool["FriendlyName"].ToString(),
					partition2["Size"].ToString(),
					partition2["Guid"]);
			} else {
				Console.WriteLine("{0} has no partition 2, was it created by linstor-wmi-helper?",
					vdisk["FriendlyName"].ToString());
			}
		}
	}

	private static void DeleteVirtualDisk(ManagementObject vdisk)
	{
		ManagementBaseObject p = VirtualDiskClass.GetMethodParameters("DeleteObject");
		ManagementBaseObject ret = vdisk.InvokeMethod("DeleteObject", p, null);

		if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't delete virtual disk error is "+ret["ReturnValue"]);
		}
	}

	private static void DeleteVirtualDisk(String name)
	{
		var vdisk = GetVirtualDiskByFriendlyName(name);
		DeleteVirtualDisk(vdisk);
	}

	private static void DeleteVirtualDisksByPattern(String pattern)
	{
		var vdisks = GetVirtualDisksByPattern(pattern);
		foreach (var vdisk in vdisks) {
			DeleteVirtualDisk((ManagementObject) vdisk);
		}
	}

	private static void ResizeVirtualDisk(String name, ulong size)
	{
		var vdisk = GetVirtualDiskByFriendlyName(name);
		var partition = GetDataPartition(vdisk);
		var vdisk_size = ulong.Parse(vdisk["Size"].ToString());
		var requested_vdisk_size = size + GPTOverhead;

		if (partition == null) {
			throw new Exception("No data partition in disk "+name+", was it created by linstor-wmi-helper?");
		}

		if (requested_vdisk_size > vdisk_size) {
			var p = VirtualDiskClass.GetMethodParameters("Resize");
			p["Size"] = requested_vdisk_size;
			var ret = vdisk.InvokeMethod("Resize", p, null);

			if (ulong.Parse(ret["ReturnValue"].ToString()) != 0) {
				throw new Exception("Couldn't resize virtual disk error is "+ret["ReturnValue"]);
			}
		} else {
			Console.WriteLine("Warning: Cannot shrink virtual disk "+name+" from "+vdisk_size+" to "+requested_vdisk_size+" bytes, only resizing data partition.");
		}

		var p2 = PartitionClass.GetMethodParameters("Resize");
		p2["Size"] = size;
		var ret2 = partition.InvokeMethod("Resize", p2, null);

		if (ulong.Parse(ret2["ReturnValue"].ToString()) != 0) {
			throw new Exception("Couldn't resize partition insize virtual disk error is "+ret2["ReturnValue"]);
		}
	}

	public static void Main(string[] args)
	{
		InitializeWMIClasses();

		if (args.Length > 1 && args[0] == "virtual-disk") {
			if (args.Length == 6 && args[1] == "create") {
				var the_pool = GetStoragePoolByFriendlyName(args[2]);
				CreateVirtualDiskWithPartition(the_pool, args[3], ulong.Parse(args[4]), args[5] == "thin");
				return;
			}
			if (args.Length == 3 && args[1] == "list") {
				PrintVirtualDiskInfo(args[2]);
				return;
			}
			if (args.Length == 3 && args[1] == "delete") {
				DeleteVirtualDisk(args[2]);
				return;
			}
			if (args.Length == 3 && args[1] == "delete-all") {
				DeleteVirtualDisksByPattern(args[2]);
				return;
			}
			if (args.Length == 4 && args[1] == "resize") {
				ResizeVirtualDisk(args[2], ulong.Parse(args[3]));
				return;
			}
		}
		Console.WriteLine("Usage: linstor-wmi-helper virtual-disk create <storage-pool-friendly-name> <newdisk-friendly-name> <size-in-bytes> <thin-or-thick>");
		Console.WriteLine("       linstor-wmi-helper virtual-disk list <pattern>");
		Console.WriteLine("       linstor-wmi-helper virtual-disk delete <disk-friendly-name>");
		Console.WriteLine("       linstor-wmi-helper virtual-disk delete-all <pattern>");
		Console.WriteLine("       linstor-wmi-helper virtual-disk resize <disk-friendly-name> <size-in-bytes>");
		Environment.ExitCode = 1;
	}
}
