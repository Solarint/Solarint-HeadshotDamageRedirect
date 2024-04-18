using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using EFT.UI.Health;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using ShotID = GStruct390;

namespace Solarint.HeadshotDamageRedirect
{
    [BepInPlugin("solarint.dmgRedirect", "Headshot Damage Redirection", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Init(Config);
            new ApplyShotPatch().Enable();
        }
    }

    internal class ApplyShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("ApplyShot");

        [PatchPrefix]
        public static void PatchPrefix(ref EBodyPart bodyPartType, ref DamageInfo damageInfo, ref Player __instance)
        {
            if (Settings.ModEnabled.Value == false)
            {
                return;
            }

            // Is the incoming damage coming to the head, and is the current player instance the main player?
            if (bodyPartType == EBodyPart.Head && __instance.IsYourPlayer)
            {
                // Did the shot actually penetrate the player's helmet?
                bool penetrated = ShotPenetrated(damageInfo);
                if (penetrated)
                {
                    float originalDamageTohead = damageInfo.Damage;

                    // Did the user set a minimum damage threshold for the mod to activate? if so, check to see if the incoming damage meets that threshold.
                    float minDmg = Settings.MinHeadDamageToRedirect.Value;
                    if (minDmg > 0 && minDmg > originalDamageTohead)
                    {
                        return;
                    }

                    // Select a random part from the ones the user has selected as valid to redirect to
                    EBodyPart newPart = SelectRandomBodyPart();

                    // Reduce the incoming head damage by a ratio the user has set
                    float newDamageToHead = ReduceDamage(originalDamageTohead, out float damageToRedirect);

                    // Create a new instance of DamageInfo with all the same info as the original
                    DamageInfo redirectedDamageInfo = CloneDamageInfo(damageInfo);

                    // Update the info in the new damageinfo to label it correctly and apply the redirected damage
                    UpdateNewDamageInfo(redirectedDamageInfo, damageInfo, damageToRedirect);

                    // Match the body part collider to the randomly selected body part
                    EBodyPartColliderType newColliderType = GetNewColliderType(newPart);

                    // Create a new shotID
                    ShotID newShotID = new ShotID(redirectedDamageInfo.SourceId, 0);

                    // Apply the redirected damage to the selected part
                    __instance.ApplyShot(redirectedDamageInfo, newPart, newColliderType, 0, newShotID);

                    // If the user set the max damage above 0, clamp the damage to what is set
                    float maxDmg = Settings.MaxHeadDamageNumber.Value;
                    if (maxDmg > 0)
                    {
                        newDamageToHead = UnityEngine.Mathf.Clamp(newDamageToHead, 0, maxDmg);
                    }

                    // Log what happened
                    LogMessage(originalDamageTohead, newDamageToHead, damageToRedirect, newPart);

                    // Update the damage to our reduced number.
                    damageInfo.Damage = newDamageToHead;

                    // All Done!
                }
            }

            if (Settings.DebugEnabled.Value && __instance.IsYourPlayer)
            {
                damageInfo.Damage = 1f;
            }
        }

        private static bool ShotPenetrated(DamageInfo damageInfo)
        {
            return (string.IsNullOrEmpty(damageInfo.BlockedBy) || string.IsNullOrEmpty(damageInfo.DeflectedBy));
        }

        private static void LogMessage(float originalDamageTohead, float newDamageToHead, float damageToRedirect, EBodyPart newPart)
        {
            string message = $"Headshot Damage To Player! Original Damage: [{originalDamageTohead}] " +
                $": New Damage: [{newDamageToHead}] " +
                $": [{damageToRedirect}] damage redirected to [{newPart}]";

            if (Settings.DisplayMessage.Value || Settings.DebugEnabled.Value)
            {
                NotificationManagerClass.DisplayMessageNotification(message, ENotificationDurationType.Default, ENotificationIconType.Alert);
            }
            Logger.LogInfo(message);
        }

