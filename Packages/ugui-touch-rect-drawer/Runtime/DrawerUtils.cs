using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace UGUIRaycastDrawer
{
    public static class DrawerUtils
    {
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly List<Vector2> TouchPositions = new List<Vector2>();

        public static void SetTouchRectDrawer(bool isEnabled)
        {
            var drawer = FindObjectOfType<TouchRectDrawer>();
            if (drawer == null) return;
            drawer.enabled = isEnabled;
        }

        public static void SetTouchResultDrawer(bool drawTouchRect, bool dumpTouchName)
        {
            var drawer = FindObjectOfType<TouchResultDrawer>();
            if (drawer == null) return;
            drawer.DrawTouchRect = drawTouchRect;
            drawer.DumpTouchName = dumpTouchName;
        }

        private static T FindObjectOfType<T>() where T : Behaviour
        {
            var drawer = Object.FindObjectOfType<T>();
            if (drawer == null) Debug.LogError($"{typeof(T).Name} not found.");
            return drawer;
        }

        internal static IReadOnlyList<Vector2> GetTouchPositions()
        {
#if ENABLE_INPUT_SYSTEM
            if (EventSystem.current == null) return System.Array.Empty<Vector2>();
            if (EventSystem.current.currentInputModule is InputSystemUIInputModule)
            {
                return GetTouchPositionsFromInputSystem();
            }
            else
            {
                return GetTouchPositionsFromInputManager();
            }
#else
            // Input Systemが有効でない時はInput Managerへ
            return GetTouchPositionsFromInputManager();
#endif
        }

        private static IReadOnlyList<Vector2> GetTouchPositionsFromInputSystem()
        {
            TouchPositions.Clear();
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) TouchPositions.Add(Mouse.current.position.ReadValue());
            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (var i = 0; i < touches.Count; i++)
                {
                    if (touches[i] == null) continue;
                    if (touches[i].isInProgress == false) continue;
                    TouchPositions.Add(touches[i].ReadValue().position);
                }
            }
#endif
            return TouchPositions;
        }

        private static IReadOnlyList<Vector2> GetTouchPositionsFromInputManager()
        {
            TouchPositions.Clear();
            TouchPositions.Add(Input.mousePosition);
            for (var i = 0; i < Input.touchCount; i++)
            {
                TouchPositions.Add(Input.GetTouch(i).position);
            }

            return TouchPositions;
        }

        private static Vector4 GetPadding(Graphic graphic)
        {
#if UNITY_2020_1_OR_NEWER
            return graphic.raycastPadding;
#else
            return Vector4.zero;
#endif
        }

        /// <summary>
        /// RaycastPadding込みのcornerを取得する
        /// </summary>
        internal static Vector3[] GetWorldRaycastCorners(Graphic graphic)
        {
            var rt = graphic.rectTransform;
            rt.GetLocalCorners(Corners);
            var padding = GetPadding(graphic);
            var l = padding.x;
            var r = -padding.z;
            var b = padding.y;
            var t = -padding.w;
            // lb
            Corners[0].x += l;
            Corners[0].y += b;
            // lt
            Corners[1].x += l;
            Corners[1].y += t;
            // rt
            Corners[2].x += r;
            Corners[2].y += t;
            // rb
            Corners[3].x += r;
            Corners[3].y += b;
            var localToWorldMatrix = rt.localToWorldMatrix;
            for (var i = 0; i < Corners.Length; i++)
            {
                Corners[i] = localToWorldMatrix.MultiplyPoint(Corners[i]);
            }

            return Corners;
        }
    }
}
