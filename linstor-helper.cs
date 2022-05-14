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
		ManagementClass m = new ManagementClass("\\\\.\\ROOT\\Microsoft\\Windows\\Storage\\providers_v2:MSFT_StorageSubSystem");
		foreach (ManagementObject o in m.GetInstances())
		{
			Console.Write("Instance");
		}
		return m.GetInstances();
	}
			
	public static void Main(string[] args)
	{
		Console.Write("Hello World!");
		GetStoragePools();
	}
}
