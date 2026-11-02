using System.Collections.Generic;
using UnityEngine;

namespace CourseIK
{
    /// <summary>Creates a real Transform hierarchy, accepts a draggable end target, and displays solved joint angles.</summary>
    [DisallowMultipleComponent]
    public sealed class IKChainDemo : MonoBehaviour
    {
        public enum Algorithm { CCD, FABRIK }

        [Header("Chain (N rotating joints = N links + one end point)")]
        [Range(2, 20)] public int jointCount = 5;
        [Range(0.5f, 2f)] public float linkLength = 1f;
        public Algorithm algorithm = Algorithm.CCD;
        [Range(1, 300)] public int maxIterations = 120;
        [Min(0.0001f)] public float tolerance = 0.002f;
        public bool solveContinuously = true;

        [Header("Scene references (created by IK menu)")]
        public Camera viewCamera;
        public Font chineseFont;
        public Material boneMaterial, jointMaterial, rootMaterial, tipMaterial;
        public Material targetMaterial, gridMaterial, rangeMaterial;
        public Transform target;
        [SerializeField] private Transform[] joints;
        [SerializeField] private Transform endEffector;
        [SerializeField] private Transform generated;
        [SerializeField] private Vector2 rootAnchor;

        private Vector2[] positions;
        private float[] lengths;
        private float[] localAngles;
        private int requestedCount;
        private float requestedLength;
        private bool dragging;
        private Vector2 dragOffset;
        private bool showRange = true;
        private LineRenderer rangeCircle, errorLine;
        private IKSolvers.Result result;
        private Vector2 angleScroll;
        private GUIStyle heading, subheading, body, muted, button, box, badge, angleText;
        private int previousWidth, previousHeight;
        private float lastSolveMs;
        private readonly List<Rect> pointLabelRects = new List<Rect>();
        private const float PanelWidth = 320f;
        private static readonly Color Ink = new Color(0.88f, 0.92f, 0.96f);
        private static readonly Color Accent = new Color(0.35f, 0.85f, 0.96f);
        private static readonly Color Pink = new Color(1f, 0.36f, 0.48f);
        private static readonly Color Green = new Color(0.36f, 0.91f, 0.67f);

        public Transform EndEffector { get { return endEffector; } }
        public Transform[] Joints { get { return joints; } }
        public float EndError { get { return endEffector != null && target != null ? Vector2.Distance(endEffector.position, target.position) : 0f; } }
        public Vector2 RootAnchor { get { return rootAnchor; } }
        public float[] LocalAngles { get { return localAngles; } }
        public float RootDrift { get { return joints != null && joints.Length > 0 ? Vector2.Distance(joints[0].position, rootAnchor) : 0f; } }
        public float MaxLengthError
        {
            get
            {
                if (joints == null || endEffector == null) return 0f;
                float error = 0f;
                for (int i = 0; i < joints.Length; i++)
                {
                    Vector3 next = i + 1 < joints.Length ? joints[i + 1].position : endEffector.position;
                    error = Mathf.Max(error, Mathf.Abs(Vector2.Distance(joints[i].position, next) - linkLength));
                }
                return error;
            }
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            requestedCount = jointCount;
            requestedLength = linkLength;
            if (joints == null || joints.Length != jointCount || target == null) Rebuild(jointCount, linkLength);
            else AllocateSolverBuffers();
            UpdateCamera();
            Solve();
        }

        private void AllocateSolverBuffers()
        {
            positions = new Vector2[jointCount + 1];
            lengths = new float[jointCount];
            localAngles = new float[jointCount];
            for (int i = 0; i < jointCount; i++) lengths[i] = linkLength;
            requestedCount = jointCount;
            requestedLength = linkLength;
        }

