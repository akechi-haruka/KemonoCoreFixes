using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PrinterAPI;
using SGNFW.Http;
using System;
using System.IO;
using UnityEngine;
using static GameInfoManager;

namespace KemonoFixes {

    [BepInPlugin("eu.haruka.kem.fixes", "KemFixes", "1.0")]
    public class KemonoFixesBehaviour : BaseUnityPlugin {

        public static ManualLogSource Log;
        public static ConfigEntry<bool> ConfigPrinterEmu;
        public static ConfigEntry<bool> ConfigDummyCameras;
        public static ConfigEntry<String> ConfigPrinterOutputDir;
        public static ConfigEntry<bool> ConfigUseHTTP;
        public static ConfigEntry<bool> ConfigDisableEncryption;
        public static ConfigEntry<bool> ConfigShowCursor;

        public void Awake() {
            Log = Logger;

            ConfigPrinterEmu = Config.Bind("General", "Printer Emulation", true, "Enables printer emulation");
            ConfigDummyCameras = Config.Bind("General", "Dummy Cameras", true, "Enables dummy cameras");
            ConfigPrinterOutputDir = Config.Bind("General", "Printer Output Path", "printer", "Directory where printed images are written to");
            ConfigShowCursor = Config.Bind("General", "Show Cursor", true, "Show and unlock mouse cursor");

            ConfigUseHTTP = Config.Bind("Network", "Use HTTP instead of HTTPS", true, "Disables the use of HTTPS");
            ConfigDisableEncryption = Config.Bind("Network", "Disable Network Encryption", true, "Disable network encryption");

            Manager.IsForceNoSecureRequest = ConfigUseHTTP.Value;

            Harmony.CreateAndPatchAll(typeof(CorePatches));
            Harmony.CreateAndPatchAll(typeof(PrinterPatches));
            Harmony.CreateAndPatchAll(typeof(CameraPatches));
        }

        public void Update() {
            if (!Cursor.visible && ConfigShowCursor.Value) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

    }

    public class CorePatches {

