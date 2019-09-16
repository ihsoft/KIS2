using UnityEditor;
using UnityEngine;

public class ExportAssetBundles {
  [MenuItem("Assets/Build AssetBundle")]
  static void ExportResource()
  {
    // Build for Windows platform.
    BuildPipeline.BuildAssetBundles(
        Application.streamingAssetsPath,
        BuildAssetBundleOptions.None,
        BuildTarget.StandaloneWindows64);

    // Refresh the Project folder.
    AssetDatabase.Refresh();
  }
}
