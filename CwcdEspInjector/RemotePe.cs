using System;
using System.Collections.Generic;
using System.Text;

namespace CwcdEsp.Injector;

/// <summary>
/// 远程 PE 导出表解析：在目标进程内存中读取指定模块的导出表，
/// 解析出指定名称的导出函数地址（用于获取 mono_* API 在目标进程中的真实地址）。
/// </summary>
internal static class RemotePe
{
    /// <summary>在目标进程内解析多个导出函数地址。</summary>
    public static Dictionary<string, IntPtr> ResolveExports(IntPtr hProcess, IntPtr moduleBase, IEnumerable<string> names)
    {
        var result = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var n in wanted) result[n] = IntPtr.Zero;

        // 1. DOS header → e_lfanew
        byte[]? dos = ReadRemote(hProcess, moduleBase, 0x40);
        if (dos == null) throw new Exception("读取 DOS 头失败");
        int e_lfanew = BitConverter.ToInt32(dos, 0x3C);

        // 2. PE 头（足够覆盖到 DataDirectory[0]）
        IntPtr peBase = moduleBase + e_lfanew;
        byte[]? pe = ReadRemote(hProcess, peBase, 0x200);
        if (pe == null) throw new Exception("读取 PE 头失败");

        // 校验 "PE\0\0"
        if (pe[0] != 0x50 || pe[1] != 0x45 || pe[2] != 0 || pe[3] != 0)
            throw new Exception("PE 签名无效");

        int optHeaderOffset = 24; // sig(4) + COFF(20)
        ushort magic = BitConverter.ToUInt16(pe, optHeaderOffset); // 0x10B=PE32, 0x20B=PE32+
        // 导出表 DataDirectory[0] 在 OptionalHeader 偏移 96(PE32)/112(PE32+)
        int exportDirOffset = optHeaderOffset + (magic == 0x20B ? 112 : 96);
        int exportRva = BitConverter.ToInt32(pe, exportDirOffset);
        int exportSize = BitConverter.ToInt32(pe, exportDirOffset + 4);
        if (exportRva == 0) throw new Exception("模块无导出表");

        // 3. 导出目录
        IntPtr exportDir = moduleBase + exportRva;
        byte[]? expDir = ReadRemote(hProcess, exportDir, 40);
        if (expDir == null) throw new Exception("读取导出目录失败");
        int numberOfNames = BitConverter.ToInt32(expDir, 24);
        int addrOfFunctions = BitConverter.ToInt32(expDir, 28);
        int addrOfNames = BitConverter.ToInt32(expDir, 32);
        int addrOfNameOrdinals = BitConverter.ToInt32(expDir, 36);

        if (numberOfNames <= 0) return result;

        // 4. 名字 RVA 数组 / 序号数组 / 函数 RVA 数组
        byte[] namesBuf = ReadRemote(hProcess, moduleBase + addrOfNames, 4 * numberOfNames)!;
        byte[] ordinalsBuf = ReadRemote(hProcess, moduleBase + addrOfNameOrdinals, 2 * numberOfNames)!;
        // 函数表大小：以序号最大值为准，简单起见按 numberOfNames 读取（序号通常 < NumberOfFunctions）
        byte[] funcsBuf = ReadRemote(hProcess, moduleBase + addrOfFunctions, 4 * numberOfNames)!;

        int found = 0;
        for (int i = 0; i < numberOfNames && found < wanted.Count; i++)
        {
            int nameRva = BitConverter.ToInt32(namesBuf, i * 4);
            string? nm = ReadRemoteAscii(hProcess, moduleBase + nameRva, 128);
            if (nm == null) continue;
            if (wanted.Contains(nm))
            {
                short ordinal = BitConverter.ToInt16(ordinalsBuf, i * 2);
                int funcRva = BitConverter.ToInt32(funcsBuf, ordinal * 4);

                // 转发导出检测：若 funcRva 落在导出目录范围内，则为转发字符串（mono 核心导出通常非转发）
                if (funcRva >= exportRva && funcRva < exportRva + exportSize)
                {
                    Console.WriteLine($"[!] 导出 '{nm}' 为转发导出，跳过（需手动处理）");
                }
                else
                {
                    result[nm] = moduleBase + funcRva;
                    found++;
                }
            }
        }
        return result;
    }

    private static byte[]? ReadRemote(IntPtr hProcess, IntPtr address, int size)
    {
        byte[] buf = new byte[size];
        if (!Win32.ReadProcessMemory(hProcess, address, buf, (nuint)size, out _))
            return null;
        return buf;
    }

    private static string? ReadRemoteAscii(IntPtr hProcess, IntPtr address, int maxLen)
    {
        byte[]? buf = ReadRemote(hProcess, address, maxLen);
        if (buf == null) return null;
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return Encoding.ASCII.GetString(buf, 0, len);
    }
}
