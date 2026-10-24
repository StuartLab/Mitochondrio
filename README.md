# Mitochondrio

A simulation of mitochondrial dynamics on a procedurally generated cytoskeleton, rendered in real time. Ported from Unity in C# using OpenGL, OpenTK, and Dear ImGui.

# How it works
The simulation models mitochondria moving along a procedurally generated cytoskeleton: microtubules extending outward from a central nucleus, with actin filaments branching off them. Mitochondria move at a set speed along the network, moving along their branch, and turning onto ones they encounter. When two mitochondria touch, they have a chance to fuse into a chain that acts as a whole. Each link in a chain can also break through fission, with its own probability. Most elements in the simulation, from the construction of the cytoskeleton to the movement and interactions of mitochondria, are fully controllable through an interface.

# Using it
Mitochondrio shows the user a control panel. The panel has four tabs, with different categories of settings that they can set.

### Cell Creation
<img width="333" height="30" alt="image" src="https://github.com/user-attachments/assets/11857241-3edf-49c5-af87-0c32bd9ccb44" />

These buttons are always visible at the bottom of the control panel.

| Setting | Effect |
| --- | --- |
| Create | Creates a new cytoskeleton and spawns the mitochondria on it, all pertaining to the settings in the panel |
| Restart | Deletes all mitochondria from the current cell, and respawns them in new locations, restarting the simulation |
| Clear | Deletes all of the cytoskeleton's branches and the mitochondria on them |

### Cytoskeleton
<img width="329" height="320" alt="image" src="https://github.com/user-attachments/assets/d833e76a-e5c1-4b28-8905-87b7ce34c74d" />

This tab controls the starting parameters of the simulation. Any settings that are from a minimum to a maximum value are randomized according to a bell curve, with the middle value being the most common.

| Setting | Effect |
| --- | --- |
| # Microtubules | The number of microtubules (main branches) that radiate out of the nucleus |
| Cytoskeleton Dimensions (X, Y, Z) | A stretch factor for the full cell along each dimension, applied on creation |
| Microtubule Length (min-max) | The number of evenly spaced nodes that make up a microtubule |
| # Actin Filaments (min-max) | The number of actin filaments (sub branches) that grow off of each microtubule |
| Actin Length (min-max) | The number of evenly spaced nodes that make up an actin |
| # Mitos per Microtubule (min-max) | The number of mitochondria that will spawn on each microtubule when Create is pressed |

### Mitochondria
<img width="325" height="164" alt="image" src="https://github.com/user-attachments/assets/bf2e71c7-5941-48b6-b9dc-a2ffca36fd43" />

This tab controls how mitochondria interact with each other. Values can be updated in real time.

| Setting | Effect |
| --- | --- |
| Fission probability (%) | The chance each second that a mitochondrion in a chain will break away from the rest |
| Fusion probability (%) | The chance that two mitochondria will fuse together into a chain upon contact (some other factors in execution could negate this) |
| Diameter | The diameter of a mitochondrion in world units. This effects collision. Too high values will break the simulation |
| Toggle smoothing (toggle) | Toggles on or off the effect of smoothing each chain of mitochondria into continuous cylinders |

### Simulation
<img width="327" height="180" alt="image" src="https://github.com/user-attachments/assets/6fc59928-0aa7-4b76-bb38-8ad04b76c265" />

This tab controls various factors of the simulation, such as time scale.

| Setting | Effect |
| --- | --- |
| View slice depth | Currently unused |
| Mitochondria speed | The speed at which mitochondria move along the cytoskeleton |
| Simulation speed | The speed at which the simulation moves forward |
| Bloom strength | Adjusts the strength of the glow effect around bright areas |

### Camera
<img width="331" height="200" alt="image" src="https://github.com/user-attachments/assets/3c211902-8f6e-435c-9d07-10940b4362bb" />

This tab controls the speed at which the camera can move around.

| Setting | Effect |
| --- | --- |
| Movement speed | The speed at which the camera moves when using the W/A/S/D keys |
| Pan sensitivity | The speed at which the camera pans when holding the middle-mouse button and dragging around |
| Look sensitivity | The speed at which the camera rotates when holding the right-mouse button and dragging around |
| Reset sensitivities | Resets the above values to the default values |
| Reset view | Resets the camera's angle and position to the default values, looking down from directly above the cell |

# Camera controls
| Key | Action |
|---|---|
| W / A / S / D | Move the camera horizontally |
| Space / Left Shift | Up / down, vertical movement |
| Right-mouse drag | Look around |
| Middle-mouse drag | Pan |
| Scroll | Zoom |
| Esc | Quit |


# Road Map
Mitochondrio is a living project, and changes are made iteratively. Current planned features include:
 - The currently unused "view slice depth", showing only a vertical slice of the cell as an electron microscope might
