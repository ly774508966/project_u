# Project U
Project U is a toolset for Unity3D development. It came from my daily work.

Check [WIKI](https://github.com/xiaobin83/project_u/wiki)

### Lua
* Execute Lua script with C# reflection support
* Hot-patch marked C# code on the fly.
* Provide LuaBehaviour which works as MonoBehaviour.
* Debug Lua script with VSCode and [devCAT.lua-debug](https://marketplace.visualstudio.com/items?itemName=devCAT.lua-debug) (modified version)

### UI
* Emoji Text, a polish on [mcraiha's Unity-UI-emoji](https://github.com/mcraiha/Unity-UI-emoji). Replace RawImage child GameObject with one child GameObject which takes its own canvas renderer, and let Unity do the layout work. And it supports markdown hyper link by sending `onHerf(link)` event when click on `[Text](link)`.

### Scene
* Generic QuadTree 
* Batched Mesh Streaming (to be merged)
* Decal System (to be merged)
* Object Paint (to be merged) 
