# SharpAcompute
## Word of warning:  
_The plugin is currently at version 0.1; in active development, and has several problems. Most notably with C# assemblies not being correctly unloaded leading to problems with hotreloading.  
This adds a lot of overhead to development time and I do not recommend using the plugin for larger project until its fixed. That said; this plugin is somehow **still** an improvement on the usability of `EffectCompositor` so I hope that you who are reading this can help with issues or pray to the Godot gods that the developers spend more time making the workflow signficantly better._
***

A C# godot plugin implementation of [Acerolas Acompute](https://github.com/GarrettGunnell/Acerola-Compute) that makes working with the new `EffectCompositor` _slightly_ less painful.  
Tested in Godot version 4.4-mono
***
- Features a GLSL compute shader wrapper language called `Acompute`.
- Several wrappers to make creating EffectCompositor effects simpler
- Hot reloading of shaders
- Memory management of created shaders
- Some examples

***
### Installation:
Installed like any other Godot plugin. Instructions can be found [here](https://docs.godotengine.org/en/stable/tutorials/plugins/editor/installing_plugins.html).

***
### Creating new effects:
Each effects consists of two files.  

1. ### AcomputeShaderResource
    `AcomputeShaderResource` is a `.tres` resource containing your provided GLSL code. The **recommended** way of creating a new resource of this type is to drag and drop a text file with the extension `.acompute`.  
    >The plugin contains an importer that will automatically create the `.tres` file off the provided `.acompute` file in the background saving it under `.godot\imported`.  

    The reason this way is preferred is that you can use an external file editor like **notepad++** that has correct GLSL syntax-highlighting to work with your shader. Saving your shader in any external software is enough for the plugin to recompile and reload it.  
   - The other way would be to create a new resource deriving from `AcomputeShaderResource` directly from within the editor. In that case you will have to use the property inspector window to modify your code, though hot-reloading still works on save. 

2. ### AcomputeCompositorEffect
    Create a new class inheriting from `AcomputeCompositorEffect`. Set up all your logic here that will be dispatched to the `AcomputeShaderResource`. Refer to the examples for more info.
***
### Notes
`AcomputeShaderResources` created using the `.acompute` import can not be quick loaded. In that case just drag and drop the file into the exported field.

![thumbnail](Misc/readmeThumbnail.png "Simple example using a Vignette & Outline effect together")
