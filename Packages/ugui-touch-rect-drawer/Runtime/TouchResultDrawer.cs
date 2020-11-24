using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UGUIRaycastDrawer
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class TouchResultDrawer : MaskableGraphic
    {
        private static readonly UIVertex[] Vertices = new UIVertex[4];

        /// <summary>
        /// raycastallする時のtmp list
        /// </summary>
        private static readonly List<RaycastResult> _tmpHits = new List<RaycastResult>();

        /// <summary>
        /// 各タッチ判定で、一番に触れたもののlist
        /// </summary>
        private readonly List<RaycastResult> _firstHits = new List<RaycastResult>();

        private readonly List<RaycastResult> _prevHits = new List<RaycastResult>();

        [SerializeField]
        private bool _drawTouchRect = true;

        /// <summary>
        /// タッチ判定表示
        /// </summary>
        public bool DrawTouchRect
        {
            get => _drawTouchRect;
            set => _drawTouchRect = value;
        }

        [SerializeField]
        private bool _dumpTouchName;

        /// <summary>
        /// タッチ名ログ出力
        /// </summary>
        public bool DumpTouchName
        {
            get => _dumpTouchName;
            set => _dumpTouchName = value;
        }

        [SerializeField]
        private Color[] _cornerColors =
        {
            new Color(1f, 0f, 0f, 0.7f),
            new Color(0f, 1f, 0f, 0.7f),
            new Color(0f, 1f, 1f, 0.7f),
            new Color(0f, 0f, 1f, 0.7f),
        };

        private PointerEventData _pointer;

        protected override void OnEnable()
        {
            base.OnEnable();
            _pointer = new PointerEventData(EventSystem.current);
        }

        /// <summary>
        /// 現在触っているものを調べる
        /// </summary>
        private void UpdateFirstHits()
        {
            var currentSystem = EventSystem.current;
            if (currentSystem == null) return;

            _firstHits.Clear();
            var touchPositions = DrawerUtils.GetTouchPositions();

            // 各入力座標で衝突するものを列挙
            for (var i = 0; i < touchPositions.Count; i++)
            {
                _pointer.Reset();
                _pointer.position = touchPositions[i];
                _tmpHits.Clear();
                currentSystem.RaycastAll(_pointer, _tmpHits);
                if (_tmpHits.Count > 0)
                {
                    // RaycastAllはソート済みなので先頭決め打ちで問題ないはず
                    _firstHits.Add(_tmpHits[0]);
                }
            }

            _tmpHits.Clear();
        }

        private void Update()
        {
            if (DrawTouchRect == false && DumpTouchName == false) return;

            // 前フレームで触れていたGameObjectを記録
            _prevHits.Clear();
            _prevHits.AddRange(_firstHits);

            // 現在フレームのタッチ判定を更新
            UpdateFirstHits();

            if (DumpTouchName)
            {
                for (var i = 0; i < _firstHits.Count; i++)
                {
                    int prevIndex = -1;
                    for (var k = 0; k < _prevHits.Count; k++)
                    {
                        if (_prevHits[k].gameObject == _firstHits[i].gameObject)
                        {
                            prevIndex = k;
                            break;
                        }
                    }

                    if (prevIndex < 0)
                    {
                        // 現在フレームで新しくタッチされたものがあればchangedしてログに出す
                        Debug.Log($"Touch: {_firstHits[i].gameObject}", _firstHits[i].gameObject);
                    }
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (DrawTouchRect == false) return;
            for (var i = 0; i < _firstHits.Count; i++)
            {
                AddQuad(vh, _firstHits[i]);
            }
        }

        private void AddQuad(VertexHelper helper, RaycastResult result)
        {
            if ((result.gameObject == null) || (result.module == null)) return;
            if (result.gameObject.TryGetComponent(out Graphic graphic) == false) return;
            var corners = DrawerUtils.GetWorldRaycastCorners(graphic);
            if (corners == null) return;
            var baseColor = color;
            var rt = rectTransform;
            var cam = canvas.worldCamera;
            for (var i = 0; i < corners.Length; i++)
            {
                var pos = RectTransformUtility.WorldToScreenPoint(result.module.eventCamera, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, pos, cam, out pos);
                Vertices[i].position = new Vector3(pos.x, pos.y, 0f);
                Vertices[i].color = GetCornerColor(i) * baseColor;
            }

            helper.AddUIVertexQuad(Vertices);
        }

        private Color GetCornerColor(int index)
        {
            if (_cornerColors.Length == 0)
            {
                return new Color(0f, 0f, 0f, 0.8f);
            }

            return _cornerColors[index % _cornerColors.Length];
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
