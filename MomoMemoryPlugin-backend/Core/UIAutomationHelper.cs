using System.Runtime.InteropServices;

namespace MomoBackend.Core;

/// <summary>
/// UI Automation 辅助类 - 使用 Windows 辅助功能 API 直接操作 UI 元素
/// 这种方式不需要真实的鼠标输入，可以实现真正的后台点击
/// </summary>
public static class UIAutomationHelper
{
    // IUIAutomation COM 接口
    private static readonly Guid CLSID_CUIAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    private static readonly Guid IID_IUIAutomation = new("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr pvarg);

    private const uint CLSCTX_INPROC_SERVER = 1;

    // UIA Pattern IDs
    public const int UIA_InvokePatternId = 10000;
    public const int UIA_SelectionItemPatternId = 10010;
    public const int UIA_TogglePatternId = 10015;
    public const int UIA_ValuePatternId = 10002;

    // UIA Property IDs
    public const int UIA_BoundingRectanglePropertyId = 30001;
    public const int UIA_NamePropertyId = 30005;
    public const int UIA_ClassNamePropertyId = 30012;
    public const int UIA_ControlTypePropertyId = 30003;

    // Control Type IDs
    public const int UIA_ButtonControlTypeId = 50000;
    public const int UIA_CheckBoxControlTypeId = 50002;
    public const int UIA_EditControlTypeId = 50004;

    /// <summary>
    /// 尝试使用 UI Automation 点击指定位置的元素
    /// </summary>
    public static bool TryClickElementAtPoint(int screenX, int screenY, out string message)
    {
        IntPtr automation = IntPtr.Zero;
        IntPtr element = IntPtr.Zero;

        try
        {
            // 创建 UI Automation 实例
            var clsid = CLSID_CUIAutomation;
            var iid = IID_IUIAutomation;
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out automation);

            if (hr != 0 || automation == IntPtr.Zero)
            {
                message = $"Failed to create UIAutomation instance: 0x{hr:X8}";
                return false;
            }

            // 获取 IUIAutomation 虚函数表
            IntPtr vtbl = Marshal.ReadIntPtr(automation);

            // ElementFromPoint 是 vtbl[7]
            var elementFromPoint = Marshal.GetDelegateForFunctionPointer<ElementFromPointDelegate>(
                Marshal.ReadIntPtr(vtbl, 7 * IntPtr.Size));

            var point = new POINT { X = screenX, Y = screenY };
            hr = elementFromPoint(automation, point, out element);

            if (hr != 0 || element == IntPtr.Zero)
            {
                message = $"No UI element found at ({screenX}, {screenY})";
                return false;
            }

            // 获取元素信息用于调试
            string elementName = GetElementName(element);
            string elementClass = GetElementClassName(element);

            // 尝试获取 InvokePattern 并调用
            if (TryInvoke(element))
            {
                message = $"Invoked element: {elementName} ({elementClass})";
                return true;
            }

            // 尝试 Toggle
            if (TryToggle(element))
            {
                message = $"Toggled element: {elementName} ({elementClass})";
                return true;
            }

            // 尝试 SelectionItem
            if (TrySelect(element))
            {
                message = $"Selected element: {elementName} ({elementClass})";
                return true;
            }

            message = $"Element found but no supported pattern: {elementName} ({elementClass})";
            return false;
        }
        catch (Exception ex)
        {
            message = $"UIAutomation error: {ex.Message}";
            return false;
        }
        finally
        {
            if (element != IntPtr.Zero)
                Marshal.Release(element);
            if (automation != IntPtr.Zero)
                Marshal.Release(automation);
        }
    }

