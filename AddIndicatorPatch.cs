using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace Solarint.GrenadeIndicator
{
    internal class AddIndicatorPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorldUnityTickListener), "Create");
        }

        [PatchPostfix]
        public static void PatchPostfix(GameObject gameObject)
        {
            try {
                gameObject.AddComponent<GrenadeIndicatorComponent>();
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }
    }
}