        private static void UpdateNewDamageInfo(DamageInfo redirectedDamageInfo, DamageInfo originalDamageInfo, float damageToRedirect)
        {
            redirectedDamageInfo.Damage = damageToRedirect;
            redirectedDamageInfo.ArmorDamage = 0f;
            redirectedDamageInfo.BlockedBy = string.Empty;
            redirectedDamageInfo.DeflectedBy = string.Empty;
            redirectedDamageInfo.DidArmorDamage = 0f;
            //redirectedDamageInfo.OverDamageFrom = EBodyPart.Head;
            redirectedDamageInfo.FireIndex = 0;
            //redirectedDamageInfo.GetOverDamage(EBodyPart.Head);
        }

        private static EBodyPartColliderType GetNewColliderType(EBodyPart newPart)
        {
            EBodyPartColliderType newColliderType;
            switch (newPart)
            {
                case EBodyPart.Chest:
                    newColliderType = EBodyPartColliderType.RibcageUp;
                    break;
                case EBodyPart.Stomach:
                    newColliderType = EBodyPartColliderType.RibcageLow;
                    break;
                case EBodyPart.LeftArm:
                    newColliderType = EBodyPartColliderType.LeftUpperArm;
                    break;
                case EBodyPart.RightArm:
                    newColliderType = EBodyPartColliderType.RightUpperArm;
                    break;
                case EBodyPart.LeftLeg:
                    newColliderType = EBodyPartColliderType.LeftThigh;
                    break;
                case EBodyPart.RightLeg:
                    newColliderType = EBodyPartColliderType.RightThigh;
                    break;
                default:
                    newColliderType = EBodyPartColliderType.RibcageUp;
                    break;
            }
            return newColliderType;
        }

        private static DamageInfo CloneDamageInfo(DamageInfo oldDamageInfo)
        {
            return new DamageInfo
            {
                Damage = oldDamageInfo.Damage,
                DamageType = oldDamageInfo.DamageType,
                PenetrationPower = oldDamageInfo.PenetrationPower,
                HitCollider = oldDamageInfo.HitCollider,
                Direction = oldDamageInfo.Direction,
                HitPoint = oldDamageInfo.HitPoint,
                MasterOrigin = oldDamageInfo.MasterOrigin,
                HitNormal = oldDamageInfo.HitNormal,
                HittedBallisticCollider = oldDamageInfo.HittedBallisticCollider,
                Player = oldDamageInfo.Player,
                Weapon = oldDamageInfo.Weapon,
                FireIndex = oldDamageInfo.FireIndex,
                ArmorDamage = oldDamageInfo.ArmorDamage,
                IsForwardHit = oldDamageInfo.IsForwardHit,
                HeavyBleedingDelta = oldDamageInfo.HeavyBleedingDelta,
                LightBleedingDelta = oldDamageInfo.LightBleedingDelta,
                BleedBlock = oldDamageInfo.BleedBlock,
                DeflectedBy = oldDamageInfo.DeflectedBy,
                BlockedBy = oldDamageInfo.BlockedBy,
                StaminaBurnRate = oldDamageInfo.StaminaBurnRate,
                DidBodyDamage = oldDamageInfo.DidBodyDamage,
                DidArmorDamage = oldDamageInfo.DidArmorDamage,
                SourceId = oldDamageInfo.SourceId,
                OverDamageFrom = oldDamageInfo.OverDamageFrom,
                BodyPartColliderType = oldDamageInfo.BodyPartColliderType,
            };
        }

        private static float ReduceDamage(float damageToHead, out float damageToRedirect)
        {
            float originalDamageToHead = damageToHead;

            float ratio = 1f - Settings.RedirectPercentage.Value / 100f;
            float newDamageToHead = originalDamageToHead * ratio;

            damageToRedirect = originalDamageToHead - newDamageToHead;
            return newDamageToHead;
        }

        private static EBodyPart SelectRandomBodyPart()
        {
            BaseBodyParts.Shuffle();

            for (int i = 0; i < BaseBodyParts.Count; i++)
            {
                EBodyPart bodyPart = BaseBodyParts[i];
                if (Settings.RedirectParts[bodyPart].Value == true)
                {
                    return bodyPart;
                }
            }

            return EBodyPart.Chest;
        }

