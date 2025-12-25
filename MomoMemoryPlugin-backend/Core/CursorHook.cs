using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MomoBackend.Core;

/// <summary>
/// GetCursorPos Hook - 通过 DLL 注入 + 共享内存通信
/// </summary>
public class CursorHook : IDisposable
{
    private IntPtr _processHandle;
    private int _targetProcessId;
    private bool _isInjected;

    // 共享内存
    private IntPtr _hMapFile;
    private IntPtr _pSharedData;
    private const string SHARED_MEM_NAME = "Local\\MomoCursorHookSharedMem";

    // 共享内存结构（与 C++ 对应）
    // struct SharedData { LONG enabled; LONG fakeX; LONG fakeY; LONG initialized; }

    public int FakeX { get; private set; }
    public int FakeY { get; private set; }
    public bool IsActive { get; private set; }

    #region Native Methods

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    // 共享内存 API
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, uint flProtect, uint dwMaxSizeHigh, uint dwMaxSizeLow, string lpName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint FILE_MAP_ALL_ACCESS = 0xF001F;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    #endregion

    /// <summary>
    /// 检查 Hook DLL 是否存在
    /// </summary>
    public static (bool Exists, string Path, string Message) CheckHookDll()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dll64 = Path.Combine(baseDir, "CursorHook64.dll");

