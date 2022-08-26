// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KSPDev.ConfigUtils;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable once IdentifierTypo
namespace KISAPIv2 {

/// <summary>Various methods to deal with the parts configs.</summary>
public class PartNodeUtilsImpl {

  #region API implementation
  /// <summary>Gets scale modifier, applied by TweakScale mod.</summary>
  /// <param name="partNode">The part's persistent state config.</param>
  /// <returns>The scale ratio.</returns>
  public double GetTweakScaleSizeModifier(ConfigNode partNode) {
    var ratio = 1.0;
    var tweakScaleNode = GetTweakScaleModule(partNode);
    if (tweakScaleNode != null) {
      var defaultScale = ConfigAccessor.GetValueByPath<double>(tweakScaleNode, "defaultScale");
      var currentScale = ConfigAccessor.GetValueByPath<double>(tweakScaleNode, "currentScale");
      if (defaultScale.HasValue && currentScale.HasValue) {
        ratio = currentScale.Value / defaultScale.Value;
      } else {
        DebugEx.Error("Bad TweakScale config:\n{0}", tweakScaleNode);
      }
    }
    return ratio;
  }

  /// <summary>Gets <c>TweakScale</c> module config.</summary>
  /// <param name="partNode">
  /// The config to extract the module config from. It can be <c>null</c>.
  /// </param>
  /// <returns>The <c>TweakScale</c> module or <c>null</c>.</returns>
  public ConfigNode GetTweakScaleModule(ConfigNode partNode) {
    return partNode != null ? PartNodeUtils.GetModuleNode(partNode, "TweakScale") : null;
  }

  /// <summary>Captures the part state into a config node and returns it.</summary>
  /// <remarks>
  /// This is not the same as a complete part persistent state. This state only captures the key
  /// module settings. The unimportant settings are dropped altogether.
  /// </remarks>
  /// <param name="part">The part to snapshot. It must be a fully activated part.</param>
  /// <returns>The part's persistent state.</returns>
  public ConfigNode GetConfigNode(Part part) {
    var snapshot = GetProtoPartSnapshot(part);
    var partNode = new ConfigNode("PART");
    snapshot.Save(partNode);

    // Prune unimportant data.
    // ReSharper disable StringLiteralTypo
    partNode.RemoveValues("parent");
    partNode.RemoveValues("position");
    partNode.RemoveValues("rotation");
    partNode.RemoveValues("istg");
    partNode.RemoveValues("dstg");
    partNode.RemoveValues("sqor");
    partNode.RemoveValues("sidx");
    partNode.RemoveValues("attm");
    partNode.RemoveValues("srfN");
    partNode.RemoveValues("attN");
    partNode.RemoveValues("connected");
    partNode.RemoveValues("attached");
    partNode.RemoveValues("flag");
    // ReSharper enable StringLiteralTypo

    partNode.RemoveNodes("ACTIONS");
    partNode.RemoveNodes("EVENTS");
    foreach (var moduleNode in partNode.GetNodes("MODULE")) {
      moduleNode.RemoveNodes("ACTIONS");
      moduleNode.RemoveNodes("EVENTS");
    }

    return partNode;
  }

  /// <summary>Makes a proto part snapshot from the part.</summary>
  /// <remarks>It erases all the vessel related info from the snapshot as well as any crew related info.</remarks>
  /// <param name="part">The part to capture.</param>
  /// <returns>A snapshot for the part's current state.</returns>
  public ProtoPartSnapshot GetProtoPartSnapshot(Part part) {
    DebugEx.Fine("Make a proto part snapshot for: {0}", part);
    MaybeFixPrefabPart(part);

    // Persist the old part's proto state to not affect it after the snapshot.
    var oldProtoPartSnapshot = part.protoPartSnapshot;
    var snapshot = new ProtoPartSnapshot(part, part.vessel != null ? part.vessel.protoVessel : null) {
        // We don't need the vessel relations. 
        attachNodes = new List<AttachNodeSnapshot>(),
        srfAttachNode = new AttachNodeSnapshot("None,-1"),
        symLinks = new List<ProtoPartSnapshot>(),
        symLinkIdxs = new List<int>(),
        state = 0,
        PreFailState = 0,
        attached = false,
    };
    part.protoPartSnapshot = oldProtoPartSnapshot;

    return snapshot;
  }

