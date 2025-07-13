/**
 * Copyright 2018, haolink
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;

using System.Text;
using System.IO;
using NepSizeUI.WinAPI;
using System.Runtime.InteropServices;

namespace NepSizeUI
{
    /// <summary>
    /// Memory Editor for 64 bit applications.
    /// </summary>
    public class MemEditor64
    {
        /// <summary>
        /// Potential names of the executable.
        /// </summary>
        private string[] _executableNames;

        /// <summary>
        /// Process handle.
        /// </summary>
        private IntPtr _handle;

        /// <summary>
        /// Process Handle Garbage collector.
        /// </summary>
        private GCHandle _gcProcessHandle;

        /// <summary>
        /// Main module address.
        /// </summary>
        private long _mainModuleAddress;

        /// <summary>
        /// Process ID of the process.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        /// Executable.
        /// </summary>
        public string ExecutableName
        {
            get
            {
                return _executableNames[0];
            }
        }

        /// <summary>
        /// EXE names which are valid.
        /// </summary>
        public string[] ExecutableNames
        {
            get
            {
                return _executableNames;
            }
        }

        /// <summary>
        /// Main Module Address.
        /// </summary>
        public long MainModuleAddress
        {
            get
            {
                return _mainModuleAddress;
            }
        }

        /// <summary>
        /// Initialise with exe name.
        /// </summary>
        /// <param name="executable"></param>
        public MemEditor64(string executable) : this([executable]) {}

        /// <summary>
        /// Initialise with multiple executives.
        /// </summary>
        /// <param name="executables"></param>
        public MemEditor64(string[] executables)
        {
            if (executables == null || executables.Length == 0)
            {
                throw new ArgumentNullException();
            }

            List<string> copy = new List<string>();
            foreach (string executable in executables)
            {
                string exe = executable.ToLower();
                if (!copy.Contains(exe))
                {
                    copy.Add(exe);
                }
            }

            this._executableNames = copy.ToArray();
            this._handle = IntPtr.Zero;
        }

        /// <summary>
        /// Disconnect on destructor.
        /// </summary>
        ~MemEditor64()
        {
            if(IsConnected())
            {
                this.Disconnect();
            }
        }

        /// <summary>
        /// Check if a connection is active.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            if (this._handle != IntPtr.Zero)
            {
                uint exitCode = 0;
                if (APIMethods.GetExitCodeProcess(this._handle, out exitCode) && exitCode == APIMethods.STILL_ACTIVE)
                {
                    return true;
                }
                this._mainModuleAddress = 0;
                this._handle = IntPtr.Zero;
            }

            return false;
        }

        /// <summary>
        /// Connect to application memory.
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            if(this.IsConnected())
            {
                return true;
            }

            PROCESSENTRY32 pEntry = new PROCESSENTRY32();
            pEntry.dwSize = Marshal.SizeOf(pEntry);

            uint procId = 0;
            int processId = -1;
            bool found = false;
            IntPtr snapshot = APIMethods.CreateToolhelp32Snapshot(SnapshotFlags.Process, procId);
            if (APIMethods.Process32First(snapshot, ref pEntry))
            {
                while (APIMethods.Process32Next(snapshot, ref pEntry))
                {
                    if (this._executableNames.Contains(pEntry.szExeFile.ToLower()))
                    {
                        processId = pEntry.th32ProcessID;
                        found = true;
                        break;
                    }
                }
            }

            if(!found)
            {
                return false;
            }

            IntPtr process = APIMethods.OpenProcess(ProccessAccess.AllAccess, false, processId);

            this.ProcessId = processId;

            if(process == IntPtr.Zero)
            {
                return false;
            }

            // Setting up the variable for the second argument for EnumProcessModules
            IntPtr[] hMods = new IntPtr[1024];

            GCHandle gch = GCHandle.Alloc(hMods, GCHandleType.Pinned); // Don't forget to free this later
            IntPtr pModules = gch.AddrOfPinnedObject();

            // Setting up the rest of the parameters for EnumProcessModules
            uint uiSize = (uint)(Marshal.SizeOf(typeof(IntPtr)) * (hMods.Length));
            uint cbNeeded = 0;

            bool mainModuleFound = false;
            long baseAddress = 0;

            if (APIMethods.EnumProcessModulesEx(process, pModules, uiSize, out cbNeeded, DwFilterFlag.filter_all) == true)
            {
                Int32 uiTotalNumberofModules = (Int32)(cbNeeded / (Marshal.SizeOf(typeof(IntPtr))));

                for (int i = 0; i < (int)uiTotalNumberofModules; i++)
                {
                    StringBuilder strbld = new StringBuilder(1024);
                    APIMethods.GetModuleFileNameEx(process, hMods[i], strbld, (int)(strbld.Capacity));

                    string moduleName = strbld.ToString();
                    if (this._executableNames.Contains(Path.GetFileName(moduleName).ToLower()))
                    {
                        MODULEINFO mi = new MODULEINFO();
                        if (APIMethods.GetModuleInformation(process, hMods[i], out mi, Marshal.SizeOf(typeof(MODULEINFO))))
                        {
                            baseAddress = (long)mi.lpBaseOfDll;

                            mainModuleFound = true;
                            break;
                        }
                    }
                }
            }

            gch.Free();

            if(!mainModuleFound)
            {
                return false;
            }

            this._mainModuleAddress = baseAddress;
            this._handle = process;
            this._gcProcessHandle = GCHandle.Alloc(this._handle, GCHandleType.Pinned);

            return true;
        }

        /// <summary>
        /// Disconnect from application.
        /// </summary>
        public void Disconnect()
        {
            if(!IsConnected())
            {
                return;
            }

            APIMethods.CloseHandle(this._handle);
            this._mainModuleAddress = 0;
            this._gcProcessHandle.Free();
            this._handle = IntPtr.Zero;
        }

        /// <summary>
        /// Allocates a memory block in the application, returns true if successful and its location.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool AllocateMemoryBlock(uint size, out long address)
        {
            address = 0;
            if(!this.IsConnected())
            {
                return false;
            }

            IntPtr allocatedMemory = APIMethods.VirtualAllocEx(this._handle, IntPtr.Zero, size, AllocationType.Reserve | AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
            if ((Int64)allocatedMemory == 0)
            {
                return false;
            }
            else
            {
                address = (long)allocatedMemory;
                return true;
            }
        }

        /// <summary>
        /// Writes data to the selected address.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <param name="forceUnprotect"></param>
        public void WriteMemory(long address, byte[] data, bool forceUnprotect = false)
        {
            if (!this.IsConnected())
            {
                return;
            }

            int bytesWritten = data.Length;

            MemoryProtection oldProtection = MemoryProtection.ExecuteReadWrite;
            if (forceUnprotect)
            {
                APIMethods.VirtualProtectEx(this._handle, (IntPtr)address, data.Length, MemoryProtection.ExecuteReadWrite, out oldProtection);
            }
            APIMethods.WriteProcessMemory(this._handle, address, data, data.Length, ref bytesWritten);

            if (forceUnprotect)
            {
                MemoryProtection ignore;
                APIMethods.VirtualProtectEx(this._handle, (IntPtr)address, data.Length, oldProtection, out ignore);
            }
        }

        /// <summary>
        /// Writes a float value.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <param name="forceUnprotect"></param>
        public void WriteFloat(long address, float value, bool forceUnprotect = false)
        {
            WriteMemory(address, BitConverter.GetBytes(value), forceUnprotect);
        }

        /// <summary>
        /// Reads a float value.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <param name="forceUnprotect"></param>
        public bool ReadFloat(long address, out float value)
        {
            value = 0;
            byte[] buffer = this.ReadMemory(address, 8);
            if (buffer == null)
            {
                return false;
            }
            value = BitConverter.ToSingle(buffer, 0);
            return true;
        }

        /// <summary>
        /// Resolves a pointer to the target address.
        /// </summary>
        /// <param name="offsets"></param>
        /// <param name="finalAddress"></param>
        /// <returns></returns>
        public bool ResolvePointer(long[] offsets, out long finalAddress)
        {
            finalAddress = 0;
            long currentAddress = 0;
            bool success = true;
            foreach(long offset in offsets)
            {
                if(!this.ReadInt64(currentAddress + offset, out currentAddress))
                {
                    success = false;
                    break;
                }
            }
            if(!success)
            {
                return false;
            }
            finalAddress = currentAddress;
            return true;
        }

        /// <summary>
        /// Reads a buffer from an address.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public byte[] ReadMemory(long address, int size)
        {
            byte[] buffer = new byte[size];
            int bytesRead = 0;
            bool okay = APIMethods.ReadProcessMemory(this._handle, address, buffer, size, ref bytesRead);

            if(!okay || bytesRead != size)
            {
                return null;
            }
            else
            {
                return buffer;
            }
        }

        /// <summary>
        /// Reads an Int64 value.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool ReadInt64(long address, out long value)
        {
            value = 0;
            byte[] buffer = this.ReadMemory(address, 8);
            if(buffer == null)
            {
                return false;
            }
            value = BitConverter.ToInt64(buffer, 0);
            return true;
        }
    }
}
