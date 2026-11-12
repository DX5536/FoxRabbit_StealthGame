using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Tools > FoxRabbit > Apply Web Release Settings (itch.io)
/// One click before building for itch.io. Based on Unity's web optimization template, adapted:
///  - Decompression Fallback ON: itch.io does not send the Content-Encoding header Brotli needs.
///  - Exceptions: Explicitly Thrown Only (TimeStopFilter_Manager catches a missing-tag exception).
///  - Managed stripping Medium, not High: Yarn Spinner finds commands by reflection.
/// </summary>
public static class WebOptimizer
{
    [MenuItem("Tools/FoxRabbit/Apply Web Release Settings (itch.io)")]
    public static void Optimize()
    {
        var target = NamedBuildTarget.WebGL;
        PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
        // shortcut: Medium is safe with reflection; try High + a link.xml for Yarn Spinner if the build is too big.
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.stripUnusedMeshComponents = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
#if UNITY_WEBGL
        // Stored in Library/ (per machine, not in git): run this menu on the machine that builds.
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
#endif

        AssetDatabase.SaveAssets(); // without this the values live in memory only and are lost on restart

        Debug.Log("[WebOptimizer] Web release settings saved:\n"
            + $"  il2cppCodeGeneration = {PlayerSettings.GetIl2CppCodeGeneration(target)}\n"
            + $"  managedStrippingLevel = {PlayerSettings.GetManagedStrippingLevel(target)}\n"
            + $"  stripEngineCode = {PlayerSettings.stripEngineCode}\n"
            + $"  stripUnusedMeshComponents = {PlayerSettings.stripUnusedMeshComponents}\n"
            + $"  dataCaching = {PlayerSettings.WebGL.dataCaching}\n"
            + $"  compressionFormat = {PlayerSettings.WebGL.compressionFormat}\n"
            + $"  decompressionFallback = {PlayerSettings.WebGL.decompressionFallback}\n"
            + $"  exceptionSupport = {PlayerSettings.WebGL.exceptionSupport}\n"
            + $"  debugSymbolMode = {PlayerSettings.WebGL.debugSymbolMode}\n"
            + $"  memoryGrowthMode = {PlayerSettings.WebGL.memoryGrowthMode}\n"
#if UNITY_WEBGL
            + $"  codeOptimization = {UnityEditor.WebGL.UserBuildSettings.codeOptimization}"
#else
            + "  codeOptimization = NOT SET (switch the build target to Web, then run this again)"
#endif
        );
    }
}
