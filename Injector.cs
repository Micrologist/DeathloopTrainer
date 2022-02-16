using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DeathloopTrainer
{
	public static class Injector
	{

		[Flags]
		public enum AllocationType : uint
		{
			Commit = 0x1000,
			Reserve = 0x2000,
			Decommit = 0x4000,
			Release = 0x8000,
			Reset = 0x80000,
			Physical = 0x400000,
			TopDown = 0x100000,
			WriteWatch = 0x200000,
			LargePages = 0x20000000
		}

		[Flags]
		public enum MemoryProtection : uint
		{
			Execute = 0x10,
			ExecuteRead = 0x20,
			ExecuteReadWrite = 0x40,
			ExecuteWriteCopy = 0x80,
			NoAccess = 0x01,
			ReadOnly = 0x02,
			ReadWrite = 0x04,
			WriteCopy = 0x08,
			GuardModifierflag = 0x100,
			NoCacheModifierflag = 0x200,
			WriteCombineModifierflag = 0x400
		}

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

		[DllImport("kernel32.dll")]
		static extern bool WriteProcessMemory(
		 IntPtr hProcess,
		 IntPtr lpBaseAddress,
		 byte[] lpBuffer,
		 Int32 nSize,
		 out IntPtr lpNumberOfBytesWritten
		);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr GetModuleHandle(string moduleName);

		[DllImport("kernel32.dll")]
		static extern IntPtr CreateRemoteThread(IntPtr hProcess,
		   IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
		   IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

		public static bool InjectDLL(string dllpath, Process proc)
		{
			if (proc.Handle == IntPtr.Zero)
				return false;


			IntPtr loc = VirtualAllocEx(proc.Handle, IntPtr.Zero, (uint)dllpath.Length, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.ReadWrite);

			if (loc.Equals(0))
			{
				return false;
			}

			bool result = WriteProcessMemory(proc.Handle, loc, Encoding.ASCII.GetBytes(dllpath), dllpath.Length, out IntPtr bytesRead);

			if (!result || bytesRead.Equals(0))
			{
				return false;
			}

			IntPtr loadLibAdr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

			IntPtr hThread = CreateRemoteThread(proc.Handle, IntPtr.Zero, 0, loadLibAdr, loc, 0, out _);

			return !hThread.Equals(0);

		}
	}

}
