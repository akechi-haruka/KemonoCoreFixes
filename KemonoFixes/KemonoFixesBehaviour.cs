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
        public static ConfigEntry<bool> ConfigDummyCameras;
        public static ConfigEntry<bool> ConfigUseHTTP;
        public static ConfigEntry<bool> ConfigDisableEncryption;
        public static ConfigEntry<bool> ConfigShowCursor;

        public void Awake() {
            Log = Logger;

            ConfigDummyCameras = Config.Bind("General", "Dummy Cameras", true, "Enables dummy cameras");
            ConfigShowCursor = Config.Bind("General", "Show Cursor", true, "Show and unlock mouse cursor");

            ConfigUseHTTP = Config.Bind("Network", "Use HTTP instead of HTTPS", true, "Disables the use of HTTPS");
            ConfigDisableEncryption = Config.Bind("Network", "Disable Network Encryption", true, "Disable network encryption");

            Manager.IsForceNoSecureRequest = ConfigUseHTTP.Value;

            Harmony.CreateAndPatchAll(typeof(CorePatches));
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

        [HarmonyPostfix, HarmonyPatch(typeof(Manager), "DefaultEncryptKey", MethodType.Getter)]
        static void DefaultEncryptKey(ref string __result) {
            if (KemonoFixesBehaviour.ConfigDisableEncryption.Value) {
                __result = null;
            }
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
