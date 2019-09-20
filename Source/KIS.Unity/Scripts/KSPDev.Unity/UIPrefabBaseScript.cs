// Kerbal Development tools for Unity scripts.
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.Unity;
using System.Collections;
using UnityEngine;

namespace KSPDev.Unity {

/// <summary>
/// Base script of the objects that need prefab registered in <c>KSPDev.Unity</c>.
/// </summary>
/// <remarks>
/// It automatically registers the asset as prefab. It also takes care of the editor case. Either
/// add this script as-is to the target assembly root object, or inherit from it if you need type
/// specification. In the latter case you'll be able to make instances by the final type instead of
/// requesting <see cref="UIPrefabBaseScript"/> with different prefab names.
/// </remarks>
/// <example>
/// <para>
/// Given your script <c>MyType</c> inherits from <c>UIPrefabBaseScript</c>, you can create an
/// object like this:
/// </para>
/// <code>
/// var newObj = UnityPrefabController.CreateInstance&lt;MyType&gt;();
/// </code>
/// <para>
/// If your script is used in more than one prefab, you will have to provide the name as well: 
/// </para>
/// <code>
/// var newObj1 = UnityPrefabController.CreateInstance&lt;MyType&gt;("name1");
/// var newObj2 = UnityPrefabController.CreateInstance&lt;MyType&gt;("name2");
/// </code>
/// </example>
/// <seealso cref="IKSPDevUnityPrefab"/>
public abstract class UIPrefabBaseScript : UIControlBaseScript, IKSPDevUnityPrefab {

  #region Unity serialized fields
  /// <summary>
  /// Tells if a copy of this control needs to be created in Unity Editor runtime for testing. 
  /// </summary>
  /// <remarks>This setting has no effect in the game.</remarks>
  [SerializeField]
  bool makeCopyInEditorRuntime;

  /// <summary>Name under which the prefab is to be registered.</summary>
  /// <seealso cref="UnityPrefabController.RegisterPrefab"/>
  [SerializeField]
  string prefabName = null;
  #endregion

  #region IKSPDevUnityPrefab implemenation
  /// <inheritdoc/>
  public bool isPrefab { get; private set; }

  /// <inheritdoc/>
  public virtual bool InitPrefab() {
    gameObject.SetActive(false);
    return UnityPrefabController.RegisterPrefab(this, prefabName);
  }
  #endregion

  #region MonoBehaviour implementations
  /// <inheritdoc/>
  public virtual void Awake() {
    if (!isPrefab && !UnityPrefabController.IsPrefabRegistered(this, prefabName)) {
      isPrefab = true;
    }
  }

  /// <inheritdoc/>
  public virtual void Start() {
    if (Application.isEditor) {
      EditorStart();
    }
  }
  #endregion

  #region Inheritable methods
  /// <summary>Notifies that the object has been started from Unity Editor in play mode.</summary>
  protected virtual void EditorStart() {
    if (!UnityPrefabController.IsPrefabRegistered(this, prefabName)) {
      InitPrefab();
    }
    if (makeCopyInEditorRuntime) {
      makeCopyInEditorRuntime = false;
      // Create editor's copy after all objects are initialized. For this we need an active game
      // object, but most prefab objects are inactive. So, create a temporary one.
      var host = new GameObject();
      host.SetActive(true);
      host.AddComponent<CoroutineStarter>().StartCoroutine(MakeEditorCopy(host));
    }
  }
  #endregion

  #region Local utility methods
  class CoroutineStarter : MonoBehaviour {
  }

  IEnumerator MakeEditorCopy(Object host) {
    yield return null;  // Wait till the next frame.
    LogInfo("Creating editor demo copy...");
    UnityPrefabController.CreateInstance(GetType(), "EditorCopy-" + prefabName, transform.parent);
    Destroy(host);
  }
  #endregion
}

}  // namespace
