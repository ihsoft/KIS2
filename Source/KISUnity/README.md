Keep the default scene intact!
Make all changes to the object in the scene. Once done, click "Apply" in the prefub inspector menu (at the top). This will populate the changes to the prefab.
* On load, the window prefab will clear all slots. The very first slot will be used as prefab for the slots in game. Avoid creating too many slots in the scene since it would take unneccessary space in the bundle. It won't affect the runtime footprint though.
* The slot prefab will drop all action buttons on load, using the very first one as a prefab fro the buttons in the game.
* The slot prefab hides all its controls on load.


# Building asset bundle


Make all updates to the scripts and objects, then choose "Build Assmeble" menu in the Assets folder context menu.