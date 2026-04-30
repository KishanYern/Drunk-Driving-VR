using UnityEngine;

/// <summary>
/// ScriptableObject that describes one selectable car.
///
/// HOW TO CREATE A CAR ENTRY:
///   Right-click in the Project window > Create > DrunkDrivingVR > Car Data
///   Fill in the fields and drag your car prefab in.
///   Then add the asset to GameStateManager.availableCars[] in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "NewCarData", menuName = "DrunkDrivingVR/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in the car selection menu.")]
    public string carName = "Sedan";

    [Tooltip("Short flavour description shown in the selection panel.")]
    [TextArea(2, 4)]
    public string description = "A reliable all-rounder.";

    [Header("Prefab")]
    [Tooltip("The car prefab to instantiate when this car is selected. " +
             "Must have CarController2_VR, VehicleManager, and WheelColliders set up.")]
    public GameObject carPrefab;

    [Header("Preview")]
    [Tooltip("2D thumbnail shown in the car selection UI. " +
             "Render a camera shot of the car and assign the resulting Sprite here.")]
    public Sprite previewSprite;

    [Tooltip("Tint colour used for UI accents (e.g. stat bar fill).")]
    public Color accentColor = Color.white;

    [Header("Stats  (1 = slow/poor  —  5 = fast/excellent)")]
    [Range(1, 5)]
    public int speedRating = 3;

    [Range(1, 5)]
    public int handlingRating = 3;

    [Range(1, 5)]
    public int weightRating = 3;
}
