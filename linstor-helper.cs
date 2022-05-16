using System;
using System.Management;

/*
	string NamespacePath = "\\\\.\\ROOT\\Microsoft\\Windows\\Storage\\providers_v2";
	string ClassName = "MSFT_StorageSubSystem";
	
	//Create ManagementClass
	ManagementClass mClass = new ManagementClass(NamespacePath + ":" + ClassName);
	
	// List the properties in the MSFT_StorageSubSystem class
	PropertyDataCollection lproperties = mClass.Properties;
	
	// display the properties  
	Console.WriteLine(string.Format("Property Names in {0}: ",ClassName));
	foreach (PropertyData property in lproperties)
	{
	    Console.WriteLine("name: {0}, Origin: {1}", property.Name, property.Origin );
	}	
*/
class Program
{
	private static ManagementObjectCollection GetStoragePools()
	{
		ManagementClass m = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage:MSFT_StoragePool");
	// 	ManagementClass m = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage\\providers_v2:MSFT_StorageSubSystem");
		// ManagementClass m = new ManagementClass("CIM_Service");
		// ManagementClass m = new ManagementClass("MSFT_StoragePool");
		EnumerationOptions e = new EnumerationOptions();
		e.EnumerateDeep = true;
		Console.Write("X1");
		// var p = m.GetMethodParameters("GetSupportedSize");
		var p = m.GetMethodParameters("GetSupportedSize");
		/*
		ManagementBaseObject params = m.GetMethodParameters("GetSupportedSize");
		*/
		p["ResiliencySettingName"] = "Simple";

		foreach (ManagementObject o in m.GetInstances(e))
		{
			Console.WriteLine("Instance "+o["FriendlyName"]);
			ManagementBaseObject r = o.InvokeMethod("GetSupportedSize", p, null);
//			UInt64[] Sizes = r["SupportedSizes"];
			// String status = r["ExtendedStatus"];

//			Assert.IsNotNull(r);
			// Console.WriteLine("Max Size "+r["VirtualDiskSizeMax"].ToString());
			Console.WriteLine("Max Size "+r["VirtualDiskSizeMax"]);
			// Console.WriteLine("Sizes "+Sizes.length);
			Console.WriteLine("retval "+r["ReturnValue"]);
			Console.WriteLine("supported sizes "+r["SupportedSizes"]);
		}
		Console.Write("X2");
		return m.GetInstances();
	}
			
	public static void Main(string[] args)
	{
		Console.Write("Hello World!");
		GetStoragePools();
	}
}
