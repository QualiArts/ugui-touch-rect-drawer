using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UGUIRaycastDrawer
{
    /// <summary>
    /// RaycastTargetがOnなGraphicに枠をつける
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [ExecuteAlways]
    public class TouchRectDrawer : MaskableGraphic
    {
        private static readonly UIVertex[] Vertices = new UIVertex[4];

        [SerializeField]
        private float _lineWidth = 4f;

        public float LineWidth
        {
            get => _lineWidth;
            set
            {
                if (Mathf.Approximately(_lineWidth, value)) return;
                _lineWidth = value;
                SetVerticesDirty();
            }
        }

        private IReadOnlyList<BaseRaycaster> _raycasters;

        // private List<RaycastTarget> _targets = new List<RaycastTarget>();
        private List<Graphic> _targets = new List<Graphic>();
        private List<Component> _components = new List<Component>();

        // UnityEngine.EventSystems.RaycasterManager.GetRaycastersでmutableなListの参照が帰ってくる
        private static readonly Lazy<MethodInfo> GetRaycasters = new Lazy<MethodInfo>(() => typeof(BaseRaycaster)
            .Assembly
            .GetType("UnityEngine.EventSystems.RaycasterManager")
            .GetMethod("GetRaycasters", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod));

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheRaycasterList();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _targets = null;
            _raycasters = null;
            _components = null;
        }

        private void CacheRaycasterList()
        {
#if UNITY_EDITOR
            // BaseRaycasterの登録がプレイモード中だけっぽい
            if (Application.isPlaying == false)
            {
                _raycasters = FindObjectsOfType<GraphicRaycaster>()
                    .Where(x => x.isActiveAndEnabled)
                    .ToArray();
                return;
            }
#endif
            _raycasters = GetRaycasters.Value.Invoke(null, Array.Empty<object>()) as IReadOnlyList<BaseRaycaster>;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false) CacheRaycasterList();
#endif

            if (_raycasters == null) return;
            _targets.Clear();

            for (var i = 0; i < _raycasters.Count; i++)
            {
                FindTargetGraphics(_raycasters[i] as GraphicRaycaster, _targets);
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_targets == null) return;
            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i].transform == null)
                {
                    _targets.RemoveAt(i);
                    continue;
                }

                AddFrame(vh, _targets[i]);
            }
        }

        /// <summary>
        /// 四角い枠をかく
        /// </summary>
        private void AddFrame(VertexHelper helper, Graphic target)
        {
            var worldCorners = DrawerUtils.GetWorldRaycastCorners(target);

            for (var i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].color = color;
            }

            var rt = rectTransform;
            var cam = canvas.worldCamera;
            for (var i = 0; i < worldCorners.Length; i++)
            {
                if (target.canvas == null) continue;
                var targetCam = target.canvas.worldCamera;
                var start = RectTransformUtility.WorldToScreenPoint(
                    targetCam, worldCorners[i]);
                var end = RectTransformUtility.WorldToScreenPoint(
                    targetCam, worldCorners[(i + 1) % worldCorners.Length]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, start, cam, out start);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, end, cam, out end);
                AddLine(helper, ref start, ref end);
            }
        }

        /// <summary>
        /// 線をかく
        /// </summary>
        private void AddLine(VertexHelper helper, ref Vector2 start, ref Vector2 end)
        {
            var offset = _lineWidth * Mathf.Max(Screen.width, Screen.height) / 1920f / 2f;
            Vertices[0].position = new Vector3(start.x - offset, start.y - offset, 0f);
            Vertices[1].position = new Vector3(end.x - offset, end.y - offset, 0f);
            Vertices[2].position = new Vector3(end.x + offset, end.y + offset, 0f);
            Vertices[3].position = new Vector3(start.x + offset, start.y + offset, 0f);
            helper.AddUIVertexQuad(Vertices);
        }

        private void FindTargetGraphics(GraphicRaycaster raycaster, List<Graphic> targetList)
        {
            if (raycaster == null) return;
            if (raycaster.TryGetComponent<Canvas>(out var cav) == false) return;
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(cav);
            var eventCamera = cav.worldCamera;
            var hasCamera = eventCamera != null;
            var farClip = hasCamera ? eventCamera.farClipPlane : 0f;
            for (var i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                var graphicsTransform = graphic.rectTransform;
                // この辺はGraphicRaycasterから拝借
                if (graphic.raycastTarget == false || graphic.canvasRenderer.cull || graphic.depth == -1) continue;
                if (hasCamera && eventCamera.WorldToScreenPoint(graphicsTransform.position).z > farClip) continue;
                if (IsRaycastTarget(graphicsTransform))
                {
                    targetList.Add(graphic);
                }
            }
        }

        /// <summary>
        /// Graphic.Raycastのうち、Rayを使わない部分の処理を抜粋
        /// </summary>
        private bool IsRaycastTarget(Transform graphicTransform)
        {
            var t = graphicTransform;
            var ignoreParentGroups = false;
            var continueTraversal = true;

            while (t != null)
            {
                _components.Clear();
                t.GetComponents(_components);
                for (var i = 0; i < _components.Count; i++)
                {
                    var cav = _components[i] as Canvas;
                    if (cav != null && cav.overrideSorting) continueTraversal = false;
                    var filter = _components[i] as ICanvasRaycastFilter;
                    if (filter == null) continue;
                    var raycastValid = true;
                    var group = _components[i] as CanvasGroup;
                    if (group != null)
                    {
                        if (ignoreParentGroups == false && group.ignoreParentGroups)
                        {
                            ignoreParentGroups = true;
                            raycastValid = group.blocksRaycasts;
                        }
                        else if (ignoreParentGroups == false)
                        {
                            raycastValid = group.blocksRaycasts;
                        }
                    }

                    if (raycastValid == false)
                    {
                        _components.Clear();
                        return false;
                    }
                }

                t = continueTraversal ? t.parent : null;
            }

            _components.Clear();
            return true;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            color = Color.green;
        }
#endif
    }
}
