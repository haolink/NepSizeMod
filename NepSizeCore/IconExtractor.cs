using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Complex internal class to gain an icon as a byte array from an EXE file.
/// </summary>
public static class IconExtractor
{
    private const uint RT_GROUP_ICON = 14;
    private const uint RT_ICON = 3;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct GRPICONDIR
    {
        public ushort Reserved;
        public ushort Type;
        public ushort Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct GRPICONDIRENTRY
    {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint BytesInRes;
        public ushort ID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ICONDIRENTRY
    {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint BytesInRes;
        public uint ImageOffset;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    private static IntPtr MAKEINTRESOURCE(ushort id)
    {
        return new IntPtr(id);
    }

    /// <summary>
    /// Reads an icon file from an executable.
    /// </summary>
    /// <param name="exePath"></param>
    /// <returns></returns>
    public static byte[] GetIconBytesFromExe(string exePath)
    {
        const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        if (hModule == IntPtr.Zero)
            return null;

        try
        {
            // Search the first GROUP_ICON ressource
            for (ushort i = 1; i < 1000; i++)
            {
                IntPtr hResInfo = FindResource(hModule, MAKEINTRESOURCE(i), MAKEINTRESOURCE((ushort)RT_GROUP_ICON));
                if (hResInfo == IntPtr.Zero)
                {
                    continue;
                }

                IntPtr hResData = LoadResource(hModule, hResInfo);
                IntPtr pResourceData = LockResource(hResData);
                uint size = SizeofResource(hModule, hResInfo);
                if (pResourceData == IntPtr.Zero || size == 0)
                {
                    continue;
                }

                byte[] groupData = new byte[size];
                Marshal.Copy(pResourceData, groupData, 0, (int)size);

                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    GRPICONDIR dir = ReadStruct<GRPICONDIR>(groupData, 0);
                    writer.Write(dir.Reserved);
                    writer.Write(dir.Type);
                    writer.Write(dir.Count);

                    List<byte[]> imageDataList = new List<byte[]>();
                    int entryOffset = Marshal.SizeOf(typeof(GRPICONDIR));
                    int iconDirEntrySize = Marshal.SizeOf(typeof(GRPICONDIRENTRY));

                    uint currentOffset = 6 + (uint)(dir.Count * 16); // ICONDIR header + entries

                    for (int j = 0; j < dir.Count; j++)
                    {
                        GRPICONDIRENTRY grpEntry = ReadStruct<GRPICONDIRENTRY>(groupData, entryOffset + j * iconDirEntrySize);

                        // Fetch the icon (RT_ICON)
                        IntPtr hIconRes = FindResource(hModule, MAKEINTRESOURCE(grpEntry.ID), MAKEINTRESOURCE((ushort)RT_ICON));
                        if (hIconRes == IntPtr.Zero)
                            return null;

                        IntPtr hIconData = LoadResource(hModule, hIconRes);
                        IntPtr pIconData = LockResource(hIconData);
                        uint iconSize = SizeofResource(hModule, hIconRes);

                        byte[] iconImage = new byte[iconSize];
                        Marshal.Copy(pIconData, iconImage, 0, (int)iconSize);
                        imageDataList.Add(iconImage);

                        // Write icon directory entry.
                        writer.Write(grpEntry.Width);
                        writer.Write(grpEntry.Height);
                        writer.Write(grpEntry.ColorCount);
                        writer.Write(grpEntry.Reserved);
                        writer.Write(grpEntry.Planes);
                        writer.Write(grpEntry.BitCount);
                        writer.Write(grpEntry.BytesInRes);
                        writer.Write(currentOffset);

                        currentOffset += grpEntry.BytesInRes;
                    }

                    // Write the images.
                    foreach (var img in imageDataList)
                    {
                        writer.Write(img);
                    }

                    return ms.ToArray(); // complete.
                }
            }
        }
        finally
        {
            FreeLibrary(hModule);
        }

        return null;
    }

    /// <summary>
    /// Read a handle struct.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    private static T ReadStruct<T>(byte[] data, int offset) where T : struct
    {
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(data, offset);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            handle.Free();
        }
    }
}