        public void Rebuild(int count, float length)
        {
            jointCount = Mathf.Clamp(count, 2, 20);
            linkLength = Mathf.Clamp(length, 0.5f, 2f);
            dragging = false;
            if (generated != null)
            {
                generated.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generated.gameObject);
                else DestroyImmediate(generated.gameObject);
            }
            generated = new GameObject("Generated_IK_Chain").transform;
            generated.SetParent(transform, false);
            rootAnchor = Vector2.zero;
            joints = new Transform[jointCount];
            Transform parent = generated;
            for (int i = 0; i < jointCount; i++)
            {
                Transform pivot = new GameObject(i == 0 ? "J0_Root_FIXED_POSITION" : "J" + i + "_Joint").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = i == 0 ? Vector3.zero : Vector3.right * linkLength;
                pivot.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 12f : (i % 2 == 0 ? 18f : -10f));
                joints[i] = pivot;
                Primitive("Joint_Marker", PrimitiveType.Sphere, pivot, new Vector3(0f, 0f, -0.08f),
                    Vector3.one * 0.24f, i == 0 ? rootMaterial : jointMaterial);
                Transform rod = Primitive("Link_" + i, PrimitiveType.Cube, pivot,
                    new Vector3(linkLength * 0.5f, 0f, 0.04f), new Vector3(linkLength, 0.12f, 0.12f), boneMaterial);
                parent = pivot;
            }
            endEffector = new GameObject("E_End_Effector").transform;
            endEffector.SetParent(parent, false);
            endEffector.localPosition = Vector3.right * linkLength;
            Primitive("End_Marker", PrimitiveType.Sphere, endEffector, new Vector3(0f, 0f, -0.09f),
                Vector3.one * 0.28f, tipMaterial);

            Primitive("Fixed_Base", PrimitiveType.Cube, generated, new Vector3(0f, -0.25f, 0.1f),
                new Vector3(0.64f, 0.15f, 0.28f), rootMaterial);
            Primitive("Fixed_Stem", PrimitiveType.Cube, generated, new Vector3(0f, -0.1f, 0.12f),
                new Vector3(0.16f, 0.3f, 0.16f), rootMaterial);

