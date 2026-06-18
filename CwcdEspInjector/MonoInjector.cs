using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace CwcdEsp.Injector;

/// <summary>
/// Mono 注入器：在目标进程中调用 mono API 加载托管程序集并执行入口方法。
///
/// 流程：
///   1) 打开进程
///   2) 解析 mono-2.0-bdwgc.dll 的导出表，取得 mono_* 函数在目标进程的真实地址
///   3) 在目标进程分配：数据结构 + 各字符串 + x64 shellcode
///   4) CreateRemoteThread 执行 shellcode（参数 = 数据结构地址）
///   5) 读取全部 5 个中间值（root/asm/image/klass/method）诊断失败步骤
///
/// shellcode 执行的调用链（Windows x64 调用约定，rcx/rdx/r8/r9 传参，32B 影子空间）：
///   root   = mono_get_root_domain()
///   mono_thread_attach(root)
///   asm    = mono_domain_assembly_open(root, assemblyPath)
///   image  = mono_assembly_get_image(asm)
///   klass  = mono_class_from_name(image, namespace, className)
///   method = mono_class_get_method_from_name(klass, methodName, 0)
///   mono_runtime_invoke(method, null, null, null)   →  执行 EntryPoint.Load()
///
/// 数据结构布局（rbx 始终指向它，128 字节）：
///   0x00..0x37  函数指针表(7×8=56)
///   0x38..0x57  字符串指针(4×8=32): pAssembly, pNamespace, pClass, pMethod
///   0x58..0x7F  输出字段(5×8=40): root, asm, image, klass, method
/// </summary>
internal sealed class MonoInjector : IDisposable
{
    private IntPtr _hProcess = IntPtr.Zero;

    public void Open(uint processId)
    {
        _hProcess = Win32.OpenProcess(Win32.PROCESS_ALL_ACCESS, false, processId);
        if (_hProcess == IntPtr.Zero)
            throw new Exception($"OpenProcess 失败（需管理员权限），错误码={MarshalSystem.GetLastError()}");
    }

