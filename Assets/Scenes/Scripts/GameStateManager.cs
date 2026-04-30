using UnityEngine;

/// <summary>
/// Singleton that persists between the Menu scene and the Game scene.
/// Holds the player's car selection so VehicleManager can read it on load.
///
/// SCENE SETUP:
///   Create an empty GameObject in the MainMenu scene.
///   Rename it "GameStateManager" and attach this script.
///   Populate the availableCars[] array with your CarData assets.
///   The object survives scene transitions automatically (DontDestroyOnLoad).
/// </summary>
public class GameStateManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    // Singleton
    // ------------------------------------------------------------------ //

    public static GameStateManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ------------------------------------------------------------------ //
    // Car catalogue
    // ------------------------------------------------------------------ //

    [Header("Car Catalogue")]
    [Tooltip("All selectable cars. Add new CarData assets here to expand the roster.")]
    public CarData[] availableCars;

    // Index of the car the player has chosen. -1 = no selection yet.
    private int _selectedCarIndex = 0;

    /// <summary>Index of the currently selected car in availableCars[].</summary>
    public int SelectedCarIndex
    {
        get => _selectedCarIndex;
        private set => _selectedCarIndex = Mathf.Clamp(value, 0, Mathf.Max(0, availableCars.Length - 1));
    }

    /// <summary>The CarData asset for the currently selected car, or null if the catalogue is empty.</summary>
    public CarData SelectedCar
    {
        get
        {
            if (availableCars == null || availableCars.Length == 0) return null;
            return availableCars[_selectedCarIndex];
        }
    }

    // ------------------------------------------------------------------ //
    // Public API
    // ------------------------------------------------------------------ //

    /// <summary>Set the selected car by index. Safe — clamps to valid range.</summary>
    public void SelectCar(int index)
    {
        SelectedCarIndex = index;
        Debug.Log($"[GameStateManager] Car selected: {SelectedCar?.carName ?? "none"} (index {index})");
    }

    /// <summary>Advance selection forward, wrapping around.</summary>
    public void SelectNext()
    {
        if (availableCars == null || availableCars.Length == 0) return;
        SelectedCarIndex = (_selectedCarIndex + 1) % availableCars.Length;
    }

    /// <summary>Advance selection backward, wrapping around.</summary>
    public void SelectPrevious()
    {
        if (availableCars == null || availableCars.Length == 0) return;
        SelectedCarIndex = (_selectedCarIndex - 1 + availableCars.Length) % availableCars.Length;
    }
}
