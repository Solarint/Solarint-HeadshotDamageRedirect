using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Solarint.HeadshotDamageRedirect
{
    [BepInPlugin("solarint.dmgRedirect", "Headshot Damage Redirection", "1.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Init(Config);
            new ApplyDamageInfoPatch().Enable();
        }
    }

    internal class ApplyDamageInfoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("ApplyDamageInfo");

        [PatchPrefix]
        public static void PatchPrefix(ref EBodyPart bodyPartType, ref DamageInfoStruct damageInfo, ref Player __instance)
        {
            if (Settings.ModEnabled.Value == false) {
                return;
            }

            // Is the incoming damage coming to the head, and is the current player instance the main player?
            if (bodyPartType == EBodyPart.Head && __instance.IsYourPlayer) {
                float chance = Settings.ChanceToRedirect.Value;
                if (chance < 100 && !RandomBool(chance)) {
                    return;
                }

                float originalDamageTohead = damageInfo.Damage;

                // Did the user set a minimum damage threshold for the mod to activate? if so, check to see if the incoming damage meets that threshold.
                float minDmg = Settings.MinHeadDamageToRedirect.Value;
                if (minDmg > 0 && minDmg > originalDamageTohead) {
                    return;
                }

                // Reduce the incoming head damage by a ratio the user has set
                float newDamageToHead = ReduceDamage(originalDamageTohead, out float damageToRedirect);

                _sb.Clear();
                var parts = _partsToRedirect;
                getPartsToRedirect(parts);
                createDamageToEachPart(parts, damageToRedirect, damageInfo, __instance, _sb);

                // If the user set the max damage above 0, clamp the damage to what is set
                float maxDmg = Settings.MaxHeadDamageNumber.Value;
                if (maxDmg > 0) {
                    newDamageToHead = UnityEngine.Mathf.Clamp(newDamageToHead, 0, maxDmg);
                }

                // Log what happened
                LogMessage(originalDamageTohead, newDamageToHead, damageToRedirect, _sb);

                // Update the damage to our reduced number.
                damageInfo.Damage = newDamageToHead;

                // All Done!
            }

            if (Settings.DebugEnabled.Value && __instance.IsYourPlayer) {
                damageInfo.Damage = 1f;
            }
        }

        private static readonly StringBuilder _sb = new StringBuilder();

        private static void createDamageToEachPart(List<EBodyPart> parts, float totalDamage, DamageInfoStruct damageInfo, Player player, StringBuilder stringBuilder)
        {
            float perPart = totalDamage / parts.Count;
            foreach (var part in parts) {
                // Create a new instance of DamageInfo with all the same info as the original
                DamageInfoStruct redirectedDamageInfo = CloneDamageInfo(damageInfo);

                // Update the info in the new damageinfo to label it correctly and apply the redirected damage
                UpdateNewDamageInfo(redirectedDamageInfo, damageInfo, perPart);

                // Match the body part collider to the randomly selected body part
                EBodyPartColliderType newColliderType = GetNewColliderType(part);

                // Create a new shotID
                ShotIdStruct newShotID = new ShotIdStruct(redirectedDamageInfo.SourceId, 0);

                // Apply the redirected damage to the selected part
                player.ApplyShot(redirectedDamageInfo, part, newColliderType, 0, newShotID);

                stringBuilder.AppendLine($"Redirected [{perPart}] to [{part}]");
            }
        }

        private static void getPartsToRedirect(List<EBodyPart> parts)
        {
            int target = Settings.BodyPartsCountToRedirectTo.Value;
            parts.Clear();
            if (target == 1) {
                parts.Add(SelectRandomBodyPart());
                return;
            }

            int max = BaseBodyParts.Count;
            if (target >= max) {
                parts.AddRange(BaseBodyParts);
                return;
            }

            const int maxIterations = 100;
            for (int i = 0; i < maxIterations; i++) {
                EBodyPart random = SelectRandomBodyPart();
                if (!parts.Contains(random)) {
                    parts.Add(random);
                }
                if (parts.Count == target) {
                    break;
                }
            }
        }

        private static readonly List<EBodyPart> _partsToRedirect = new List<EBodyPart>();

        private static void LogMessage(float originalDamageTohead, float newDamageToHead, float damageToRedirect, StringBuilder stringBuilder)
        {
            string message = $"Headshot Damage To Player! Original Damage: [{originalDamageTohead}] " +
                $": New Damage: [{newDamageToHead}] " +
                $": [{damageToRedirect}] total damage redirected";

            stringBuilder.AppendLine(message);

            if (Settings.DisplayMessage.Value || Settings.DebugEnabled.Value) {
                NotificationManagerClass.DisplayMessageNotification(stringBuilder.ToString(), ENotificationDurationType.Default, ENotificationIconType.Alert);
            }
            Logger.LogInfo(stringBuilder.ToString());
        }

        private static bool RandomBool(float v)
        {
            return UnityEngine.Random.Range(0f, 100f) < v;
        }

        private static void UpdateNewDamageInfo(DamageInfoStruct redirectedDamageInfo, DamageInfoStruct originalDamageInfo, float damageToRedirect)
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
            switch (newPart) {
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

        private static DamageInfoStruct CloneDamageInfo(DamageInfoStruct oldDamageInfo)
        {
            return new DamageInfoStruct {
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
            damageToRedirect *= Settings.HeadshotMultiplier.Value;
            return newDamageToHead;
        }

        private static EBodyPart SelectRandomBodyPart()
        {
            BaseBodyParts.Shuffle();

            for (int i = 0; i < BaseBodyParts.Count; i++) {
                EBodyPart bodyPart = BaseBodyParts[i];
                if (Settings.RedirectParts[bodyPart].Value == true) {
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

        public static Random ThisThreadsRandom {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }
    }

    // Code used from https://stackoverflow.com/questions/273313/randomize-a-listt
    internal static class MyExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1) {
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
        public static ConfigEntry<float> ChanceToRedirect;
        public static ConfigEntry<float> HeadshotMultiplier;
        public static ConfigEntry<int> BodyPartsCountToRedirectTo;

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
                new ConfigDescription(description,
                new AcceptableValueRange<float>(0f, 100f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Debug Enabled";
            description = "Force Notification, Set all damage that isn't the head to 1";
            defaultBool = false;

            DebugEnabled = Config.Bind(
                GeneralSectionTitle, name, defaultBool,
                new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { Order = optionCount--, IsAdvanced = true }
                ));

            // Optional Settings
            name = "Percentage Chance to Redirect";
            description =
                "100 means this is disabled. " +
                "If below 100, this will roll a chance to actually redirect damage. " +
                "if the roll fails, this mod will affect nothing, and full damage will go to your head.";
            defaultFloat = 100f;

            ChanceToRedirect = Config.Bind(
                OptionalSectionTitle, name, defaultFloat,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(0f, 100f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Headshot Damage Multiplier";
            description =
                "1 means this is disabled. " +
                "If above 1, damage to the head will be multiplied before being sent to another body part, making it more punishing.";
            defaultFloat = 1f;

            HeadshotMultiplier = Config.Bind(
                OptionalSectionTitle, name, defaultFloat,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(1f, 3f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            name = "Minimum Head Damage To Redirect";
            description =
                "0 means this is disabled. " +
                "If set above 0, this will be the minimum damage to your head for damage redirection to occur " +
                "So for example, if the player receives 20 damage to the head, and this is set to 30, no damage will be redirected at all, and the mod will do nothing. " +
                "This happens BEFORE redirection!";
            defaultFloat = 0f;

            MaxHeadDamageNumber = Config.Bind(
                OptionalSectionTitle, name, defaultFloat,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(0f, 100f),
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
                new ConfigDescription(description,
                new AcceptableValueRange<float>(0f, 100f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            // Body Part Selection
            List<EBodyPart> baseParts = ApplyDamageInfoPatch.BaseBodyParts;

            name = "Parts to Redirect To.";
            description =
                "How many parts to spread the damage received to.";

            BodyPartsCountToRedirectTo = Config.Bind(
                SelectPartSection, name, 1,
                new ConfigDescription(description,
                new AcceptableValueRange<int>(1, baseParts.Count),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            for (int i = 0; i < baseParts.Count; i++) {
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

        public static int CheckPartsEnabledCount()
        {
            int result = 0;
            foreach (var part in RedirectParts.Values)
                if (part.Value == true)
                    result++;

            return result;
        }
    }
}