    /// <summary>执行注入。返回 true=成功，false=部分失败（查看日志诊断）。</summary>
    public bool Inject(IntPtr monoBase, string assemblyPath, string ns, string className, string methodName)
    {
        if (_hProcess == IntPtr.Zero) throw new Exception("进程未打开");

        // 1. 解析 mono 导出
        string[] exports =
        {
            "mono_get_root_domain",
            "mono_thread_attach",
            "mono_domain_assembly_open",
            "mono_assembly_get_image",
            "mono_class_from_name",
            "mono_class_get_method_from_name",
            "mono_runtime_invoke",
        };
        Log.Information("解析 mono 导出表...");
        Dictionary<string, IntPtr> funcs = RemotePe.ResolveExports(_hProcess, monoBase, exports);

        foreach (var kv in funcs)
        {
            if (kv.Value == IntPtr.Zero)
                throw new Exception($"未能解析导出函数: {kv.Key}");
            Log.Information("  {Name} = 0x{Addr:X}", kv.Key, kv.Value.ToInt64());
        }

        // 2. 分配字符串（ASCII, 以 \0 结尾）
        Log.Information("写入字符串: assemblyPath={Path}", assemblyPath);
        IntPtr pAssembly = WriteString(assemblyPath);
        IntPtr pNamespace = WriteString(ns);
        IntPtr pClass = WriteString(className);
        IntPtr pMethod = WriteString(methodName);

        // 3. 构造数据结构（128 字节）
        byte[] data = new byte[128];
        WriteI64(data, 0, funcs["mono_get_root_domain"].ToInt64());
        WriteI64(data, 8, funcs["mono_thread_attach"].ToInt64());
        WriteI64(data, 16, funcs["mono_domain_assembly_open"].ToInt64());
        WriteI64(data, 24, funcs["mono_assembly_get_image"].ToInt64());
        WriteI64(data, 32, funcs["mono_class_from_name"].ToInt64());
        WriteI64(data, 40, funcs["mono_class_get_method_from_name"].ToInt64());
        WriteI64(data, 48, funcs["mono_runtime_invoke"].ToInt64());
        WriteI64(data, 56, pAssembly.ToInt64());
        WriteI64(data, 64, pNamespace.ToInt64());
        WriteI64(data, 72, pClass.ToInt64());
        WriteI64(data, 80, pMethod.ToInt64());
        // 88..127: 输出字段（root/asm/image/klass/method），由 shellcode 回写

        IntPtr pData = AllocAndWrite(data, Win32.PAGE_READWRITE);
        Log.Information("数据结构 @ 0x{Addr:X}", pData.ToInt64());

        // 4. shellcode
        byte[] shellcode = BuildShellcode();
        IntPtr pCode = AllocAndWrite(shellcode, Win32.PAGE_EXECUTE_READWRITE);
        Log.Information("shellcode @ 0x{Addr:X} ({Len} bytes)", pCode.ToInt64(), shellcode.Length);

        // 5. CreateRemoteThread(shellcode, param=pData)
        IntPtr hThread = Win32.CreateRemoteThread(_hProcess, IntPtr.Zero, 0, pCode, pData, 0, out uint tid);
        if (hThread == IntPtr.Zero)
            throw new Exception($"CreateRemoteThread 失败，错误码={MarshalSystem.GetLastError()}");
        Log.Information("远程线程已创建 tid={Tid}，等待执行完成...", tid);

        Win32.WaitForSingleObject(hThread, Win32.INFINITE);
        Win32.GetExitCodeThread(hThread, out uint exitCode);
        Win32.CloseHandle(hThread);
        Log.Information("远程线程退出，code={Code} (0x{Hex:X})", exitCode, exitCode);

        // 0xE06D7363 = C++ EH 异常（Mono 内部抛出）
        if (exitCode == 0xE06D7363)
        {
            Log.Warning("退出码 0xE06D7363 = C++ 异常，Mono 运行时内部出错（通常是依赖解析失败或类型加载失败）");
        }

        // 6. 读取全部 5 个中间值，精确定位失败步骤
        byte[] result = new byte[128];
        Win32.ReadProcessMemory(_hProcess, pData, result, (nuint)result.Length, out _);

        long rootPtr   = BitConverter.ToInt64(result, 0x58);
        long asmPtr    = BitConverter.ToInt64(result, 0x60);
        long imagePtr  = BitConverter.ToInt64(result, 0x68);
        long klassPtr  = BitConverter.ToInt64(result, 0x70);
        long methodPtr = BitConverter.ToInt64(result, 0x78);

        Log.Information("===== 注入诊断 =====");
        Log.Information("  mono_get_root_domain()          = 0x{Val:X}  {Status}", rootPtr, rootPtr != 0 ? "OK" : "FAIL");
        Log.Information("  mono_domain_assembly_open()     = 0x{Val:X}  {Status}", asmPtr, asmPtr != 0 ? "OK" : "FAIL");
        Log.Information("  mono_assembly_get_image()       = 0x{Val:X}  {Status}", imagePtr, imagePtr != 0 ? "OK" : "FAIL");
        Log.Information("  mono_class_from_name()          = 0x{Val:X}  {Status}", klassPtr, klassPtr != 0 ? "OK" : "FAIL");
        Log.Information("  mono_class_get_method_from_name() = 0x{Val:X}  {Status}", methodPtr, methodPtr != 0 ? "OK" : "FAIL");

        // 逐步诊断
        if (rootPtr == 0)
            Log.Error("FAIL: mono_get_root_domain 返回 NULL — Mono 运行时未初始化");
        else if (asmPtr == 0)
        {
            Log.Error("FAIL: mono_domain_assembly_open 返回 NULL — 无法加载程序集");
            Log.Error("  可能原因: 文件路径错误 / 文件不存在 / 程序集格式损坏 / 依赖 DLL(0Harmony) 缺失");
            Log.Error("  程序集路径: {Path}", assemblyPath);
        }
        else if (imagePtr == 0)
            Log.Error("FAIL: mono_assembly_get_image 返回 NULL — 程序集已加载但无法获取 image");
        else if (klassPtr == 0)
        {
            Log.Error("FAIL: mono_class_from_name 返回 NULL — 类未找到");
            Log.Error("  命名空间: {Ns}, 类名: {Class}", ns, className);
        }
        else if (methodPtr == 0)
        {
            Log.Error("FAIL: mono_class_get_method_from_name 返回 NULL — 方法未找到");
            Log.Error("  类: {Ns}.{Class}, 方法名: {Method}", ns, className, methodName);
        }
        else
        {
            Log.Information("OK: 全部步骤通过，mono_runtime_invoke 已调用 method=0x{Val:X}", methodPtr);
        }

        bool success = methodPtr != 0;

        // 7. 清理
        VirtualFreeSafe(pCode);
        VirtualFreeSafe(pData);
        VirtualFreeSafe(pAssembly);
        VirtualFreeSafe(pNamespace);
        VirtualFreeSafe(pClass);
        VirtualFreeSafe(pMethod);

        return success;
    }

