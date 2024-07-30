using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace Solarint.GrenadeIndicator
{
    public class DebugGizmos
    {
        static DebugGizmos()
        {
            GameWorld.OnDispose += dispose;
        }

        private static void dispose()
        {
            _labels.Clear();
        }

        public static void OnGUI()
        {
            foreach (var obj in _labels) {
                if (!obj.Enabled) continue;
                string text = obj.Text.IsNullOrEmpty() ? obj.StringBuilder.ToString() : obj.Text;
                OnGUIDrawLabel(obj.WorldPos, text, obj.Style, obj.Scale);
            }
        }

        private static GUIStyle DefaultStyle;

        public static GUIObject CreateLabel(Vector3 worldPos, string text, GUIStyle guiStyle, float scale)
        {
            ApplyToStyle.TextColorAllStates(Plugin.GetColor(Settings.IndicatorColorMode.Value), guiStyle);
            GUIObject obj = new GUIObject { WorldPos = worldPos, Text = text, Style = guiStyle, Scale = scale };
            AddGUIObject(obj);
            return obj;
        }

        public static void AddGUIObject(GUIObject obj)
        {
            if (!_labels.Contains(obj)) {
                _labels.Add(obj);
            }
        }

        public static void DestroyLabel(GUIObject obj)
        {
            _labels.Remove(obj);
        }

        public static void OnGUIDrawLabel(Vector3 worldPos, string text, GUIStyle guiStyle = null, float scale = 1f)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0) {
                return;
            }

            if (guiStyle == null) {
                if (DefaultStyle == null) {
                    DefaultStyle = new GUIStyle(GUI.skin.box);
                    DefaultStyle.alignment = TextAnchor.MiddleLeft;
                    DefaultStyle.fontSize = 20;
                    DefaultStyle.margin = new RectOffset(3, 3, 3, 3);
                    ApplyToStyle.BackgroundAllStates(null, DefaultStyle);
                }
                guiStyle = DefaultStyle;
            }

            int origFontSize = guiStyle.fontSize;
            guiStyle.fontSize = Mathf.RoundToInt(origFontSize * scale);

            GUIContent content = new GUIContent(text);
            float screenScale = GetScreenScale();
            Vector2 guiSize = guiStyle.CalcSize(content);
            float x = (screenPos.x * screenScale) - (guiSize.x / 2);
            float y = Screen.height - ((screenPos.y * screenScale) + guiSize.y);
            Rect rect = new Rect(new Vector2(x, y), guiSize);
            GUI.Label(rect, content, guiStyle);
            guiStyle.fontSize = origFontSize;
        }

        private static readonly List<GUIObject> _labels = new List<GUIObject>();

        private static float GetScreenScale()
        {
            if (_nextCheckScreenTime < Time.time && CameraClass.Instance.SSAA.isActiveAndEnabled) {
                _nextCheckScreenTime = Time.time + 10f;
                _screenScale = (float)CameraClass.Instance.SSAA.GetOutputWidth() / (float)CameraClass.Instance.SSAA.GetInputWidth();
            }
            return _screenScale;
        }

        private static float _screenScale = 1.0f;
        private static float _nextCheckScreenTime;
    }
}