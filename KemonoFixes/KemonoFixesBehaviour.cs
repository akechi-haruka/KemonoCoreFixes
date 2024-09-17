﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ParadeA.Protocol;
using PrinterAPI;
using SGNFW.Common;
using SGNFW.Common.Server;
using SGNFW.Http;
using SGNFW.Login;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;
using static GameInfoManager;

namespace KemonoFixes {

    [BepInPlugin("eu.haruka.kem.fixes", "KemFixes", "1.0")]
    public class KemonoFixesBehaviour : BaseUnityPlugin {

        public static ManualLogSource Log;
        public static ConfigEntry<bool> ConfigDummyCameras;
        public static ConfigEntry<bool> ConfigDummyCamerasF;
        public static ConfigEntry<bool> ConfigUseHTTP;
        public static ConfigEntry<bool> ConfigDisableEncryption;
        public static ConfigEntry<bool> ConfigShowCursor;
        public static ConfigEntry<String> ConfigPrimaryCamera;

        public void Awake() {
            Log = Logger;

            ConfigDummyCameras = Config.Bind("General", "Dummy Cameras", true, "Enables dummy cameras to fill missing camera slots");
            ConfigDummyCamerasF = Config.Bind("General", "Force Dummy Cameras", true, "Forces dummy cameras, regardless if you have a camera attached.");
            ConfigShowCursor = Config.Bind("General", "Show Cursor", true, "Show and unlock mouse cursor");
            ConfigPrimaryCamera = Config.Bind("General", "Primary Camera", "", "The camera name to use for card reading. \"Force Dummy Cameras\" must be disabled to use this.");

            ConfigUseHTTP = Config.Bind("Network", "Use HTTP instead of HTTPS", true, "Disables the use of HTTPS");
            ConfigDisableEncryption = Config.Bind("Network", "Disable Network Encryption", true, "Disable network encryption");

            Manager.IsForceNoSecureRequest = ConfigUseHTTP.Value;

            foreach (var cam in WebCamTexture.devices) {
                Log.LogInfo("Attached Camera: " + cam.name);
            }

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

                if (KemonoFixesBehaviour.ConfigPrimaryCamera.Value != "") {
                    List<WebCamDevice> list = new List<WebCamDevice>();
                    foreach (WebCamDevice cam in __result) {
                        if (cam.name.Contains(KemonoFixesBehaviour.ConfigPrimaryCamera.Value)) {
                            list.Add(cam);
                        }
                    }
                    __result = list.ToArray();
                }

                if (__result.Length == 0 || KemonoFixesBehaviour.ConfigDummyCamerasF.Value) {
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
                } else if (__result.Length == 1) {
                    __result = new WebCamDevice[] {
                        __result[0],
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
        }

        [HarmonyPrefix, HarmonyPatch(typeof(USBCameraDevice), MethodType.Constructor, typeof(int), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
        static bool USBCameraDeviceCtor(USBCameraDevice __instance, int in_pos, string in_deviceName, bool vertical, bool horizon, bool twice, bool swing) {
            KemonoFixesBehaviour.Log.LogDebug("USBCameraDeviceCtor("+in_pos+", "+in_deviceName+")");

            if (KemonoFixesBehaviour.ConfigDummyCameras.Value) {
                GameInfoManager.Instance.CameraInfo.m_Camera[in_pos] = __instance; // fix dummy code not being read if emulating one camera
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CameraCheck), "Check")]
        static void Check(ref int __result, ref int ___m_error_id) {
            if (KemonoFixesBehaviour.ConfigDummyCameras.Value && (___m_error_id == 3011 || ___m_error_id == 3005)) { // ignore errors for camera 2 missing (no known purpose?) and dummy code (not needed)
                ___m_error_id = 0;
                __result = 0;
            }
        }

    }
}