  /// <summary>Makes a proto part copy.</summary>
  /// <remarks>This method is slow and triggers game events.</remarks>
  /// <param name="srcSnapshot">The snapshot to make copy of.</param>
  /// <returns>A copy of the snapshot.</returns>
  public ProtoPartSnapshot FullProtoPartCopy(ProtoPartSnapshot srcSnapshot) {
    var node = new ConfigNode();
    srcSnapshot.Save(node);
    return GetProtoPartSnapshotFromNode(null, node);
  }

  /// <summary>Returns all the resource on the part.</summary>
  /// <param name="partNode">
  /// The part's config or a persistent state. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The found resources.</returns>
  public ProtoPartResourceSnapshot[] GetResources(ConfigNode partNode) {
    return PartNodeUtils2.GetPartNode(partNode).GetNodes("RESOURCE")
        .Select(n => new ProtoPartResourceSnapshot(n))
        .ToArray();
  }

  /// <summary>Updates the part's resource.</summary>
  /// <param name="partNode">
  /// The part's config or a persistent state. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <param name="name">The name of the resource.</param>
  /// <param name="amount">The new amount or the delta.</param>
  /// <param name="isAmountRelative">
  /// Tells if the amount must be added to the current item's amount instead of simply replacing it.
  /// </param>
  /// <returns>The new amount or <c>null</c> if the resource was not found.</returns>
  /// FIXME: unused
  public double? UpdateResource(ConfigNode partNode, string name, double amount, bool isAmountRelative = false) {
    var node = PartNodeUtils2.GetPartNode(partNode).GetNodes("RESOURCE")
        .FirstOrDefault(r => r.GetValue("name") == name);
    double? setAmount = null;
    if (node != null) {
      setAmount = amount;
      if (isAmountRelative) {
        setAmount += ConfigAccessor.GetValueByPath<double>(node, "amount") ?? 0.0;
      }
      ConfigAccessor.SetValueByPath(node, "amount", setAmount.Value);
    } else {
      DebugEx.Error("Cannot find resource '{0}' in config:\n{1}", name, partNode);
    }
    return setAmount;
  }

  /// <summary>Returns all the science from the part's saved state.</summary>
  /// <param name="partNode">
  /// The persistent part's state. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The found science data.</returns>
  /// FIXME: unused
  public IEnumerable<ScienceData> GetPartScience(ConfigNode partNode) {
    return partNode.GetNodes("MODULE")
        .SelectMany(GetModuleScience)
        .ToArray();
  }

  /// <summary>Returns all the science from the module's saved state.</summary>
  /// <param name="moduleNode">The persistent module's state.</param>
  /// <returns>The found science data.</returns>
  public IEnumerable<ScienceData> GetModuleScience(ConfigNode moduleNode) {
    //FIXME: use utls
    return moduleNode.GetNodes("ScienceData")
        .Select(n => new ScienceData(n))
        .ToArray();
  }

  /// <summary>Makes a part snapshot from the saved part state.</summary>
  /// <param name="refVessel">
  /// The vessel that is an actor. Depending on how this snapshot will be used, the meaning of this argument may be
  /// different. It could be a part "creator" (e.g. a kerbal) or a part "owner" (e.g. an inventory).
  /// </param>
  /// <param name="node">The saved state.</param>
  /// <param name="keepPersistentId">
  /// Tells if the original <c>persistentId</c> from the item config should be preserved. If this setting is not set,
  /// then a new ID will be generated by the game to make sure it's unique.
  /// </param>
  /// <returns>A snapshot for the given state.</returns>
  /// <exception cref="ArgumentException">if the game scene or the reference inventory are not good.</exception>
  public ProtoPartSnapshot GetProtoPartSnapshotFromNode(
      Vessel refVessel, ConfigNode node, bool keepPersistentId = false) {
    ProtoPartSnapshot pPart = null;
    pPart = new ProtoPartSnapshot(node, refVessel != null ? refVessel.protoVessel : null, HighLogic.CurrentGame);

    if (keepPersistentId) {
      var originalPartId = uint.Parse(node.GetValue("persistentId"));
      if (originalPartId != pPart.persistentId) {
        DebugEx.Fine("ProtoPartSnapshot persistentId changed back from {0} to {1}. It's a state snapshot action.",
                     pPart.persistentId, originalPartId);
        pPart.persistentId = originalPartId;
      }
    }

    return pPart;
  }