            target = new GameObject("Target_DRAG_IN_GAME_VIEW").transform;
            target.SetParent(generated, false);
            float total = jointCount * linkLength;
            target.position = new Vector3(total * 0.54f, total * 0.42f, 0f);
            Circle("Target_Ring", target, 0.24f, 64, targetMaterial, 0.035f, -0.28f);
            Segment("Target_Cross_X", target, new Vector3(-0.34f, 0f, -0.28f), new Vector3(0.34f, 0f, -0.28f), targetMaterial, 0.018f);
            Segment("Target_Cross_Y", target, new Vector3(0f, -0.34f, -0.28f), new Vector3(0f, 0.34f, -0.28f), targetMaterial, 0.018f);
            Transform backdrop = new GameObject("Grid_and_Reach_Reference").transform;
            backdrop.SetParent(generated, false);
            int extent = Mathf.CeilToInt(total * 1.1f);
            for (int i = -extent; i <= extent; i++)
            {
                Segment("Grid_X_" + i, backdrop, new Vector3(-extent, i, 0.5f), new Vector3(extent, i, 0.5f), gridMaterial, 0.008f);
                Segment("Grid_Y_" + i, backdrop, new Vector3(i, -extent, 0.5f), new Vector3(i, extent, 0.5f), gridMaterial, 0.008f);
            }
            rangeCircle = Circle("Maximum_Reach", backdrop, total, 160, rangeMaterial, 0.018f, 0.35f);
            rangeCircle.enabled = showRange;
            errorLine = Segment("End_To_Target_Error", backdrop, Vector3.zero, Vector3.zero, targetMaterial, 0.018f);
            AllocateSolverBuffers();
            UpdateCamera();
            Solve();
        }

        private static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return go.transform;
        }

        private static LineRenderer Segment(string name, Transform parent, Vector3 a, Vector3 b, Material mat, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPositions(new[] { a, b });
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = mat;
            line.numCapVertices = 3;
            return line;
        }

        private static LineRenderer Circle(string name, Transform parent, float radius, int count, Material mat, float width, float z)
        {
            LineRenderer line = Segment(name, parent, Vector3.zero, Vector3.zero, mat, width);
            line.positionCount = count;
            line.loop = true;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z));
            }
            return line;
        }

        private float GuiScale { get { return Mathf.Max(0.25f, Mathf.Min(Screen.width / 1280f, Screen.height / 800f)); } }

        private void UpdateCamera()
        {
            if (viewCamera == null) return;
            float scale = GuiScale;
            viewCamera.rect = new Rect((PanelWidth + 22f) * scale / Screen.width, 60f * scale / Screen.height,
                Mathf.Max(0.1f, 1f - (PanelWidth + 38f) * scale / Screen.width),
                Mathf.Max(0.1f, 1f - 144f * scale / Screen.height));
            viewCamera.transform.position = new Vector3(0f, 0f, -25f);
            viewCamera.transform.rotation = Quaternion.identity;
            viewCamera.orthographicSize = jointCount * linkLength * 1.13f;
            previousWidth = Screen.width;
            previousHeight = Screen.height;
        }

        private void Update()
        {
            if (Screen.width != previousWidth || Screen.height != previousHeight) UpdateCamera();
            if (Input.GetKeyDown(KeyCode.R)) Rebuild(jointCount, linkLength);
            if (Input.GetKeyDown(KeyCode.Space)) solveContinuously = !solveContinuously;
            HandleDrag();
            if (solveContinuously) Solve();
            UpdateErrorLine();
        }

        private void HandleDrag()
        {
            if (viewCamera == null || target == null || endEffector == null) return;
            if (Input.GetMouseButtonDown(0) && viewCamera.pixelRect.Contains(Input.mousePosition))
            {
                Vector2 screenMouse = Input.mousePosition;
                Vector2 endScreen = viewCamera.WorldToScreenPoint(endEffector.position);
                Vector2 targetScreen = viewCamera.WorldToScreenPoint(target.position);
                float hitRadius = Mathf.Max(18f, 28f * GuiScale);
                bool onEnd = Vector2.Distance(screenMouse, endScreen) <= hitRadius;
                bool onTarget = Vector2.Distance(screenMouse, targetScreen) <= hitRadius;
                if (onEnd || onTarget)
                {
                    Vector2 world;
                    if (PointerOnPlane(out world))
                    {
                        if (onEnd && !onTarget) target.position = new Vector3(endEffector.position.x, endEffector.position.y, 0f);
                        dragOffset = (Vector2)target.position - world;
                        dragging = true;
                    }
                }
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) dragging = false;
            if (dragging)
            {
                Vector2 world;
                if (PointerOnPlane(out world))
                {
                    world += dragOffset;
                    target.position = new Vector3(world.x, world.y, 0f);
                }
            }
        }

        private bool PointerOnPlane(out Vector2 world)
        {
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            float enter;
            if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = Vector2.zero;
            return false;
        }

        public void SetTarget(Vector2 point)
        {
            target.position = new Vector3(point.x, point.y, 0f);
            Solve();
        }

        public void Solve(int iterationOverride = -1)
        {
            if (joints == null || joints.Length == 0 || target == null) return;
            if (positions == null || positions.Length != jointCount + 1) AllocateSolverBuffers();
            for (int i = 0; i < jointCount; i++) positions[i] = joints[i].position;
            positions[jointCount] = endEffector.position;
            positions[0] = rootAnchor;
            double start = Time.realtimeSinceStartupAsDouble;
            int limit = iterationOverride > 0 ? iterationOverride : maxIterations;
            result = algorithm == Algorithm.CCD
                ? IKSolvers.CCD(positions, lengths, target.position, limit, tolerance)
                : IKSolvers.FABRIK(positions, lengths, target.position, limit, tolerance);

            // IK positions -> global headings -> parent-relative angles.
            // Child localPosition NEVER changes: fixed length is enforced by the hierarchy itself.
            float previousHeading = 0f;
            joints[0].position = new Vector3(rootAnchor.x, rootAnchor.y, 0f);
            for (int i = 0; i < jointCount; i++)
            {
                Vector2 direction = positions[i + 1] - positions[i];
                float globalHeading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                localAngles[i] = Mathf.DeltaAngle(previousHeading, globalHeading);
                joints[i].localRotation = Quaternion.Euler(0f, 0f, localAngles[i]);
                previousHeading = globalHeading;
            }
            lastSolveMs = (float)((Time.realtimeSinceStartupAsDouble - start) * 1000.0);
            UpdateErrorLine();
        }

        private void UpdateErrorLine()
        {
            if (errorLine == null && generated != null)
                foreach (LineRenderer line in generated.GetComponentsInChildren<LineRenderer>())
                {
                    if (line.name == "End_To_Target_Error") errorLine = line;
                    if (line.name == "Maximum_Reach") rangeCircle = line;
                }
            if (errorLine == null || endEffector == null || target == null) return;
            Vector3 a = endEffector.position, b = target.position;
            a.z = b.z = -0.3f;
            errorLine.SetPosition(0, a);
            errorLine.SetPosition(1, b);
            errorLine.enabled = EndError > tolerance * 2f;
        }

        private void Styles()
        {
            if (body != null) return;
            if (chineseFont != null) GUI.skin.font = chineseFont;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            subheading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            muted = new GUIStyle(body) { fontSize = 14 };
            angleText = new GUIStyle(body) { fontSize = 15 };
            badge = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 0 };
            box = new GUIStyle(GUI.skin.box);
            foreach (GUIStyle style in new[] { heading, subheading, body, angleText, badge }) style.normal.textColor = Ink;
            muted.normal.textColor = new Color(0.61f, 0.69f, 0.76f);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private bool Button(Rect rect, string text, bool selected = false)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = selected ? new Color(0.16f, 0.62f, 0.74f) : new Color(0.27f, 0.34f, 0.43f);
            bool pressed = GUI.Button(rect, text, button);
            GUI.backgroundColor = old;
            return pressed;
        }

        private void OnGUI()
        {
            Styles();
            float scale = GuiScale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            Fill(new Rect(0, 0, width, 78), new Color(0.055f, 0.079f, 0.11f));
            Fill(new Rect(0, 78, PanelWidth, height - 78), new Color(0.075f, 0.10f, 0.14f));
            GUI.Label(new Rect(24, 14, 520, 40), "关节链 · 逆向运动学", heading);
            GUI.Label(new Rect(26, 52, 700, 23), "固定根部位置，拖动末端，让算法计算每个关节的角度", muted);

            GUI.Label(new Rect(22, 96, 280, 28), "01   创建关节链", subheading);
            GUI.Label(new Rect(22, 132, 280, 26), "转动关节数（含根部）:  " + requestedCount, body);
            if (Button(new Rect(22, 164, 38, 30), "−")) requestedCount = Mathf.Max(2, requestedCount - 1);
            requestedCount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(74, 175, 165, 20), requestedCount, 2, 20));
            if (Button(new Rect(254, 164, 38, 30), "+")) requestedCount = Mathf.Min(20, requestedCount + 1);
            GUI.Label(new Rect(22, 205, 280, 24), "每段长度: " + requestedLength.ToString("F1") + "  单位", body);
            requestedLength = Mathf.Round(GUI.HorizontalSlider(new Rect(24, 238, 266, 20), requestedLength, 0.5f, 2f) * 10f) / 10f;
            if (Button(new Rect(22, 267, 270, 36), "创建 / 重建关节链", true)) Rebuild(requestedCount, requestedLength);
            GUI.Label(new Rect(22, 309, 280, 38), "当前 " + jointCount + " 个转动关节 / " + jointCount + " 段骨骼\n另有 1 个末端点 E（不计转动关节）", muted);

            GUI.Label(new Rect(22, 361, 280, 28), "02   选择求解方法", subheading);
            if (Button(new Rect(22, 399, 130, 34), "CCD", algorithm == Algorithm.CCD)) { algorithm = Algorithm.CCD; Solve(); }
            if (Button(new Rect(162, 399, 130, 34), "FABRIK", algorithm == Algorithm.FABRIK)) { algorithm = Algorithm.FABRIK; Solve(); }
            if (Button(new Rect(22, 443, 130, 34), solveContinuously ? "暂停求解" : "继续求解")) solveContinuously = !solveContinuously;
            if (Button(new Rect(162, 443, 130, 34), "单轮迭代")) { solveContinuously = false; Solve(1); }
            GUI.Label(new Rect(22, 488, 280, 26), "03   关节局部角度（°）", subheading);

            if (localAngles != null)
            {
                float scrollHeight = Mathf.Max(80f, height - 610f);
                angleScroll = GUI.BeginScrollView(new Rect(22, 526, 278, scrollHeight), angleScroll,
                    new Rect(0, 0, 246, localAngles.Length * 28f));
                for (int i = 0; i < localAngles.Length; i++)
                {
                    if (i % 2 == 0) Fill(new Rect(0, i * 28f, 246, 27), new Color(0.095f, 0.13f, 0.18f));
                    GUI.Label(new Rect(8, i * 28f + 3, 150, 24), i == 0 ? "J0  根部旋转" : "J" + i, angleText);
                    GUI.Label(new Rect(157, i * 28f + 3, 90, 24), localAngles[i].ToString("+0.00;-0.00;0.00") + "°", angleText);
                }
                GUI.EndScrollView();
            }
            if (Button(new Rect(22, height - 72, 130, 32), "重置姿态  R")) Rebuild(jointCount, linkLength);
            if (Button(new Rect(162, height - 72, 130, 32), showRange ? "隐藏可达圆" : "显示可达圆"))
            {
                showRange = !showRange;
                if (rangeCircle != null) rangeCircle.enabled = showRange;
            }
            GUI.Label(new Rect(22, height - 33, 280, 24), "根部固定位置，允许绕 Z 轴转动", muted);

            float stageX = PanelWidth + 24, stageWidth = width - stageX - 22;
            if (joints != null && target != null)
            {
                bool reachable = Vector2.Distance(rootAnchor, target.position) <= jointCount * linkLength + tolerance;
                Color statusColor = !reachable ? Pink : (EndError <= tolerance * 1.1f ? Green : Accent);
                string status = !reachable ? "目标超出链长：已伸直到最近位置" : EndError <= tolerance * 1.1f ? "已收敛" : "正在迭代";
                GUI.color = statusColor;
                GUI.Label(new Rect(stageX + 14, 96, stageWidth - 28, 28), status, subheading);
                GUI.color = Color.white;
                GUI.Label(new Rect(stageX + 14, 129, stageWidth - 28, 28),
                    "末端误差 " + EndError.ToString("F4") + "    |    本帧迭代 " + result.iterations + "    |    " + lastSolveMs.ToString("F2") + " ms", muted);
                GUI.Label(new Rect(stageX + 14, 158, stageWidth - 28, 28),
                    "根部偏移 " + RootDrift.ToString("F5") + "    |    最大骨长误差 " + MaxLengthError.ToString("F5"), muted);
                pointLabelRects.Clear();
                DrawPointLabel(joints[0].position, "J0 · 固定", Green, scale, -35f);
                DrawPointLabel(endEffector.position, "E · 末端", Ink, scale, 18f);
                if (EndError > 0.15f) DrawPointLabel(target.position, "目标", Pink, scale, -32f);
                for (int i = 1; i < joints.Length; i++) DrawPointLabel(joints[i].position, "J" + i, Accent, scale, -30f);
            }
            Fill(new Rect(stageX, height - 47, stageWidth, 34), new Color(0.055f, 0.079f, 0.11f));
            GUI.Label(new Rect(stageX + 14, height - 40, stageWidth - 20, 28),
                "鼠标左键拖动白色末端 / 红色目标   ·   Space 暂停 / 继续   ·   可达圆半径 = 总骨长", muted);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawPointLabel(Vector3 world, string text, Color color, float scale, float yOffset)
        {
            if (viewCamera == null) return;
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            if (!viewCamera.pixelRect.Contains(screen)) return;
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale;
            Rect rect = new Rect(x - 48f, y + yOffset, 96f, 24f);
            bool fits = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                rect.y = y + yOffset + (attempt == 0 ? 0f : (attempt % 2 == 1 ? -1f : 1f) * ((attempt + 1) / 2) * 27f);
                bool overlaps = rect.y < 190f || rect.yMax > Screen.height / scale - 60f;
                foreach (Rect existing in pointLabelRects) if (rect.Overlaps(existing)) overlaps = true;
                if (!overlaps) { fits = true; break; }
            }
            if (!fits) return;
            pointLabelRects.Add(rect);
            Fill(rect, new Color(0.045f, 0.065f, 0.09f, 0.84f));
            GUI.color = color;
            GUI.Label(rect, text, badge);
            GUI.color = Color.white;
        }
    }
}
