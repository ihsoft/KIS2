# Overview

This is a Unity project folder. Load it from the Unity editor to manage the content. Use it to
design/modify KIS assets. Once done, a custom script within Unity should be used to make and
export an asset bundle - `ui_prefab`, a file that contains all the UI elements for `KIS` and
that can be loaded from the game (see `KSPDev.PrefabUtils.LoadAllAssets`).

# Dependencies

This project requires Uinity editor of version `2019.4.28f1`. It's a good idea to upgarde as the
main game does. The higher versions are not recommended. Too low versions are not recommended too.

This project won't work alone. It needs `KIS` code that controls the UI elements. That project is
named `KIS.Unity`. A pre-built version of the DLL is stored in the `Assets/Plugins` folder. Don't
forget to update it if the `KIS.Unity` project has changed.

The mod release must have both the `ui_prefab` and `KIS.Unity.dll` as theese two assemblies work
together.

# Building the asset bundle

Once all changes are done to the prefabs, choose the context menu command `Build AssetBundle` on
the `KIS Assetds` folder. This will create `ui_prefab` file in the `StreamingAssets` folder.

In order for the prefab to be embed into the bundle, it must have assigned to `ui_prefab`
AssetBundle. Only prefabs that `KIS` is going to instantiate must be labeled like that.

# The prefab design concept

### Hints on working with the project

* Keep the default scene so that it represents all the UI elements that are needed for the mod UI.
  This includes the elements that are not public, but are used to construct the other objects.
* Make sure that all objects in the hierarchy are fully sync'ed to the prefabs in the `KIS Assets`
  folder. This can be verified at the top of the inspector in the `Prefab` line. The "Overrides"
  dropdown must say "No Overrides". If there are some, check them out and either apply all or
  rollback.

### UI objects concept and layout

* The window prefab will clear all the slots from the prefab on load in the game. It will use the
  very first slot as prefab for the slots in game. So, it's ok to add more samples into the window
  prefab in the editor. However, keep in mind that they will take extra space in the asset bundle.
* The slot prefab will drop all the action buttons on load, and the very first one will be used as
  a prefab for the buttons in the game. Again, keep in mind that the buttons will endup in the
  bundle, taking some space, even though they won't be exposed in the UI.
* The slot prefab hides all its controls on load. The prefab may have any cntrols visible if it
  helps the design process.