    /// <summary>
    /// 生成 x64 shellcode。
    /// 数据结构布局（rbx 始终指向它）：
    ///   0x00 函数指针表(7×8) | 0x38 字符串指针(4×8) | 0x58 输出字段(5×8)
    /// </summary>
    private static byte[] BuildShellcode()
    {
        return new byte[]
        {
            0x48, 0x83, 0xEC, 0x28,             // sub rsp, 28h
            0x48, 0x89, 0xCB,                   // mov rbx, rcx
            // root = mono_get_root_domain()  -> [rbx+0x58]
            0x48, 0x8B, 0x03,                   // mov rax, [rbx]
            0xFF, 0xD0,                         // call rax
            0x48, 0x89, 0x43, 0x58,             // mov [rbx+58h], rax
            0x49, 0x89, 0xC4,                   // mov r12, rax
            // mono_thread_attach(root)
            0x4C, 0x89, 0xE1,                   // mov rcx, r12
            0x48, 0x8B, 0x43, 0x08,             // mov rax, [rbx+8]
            0xFF, 0xD0,                         // call rax
            // asm = mono_domain_assembly_open(root, path) -> [rbx+0x60]
            0x4C, 0x89, 0xE1,                   // mov rcx, r12
            0x48, 0x8B, 0x53, 0x38,             // mov rdx, [rbx+38h]
            0x48, 0x8B, 0x43, 0x10,             // mov rax, [rbx+10h]
            0xFF, 0xD0,                         // call rax
            0x48, 0x89, 0x43, 0x60,             // mov [rbx+60h], rax
            0x49, 0x89, 0xC5,                   // mov r13, rax
            // image = mono_assembly_get_image(asm) -> [rbx+0x68]
            0x4C, 0x89, 0xE9,                   // mov rcx, r13
            0x48, 0x8B, 0x43, 0x18,             // mov rax, [rbx+18h]
            0xFF, 0xD0,                         // call rax
            0x48, 0x89, 0x43, 0x68,             // mov [rbx+68h], rax
            0x49, 0x89, 0xC6,                   // mov r14, rax
            // klass = mono_class_from_name(image, ns, class) -> [rbx+0x70]
            0x4C, 0x89, 0xF1,                   // mov rcx, r14
            0x48, 0x8B, 0x53, 0x40,             // mov rdx, [rbx+40h]
            0x4D, 0x8B, 0x43, 0x48,             // mov r8,  [rbx+48h]
            0x48, 0x8B, 0x43, 0x20,             // mov rax, [rbx+20h]
            0xFF, 0xD0,                         // call rax
            0x48, 0x89, 0x43, 0x70,             // mov [rbx+70h], rax
            0x49, 0x89, 0xC7,                   // mov r15, rax
            // method = mono_class_get_method_from_name(klass, method, 0) -> [rbx+0x78]
            0x4C, 0x89, 0xF9,                   // mov rcx, r15
            0x48, 0x8B, 0x53, 0x50,             // mov rdx, [rbx+50h]
            0x4D, 0x31, 0xC0,                   // xor r8, r8
            0x48, 0x8B, 0x43, 0x28,             // mov rax, [rbx+28h]
            0xFF, 0xD0,                         // call rax
            0x48, 0x89, 0x43, 0x78,             // mov [rbx+78h], rax
            0x49, 0x89, 0xC4,                   // mov r12, rax
            // mono_runtime_invoke(method, null, null, null)
            0x4C, 0x89, 0xE1,                   // mov rcx, r12
            0x48, 0x31, 0xD2,                   // xor rdx, rdx
            0x4D, 0x31, 0xC0,                   // xor r8, r8
            0x4D, 0x31, 0xC9,                   // xor r9, r9
            0x48, 0x8B, 0x43, 0x30,             // mov rax, [rbx+30h]
            0xFF, 0xD0,                         // call rax
            // 收尾
            0x48, 0x83, 0xC4, 0x28,             // add rsp, 28h
            0xC3,                               // ret
        };
    }

    // ===== 内存分配/写入辅助 =====

    private IntPtr WriteString(string s)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(s + "\0");
        return AllocAndWrite(bytes, Win32.PAGE_READWRITE);
    }

    private IntPtr AllocAndWrite(byte[] data, uint protect)
    {
        IntPtr addr = Win32.VirtualAllocEx(_hProcess, IntPtr.Zero, (nuint)data.Length, Win32.MEM_COMMIT | Win32.MEM_RESERVE, protect);
        if (addr == IntPtr.Zero)
            throw new Exception($"VirtualAllocEx 失败，错误码={MarshalSystem.GetLastError()}");
        if (!Win32.WriteProcessMemory(_hProcess, addr, data, (nuint)data.Length, out _))
            throw new Exception("WriteProcessMemory 失败");
        return addr;
    }

    private void VirtualFreeSafe(IntPtr addr)
    {
        if (addr != IntPtr.Zero)
            Win32.VirtualFreeEx(_hProcess, addr, 0, Win32.MEM_RELEASE);
    }

    private static void WriteI64(byte[] buf, int offset, long value)
    {
        BitConverter.TryWriteBytes(new Span<byte>(buf, offset, 8), value);
    }

    public void Dispose()
    {
        if (_hProcess != IntPtr.Zero)
        {
            Win32.CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
        }
    }
}

/// <summary>Marshal.GetLastError 封装（独立类避免与 System.Diagnostics 冲突）。</summary>
internal static class MarshalSystem
{
    public static int GetLastError() => System.Runtime.InteropServices.Marshal.GetLastWin32Error();
}
