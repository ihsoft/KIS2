// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace KSPDev.ConfigUtils {

/// <summary>Various methods to deal with the config nodes of the parts.</summary>
public static class PartNodeUtils2 {
  /// <summary>Extracts a module config node from the part config.</summary>
  /// <param name="partNode">
  /// The part's config. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <param name="moduleName">The name of the module to extract.</param>
  /// <returns>The module node or <c>null</c> if not found.</returns>
  public static ConfigNode GetModuleNode(ConfigNode partNode, string moduleName) {
    var res = GetModuleNodes(partNode, moduleName);
    return res.Length > 0 ? res[0] : null;
  }

  /// <summary>Extracts a module config node from the part config.</summary>
  /// <param name="partNode">
  /// The part's config. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The module node or <c>null</c> if not found.</returns>
  /// <typeparam name="T">The type of the module to get node for.</typeparam>
  public static ConfigNode GetModuleNode<T>(ConfigNode partNode) {
    return GetModuleNode(partNode, typeof(T).Name);
  }

  /// <summary>Extracts all module config nodes from the part config.</summary>
  /// <param name="partNode">
  /// The part's config. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <param name="moduleName">The name of the module to extract.</param>
  /// <returns>The array of found module nodes.</returns>
  public static ConfigNode[] GetModuleNodes(ConfigNode partNode, string moduleName) {
    if (partNode.HasNode("PART")) {
      partNode = partNode.GetNode("PART");
    }
    return partNode.GetNodes("MODULE")
        .Where(m => m.GetValue("name") == moduleName)
        .ToArray();
  }

  /// <summary>Extracts all module config nodes from the part config.</summary>
  /// <param name="partNode">
  /// The part's config. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The array of found module nodes.</returns>
  /// <typeparam name="T">The type of the module to get node for.</typeparam>
  public static ConfigNode[] GetModuleNodes<T>(ConfigNode partNode) {
    return GetModuleNodes(partNode, typeof(T).Name);
  }

  /// <summary>Returns a "real part" config node.</summary>
  /// <param name="partConfig">
  /// The part's config. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The actual node that has the part's settings.</returns>
  public static ConfigNode GetPartNode(ConfigNode partConfig) {
    if (partConfig.HasNode("PART")) {
      partConfig = partConfig.GetNode("PART");
    }
    return partConfig;
  }

  /// <summary>Efficiently verifies if the nodes are equal.</summary>
  /// <param name="left">The first node.</param>
  /// <param name="right">The second node.</param>
  /// <param name="nodeNameCheckFn">
  /// The filter to apply on the top level nodes. If not provided, then all subnodes from <paramref name="left"/> and
  /// <paramref name="right"/> will be recursively checked. This check is not performed in the subnodes.
  /// </param>
  /// <returns><c>true</c> if the nodes are equal.</returns>
  public static bool CompareNodes(ConfigNode left, ConfigNode right, Func<string, bool> nodeNameCheckFn = null) {
    var leftValues = left.values;
    var rightValues = right.values;
    var leftNodes = left.nodes;
    var rightNodes = right.nodes;
    if (leftValues.Count != rightValues.Count || leftNodes.Count != rightNodes.Count) {
      return false;
    }
    for (var i = leftValues.Count - 1; i >= 0; i--) {
      if (leftValues[i].name != rightValues[i].name || leftValues[i].value != rightValues[i].value) {
        return false;
      }
    }
    for (var i = leftNodes.Count - 1; i >= 0; i--) {
      if (nodeNameCheckFn != null && !nodeNameCheckFn.Invoke(leftNodes[i].name)) {
        continue;
      }
      if (leftNodes[i].name != rightNodes[i].name || !CompareNodes(leftNodes[i], rightNodes[i], null)) {
        return false;
      }
    }
    return true;
  }
}

}  // namespace
