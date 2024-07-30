using System.Text;
using UnityEngine;

namespace Solarint.GrenadeIndicator
{
    public sealed class GUIObject
    {
        public Vector3 WorldPos;
        public string Text;
        public GUIStyle Style;
        public float Scale = 1;
        public StringBuilder StringBuilder = new StringBuilder();
        public bool Enabled = true;
    }
}