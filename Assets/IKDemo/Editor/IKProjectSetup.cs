using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CourseIK.EditorTools
{
    /// <summary>Builds a saved scene; no manual component or Inspector reference assignment is required.</summary>
    [InitializeOnLoad]
    public static class IKProjectSetup
    {
        private const string ScenePath = "Assets/IKDemo/Scenes/IK_Demo.unity";
        private static double nextPoll;
        private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static IKProjectSetup()
        {
            EditorApplication.delayCall += AutoCreate;
            EditorApplication.update += PollCommand;
        }

        private static void AutoCreate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AutoCreate;
                return;
            }
            if (!File.Exists(ScenePath)) CreateScene();
        }

        [MenuItem("IK/1 - Create or open demo scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Directory.CreateDirectory("Assets/IKDemo/Scenes");
            Directory.CreateDirectory("Assets/IKDemo/Materials");
            AssetDatabase.Refresh();
            // Create additively so an existing scene is never overwritten or silently discarded.
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.065f, 0.09f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraGO.AddComponent<AudioListener>();
            var demo = new GameObject("IK_Demo_Controller").AddComponent<IKChainDemo>();
            demo.viewCamera = camera;
            demo.chineseFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/IKDemo/Fonts/simhei.ttf");
            demo.boneMaterial = MakeMaterial("Bone", new Color(0.76f, 0.85f, 0.93f));
            demo.jointMaterial = MakeMaterial("Joint", new Color(0.25f, 0.78f, 0.94f));
            demo.rootMaterial = MakeMaterial("Root", new Color(0.27f, 0.83f, 0.58f));
            demo.tipMaterial = MakeMaterial("EndEffector", new Color(1f, 0.9f, 0.66f));
            demo.targetMaterial = MakeMaterial("Target", new Color(1f, 0.31f, 0.45f));
            demo.gridMaterial = MakeMaterial("Grid", new Color(0.078f, 0.11f, 0.15f));
            demo.rangeMaterial = MakeMaterial("Reach", new Color(0.13f, 0.24f, 0.28f));
            demo.Rebuild(5, 1f);
            demo.gameObject.AddComponent<IKRuntimeVerification>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (previous.IsValid() && previous.path == "Assets/Scenes/SampleScene.unity" && !previous.isDirty)
                EditorSceneManager.CloseScene(previous, true);
            Selection.activeGameObject = demo.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(12f, 12f, 1f)), false);
            AssetDatabase.SaveAssets();
            Debug.Log("IK_READY: " + ScenePath);
            RunVerification();
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = "Assets/IKDemo/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Unlit/Color")) { name = name, color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        [Serializable] private sealed class Check
        {
            public string algorithm;
            public int joints;
            public string scenario;
            public bool passed;
            public float error, rootDrift, lengthError;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public int checks;
            public int failures;
            public List<Check> results = new List<Check>();
        }

        [MenuItem("IK/2 - Verify IK mathematics and scene")]
        public static void RunVerification()
        {
            var report = new Report { unityVersion = Application.unityVersion };
            foreach (int count in new[] { 2, 5, 10, 20 })
            foreach (bool fabrik in new[] { false, true })
            {
                float[] lengths = new float[count];
                Vector2[] p = new Vector2[count + 1];
                Vector2 root = new Vector2(1.25f, -0.75f);
                for (int i = 0; i < count; i++) lengths[i] = 1f;
                var targets = new[] {
                    new Vector2(0.6f, 0.4f) * count,
                    new Vector2(-0.4f, 0.5f) * count,
                    new Vector2(-0.3f, -0.45f) * count,
                    Vector2.zero,
                    Vector2.right * (count + 2f),
                    Vector2.right * (count * 0.45f),
                    Vector2.up * (count * 0.7f),
                    Vector2.left * count
                };
                string[] names = { "reachable_Q1", "reachable_Q2", "reachable_Q3", "target_at_root", "unreachable", "inward_after_stretch", "upwards", "reach_boundary" };
                p[0] = root;
                for (int i = 0; i < count; i++)
                {
                    float a = (12f + i * 9f) * Mathf.Deg2Rad;
                    p[i + 1] = p[i] + new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                }
                for (int t = 0; t < targets.Length; t++)
                {
                    Vector2 target = root + targets[t];
                    IKSolvers.Result r = new IKSolvers.Result();
                    // The live application also continues from its last pose over successive frames.
                    for (int frame = 0; frame < 60; frame++)
                    {
                        r = fabrik ? IKSolvers.FABRIK(p, lengths, target, 120, 0.002f) : IKSolvers.CCD(p, lengths, target, 120, 0.002f);
                        if (r.error <= 0.002f || !r.reachable) break;
                    }
                    float lengthError = 0f;
                    for (int i = 0; i < count; i++) lengthError = Mathf.Max(lengthError, Mathf.Abs(Vector2.Distance(p[i], p[i + 1]) - 1f));
                    float expectedError = Mathf.Max(0f, targets[t].magnitude - count);
                    bool passed = !float.IsNaN(r.error) && Mathf.Abs(r.error - expectedError) <= 0.003f
                        && Vector2.Distance(p[0], root) < 0.00001f && lengthError < 0.001f;
                    report.results.Add(new Check { algorithm = fabrik ? "FABRIK" : "CCD", joints = count, scenario = names[t],
                        passed = passed, error = r.error, rootDrift = Vector2.Distance(p[0], root), lengthError = lengthError });
                    report.checks++;
                    if (!passed) report.failures++;
                }
            }
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Verification"));
            File.WriteAllText(Path.Combine(ProjectRoot, "Verification", "solver-tests.json"), JsonUtility.ToJson(report, true));
            Debug.Log("IK_VERIFY: " + report.checks + " checks, " + report.failures + " failures.");
        }

        [MenuItem("IK/3 - Run Play Mode verification")]
        public static void PlayTest()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Verification"));
            File.WriteAllText(Path.Combine(ProjectRoot, "Verification", "run-playmode-check"), "run");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("IK/4 - Build Windows demo")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Build", "IK_Demo"));
            PlayerSettings.productName = "IK - Joint Chain Demo";
            PlayerSettings.companyName = "Course Project";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(ProjectRoot, "Build", "IK_Demo", "IK_Demo.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText(Path.Combine(ProjectRoot, "Verification", "build-result.txt"),
                result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nBytes: " + result.summary.totalSize);
            Debug.Log("IK_BUILD: " + result.summary.result);
        }

        private static void PollCommand()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating
                ) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            string file = Path.Combine(ProjectRoot, "Verification", "editor-command.txt");
            if (!File.Exists(file)) return;
            string action = File.ReadAllText(file).Trim();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (action == "stop") { File.Delete(file); EditorApplication.isPlaying = false; }
                return;
            }
            File.Delete(file);
            try
            {
                if (action == "verify") RunVerification();
                else if (action == "playtest") PlayTest();
                else if (action == "build") BuildWindows();
                else if (action == "open") CreateScene();
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(ProjectRoot, "Verification", "editor-command-error.txt"), e.ToString());
                Debug.LogException(e);
            }
        }
    }
}