  /// <summary>Makes a fully populated node fo part based on the snapshot.</summary>
  /// <remarks>
  /// All IDs in the node will be updated to be universe unique. The returned node is safe to be sued when creating
  /// vessel nodes via <see cref="ProtoVessel.CreateVesselNode"/>.
  /// </remarks>
  /// <param name="actorVessel">The vessel to get the situation from. The resulted vessel will inherit it.</param>
  /// <param name="partNode">The parts saved state.</param>
  /// <returns>The resulted config node.</returns>
  /// <seealso cref="MakeNewVesselNode"/>
  public ConfigNode MakeNewPartConfig(Vessel actorVessel, ConfigNode partNode) {
    var configNode = new ConfigNode("PART");
    var snapshot = new ProtoPartSnapshot(partNode, null, null);
    snapshot.Save(configNode);
    configNode.SetValue("flag", actorVessel.rootPart.flagURL, createIfNotFound: true);
    var flightId = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
    configNode.SetValue("uid", flightId, createIfNotFound: true);
    configNode.SetValue("mid", flightId, createIfNotFound: true);
    configNode.SetValue("state", 0);
    return configNode;
  }

  /// <summary>Makes a node for the proto vessel.</summary>
  /// <param name="actorVessel">The vessel to get the situation from. The resulted vessel will inherit it.</param>
  /// <param name="rootItem">The item state to make the vessel from.</param>
  /// <param name="partPosition">The vessel's position.</param>
  /// <param name="rotation">The vessel's location.</param>
  /// <returns>The node that can be used to restore a vessel.</returns>
  public ConfigNode MakeNewVesselNode(
      Vessel actorVessel, InventoryItem rootItem, Vector3 partPosition, Quaternion rotation) {
    //CheckGroundCollision()
    var orbit = new Orbit(actorVessel.orbit);
    var vesselName = rootItem.avPart.title;
    var itemConfig = new ConfigNode();
    rootItem.snapshot.Save(itemConfig);
    var vesselNode = ProtoVessel.CreateVesselNode(
        vesselName, VesselType.DroppedPart, orbit, 0, new[] { MakeNewPartConfig(actorVessel, itemConfig) });

    //FIXME: need more than one part to skip repositioning! !vesselSpawning && skipGroundPositioning
    vesselNode.SetValue("skipGroundPositioning", newValue: true, createIfNotFound: true);
    vesselNode.SetValue("vesselSpawning", newValue: true, createIfNotFound: true);
    vesselNode.AddValue("prst", value: true);
    vesselNode.SetValue("sit", newValue: actorVessel.situation.ToString(), createIfNotFound: true);
    vesselNode.SetValue("landed", newValue: actorVessel.Landed, createIfNotFound: true);
    vesselNode.SetValue("splashed", newValue: actorVessel.Splashed, createIfNotFound: true);
    vesselNode.SetValue("displaylandedAt", actorVessel.displaylandedAt, createIfNotFound: true);

    double alt;
    double lat;
    double lon;
    actorVessel.mainBody.GetLatLonAlt(partPosition, out lat, out lon, out alt);
    vesselNode.SetValue("lat", newValue: lat, createIfNotFound: true);
    vesselNode.SetValue("lon", newValue: lon, createIfNotFound: true);
    vesselNode.SetValue("alt", newValue: alt, createIfNotFound: true);

    var refRotation = actorVessel.mainBody.bodyTransform.rotation.Inverse() * rotation;
    vesselNode.SetValue("rot", KSPUtil.WriteQuaternion(refRotation));
    vesselNode.SetValue("PQSMin", 0, createIfNotFound: true);
    vesselNode.SetValue("PQSMax", 0, createIfNotFound: true);

    return vesselNode;
  }
  #endregion

