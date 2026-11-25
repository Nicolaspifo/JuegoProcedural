using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compila el APK de Android y el proyecto de Xcode de iOS con las escenas del build.
public static class MobileBuild
{
    private const string PackageId = "com.apuntademachete.juego";

    private static string[] Scenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

    private static void Common()
    {
        PlayerSettings.companyName = "A Punta de Machete";
        PlayerSettings.productName = "A punta de machete";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageId);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
    }

    public static void Android()
    {
        Common();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;   // APK para instalar directo
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        Run(BuildTarget.Android, System.Environment.GetEnvironmentVariable("BUILD_OUT"));
    }

    public static void IOS()
    {
        Common();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        Run(BuildTarget.iOS, System.Environment.GetEnvironmentVariable("BUILD_OUT"));
    }

    public static void Web()
    {
        Common();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        // Sin comprimir: Firebase Hosting ya sirve los archivos con gzip/brotli.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        Run(BuildTarget.WebGL, System.Environment.GetEnvironmentVariable("BUILD_OUT"));
    }

    private static void Run(BuildTarget target, string path)
    {
        BuildReport r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        });
        Debug.Log($"[MobileBuild] {target}: {r.summary.result} errores={r.summary.totalErrors} avisos={r.summary.totalWarnings} tamaño={r.summary.totalSize / (1024 * 1024)} MB");
        foreach (var step in r.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception) Debug.Log($"[MobileBuild] ERROR: {m.content}");
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
