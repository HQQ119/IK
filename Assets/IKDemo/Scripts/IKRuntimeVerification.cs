using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CourseIK
{
    /// <summary>Only runs when explicitly requested by the IK menu or the -ikSmokeTest launch argument.</summary>
    public sealed class IKRuntimeVerification : MonoBehaviour
    {
        [Serializable] private sealed class LiveState
        {
            public int width, height, jointCount;
            public string algorithm;
            public bool solving;
            public float error, rootDrift, lengthError;
            public Vector2 target, end, targetScreen, endScreen;
        }
        private float nextTelemetry;

        private void Update()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, "Verification");
            if (Time.unscaledTime < nextTelemetry || !File.Exists(Path.Combine(folder, "watch-input"))) return;
            nextTelemetry = Time.unscaledTime + 0.25f;
            var demo = GetComponent<IKChainDemo>();
            var state = new LiveState { width = Screen.width, height = Screen.height, jointCount = demo.jointCount,
                algorithm = demo.algorithm.ToString(), solving = demo.solveContinuously, error = demo.EndError,
                rootDrift = demo.RootDrift, lengthError = demo.MaxLengthError,
                target = demo.target.position, end = demo.EndEffector.position,
                targetScreen = demo.viewCamera.WorldToScreenPoint(demo.target.position),
                endScreen = demo.viewCamera.WorldToScreenPoint(demo.EndEffector.position) };
            File.WriteAllText(Path.Combine(folder, "live-state.json"), JsonUtility.ToJson(state, true));
        }

        [Serializable] private sealed class Case
        {
            public string name;
            public bool passed;
            public float error, rootDrift, lengthError;
            public int joints;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public int checks, failures;
            public List<Case> results = new List<Case>();
        }

        private IEnumerator Start()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
#if UNITY_EDITOR
            string folder = Path.Combine(root, "Verification");
#else
            string folder = Path.Combine(root, "Verification");
#endif
            bool cli = Array.IndexOf(Environment.GetCommandLineArgs(), "-ikSmokeTest") >= 0;
            string marker = Path.Combine(folder, "run-playmode-check");
            if (!cli && !File.Exists(marker)) yield break;
            if (File.Exists(marker)) File.Delete(marker);
            Directory.CreateDirectory(folder);
            var demo = GetComponent<IKChainDemo>();
            var report = new Report { unityVersion = Application.unityVersion };
            yield return null;
            foreach (int count in new[] { 2, 5, 20 })
            foreach (IKChainDemo.Algorithm algorithm in new[] { IKChainDemo.Algorithm.CCD, IKChainDemo.Algorithm.FABRIK })
            {
                demo.algorithm = algorithm;
                demo.Rebuild(count, 1f);
                Vector2[] targets = { new Vector2(0.5f, 0.45f) * count, new Vector2(-0.4f, 0.35f) * count,
                    Vector2.right * (count + 1f), Vector2.right * (count * 0.45f), Vector2.zero };
                string[] names = { "reachable", "opposite_side", "unreachable", "inward_recovery", "at_root" };
                for (int t = 0; t < targets.Length; t++)
                {
                    demo.SetTarget(targets[t]);
                    for (int frame = 0; frame < 60 && demo.EndError > demo.tolerance; frame++) yield return null;
                    float expected = Mathf.Max(0f, targets[t].magnitude - count);
                    bool passed = Mathf.Abs(demo.EndError - expected) < 0.004f && demo.RootDrift < 0.00001f && demo.MaxLengthError < 0.0002f;
                    report.results.Add(new Case { name = algorithm + "_" + names[t], joints = count, passed = passed,
                        error = demo.EndError, rootDrift = demo.RootDrift, lengthError = demo.MaxLengthError });
                    report.checks++;
                    if (!passed) report.failures++;
                }
            }
            demo.algorithm = IKChainDemo.Algorithm.CCD;
            demo.Rebuild(5, 1f);
            demo.SetTarget(new Vector2(2.7f, 2.1f));
            for (int i = 0; i < 6; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "IK_Demo_reachable.png"));
            yield return null;
            demo.SetTarget(new Vector2(5.4f, 0.7f));
            for (int i = 0; i < 5; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "IK_Demo_unreachable.png"));
            yield return null;
            demo.Rebuild(5, 1f);
            demo.SetTarget(new Vector2(2.7f, 2.1f));
            File.WriteAllText(Path.Combine(folder, "playmode-tests.json"), JsonUtility.ToJson(report, true));
            Debug.Log("IK_PLAYTEST: " + report.checks + " checks, " + report.failures + " failures.");
            // Leave the working demonstration visible, with the default chain restored.
        }
    }
}