        if (File.Exists(dll64))
        {
            return (true, dll64, "Hook DLL 已就绪");
        }
        return (false, "", "未找到 CursorHook64.dll，请先编译 HookDll");
    }

    /// <summary>
    /// 创建/打开共享内存
    /// </summary>
    public (bool Success, string Message) CreateSharedMemory()
    {
        bool isNew = false;

        // 先尝试打开已存在的
        _hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, false, SHARED_MEM_NAME);

        if (_hMapFile == IntPtr.Zero)
        {
            // 创建新的共享内存（16字节：4个 LONG）
            _hMapFile = CreateFileMapping(INVALID_HANDLE_VALUE, IntPtr.Zero, PAGE_READWRITE, 0, 16, SHARED_MEM_NAME);
            isNew = true;
        }

        if (_hMapFile == IntPtr.Zero)
        {
            return (false, $"无法创建共享内存 (错误: {Marshal.GetLastWin32Error()})");
        }

        _pSharedData = MapViewOfFile(_hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, UIntPtr.Zero);

        if (_pSharedData == IntPtr.Zero)
        {
            CloseHandle(_hMapFile);
            _hMapFile = IntPtr.Zero;
            return (false, $"无法映射共享内存 (错误: {Marshal.GetLastWin32Error()})");
        }

        // 只有新创建的共享内存才初始化为 0
        if (isNew)
        {
            Marshal.WriteInt32(_pSharedData, 0, 0);  // enabled
            Marshal.WriteInt32(_pSharedData, 4, 0);  // fakeX
            Marshal.WriteInt32(_pSharedData, 8, 0);  // fakeY
            Marshal.WriteInt32(_pSharedData, 12, 0); // initialized
            return (true, "共享内存已创建");
        }

        return (true, "共享内存已打开");
    }

    /// <summary>
    /// 附加到目标进程
    /// </summary>
    public (bool Success, string Message) Attach(int processId)
    {
        try
        {
            _targetProcessId = processId;
            _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);

            if (_processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                return (false, $"无法打开进程 (错误: {error})。请以管理员身份运行。");
            }

            return (true, $"已附加到进程 PID: {processId}");
        }
        catch (Exception ex)
        {
            return (false, $"附加失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 注入 Hook DLL
    /// </summary>
    public (bool Success, string Message) InjectHook()
    {
        if (_processHandle == IntPtr.Zero)
            return (false, "未附加到进程");

        // 先创建/打开共享内存
        var smResult = CreateSharedMemory();
        if (!smResult.Success)
            return smResult;

        // 检查 DLL 是否已经加载（initialized == 1 表示已加载）
        int initialized = Marshal.ReadInt32(_pSharedData, 12);
        if (initialized == 1)
        {
            // DLL 已经加载，直接复用
            _isInjected = true;
            return (true, "DLL 已加载，复用现有 Hook");
        }

        // 确定 DLL 路径
        bool is64Bit = Is64BitProcess(_targetProcessId);
        if (!is64Bit)
            return (false, "目前仅支持 64 位进程");

        string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CursorHook64.dll");

        if (!File.Exists(dllPath))
            return (false, $"DLL 不存在: {dllPath}");

        // 注入 DLL
        var result = InjectDll(dllPath);
        if (result.Success)
        {
            _isInjected = true;

            // 等待 DLL 初始化
            Thread.Sleep(100);

            // 检查是否初始化成功
            initialized = Marshal.ReadInt32(_pSharedData, 12);
            if (initialized != 1)
            {
                return (false, "DLL 注入成功但 Hook 初始化失败");
            }
        }
        return result;
    }

    /// <summary>
    /// 设置假坐标
    /// </summary>
    public (bool Success, string Message) SetFakePosition(int x, int y)
    {
        if (_pSharedData == IntPtr.Zero)
            return (false, "共享内存未初始化");

        FakeX = x;
        FakeY = y;

        Marshal.WriteInt32(_pSharedData, 4, x);  // fakeX
        Marshal.WriteInt32(_pSharedData, 8, y);  // fakeY

        return (true, $"假坐标已设置: ({x}, {y})");
    }

    /// <summary>
    /// 启用/禁用 Hook
    /// </summary>
    public (bool Success, string Message) EnableHook(bool enable)
    {
        if (_pSharedData == IntPtr.Zero)
            return (false, "共享内存未初始化");

        IsActive = enable;
        Marshal.WriteInt32(_pSharedData, 0, enable ? 1 : 0);  // enabled

        return (true, enable ? "Hook 已启用" : "Hook 已禁用");
    }

    /// <summary>
    /// 设置假坐标并启用 Hook（便捷方法）
    /// </summary>
    public (bool Success, string Message) SetFakePositionAndEnable(int x, int y)
    {
        var setResult = SetFakePosition(x, y);
        if (!setResult.Success)
            return setResult;

        return EnableHook(true);
    }

    /// <summary>
    /// 禁用 Hook（便捷方法）
    /// </summary>
    public (bool Success, string Message) DisableHook()
    {
        return EnableHook(false);
    }

    #region Private Methods

    private bool Is64BitProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (Environment.Is64BitOperatingSystem)
            {
                IsWow64Process(process.Handle, out bool isWow64);
                return !isWow64;
            }
            return false;
        }
        catch
        {
            return Environment.Is64BitProcess;
        }
    }

    private (bool Success, string Message) InjectDll(string dllPath)
    {
        byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
        IntPtr pathMemory = VirtualAllocEx(_processHandle, IntPtr.Zero, (uint)dllPathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

        if (pathMemory == IntPtr.Zero)
            return (false, $"无法分配内存 (错误: {Marshal.GetLastWin32Error()})");

        if (!WriteProcessMemory(_processHandle, pathMemory, dllPathBytes, dllPathBytes.Length, out _))
        {
            VirtualFreeEx(_processHandle, pathMemory, 0, MEM_RELEASE);
            return (false, $"无法写入内存 (错误: {Marshal.GetLastWin32Error()})");
        }

        IntPtr kernel32 = GetModuleHandle("kernel32.dll");
        IntPtr loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryW");

        if (loadLibraryAddr == IntPtr.Zero)
        {
            VirtualFreeEx(_processHandle, pathMemory, 0, MEM_RELEASE);
            return (false, "无法获取 LoadLibraryW 地址");
        }

        IntPtr threadHandle = CreateRemoteThread(_processHandle, IntPtr.Zero, 0, loadLibraryAddr, pathMemory, 0, out _);

        if (threadHandle == IntPtr.Zero)
        {
            VirtualFreeEx(_processHandle, pathMemory, 0, MEM_RELEASE);
            return (false, $"无法创建远程线程 (错误: {Marshal.GetLastWin32Error()})");
        }

        WaitForSingleObject(threadHandle, 5000);
        GetExitCodeThread(threadHandle, out uint exitCode);
        CloseHandle(threadHandle);
        VirtualFreeEx(_processHandle, pathMemory, 0, MEM_RELEASE);

        if (exitCode == 0)
            return (false, "DLL 加载失败");

        return (true, $"DLL 注入成功");
    }

    #endregion

    public static string GetImplementationGuide()
    {
        return @"
═══════════════════════════════════════════════════════════
  Hook GetCursorPos 使用指南
═══════════════════════════════════════════════════════════

【工作原理】
  1. 创建共享内存用于进程间通信
  2. 注入 DLL 到目标进程
  3. DLL 自动 Hook GetCursorPos
  4. 通过共享内存设置假坐标
  5. 目标程序调用 GetCursorPos 时返回假坐标

【使用步骤】
  1. 以管理员身份运行程序
  2. 选择目标窗口
  3. 选择 hook_cursor 模式
  4. 执行点击

【注意事项】
  - 需要管理员权限
  - 目前仅支持 64 位进程
  - 可能被杀毒软件拦截

═══════════════════════════════════════════════════════════
";
    }

    public void Dispose()
    {
        // 禁用 hook
        if (_pSharedData != IntPtr.Zero)
        {
            Marshal.WriteInt32(_pSharedData, 0, 0);
            UnmapViewOfFile(_pSharedData);
            _pSharedData = IntPtr.Zero;
        }

        if (_hMapFile != IntPtr.Zero)
        {
            CloseHandle(_hMapFile);
            _hMapFile = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        _isInjected = false;
    }
}