    private static string GetElementName(IntPtr element)
    {
        try
        {
            IntPtr vtbl = Marshal.ReadIntPtr(element);
            // get_CurrentName 是 IUIAutomationElement vtbl[23]
            var getName = Marshal.GetDelegateForFunctionPointer<CurrentNameDelegate>(
                Marshal.ReadIntPtr(vtbl, 23 * IntPtr.Size));

            int hr = getName(element, out IntPtr name);
            if (hr == 0 && name != IntPtr.Zero)
            {
                string result = Marshal.PtrToStringBSTR(name);
                Marshal.FreeBSTR(name);
                return result ?? "";
            }
        }
        catch { }
        return "";
    }

    private static string GetElementClassName(IntPtr element)
    {
        try
        {
            IntPtr vtbl = Marshal.ReadIntPtr(element);
            // get_CurrentClassName 是 IUIAutomationElement vtbl[24]
            var getClass = Marshal.GetDelegateForFunctionPointer<CurrentNameDelegate>(
                Marshal.ReadIntPtr(vtbl, 24 * IntPtr.Size));

            int hr = getClass(element, out IntPtr name);
            if (hr == 0 && name != IntPtr.Zero)
            {
                string result = Marshal.PtrToStringBSTR(name);
                Marshal.FreeBSTR(name);
                return result ?? "";
            }
        }
        catch { }
        return "";
    }

    private static bool TryInvoke(IntPtr element)
    {
        try
        {
            IntPtr vtbl = Marshal.ReadIntPtr(element);
            // GetCurrentPattern 是 IUIAutomationElement vtbl[16]
            var getPattern = Marshal.GetDelegateForFunctionPointer<GetCurrentPatternDelegate>(
                Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size));

            int hr = getPattern(element, UIA_InvokePatternId, out IntPtr pattern);
            if (hr != 0 || pattern == IntPtr.Zero)
                return false;

            try
            {
                // Invoke 是 IUIAutomationInvokePattern vtbl[3]
                IntPtr patternVtbl = Marshal.ReadIntPtr(pattern);
                var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                    Marshal.ReadIntPtr(patternVtbl, 3 * IntPtr.Size));

                hr = invoke(pattern);
                return hr == 0;
            }
            finally
            {
                Marshal.Release(pattern);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryToggle(IntPtr element)
    {
        try
        {
            IntPtr vtbl = Marshal.ReadIntPtr(element);
            var getPattern = Marshal.GetDelegateForFunctionPointer<GetCurrentPatternDelegate>(
                Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size));

            int hr = getPattern(element, UIA_TogglePatternId, out IntPtr pattern);
            if (hr != 0 || pattern == IntPtr.Zero)
                return false;

            try
            {
                // Toggle 是 IUIAutomationTogglePattern vtbl[3]
                IntPtr patternVtbl = Marshal.ReadIntPtr(pattern);
                var toggle = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                    Marshal.ReadIntPtr(patternVtbl, 3 * IntPtr.Size));

                hr = toggle(pattern);
                return hr == 0;
            }
            finally
            {
                Marshal.Release(pattern);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySelect(IntPtr element)
    {
        try
        {
            IntPtr vtbl = Marshal.ReadIntPtr(element);
            var getPattern = Marshal.GetDelegateForFunctionPointer<GetCurrentPatternDelegate>(
                Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size));

            int hr = getPattern(element, UIA_SelectionItemPatternId, out IntPtr pattern);
            if (hr != 0 || pattern == IntPtr.Zero)
                return false;

            try
            {
                // Select 是 IUIAutomationSelectionItemPattern vtbl[3]
                IntPtr patternVtbl = Marshal.ReadIntPtr(pattern);
                var select = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                    Marshal.ReadIntPtr(patternVtbl, 3 * IntPtr.Size));

                hr = select(pattern);
                return hr == 0;
            }
            finally
            {
                Marshal.Release(pattern);
            }
        }
        catch
        {
            return false;
        }
    }

    // COM 委托定义
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ElementFromPointDelegate(IntPtr self, POINT pt, out IntPtr element);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CurrentNameDelegate(IntPtr self, out IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCurrentPatternDelegate(IntPtr self, int patternId, out IntPtr pattern);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InvokeDelegate(IntPtr self);
}
