# DrunkDrivingVR

A VR simulation that demonstrates the dangers of drunk driving, built for Meta Quest using Unity 6.

## Overview

DrunkDrivingVR places the player in a virtual environment where they can interact with an alcohol bottle and drive a car. Drinking from the bottle progressively impairs the player's vision and motor control, making driving increasingly difficult. The goal is to educate players on how alcohol affects driving ability.

## Features

- **VR Car Driving** — Accelerate, brake, and steer using Meta Quest controllers
- **Grabbable Alcohol Bottle** — Pick up and drop a bottle using the right-hand trigger
- **Drink Detection** — Raise the bottle to your face to drink; impairment builds with each sip
- **Drunk Effects** — Camera wobble and sway that intensify with alcohol consumption and fade over time
- **Car Enter/Exit** — Walk up to the car door handle and grab it to get in or out

## Controls

| Action | Input |
|---|---|
| Pick up / drop bottle | Right index trigger |
| Drink | Hold bottle near face for 1.5s |
| Enter / exit car | Grab door handle (grip or index trigger) |
| Accelerate | Right trigger (while in car) |
| Brake / reverse | Left trigger (while in car) |
| Steer | Left thumbstick (while in car) |

## Tech Stack

- **Engine:** Unity 6 (6000.3.9f1) with URP
- **VR SDK:** Meta XR SDK 85.0.0
- **Platform:** Meta Quest (via OpenXR)
- **Packages:** XR Interaction Toolkit 3.3.1, Input System 1.18.0

## Project Structure

```
Assets/
├── Bottle/                     # Jager bottle model and textures
├── Scenes/
│   ├── SampleScene.unity       # Main scene
│   └── Scripts/
│       ├── BottleGrabbable.cs  # Bottle grab/drop and drink detection
│       ├── DrunkEffect.cs      # Camera wobble impairment effect
│       ├── CarController2.cs   # VR car driving controller
│       └── Vehicle Manager/
│           ├── VehicleManager.cs              # Car enter/exit logic
│           └── DoorHandleInteraction_OVR.cs   # Door handle grab interaction
├── Settings/                   # URP render pipeline settings
└── Stylized Vehicles Pack Free/  # Car model assets
```

## Getting Started

### Prerequisites

- Unity 6 (6000.3.9f1 or compatible)
- Meta Quest headset
- Android SDK configured for Quest builds

### Setup

1. Clone the repository:
   ```
   git clone https://github.com/Monstucture/DrunkDrivingVR.git
   ```
2. Open the project in Unity Hub
3. Open `Assets/Scenes/SampleScene.unity`
4. Connect your Quest headset and build to Android, or use Quest Link for editor testing

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

Note: Test commit from Jonathan to verify Git push access.