  #region Local utility methods
  /// <summary>Fixes the prefab fields.</summary>
  /// <remarks>
  /// Prefab may have fields initialized to "null". Such fields cannot be saved via <c>BaseFieldList</c> when making a
  /// snapshot. So, this method goes through the persistent fields of all the prefab modules and replaces <c>null</c>'s
  /// with a default value of the related type. It'll unlikely break anything since by design such fields are not
  /// assumed to be used until the part is loaded, and it's impossible to have <c>null</c> value read from a config.
  /// </remarks>
  /// <param name="part">The part to cleanup. If it's not a prefab, then the call is NOOP.</param>
  /// FIXME: merge with the one below.
  static void MaybeFixPrefabPart(Part part) {
    if (ReferenceEquals(part, part.partInfo.partPrefab)) {
      CleanupModuleFieldsInPart(part);
    }
  }

  /// <summary>Walks through all modules in the part and fixes null persistent fields.</summary>
  /// <remarks>Used to prevent NREs in methods that persist KSP fields.
  /// <para>
  /// Bad modules that cannot be fixed will be dropped which may make the part to be not behaving as
  /// expected. It's guaranteed that the <i>stock</i> modules that need fixing will be fixed
  /// successfully. So, the failures are only expected on the modules from the third-parties mods.
  /// </para></remarks>
  /// <param name="part">The part to fix.</param>
  static void CleanupModuleFieldsInPart(Part part) {
    var badModules = new List<PartModule>();
    foreach (var module in part.Modules) {
      try {
        CleanupFieldsInModule(module);
      } catch {
        badModules.Add(module);
      }
    }
    // Cleanup modules that block KIS. It's a bad thing to do but not working KIS is worse.
    foreach (var moduleToDrop in badModules) {
      DebugEx.Error(
          "Module on part prefab {0} is setup improperly: name={1}. Drop it!", part, moduleToDrop);
      part.RemoveModule(moduleToDrop);
    }
  }

  /// <summary>Fixes null persistent fields in the module.</summary>
  /// <remarks>Used to prevent NREs in methods that persist KSP fields.</remarks>
  /// <param name="module">The module to fix.</param>
  static void CleanupFieldsInModule(PartModule module) {
    // HACK: Fix uninitialized fields in science lab module.
    var scienceModule = module as ModuleScienceLab;
    if (scienceModule != null) {
      scienceModule.ExperimentData = new List<string>();
      DebugEx.Warning(
          "WORKAROUND. Fix null field in ModuleScienceLab module on the part prefab: {0}", module);
    }
    
    // Ensure the module is awaken. Otherwise, any access to base fields list will result in NRE.
    // HACK: Accessing Fields property of a non-awaken module triggers NRE. If it happens then do
    // explicit awakening of the *base* module class.
    try {
      module.Fields.GetEnumerator();
    } catch {
      DebugEx.Warning(
          "WORKAROUND. Module {0} on part prefab is not awaken. Call Awake on it", module);
      module.Awake();
    }
    foreach (var baseField in module.Fields) {
      if (baseField.isPersistant && baseField.GetValue(module) == null) {
        var proto = new StandardOrdinaryTypesProto();
        var defValue = proto.ParseFromString("", baseField.FieldInfo.FieldType);
        DebugEx.Warning("WORKAROUND. Found null field {0} in module prefab {1},"
                        + " fixing to default value of type {2}: {3}",
                        baseField.name, module, baseField.FieldInfo.FieldType, defValue);
        baseField.SetValue(defValue, module);
      }
    }
  }
  #endregion
}

}  // namespace
