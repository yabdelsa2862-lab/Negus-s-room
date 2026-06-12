# How to Open RoomGame in Unity

## Requirements
- Unity 2022.3 LTS or newer (Unity Hub recommended)

## Steps

1. Open **Unity Hub**
2. Click **Open** → **Add project from disk**
3. Navigate to this `RoomGame` folder and click **Select Folder**
4. Unity will import the project (first open takes ~1–2 minutes)
5. In the **Project** window, go to `Assets / Scenes` and double-click **RoomScene**
6. Press the **Play ▶** button

## Controls
| Key / Input     | Action                    |
|-----------------|---------------------------|
| WASD            | Walk                      |
| Left Shift      | Run                       |
| Space           | Jump                      |
| Mouse           | Look around               |
| Left Click      | Kick a physics ball       |
| Escape          | Unlock mouse cursor       |

## Room Dimensions
| Dimension | Feet   | Metres  |
|-----------|--------|---------|
| Width     | 56.25  | 17.145  |
| Depth     | 22.00  | 6.706   |
| Height    | 9.00   | 2.743   |

The `RoomSetup.cs` script builds the room at runtime from cubes.
You can tweak ball count, bounce, and kick force in the Inspector
by selecting the **Room** object after pressing Play.
