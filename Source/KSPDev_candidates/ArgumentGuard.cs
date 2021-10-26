// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;

namespace KSPDev.ProcessingUtils {

public static class ArgumentGuard2 {
  /// <summary>Throws if collection is empty.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void HasElements<T>(ICollection<T> arg, string argName, string message = null, object context = null) {
    if (arg.Count == 0) {
      var excMsg = message != null
          ? $"Collection {argName} is expected to be not empty: {message}"
          : $"Collection {argName} is expected to be not empty";
      throw new ArgumentOutOfRangeException(argName, arg, Preconditions.MakeContextError(context, excMsg));
    }
  }
}

}  // namespace
