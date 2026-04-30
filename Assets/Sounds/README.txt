Drop your custom sound files here.

Suggested files for the beer-bottle / pickup system:

  refill.wav     -> played each time a bottle is refilled / respawned
  pickup.wav     -> played when the player grabs a refill pickup
  drink_loop.wav -> looped sound while drinking from a bottle (optional)
  sip.wav        -> one-shot sound when each sip lands (optional)

Wiring them up
--------------

1. After importing the file, click it in the Project window and confirm the
   Inspector shows it as an AudioClip.

2. Select the GameObject in your scene that has the BeerBottleSpawner component.
   Drag your refill clip into the spawner's "Refill Audio -> Refill Clip" slot.
   (Optional: also wire Drink Loop Clip and Sip Complete Clip on the same component.)

3. Select / create the GameObject that has the BeerRefillPickupSpawner component.
   Drag your pickup clip into "Audio (forwarded to pickup) -> Pickup Clip".

You can also drag the clips directly onto a single bottle prefab's
BottleGrabbable component (Audio header) if you want per-bottle overrides.

Any .wav, .ogg, or .mp3 file that Unity recognises as an AudioClip will work.
