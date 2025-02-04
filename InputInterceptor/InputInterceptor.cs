﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

using Context = System.IntPtr;
using Device = System.Int32;
using Filter = System.UInt16;
using Precedence = System.Int32;

namespace InputInterceptorNS {

    public static class InputInterceptor {

        private static DllWrapper DllWrapper;

        public static Boolean Initialized = DllWrapper != null;
        public static Boolean Disposed => DllWrapper?.Disposed ?? true;

        public static Context CreateContext() => DllWrapper.CreateContext();
        public static void DestroyContext(Context context) => DllWrapper.DestroyContext(context);
        public static Precedence GetPrecedence(Context context, Device device) => DllWrapper.GetPrecedence(context, device);
        public static void SetPrecedence(Context context, Device device, Precedence precedence) => DllWrapper.SetPrecedence(context, device, precedence);
        public static Filter GetFilter(Context context, Device device) => DllWrapper.GetFilter(context, device);
        public static void SetFilter(Context context, Predicate interception_predicate, KeyboardFilter filter) => DllWrapper.SetFilter(context, interception_predicate, (Filter)filter);
        public static void SetFilter(Context context, Predicate interception_predicate, MouseFilter filter) => DllWrapper.SetFilter(context, interception_predicate, (Filter)filter);
        public static void SetFilter(Context context, Predicate interception_predicate, Filter filter) => DllWrapper.SetFilter(context, interception_predicate, filter);
        public static Device Wait(Context context) => DllWrapper.Wait(context);
        public static Device WaitWithTimeout(Context context, UInt64 milliseconds) => DllWrapper.WaitWithTimeout(context, milliseconds);
        public static Int32 Send(Context context, Device device, ref Stroke stroke, UInt32 nstroke) => DllWrapper.Send(context, device, ref stroke, nstroke);
        public static Int32 Receive(Context context, Device device, ref Stroke stroke, UInt32 nstroke) => DllWrapper.Receive(context, device, ref stroke, nstroke);
        public static UInt32 GetHardwareId(Context context, Device device, IntPtr hardware_id_buffer, UInt32 buffer_size) => DllWrapper.GetHardwareId(context, device, hardware_id_buffer, buffer_size);
        public static Boolean IsInvalid(Device device) => DllWrapper.IsInvalid(device) != 0;
        public static Boolean IsKeyboard(Device device) => DllWrapper.IsKeyboard(device) != 0;
        public static Boolean IsMouse(Device device) => DllWrapper.IsMouse(device) != 0;

        static InputInterceptor() {
            DllWrapper = null;
        }

        public static Boolean Initialize() {
            if (Initialized)
                return true;
            try {
                Byte[] DllBytes = Helpers.GetResource($"interception_x{(IntPtr.Size == 8 ? "64" : "86")}.dll");
                DllWrapper = new DllWrapper(DllBytes);
                return true;
            } catch (Exception exception) {
                Console.WriteLine(exception);
                return false;
            }
        }

        public static Boolean Dispose() {
            if (Disposed)
                return true;
            try {
                DllWrapper.Dispose();
                DllWrapper = null;
                return true;
            } catch (Exception exception) {
                Console.WriteLine(exception);
                return false;
            }
        }

        public static Boolean CheckDriverInstalled() {
            RegistryKey baseRegistryKey = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet").OpenSubKey("Services");
            RegistryKey keyboardRegistryKey = baseRegistryKey.OpenSubKey("keyboard");
            RegistryKey mouseRegistryKey = baseRegistryKey.OpenSubKey("mouse");
            if (keyboardRegistryKey == null || mouseRegistryKey == null)
                return false;
            if ((String)keyboardRegistryKey.GetValue("DisplayName", String.Empty) != "Keyboard Upper Filter Driver")
                return false;
            if ((String)mouseRegistryKey.GetValue("DisplayName", String.Empty) != "Mouse Upper Filter Driver")
                return false;
            return true;
        }

        public static Boolean CheckAdministratorRights() {
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static Boolean ExecuteInstaller(String arguments) {
            Boolean result = false;
            if (CheckAdministratorRights() && !CheckDriverInstalled()) {
                String randomTempFileName = Path.GetTempFileName();
                try {
                    File.WriteAllBytes(randomTempFileName, Helpers.GetResource("install_interception.exe"));
                    Process process = new Process();
                    process.StartInfo.FileName = randomTempFileName;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    result = process.ExitCode == 0;
                    File.Delete(randomTempFileName);
                } catch (Exception exception) {
                    Console.WriteLine(exception);
                }
            }
            return result;
        }

        public static Boolean InstallDriver() {
            return ExecuteInstaller("/install");
        }

        public static Boolean UninstallDriver() {
            return ExecuteInstaller("/uninstall");
        }

        public static List<DeviceData> GetDeviceList(Predicate predicate = null) {
            Context context = CreateContext();
            List<DeviceData> result = GetDeviceList(context, predicate);
            DestroyContext(context);
            return result;
        }

        public static List<DeviceData> GetDeviceList(Context context, Predicate predicate = null) {
            List<DeviceData> result = new List<DeviceData>();
            Char[] buffer = new Char[1024];
            GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr bufferPtr = gcHandle.AddrOfPinnedObject();
            for (Device device = 1; device <= 20; device += 1) {
                if (predicate == null ? !IsInvalid(device) : predicate(device)) {
                    UInt32 length = GetHardwareId(context, device, bufferPtr, (UInt32)buffer.Length);
                    if (length > 0) {
                        String name = new String(buffer, 0, (Int32)length);
                        result.Add(new DeviceData(device, name));
                    }
                }
            }
            gcHandle.Free();
            return result;
        }

    }

}
