// Kerbal Inventory System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KSPDev.ConfigUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System.Collections.Generic;
using System.Linq;

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

  /// <summary>Creates a simplified snapshot of the part's persistent state.</summary>
  /// <remarks>
  /// This is not the same as a complete part persistent state. This state only captures the key
  /// module settings.
  /// </remarks>
  /// <param name="part">The part to snapshot. It must be a fully activated part.</param>
  /// <returns>The part's snapshot.</returns>
  public ConfigNode PartSnapshot(Part part) {
    if (ReferenceEquals(part, part.partInfo.partPrefab)) {
      // HACK: Prefab may have fields initialized to "null". Such fields cannot be saved via
      //   BaseFieldList when making a snapshot. So, go through the persistent fields of all prefab
      //   modules and replace nulls with a default value of the type. It'll unlikely break anything
      //   since by design such fields are not assumed to be used until the part is loaded, and it's
      //   impossible to have "null" value read from a config.
      CleanupModuleFieldsInPart(part);
    }

    // Persist the old part's proto state to not affect it after the snapshot.
    var oldVessel = part.vessel;
    var oldPartSnapshot = part.protoPartSnapshot;
    var oldCrewSnapshot = part.protoModuleCrew;
    if (oldVessel == null) {
      part.vessel = part.gameObject.AddComponent<Vessel>();
      DebugEx.Fine("Making a fake vessel for the part to make a snapshot: part={0}, vessel={1}",
                   part, part.vessel);
    }

    var snapshot = new ProtoPartSnapshot(part, null) {
        attachNodes = new List<AttachNodeSnapshot>(),
        srfAttachNode = new AttachNodeSnapshot("attach,-1"),
        symLinks = new List<ProtoPartSnapshot>(),
        symLinkIdxs = new List<int>()
    };
    var partNode = new ConfigNode("PART");
    snapshot.Save(partNode);

    // Rollback the part's proto state to the original settings.
    if (oldVessel != part.vessel) {
      DebugEx.Fine("Destroying the fake vessel: part={0}, vessel={1}", part, part.vessel);
      UnityEngine.Object.DestroyImmediate(part.vessel);
    }
    part.vessel = oldVessel;
    part.protoPartSnapshot = oldPartSnapshot;
    part.protoModuleCrew = oldCrewSnapshot;

    // Prune unimportant data.
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

    partNode.RemoveNodes("ACTIONS");
    partNode.RemoveNodes("EVENTS");
    foreach (var moduleNode in partNode.GetNodes("MODULE")) {
      moduleNode.RemoveNodes("ACTIONS");
      moduleNode.RemoveNodes("EVENTS");
    }

    return partNode;
  }

  /// <summary>Returns all the resource on the part.</summary>
  /// <param name="partNode">
  /// The part's config or a persistent state. It can be a top-level node or the <c>PART</c> node.
  /// </param>
  /// <returns>The found resources.</returns>
  public ProtoPartResourceSnapshot[] GetResources(ConfigNode partNode) {
    if (partNode.HasNode("PART")) {
      partNode = partNode.GetNode("PART");
    }
    return partNode.GetNodes("RESOURCE")
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
  public double? UpdateResource(ConfigNode partNode, string name, double amount,
                                bool isAmountRelative = false) {
    if (partNode.HasNode("PART")) {
      partNode = partNode.GetNode("PART");
    }
    var node = partNode.GetNodes("RESOURCE")
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
  public double GetPartDryMass(
      AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
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
  public double GetPartDryCost(
      AvailablePart avPart, PartVariant variant = null, ConfigNode partNode = null) {
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
    VariantsUtils.ExecuteAtPartVariant(avPart, variant,
                                       p => itemCost += p.GetModuleCosts(avPart.cost));
    return itemCost;
  }

  /// <summary>Makes a part snapshot from the saved part state.</summary>
  /// <param name="refInventory">The stock inventory module the part is being made for.</param>
  /// <param name="node">The saved state.</param>
  /// <returns>A snapshot for the given state.</returns>
  /// <exception cref="ArgumentException">if the game scene or the reference inventory are not good.</exception>
  public ProtoPartSnapshot GetProtoPartSnapshotFromNode(ModuleInventoryPart refInventory, ConfigNode node) {
    ProtoPartSnapshot pPart = null;
    if (refInventory.vessel != null) {
      pPart = new ProtoPartSnapshot(node, refInventory.vessel.protoVessel, HighLogic.CurrentGame);
    } else {
      if (HighLogic.LoadedSceneIsEditor
          || HighLogic.LoadedSceneIsMissionBuilder
          || HighLogic.LoadedScene == GameScenes.MAINMENU
          || refInventory.kerbalMode) {
        pPart = new ProtoPartSnapshot(node, null, HighLogic.CurrentGame);
      }
    }
    if (pPart == null) {
      throw new ArgumentException("Cannot make snapshot in scene " + HighLogic.CurrentGame + " for node:\n" + node);
    }

    // The ID gets adjusted to be unique in the game, but for the purpose of persistence we need it to stay unchanged.
    var originalPartId = uint.Parse(node.GetValue("persistentId"));
    if (originalPartId != pPart.persistentId) {
      pPart.persistentId = originalPartId;
      DebugEx.Info("ProtoPartSnapshot persistentId changed back from {0} to {1}. It's a state snapshot action.",
                   pPart.persistentId, originalPartId);
    }

    return pPart;
  }

  /// <summary>Saves a proto part snapshot into the config node.</summary>
  /// <param name="pPart">The proto part to store.</param>
  /// <returns>The resulted config node.</returns>
  public ConfigNode GetConfigNodeFromProtoPartSnapshot(ProtoPartSnapshot pPart) {
    var state = new ConfigNode("PART");
    pPart.Save(state);
    return state;
  }
  #endregion

  #region Local utility methods
  /// <summary>Walks thru all modules in the part and fixes null persistent fields.</summary>
  /// <remarks>Used to prevent NREs in methods that persist KSP fields.
  /// <para>
  /// Bad modules that cannot be fixed will be dropped which may make the part to be not behaving as
  /// expected. It's guaranteed that the <i>stock</i> modules that need fixing will be fixed
  /// successfully. So, the failures are only expected on the modules from the third-parties mods.
  /// </para></remarks>
  /// <param name="part">The part to fix.</param>
  static void CleanupModuleFieldsInPart(Part part) {
    var badModules = new List<PartModule>();
    foreach (var moduleObj in part.Modules) {
      var module = moduleObj as PartModule;
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
    foreach (var field in module.Fields) {
      var baseField = field as BaseField;
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
