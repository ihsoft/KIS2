// Kerbal Development tools for Unity scripts.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

// ReSharper disable once CheckNamespace
namespace KSPDev.Unity {

/// <summary>Interface for the scripts that need to react on Unity control changes.</summary>
/// <remarks>
/// The actors that do the change are responsible for calling an appropriate method. The callers
/// decide on which component to call the callbacks, and the call is done via Unity messaging
/// system. I.e. this is a "sugar" interface.
/// </remarks>
public interface IKspDevUnityControlChanged {
  /// <summary>Notifies that the control position or size has changed.</summary>
  /// <seealso cref="UiWindowDragControllerScript"/>
  void ControlUpdated();
}

}