        [HarmonyPostfix, HarmonyPatch(typeof(AmManager), "checkTarget")]
        static void checkTarget(ref bool ___m_isTarget) {
            ___m_isTarget = true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AmManager), "KickAMDaemon")]
        static bool KickAMDaemon(ref bool __result) {
            __result = true;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Manager), "DefaultEncryptKey", MethodType.Getter)]
        static void DefaultEncryptKey(ref string __result) {
            if (KemonoFixesBehaviour.ConfigDisableEncryption.Value) {
                __result = null;
            }
        }

    }

    public unsafe class PrinterPatches {

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_open", typeof(ushort*))]
        static bool chcusb_open(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_open");
            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_listupPrinter", typeof(byte*))]
        static bool chcusb_listupPrinter(ref ushort __result, byte* rIdArray) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_listupPrinter");
            __result = 1;
            *rIdArray = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_selectPrinter", typeof(byte), typeof(ushort*))]
        static bool chcusb_selectPrinter(ref byte __result, byte printerId, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_selectPrinter("+printerId+")");
            *rResult = 0;
            __result = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getPrinterInfo", typeof(ushort), typeof(void*), typeof(uint*))]
        static bool chcusb_getPrinterInfo(ref bool __result, ushort tagNumber, void* rBuffer, uint* rLen) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getPrinterInfo(" + tagNumber + ")");
            if (tagNumber == 8) {
                KemonoFixesBehaviour.Log.LogDebug("Get StandbyState");
                *((byte*)rBuffer) = 1;
                *rLen = 1;
            } else {
                *rLen = 0;
            }
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_imageformat", typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(byte*), typeof(ushort*))]
        static bool chcusb_imageformat(ref bool __result, ushort format, ushort ncomp, ushort depth, ushort width, ushort height, byte* inputImage, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_imageformat(" + format + ", " + ncomp + ", " + depth + ", " + width + ", " + height + ")");

            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_setmtf", typeof(int*))]
        static bool chcusb_setmtf(int* mtf) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_setmtf");
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_makeGamma", typeof(ushort), typeof(byte*), typeof(byte*), typeof(byte*))]
        static bool chcusb_makeGamma(ushort wk, byte* intoneR, byte* intoneG, byte* intoneB) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_makeGamma");
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_setIcctable", typeof(byte*), typeof(byte*), typeof(ushort), typeof(byte*), typeof(byte*), typeof(byte*), typeof(byte*), typeof(byte*), typeof(byte*), typeof(ushort*))]
        static bool chcusb_setIcctable(ref bool __result, byte* inProfileName, byte* outProfileName, ushort renderingIntents, byte* inToneR, byte* inToneG, byte* inToneB, byte* outToneR, byte* outToneG, byte* outToneB, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_setIcctable");
            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_copies", typeof(ushort), typeof(ushort*))]
        static bool chcusb_copies(ref bool __result, ushort copies, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_copies(" + copies + ")");
            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_status", typeof(ushort*))]
        static bool chcusb_status(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_statusAll", typeof(byte*), typeof(ushort*))]
        static bool chcusb_statusAll(ref bool __result, byte* rIdArray, ushort* rResultArray) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_statusAll");
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_startpage", typeof(ushort*), typeof(ushort*))]
        static bool chcusb_startpage(ref bool __result, ushort* pageID, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_startpage");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_endpage", typeof(ushort*))]
        static bool chcusb_endpage(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_endpage");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_write", typeof(byte*), typeof(uint*), typeof(ushort*))]
        static bool chcusb_write(ref bool __result, byte* data, uint* writeSize, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            int sz = (int)*writeSize;
            KemonoFixesBehaviour.Log.LogDebug("chcusb_write(" + sz + ")");

            // HACK: actually use *data here instead of the LoadColorImage hack below lol

            *rResult = 0;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrinterBuffer), "LoadColorImage", typeof(Texture2D))]
        static bool LoadColorImage(Texture2D img) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("LoadColorImage");
            byte[] file = img.EncodeToPNG();

            if (!Directory.Exists(KemonoFixesBehaviour.ConfigPrinterOutputDir.Value)) {
                Directory.CreateDirectory(KemonoFixesBehaviour.ConfigPrinterOutputDir.Value);
            }
            String fname = KemonoFixesBehaviour.ConfigPrinterOutputDir.Value + Path.DirectorySeparatorChar + DateTime.Now.Ticks + ".png";
            File.WriteAllBytes(fname, file);

            KemonoFixesBehaviour.Log.LogInfo("Saved printed image to: " + fname);
            return true;
        }

            [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_writeLaminate", typeof(byte*), typeof(uint*), typeof(ushort*))]
        static bool chcusb_writeLaminate(ref bool __result, byte* data, uint* writeSize, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_writeLaminate");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_setPrinterInfo", typeof(ushort), typeof(void*), typeof(uint*), typeof(ushort*))]
        static bool chcusb_setPrinterInfo(ref bool __result, ushort tagNumber, void* rBuffer, uint* rLen, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_setPrinterInfo(" + tagNumber + ")");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getGamma", typeof(byte*), typeof(byte*), typeof(byte*), typeof(byte*), typeof(ushort*))]
        static bool chcusb_getGamma(ref bool __result, byte* filename, byte* r, byte* g, byte* b, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getGamma");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getMtf", typeof(byte*), typeof(int*), typeof(ushort*))]
        static bool chcusb_getMtf(ref bool __result, byte* filename, int* mtf, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getMtf");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_cancelCopies", typeof(ushort), typeof(ushort*))]
        static bool chcusb_cancelCopies(ref bool __result, ushort pageID, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_cancelCopies");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_setPrinterToneCurve", typeof(ushort), typeof(ushort), typeof(ushort*), typeof(ushort*))]
        static bool chcusb_setPrinterToneCurve(ref bool __result, ushort type, ushort number, ushort* data, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_setPrinterToneCurve");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getPrinterToneCurve", typeof(ushort), typeof(ushort), typeof(ushort*), typeof(ushort*))]
        static bool chcusb_getPrinterToneCurve(ref bool __result, ushort type, ushort number, ushort* data, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getPrinterToneCurve");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getPrintIDStatus", typeof(ushort), typeof(void*), typeof(ushort*))]
        static bool chcusb_getPrintIDStatus(ref bool __result, ushort pageID, void* rBuffer, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getPrintIDStatus");
            __result = true;
            ((ushort*)rBuffer)[3] = 2212;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_setPrintStandby", typeof(ushort*))]
        static bool chcusb_setPrintStandby(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_setPrintStandby");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_testCardFeed", typeof(ushort), typeof(ushort*))]
        static bool chcusb_testCardFeed(ref bool __result, ushort times, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_testCardFeed");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_blinkLED", typeof(ushort*))]
        static bool chcusb_blinkLED(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_blinkLED");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_resetPrinter", typeof(ushort*))]
        static bool chcusb_resetPrinter(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_resetPrinter");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_getErrorStatus", typeof(ushort*))]
        static bool chcusb_getErrorStatus(ref bool __result, ushort* rBuffer) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_getErrorStatus");
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_MakeThread", typeof(ushort))]
        static bool chcusb_MakeThread(ref bool __result, ushort maxCount) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_MakeThread("+maxCount+")");
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_ReleaseThread", typeof(ushort*))]
        static bool chcusb_ReleaseThread(ref bool __result, ushort* rResult) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_ReleaseThread");
            __result = true;
            *rResult = 0;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PrnFunc.NativeMethods), "chcusb_AttachThreadCount", typeof(ushort*), typeof(ushort*))]
        static bool chcusb_AttachThreadCount(ref bool __result, ushort* rCount, ushort* rMaxCount) {
            if (!KemonoFixesBehaviour.ConfigPrinterEmu.Value) { return true; }
            KemonoFixesBehaviour.Log.LogDebug("chcusb_AttachThreadCount");
            __result = true;
            *rCount = 1;
            *rMaxCount = 1;
            return false;
        }

    }

    public class CameraPatches {

        [HarmonyPostfix, HarmonyPatch(typeof(WebCamTexture), "devices", MethodType.Getter)]
        static void get_devices(ref WebCamDevice[] __result) {
            if (KemonoFixesBehaviour.ConfigDummyCameras.Value) {
                __result = new WebCamDevice[] {
                    new WebCamDevice(){
                        m_Name = "Dummy1",
                        m_DepthCameraName = "Dummy1",
                        m_Resolutions = new Resolution[] {
                            new Resolution {
                                width = 1920,
                                height = 1080
                            },
                            new Resolution {
                                width = 1280,
                                height = 720
                            },
                        }
                    },
                    new WebCamDevice(){
                        m_Name = "Dummy2",
                        m_DepthCameraName = "Dummy2",
                        m_Resolutions = new Resolution[] {
                            new Resolution {
                                width = 1920,
                                height = 1080
                            },
                            new Resolution {
                                width = 1280,
                                height = 720
                            },
                        }
                    }
                };

            }
        }

        private static int CurrentCamera = 0;

        [HarmonyPrefix, HarmonyPatch(typeof(QrCodeDecoder), "OnFoundResult")]
        static bool OnFoundResult(ref bool __result, out QrCodeDecoder.FoundResult foundResult) {
            if (KemonoFixesBehaviour.ConfigDummyCameras.Value) {
                foundResult = QrCodeDecoder.FoundResult.RecognitionCode;
                __result = true;
                return false;
            }
            foundResult = QrCodeDecoder.FoundResult.Nothing;
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(USBCameraDevice), MethodType.Constructor, typeof(int), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
        static bool USBCameraDeviceCtor(int in_pos, string in_deviceName, bool vertical, bool horizon, bool twice, bool swing) {
            KemonoFixesBehaviour.Log.LogDebug("USBCameraDeviceCtor("+in_pos+", "+in_deviceName+")");
            CurrentCamera = in_pos;
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CameraCheck), "Check")]
        static void Check(ref int __result, ref int ___m_error_id) {
            if (KemonoFixesBehaviour.ConfigDummyCameras.Value && ___m_error_id == 3011) { // idk, probably dnspy is giving me something wrong here
                ___m_error_id = 0;
                __result = 0;
            }
        }

    }
}
