using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Solarint.HeadshotDamageRedirect
{
    [BepInPlugin("solarint.dmgRedirect", "Solarint-HeadshotDamageRedirect", "1.0.0")]
    public class HeadshotDamageRedirectPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                Settings.Init(Config);
                new ApplyShotPatch().Enable();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }

    internal class ApplyShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("ApplyShot");
        [PatchPrefix]
        public static void PatchPrefix(ref EBodyPart bodyPartType, ref DamageInfo damageInfo, EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, GStruct390 shotId, ref Player __instance)
        {
            if (Settings.ModEnabled.Value == false)
            {
                return;
            }

            if (bodyPartType == EBodyPart.Head && __instance.IsYourPlayer)
            {
                EBodyPart newPart = SelectRandom();
                float oldDmg = damageInfo.Damage;
                float newDamage = ReduceDamage(damageInfo);

                Logger.LogInfo($"Headshot Damage To Player! Original Damage: {oldDmg} New Damage: {newDamage} :: {damageInfo.Damage} damage redirected to {newPart}");

                __instance.ApplyShot(damageInfo, newPart, colliderType, armorPlateCollider, shotId);

                damageInfo.Damage = newDamage;
            }
        }

        private static float ReduceDamage(DamageInfo damageInfo)
        {
            float oldDmg = damageInfo.Damage;
            float ratio = 1f - Settings.RedirectPercentage.Value / 100f;
            float newDmg = oldDmg * ratio;
            damageInfo.Damage = oldDmg - newDmg;
            return newDmg;
        }

        private static EBodyPart SelectRandom()
        {
            BodyParts.Randomize();

            for (int i = 0; i < BodyParts.Count; i++)
            {
                if (IsPartEnabled(BodyParts[i]))
                {
                    return BodyParts[i];
                }
            }

            return EBodyPart.Stomach;
        }

        private static bool IsPartEnabled(EBodyPart part)
        {
            return Settings.RedirectParts.Value.HasFlag(part);
        }

        private static readonly List<EBodyPart> BodyParts = new List<EBodyPart>
        {
            EBodyPart.LeftLeg,
            EBodyPart.RightLeg,
            EBodyPart.LeftArm,
            EBodyPart.RightArm,
            EBodyPart.Chest,
            EBodyPart.Stomach
        };
    }

    internal class Settings
    {
        private const string GeneralSectionTitle = "General";

        public static ConfigEntry<bool> ModEnabled;

        public static ConfigEntry<float> RedirectPercentage;
        public static ConfigEntry<EBodyPart> RedirectParts; 
        public const EBodyPart SettingsDefaults = EBodyPart.Chest | EBodyPart.Stomach | EBodyPart.LeftArm | EBodyPart.RightArm | EBodyPart.LeftLeg | EBodyPart.RightLeg;

        public static void Init(ConfigFile Config)
        {
            ModEnabled = Config.Bind(
                GeneralSectionTitle,
                "Enable Damage Redirection",
                true,
                "Turns this mod on or Off"
                );

            RedirectPercentage = Config.Bind(
                GeneralSectionTitle,
                "Damage Redirection Percentage",
                60f,
                new ConfigDescription(
                    "The amount of damage in percentage, to redirect to another body part when the player is headshot.",
                    new AcceptableValueRange<float>(1f, 100f)
                ));

            RedirectParts = Config.Bind(
                "Redirect to Parts",
                "Headshot damage will redirect randomly to all selected parts.",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables corpse looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );

        }
    }
}
