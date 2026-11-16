# Hand Duel: Borrowlight

A seated, hands-first Quest arcade by Aryan Neopane. Reshape a shared ribbon,
claim light and steal it once; your rival inherits the route. Includes a bot,
headset handoff, Aftershape, Tic-tac-toe and Rock Paper Scissors. Native play is offline.

## Play on Quest

[Join the Competition channel](https://www.meta.com/s/4ykAGmxTN) for **0.5.6 / code 17**.
Verified November 16, 2026: the invite opens Hand Duel's COMPETITION join page;
Meta confirms the current build is attached. Sign in and review Meta's join terms.
Alternatively, enable developer mode/USB debugging and install the included publisher-signed APK:

```sh
adb install -r build/Hand-Duel-0.5.6.apk
adb shell am start -n com.keepsrunning.showofhands/com.unity3d.player.UnityPlayerActivity
```

A differently signed development install must be removed before installing this APK;
uninstalling removes its local progress. Start Borrowlight → Learn. Hold B, move it
to the gold ring and release; repeat with A. No Send button. Duel offers Pass the
Quest or separate left/right roles; Play with bot needs one person. Use Centre if needed.

## Build from source

Open `game/` in **Unity 6000.0.66f2 on Windows**, with Android Build Support,
SDK/NDK and OpenJDK installed. Let the pinned packages restore; switch to Android.
With Unity closed, run:

```powershell
./game/tools/Build-Quest.ps1 -Unity 'PATH/TO/Unity.exe'
```

Output: `game/Builds/ShowOfHands.apk` (development certificate; release keys are excluded).
The normal build strips diagnostic/benchmark flags. Source targets Quest 2/3/3S
through OpenXR, ARM64 and Vulkan; physical validation is on Quest 2. Quest 3/3S
device testing remains unverified. Clean checkout: Android APK built successfully
and all 80 EditMode tests passed. Another 21 simulated gameplay flows passed during app testing.

## Linux hosting handoff

The game is a native APK, not WebXR. A server does not run the game or replace
the Competition invite. To preview the optional static project page:

```sh
python3 -m http.server 8080 --directory web
```

Serve `web/` over HTTPS using your hosting setup. Its final demo has burned-in
English captions. Do not serve the repository root or add accounts/databases.
The page includes the verified current Competition invite.
The demo still needs YouTube/Vimeo hosting for the submission's required video URL.
Keep this repository private unless the owner explicitly changes visibility.
