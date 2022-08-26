// Kerbal Inventory System v2
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using KISAPIv2;
using KSPDev.LogUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KIS2 {

/// <summary>
/// Controller that detects the kerbals going EVA and simulates <c>OnLoad</c> on the inventory modules.
/// </summary>
/// <remarks>
/// When kerbal goes EVA in flight, its part doesn't get <c>OnLoad</c> method called. Instead, the game populates
/// kerbal's stock inventory directly, bypassing the generic inventory logic. Thus, it's not possible to react to
/// the state load when kerbal exist the vessel. This controller resolves it by reacting to event and simulating the
/// <c>OnLoad</c> call on the KIS inventory module as it would happen if the game was loaded with a kerbal already in
/// space.    
/// </remarks>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
public class KerbalGoingEvaController : MonoBehaviour {
  #region MonoBehaviour messages
  void Awake() {
    DebugEx.Fine("[{0}] Controller started", nameof(KerbalGoingEvaController));
    GameEvents.onCrewOnEva.Add(OnCrewOnEvaEvent);
  }

  void OnDestroy() {
    DebugEx.Fine("[{0}] Controller stopped", nameof(KerbalGoingEvaController));
    GameEvents.onCrewOnEva.Remove(OnCrewOnEvaEvent);
  }
  #endregion

  #region Local utility methods
  /// <summary>Reacts on kerbal leaving spacecraft and becoming EVA.</summary>
  void OnCrewOnEvaEvent(GameEvents.FromToAction<Part, Part> fv) {
    fv.to.FindModulesImplementing<IKisInventory>().ForEach(x => x.ownerPart.StartCoroutine(WaitAndLoadModule(x)));
  }

  // ReSharper disable once MemberCanBeMadeStatic.Local
  IEnumerator WaitAndLoadModule(IKisInventory inventory) {
    var part = inventory.ownerPart;
    while (true) {
      yield return new WaitForEndOfFrame();
      if (part == null || part.State == PartStates.DEAD) {
        yield break;  // End coroutine.
      }
      if (part.State == PartStates.IDLE
          && inventory.stockInventoryModule != null
          && inventory.stockInventoryModule.storedParts != null) {
        break;  // The part is ready to be loaded.
      }
    }
    var module = inventory as PartModule;
    // ReSharper disable once PossibleNullReferenceException
    HostedDebugLog.Info(module.part, "Simulating OnLoad on the EVA kerbal");
    module.OnLoad(new ConfigNode(""));
  }
  #endregion
}

}
