# Room 2022 Classroom Simulation

An interactive 3D simulation of a real-world high school classroom environment (Room 2022), built using the Unity game engine for the Grade 12 Classroom Simulation project. The simulation focuses on attention to detail, navigation realism, physics interactions, and custom asset attributions.

---

## Core Simulation Features

1. **3D Room Map**: A full scale, navigable representation of Classroom Room 2022.
2. **Custom Textures**: Surface materials constructed using actual photographs taken of real surfaces in the classroom.
3. **Physics Interactivity**: Physics-driven desks, chairs, and stools. Players can lift, rotate, carry, and throw items.
4. **Openable Cabinets**: Programmatically generated openable cabinets and cabinet doors, which can be locked or unlocked dynamically.
5. **Interactive Computer OS (roomOS)**: A retro operating system accessible on the classroom screens featuring a terminal, text editor (notepad), web browser, network monitor, and a fully playable Snake game.
6. **Start Menu UI**: A clean, premium dark-slate start menu featuring navigation controls, asset attributions, and direct simulation loading.

---

## Control Layout (Keymap)

| Action | Key / Input | Description |
| :--- | :--- | :--- |
| **Move Around** | `W` `A` `S` `D` / `Arrow Keys` | Standard player locomotion in 3D space. |
| **Look / Rotate View** | `Mouse Movement` | Look around the environment. |
| **Jump** | `Spacebar` | Jump over obstacles. |
| **Grab / Pick Up Object** | `Left Click` (Hold) | Grabs light physics objects (computers, screens, stools) or pulls heavy objects (desks). |
| **Rotate Grabbing Object** | `Right Click` (Hold) + `Mouse` | Rotates the object currently carried in hand on local axes. |
| **Throw / Drop Object** | `F` / `E` / `Middle Click` / Release `Left Click` | Launches the carried physics object forward. |
| **Interact with PC** (currently doesn't work) | `E` | Log into the room computer network terminal (only when not carrying an object). |
| **Lock / Unlock Cabinet** | `L` | Toggles the lock state on looked-at cabinet doors, locking them in place. |
| **Toggle Cursor Lock / Pause** | `Escape` | Toggles mouse cursor lock state between locked (for look movement) and unlocked (for menus). |

---

## Collaborators and Attributions

This project was built using a combination of customized student assets and peer-shared 3D models:

### 1. Custom Textures & Cabinet Modeling (Created by Me)
- **Classroom Textures**: Photographed real-world classroom surfaces (bricks, wood desks, stools, cabinet panels, chalkboard, walls) to compile custom texture maps and materials.
- **Lockers & Cabinets**: Modeled the various openable cabinets in the room, aligning their geometry to fit the room's constraints.
- **Physical Interactions**: Implemented cabinet door swing scripts and lock parameters.

### 2. Furnishings & Accessories (Shared by Kolby)
- **Classroom Desks**: 3D desk models (`desk.fbx`, `desk2.fbx`, `desk3.fbx`, `desk4.fbx`, `desk9.fbx`) utilized to build row groupings.
- **Computer Hardware**: Computer towers (`computer.fbx`, `computertest.fbx`) and monitor screen frames (`Untitled.fbx`).
- **Input Devices**: Detailed mechanical keyboard models (`Keyboard.fbx`).
- **Seating**: Round stools (`Stool.fbx`) positioned at desks.
- **Bicycle Model**: Detailed bicycle frame model (`2026-06-09.glb`) placed as a decorative classroom accessory.

### 3. Structural Storage & Safety (Shared by Ibrahim)
- **Protective Cage Model**: Large cage model (`Cage.fbx`) placed at the back of the classroom for securing equipment.
- **Storage Shelves**: Standard modular classroom shelves (`Shelf1.fbx`, `ShelfBlueDesk.fbx`) to hold decor and textbooks.

---

## Shared / Downloaded Asset Directory

To comply with simulation guidelines, below is the inventory of all shared/downloaded assets utilized in this project, detailed in full sentences:

1. **Classroom Desks (`desk.fbx`, `desk2.fbx`, `desk3.fbx`, `desk4.fbx`, `desk9.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: These models represent the primary student desks located throughout the room and serve as static platforms for monitors and keyboards.
2. **Computer Towers (`computer.fbx`, `computertest.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: These assets represent the computer towers set up under student tables and are interactable physics components.
3. **Monitor Screens (`Untitled.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: These models represent the flat-panel computer screens mounted on desks, carrying the canvas for the interactive roomOS.
4. **Keyboards (`Keyboard.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: These detailed keyboard models sit in front of the monitors to represent workstation inputs.
5. **Stools (`Stool.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: These models are physics-active stools located at each student desk, allowing players to move them around.
6. **Bicycle Model (`2026-06-09.glb`)**
   - *Source/Credit*: Shared by peer contributor **Kolby**.
   - *Purpose*: This aesthetic bicycle asset acts as a classroom decoration model stored near the exit.
7. **Protective Equipment Cage (`Cage.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Ibrahim**.
   - *Purpose*: This cage represents a locked equipment storage cage used to restrict access to valuable room hardware.
8. **Storage Shelves (`Shelf1.fbx`, `ShelfBlueDesk.fbx`)**
   - *Source/Credit*: Shared by peer contributor **Ibrahim**.
   - *Purpose*: These models represent the structural storage shelving brackets holding simulation items against the walls.
