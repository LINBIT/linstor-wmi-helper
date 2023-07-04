== LINSTOR WMI helper

This C# tool provides some basic functionality for
managing virtual disks for Microsoft Storage Spaces.
It is meant to be used by LINSTOR but can also
be used as a standalone tool. It mainly exists
because managing virtual disks in PowerShell is
just too slow.

For usage information, just run it without arguments.
To build it you need a Microsoft C# compiler. Mono
won't work because Mono is missing the Windows
Management Interface (wmi) libraries. By using wine
for the C# compiler one can build it also under
Linux.

