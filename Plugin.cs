using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace Solarint.GrenadeIndicator
{
    [BepInPlugin("solarint.grenadeIndicator", "Grenade Indicator", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Init(Config);
            new AddIndicatorPatch().Enable();
        }

        private void Update()
        {
        }

        private void OnGUI()
        {
            TrackedGrenade.OnGUI();
            DebugGizmos.OnGUI();
        }

        public static Color GetColor(int mode)
        {
            switch (mode) {
                case 0:
                    return Color.red;

                case 1:
                    return Color.white;

                case 2:
                    return Color.blue;

                case 3:
                    return Color.green;

                case 4:
                    return _randomColor;

                default:
                    return new Color(Settings.CustomRed.Value, Settings.CustomGreen.Value, Settings.CustomBlue.Value);
            }
        }

        private static float _randomFloat => Random.Range(0.33f, 1f);
        private static Color _randomColor => new Color(_randomFloat, _randomFloat, _randomFloat);
    }

    public class GrenadeIndicatorComponent : MonoBehaviour
    {
        private const float MAX_GRENADE_TRACK_DIST = 125f;
        private Camera _camera;

        private void Start()
        {
            GameWorld.OnDispose += Dispose;
        }

        private void Update()
        {
            if (_subscribed) {
                return;
            }
            var botEvent = Singleton<BotEventHandler>.Instance;
            if (botEvent != null) {
                botEvent.OnGrenadeThrow += grenadeThrown;
                _subscribed = true;
            }
        }

        public void Dispose()
        {
            GameWorld.OnDispose -= Dispose;
            var botEvent = Singleton<BotEventHandler>.Instance;
            if (botEvent != null) {
                botEvent.OnGrenadeThrow -= grenadeThrown;
            }

            foreach (var tracker in _grenades.Values) {
                var grenade = tracker?.Grenade;
                if (grenade != null) {
                    grenade.DestroyEvent -= removeGrenade;
                }
                tracker?.Dispose();
            }

            _grenades.Clear();
        }

        private void grenadeThrown(Grenade grenade, Vector3 position, Vector3 force, float mass)
        {
            if (grenade == null) {
                return;
            }

            if (!Settings.ModEnabled.Value) {
                return;
            }
            if (_camera == null) {
                _camera = Camera.main;
            }
            if ((position - _camera.transform.position).sqrMagnitude > MAX_GRENADE_TRACK_DIST * MAX_GRENADE_TRACK_DIST) {
                return;
            }
            grenade.DestroyEvent += removeGrenade;
            _grenades.Add(grenade.Id, grenade.gameObject.AddComponent<TrackedGrenade>());
        }

        private void removeGrenade(Throwable grenade)
        {
            if (grenade == null) {
                return;
            }
            grenade.DestroyEvent -= removeGrenade;
            if (_grenades.TryGetValue(grenade.Id, out var indicator)) {
                indicator.Dispose();
                _grenades.Remove(grenade.Id);
            }
        }

        private readonly Dictionary<int, TrackedGrenade> _grenades = new Dictionary<int, TrackedGrenade>();

        private bool _subscribed;
    }

    public class TrackedGrenade : MonoBehaviour
    {
        public static void OnGUI()
        {
            if (DefaultStyle == null) {
                createStyle();
            }
        }

        private static void createStyle()
        {
            DefaultStyle = new GUIStyle(GUI.skin.box) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32
            };
            ApplyToStyle.TextColorAllStates(Color.red, DefaultStyle);
            ApplyToStyle.BackgroundAllStates(null, DefaultStyle);
        }

        private static GUIStyle DefaultStyle;

        private const float MAX_GRENADE_INDICATE_DIST = 40f;

        public Grenade Grenade { get; private set; }
        private float Distance;
        private Vector3 Position;

        private Camera _camera = Camera.main;

        private GUIObject _indicator;

        private void Awake()
        {
            Grenade = this.GetComponent<Grenade>();
            _indicator = DebugGizmos.CreateLabel(Grenade.transform.position, "[!]", DefaultStyle, 1f);

            if (Settings.TrailEnabled.Value) {
                _trailRenderer = this.gameObject.AddComponent<TrailRenderer>();
                _trailRenderer.enabled = true;
                _trailRenderer.emitting = true;
                _trailRenderer.startWidth = Settings.TrailStartSize.Value;
                _trailRenderer.endWidth = Settings.TrailEndSize.Value;
                _trailRenderer.material.color = Plugin.GetColor(Settings.TrailColorMode.Value);
                _trailRenderer.colorGradient.mode = GradientMode.Fixed;
                _trailRenderer.time = Settings.TrailExpireTime.Value;
                _trailRenderer.numCapVertices = 5;
                _trailRenderer.numCornerVertices = 5;
                _trailRenderer.receiveShadows = Settings.TrailShadows.Value;
            }

            //_light = grenade.gameObject.AddComponent<Light>();
            //_light.enabled = true;
            //_light.color = Color.red;
            //_light.type = LightType.Point;
            //_light.range = 25f;
            //_light.intensity = 50f;
        }

        private TrailRenderer _trailRenderer;
        //private Light _light;

        private void Update()
        {
            if (Grenade == null || Grenade.transform == null) return;

            Position = Grenade.transform.position;
            _indicator.WorldPos = Position;
            Vector3 cameraPos = _camera.transform.position;
            Vector3 direction = Position - cameraPos;
            Distance = direction.magnitude;

            bool showIndicator = Distance < MAX_GRENADE_INDICATE_DIST;
            if (showIndicator &&
                Settings.RequireLOS.Value &&
                Physics.Raycast(cameraPos, direction, Distance, LayerMaskClass.HighPolyWithTerrainMaskAI)) {
                showIndicator = false;
            }
            _indicator.Enabled = showIndicator;

            float clamped = Mathf.Clamp(Distance, 0f, MAX_GRENADE_INDICATE_DIST);
            float scaled = 1f - clamped / MAX_GRENADE_INDICATE_DIST;
            scaled = Mathf.Clamp(scaled, 0.1f, 1f);
            _indicator.Scale = scaled * Settings.IndicatorSize.Value;
        }

        public void Dispose()
        {
            DebugGizmos.DestroyLabel(_indicator);
            Destroy(this);
        }
    }

    internal class Settings
    {
        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> RequireLOS;
        public static ConfigEntry<float> IndicatorSize;
        public static ConfigEntry<bool> TrailEnabled;
        public static ConfigEntry<bool> TrailShadows;
        public static ConfigEntry<float> TrailStartSize;
        public static ConfigEntry<float> TrailEndSize;
        public static ConfigEntry<float> TrailExpireTime;
        public static ConfigEntry<int> IndicatorColorMode;
        public static ConfigEntry<int> TrailColorMode;

        public static ConfigEntry<float> CustomRed;
        public static ConfigEntry<float> CustomGreen;
        public static ConfigEntry<float> CustomBlue;

        public static void Init(ConfigFile Config)
        {
            const string GeneralSectionTitle = "General";

            int optionCount = 0;

            ModEnabled = Config.Bind(
                GeneralSectionTitle, "Enable Indicator", true,
                new ConfigDescription(string.Empty, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            RequireLOS = Config.Bind(
                GeneralSectionTitle, "Require Line of Sight", false,
                new ConfigDescription(string.Empty, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            IndicatorSize = Config.Bind(
                GeneralSectionTitle, "Indicator Size", 1f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0.25f, 5f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailEnabled = Config.Bind(
                GeneralSectionTitle, "Draw Trail", false,
                new ConfigDescription(string.Empty, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailShadows = Config.Bind(
                GeneralSectionTitle, "Draw Shadows on Trail", false,
                new ConfigDescription(string.Empty, null,
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailStartSize = Config.Bind(
                GeneralSectionTitle, "Trail Start Size", 0.075f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0.01f, 0.2f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailEndSize = Config.Bind(
                GeneralSectionTitle, "Trail End Size", 0.001f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0.001f, 0.2f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailExpireTime = Config.Bind(
                GeneralSectionTitle, "Trail Expire Time", 0.8f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0.2f, 5f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            IndicatorColorMode = Config.Bind(
                GeneralSectionTitle, "Indicator Color Mode", 0,
                new ConfigDescription("0 = red, 1 = white, 2 = blue, 3 = green, 4 = random, 5 = custom",
                new AcceptableValueRange<int>(0, 5),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            TrailColorMode = Config.Bind(
                GeneralSectionTitle, "Trail Color Mode", 0,
                new ConfigDescription("0 = red, 1 = white, 2 = blue, 3 = green, 4 = random, 5 = custom",
                new AcceptableValueRange<int>(0, 5),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            CustomRed = Config.Bind(
                GeneralSectionTitle, "Custom R", 1f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            CustomGreen = Config.Bind(
                GeneralSectionTitle, "Custom G", 1f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));

            CustomBlue = Config.Bind(
                GeneralSectionTitle, "Custom B", 1f,
                new ConfigDescription(string.Empty,
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = optionCount-- }
                ));
        }
    }
}