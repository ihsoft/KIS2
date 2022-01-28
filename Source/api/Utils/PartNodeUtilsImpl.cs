// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KSPDev.ConfigUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
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
  /// <returns>A snapshot fo the part's current state.</returns>
  public ProtoPartSnapshot GetProtoPartSnapshot(Part part) {
    DebugEx.Fine("Make a proto part snapshot for: {0}", part);
    MaybeFixPrefabPart(part);

    // Persist the old part's proto state to not affect it after the snapshot.
    var oldCrewSnapshot = part.protoModuleCrew;
    part.protoModuleCrew = null;
    var oldProtoPartSnapshot = part.protoPartSnapshot;
    var snapshot = new ProtoPartSnapshot(part, null) {
        // We don't need the vessel relations. 
        attachNodes = new List<AttachNodeSnapshot>(),
        srfAttachNode = new AttachNodeSnapshot("None,-1"),
        symLinks = new List<ProtoPartSnapshot>(),
        symLinkIdxs = new List<int>(),
        state = 0,
        PreFailState = 0,
        attached = false,
    };
    part.protoModuleCrew = oldCrewSnapshot;
    part.protoPartSnapshot = oldProtoPartSnapshot;

    return snapshot;
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
  /// <returns>The found science.</returns>
  public ScienceData[] GetScience(ConfigNode partNode) {
    return partNode.GetNodes("MODULE")
        .SelectMany(m => m.GetNodes("ScienceData"))
        .Select(n => new ScienceData(n))
        .ToArray();
  }

  /// <summary>Calculates part's dry mass given the config and the variant.</summary>
  /// <param name="avPart">The part's proto.</param>
  /// <param name="variant">
  /// The part's variant. If it's <c>null</c>, then the variant will be attempted to read from
  /// <paramref name="partNode"/>.
  /// </param>
  /// <param name="partNode">
  /// The part's persistent config. It will be looked up for the variant if it's not specified.
  /// </param>
  /// <returns>The dry cost of the part.</returns>
  public double GetPartDryMass(AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
    var itemMass = avPart.partPrefab.mass;
    if (variant == null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    VariantsUtils.ExecuteAtPartVariant(avPart, variant, p => itemMass += p.GetModuleMass(p.mass));
    return itemMass;
  }

  /// <summary>Calculates part's dry cost given the config and the variant.</summary>
  /// <param name="avPart">The part's proto.</param>
  /// <param name="variant">
  /// The part's variant. If it's <c>null</c>, then the variant will be attempted to read from
  /// <paramref name="partNode"/>.
  /// </param>
  /// <param name="partNode">
  /// The part's persistent config. It will be looked up for the various cost modifiers.
  /// </param>
  /// <returns>The dry cost of the part.</returns>
  public double GetPartDryCost(AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
    // TweakScale compatibility
    if (partNode != null) {
      var tweakScale = KisApi.PartNodeUtils.GetTweakScaleModule(partNode);
      if (tweakScale != null) {
        var tweakedCost = ConfigAccessor.GetValueByPath<double>(tweakScale, "DryCost");
        if (tweakedCost.HasValue) {
          // TODO(ihsoft): Get back to this code once TweakScale supports variants.
          return tweakedCost.Value;
        }
        DebugEx.Error("No dry cost specified in a tweaked part {0}:\n{1}", avPart.name, tweakScale);
      }
    }
    var itemCost = avPart.cost;
    if (variant == null && partNode != null) {
      variant = VariantsUtils.GetCurrentPartVariant(avPart, partNode);
    }
    VariantsUtils.ExecuteAtPartVariant(avPart, variant, p => itemCost += p.GetModuleCosts(avPart.cost));
    return itemCost;
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
    var vesselNode = ProtoVessel.CreateVesselNode(
        vesselName, VesselType.DroppedPart, orbit, 0, new[] { MakeNewPartConfig(actorVessel, rootItem.itemConfig) });

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

  /// <summary>
  /// Creates a copy of the parts persistent config node that only has the values and nodes that are important for
  /// comparision.
  /// </summary>
  /// <remarks>
  /// Use this method when two config nodes need to be compared for equality. This method only keeps the values and
  /// nodes that make sense in this matter.
  /// </remarks>
  /// <param name="srcNode">The node to make a copy from.</param>
  /// <returns>
  /// The adjusted copy of the config node. It will intentionally have name "PART-SUBNODE". The caller must be sure that
  /// the right values are being compared.
  /// </returns>
  public ConfigNode MakeComparablePartNode(ConfigNode srcNode) {
    var res = new ConfigNode("PART-SUBNODE");
    res.SetValue("name", srcNode.GetValue("name"), createIfNotFound: true);
    var checkNodes = new[] { "MODULE", "RESOURCE", "SCIENCE" };
    for (var i = 0; i < srcNode.nodes.Count; i++) {
      var node = srcNode.nodes[i];
      if (!checkNodes.Contains(node.name)) {
        continue;
      }
      node = node.CreateCopy();
      node.RemoveNodes("EVENTS");
      node.RemoveNodes("ACTIONS");
      res.AddNode(node);
    }
    return res;
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