        public static readonly List<EBodyPart> BaseBodyParts = new List<EBodyPart>
        {
            EBodyPart.Chest,
            EBodyPart.Stomach,
            EBodyPart.LeftArm,
            EBodyPart.RightArm,
            EBodyPart.LeftLeg,
            EBodyPart.RightLeg
        };
    }

    // Code used from https://stackoverflow.com/questions/273313/randomize-a-listt
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom
        {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }
    }

    // Code used from https://stackoverflow.com/questions/273313/randomize-a-listt
    static class MyExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    internal class Settings
    {
        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> DisplayMessage;
        public static ConfigEntry<bool> DebugEnabled;
        // public static ConfigEntry<bool> OneHitKillProtection;
        public static ConfigEntry<float> RedirectPercentage;
        public static ConfigEntry<float> MaxHeadDamageNumber;
        public static ConfigEntry<float> MinHeadDamageToRedirect;

        public static Dictionary<EBodyPart, ConfigEntry<bool>> RedirectParts = new Dictionary<EBodyPart, ConfigEntry<bool>>();

        public static void Init(ConfigFile Config)
        {
            const string GeneralSectionTitle = "General"; 
            const string OptionalSectionTitle = "Optional";
            const string SelectPartSection = "Redirection Body Part Targets";

            int optionCount = 0;

            string name = "Enable Damage Redirection";
            string description = "Turns this mod On or Off";
            bool defaultBool = true;

            ModEnabled = Config.Bind(
                GeneralSectionTitle, name, defaultBool,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Display Notification on Damage Received";
            description = "Display an in game notification when damage to your head is detected and modified.";
            defaultBool = false;

            DisplayMessage = Config.Bind(
                GeneralSectionTitle, name, defaultBool,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Damage Redirection Percentage";
            description = "The amount of damage in percentage, to redirect to another body part when the player is headshot. " +
                    "So if this is set to 60, and you receive 50 damage to your head, you will instead receive 20 damage to their head, " +
                    "and 40 will be redirected to a random body part selected. ";
            float defaultFloat = 60f;

            RedirectPercentage = Config.Bind(
                GeneralSectionTitle, name, defaultFloat,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Debug Enabled";
            description = "Force Notification, Set all damage that isn't the head to 1";
            defaultBool = false;

            DebugEnabled = Config.Bind(
                GeneralSectionTitle, name, defaultBool,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- , IsAdvanced = true}
                ));

            // Optional Settings
            name = "Maximum Head Damage Limit";
            description =
                "0 means this is disabled. " +
                "If set above 0, this will be the maximum cap on how much damage a single shot can do to the head. " +
                "So if the player receives 60 damage to the head, and this is set to 30, it will limit the incoming damage to 30. " +
                "This happens AFTER redirection!";
            defaultFloat = 0f;

            MaxHeadDamageNumber = Config.Bind(
                OptionalSectionTitle, name, defaultFloat,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Minimum Head Damage To Redirect";
            description =
                "0 means this is disabled. " +
                "If set above 0, this will be the minimum damage to your head for damage redirection to occur " +
                "So for example, if the player receives 20 damage to the head, and this is set to 30, no damage will be redirected at all, and the mod will do nothing. " +
                "This happens BEFORE redirection!";
            defaultFloat = 0f;

            MinHeadDamageToRedirect = Config.Bind(
                OptionalSectionTitle, name, defaultFloat,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            // Body Part Selection
            List<EBodyPart> baseParts = ApplyShotPatch.BaseBodyParts;
            for (int i = 0; i < baseParts.Count; i++)
            {
                EBodyPart part = baseParts[i];
                name = part.ToString();
                description = 
                    $"Headshot damage will be able to be redirected to [{part}]. " +
                    $"Which part is selected is random, but will only select from the parts enabled here. " +
                    $"For example, If you wish to have HDR only redirect to the chest, disable all other parts except the chest.";

                ConfigEntry<bool> config = Config.Bind(
                SelectPartSection, name, true, 
                new ConfigDescription(description, null, 
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

                RedirectParts.Add(part, config);
            }
        }
    }
}