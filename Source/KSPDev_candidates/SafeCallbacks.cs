// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to invoke callbacks so that the main flow is not affected.</summary>
public static class SafeCallbacks {
  /// <summary>Executes an action, intercepting any exceptions that it rises.</summary>
  /// <remarks>
  /// The exceptions that the callback may rise are logged as errors, but the flow is not get
  /// interrupted. Such behavior may be handy when calling cleanup methods or notifying multiple
  /// recepients where the failure is not an option.
  /// </remarks>
  /// <param name="fn">The action to exectue.</param>
  public static void Action(Action fn) {
    try {
      fn.Invoke();
    } catch (Exception ex) {
      DebugEx.Error("Callback execution failed: {0}", ex);
    }
  }

  /// <summary>Executes a function, intercepting any exceptions that it rises.</summary>
  /// <remarks>
  /// The exceptions that the callback may rise are logged as errors, but the flow is not get
  /// interrupted. Such behavior may be handy when calling cleanup methods or notifying multiple
  /// recepients where the failure is not an option.
  /// </remarks>
  /// <param name="fn">The function to call.</param>
  /// <param name="failValue">
  /// The value that will be returned in case of the function has failed.
  /// </param>
  /// <returns>The function return value or <paramref name="failValue"/> if it failed.</returns>
  /// <typeparam name="T">The return type.</typeparam>
  public static T Func<T>(Func<T> fn, T failValue) {
    try {
      return fn.Invoke();
    } catch (Exception ex) {
      DebugEx.Error("Callback execution failed: {0}", ex);
    }
    return failValue;
  }
}

}  // namespace
