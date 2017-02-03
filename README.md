# BlacksmithAtmosphericScattering
My own flavor of Unity Technologies' Atmospheric Scattering that was used in The Blacksmith short demo.

Updated to be compatabile with Unity 5.4 and 5.5 with its reversed z-buffer.

Usage:

Get The Blacksmith Atmospheric Scattering project from the Asset Store.

On Unity 5.4 and up lighting must be rebuilt after download.

Backup the project.

Paste the Assets folder here over the one in the project and choose replace all files.

About:

This is my own personal update of the project and is specialized for my own needs.

It features quite a large code refactor aimed at performance optimization.

It now only sends the majority of the fields to the shaders in OnEnable to improve performance.

A manual update of all fields to the shaders can be forced with AtmosphericScattering.instance.UpdateStaticUniforms(), however adding your own methods to update only the fields you need would be wise.

Includes several bug fixes.

The atmospheric scattering script no longer generates any garbadge.

Includes shader modifications to eliminate artifacts and personal unwanted behaviour.

Fixed all compiler warnings and errors in Unity 5.4 and Unity 5.5.

Light shafts are now visible in scene view on Unity 5.4 and up.

If you can't see the light shafts then go to your Project Settings and under Quality ramp up the shadow distance to around 10000. This is an unfortunate requirement of the effect and not a bug. There's no way to alter this requirement.

Occlusion (light shafts) are the most expensive part of the package and disabling it can give you a big performance boost.
4x Downsampling and 164 occlusion samples are my own pereffered sweet spot for performance with occlusion enabled.

On a mid to low range Desktop PC running the default scene performnace was around 0.7ms on the CPU and 0.5ms on the GPU

Tested on Unity 5.4 and 5.5.
Should probably still work in 5.3.
Will not be tested on Unity 5.6 until well after it is released.
