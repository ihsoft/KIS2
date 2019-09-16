// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

namespace KSPDev.Unity {

/// <summary>Interface for the scripts that need to react on Unity control drag.</summary>
public interface IKSPDevUnityControlMoved {
  /// <summary>
  /// Called each time the control position has changed due to player interaction.
  /// </summary>
  /// <remarks>
  /// This method is called via the Unity messaging system on the same object, as the parent of the
  /// acting component. It's not required to implement this interface to get the notification.
  /// <para>
  /// Note, that if multiple components on the object implement this method, it's not defined in
  /// what order they will be called.
  /// </para>
  /// </remarks>
  /// <seealso cref="UIWindowDragControllerScript"/>
  void ControlMoved();
}

}
