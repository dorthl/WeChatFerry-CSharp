using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WeChatFerry
{
  public class WeChatFerryServer : IDisposable
  {
    private delegate void WxInitSDKDelegate(bool isHook, int pid);
    private delegate void WxDestroySDKDelegate();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    public IntPtr SdkDllIntPtr { get; private set; }
    private readonly IntPtr initSdkFunction;
    private readonly IntPtr destroySdkFunction;

    public WeChatFerryServer(int pid, bool isHook = false, string sdkPath = null )
    {
      if (sdkPath == null) sdkPath = Path.Combine(Directory.GetCurrentDirectory(), "wcfbin", "sdk.dll");

      if (!File.Exists(sdkPath)) throw new FileNotFoundException("sdk.dll not found", sdkPath);
      SdkDllIntPtr = LoadLibrary(sdkPath);
      if (SdkDllIntPtr == IntPtr.Zero) throw new Exception("Failed to load sdk.dll");

      // 获取函数指针
      initSdkFunction = GetProcAddress(SdkDllIntPtr, "WxInitSDK");
      if (initSdkFunction == IntPtr.Zero) throw new Exception("Failed to load WxInitSDK Function");

      destroySdkFunction = GetProcAddress(SdkDllIntPtr, "WxDestroySDK");
      if (destroySdkFunction == IntPtr.Zero) throw new Exception("Failed to load WxDestroySDK Function");

      Marshal.GetDelegateForFunctionPointer<WxInitSDKDelegate>(initSdkFunction)(isHook, pid);
    }

    public void Dispose()
    {
      Marshal.GetDelegateForFunctionPointer<WxDestroySDKDelegate>(destroySdkFunction)();
      FreeLibrary(SdkDllIntPtr);
      GC.SuppressFinalize(this);
    }
  }
}

