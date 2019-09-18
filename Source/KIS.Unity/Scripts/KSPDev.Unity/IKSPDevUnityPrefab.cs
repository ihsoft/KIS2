// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

namespace KSPDev.Unity {

/// <summary>
/// Interface for the Unity scripts that need pre-processing before a game prefab is created.
/// </summary>
/// <remarks>
/// The code that loads dynamic prefabs is responsible to call these methods. The call may be made
/// via reflections or Unity messaging, so this interface is a "sugar" interface.
/// <para>KSPDev calls the init method via reflections.</para>
/// </remarks>
/// <seealso cref="UIPrefabBaseScript"/>
public interface IKSPDevUnityPrefab {
  /// <summary>Called by the game on load or by Unity Edtior on the first object start.</summary>
  /// <remarks>
  /// This method should register the instance as a prefab in the
  /// <see cref="UnityPrefabController"/>. In a trivial case, the instance is simply passed to the
  /// controller "as-is". In more advanced use-cases, the instance may be modified prior to the
  /// registration. The final state of the object after this call will be used when making a new
  /// instance from the prefab.
  /// <para>
  /// When this method is called, all the other components in the asset's hierarchy are already
  /// created, but not all of them may be started.
  /// </para>
  /// <para>
  /// Avoid putting advanced logic here. In general, the prefab initialization only deals with
  /// showing/hiding objects and setting default values to the dynamic controls.
  /// </para>
  /// <para>
  /// This method is called on all components in the hierarchy, walking from the top-most parent
  /// down to the bottom-most child.
  /// </para>
  /// </remarks>
  /// <returns>
  /// <c>true</c> if a new prefab has been registered. This value is intended for the decscendants
  /// so that know if their logic should kick in. 
  /// </returns>
  /// <seealso cref="UnityPrefabController.RegisterPrefab"/>
  /// <seealso cref="UnityPrefabController.CreateInstance"/>
  bool InitPrefab();
}

}
