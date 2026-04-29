# DeFocuser-Lite
An open source DIY focus motor with automatic range calibration.

This focuser uses the same motor used by ZWO in the older EAF models (Non pro models)
The 3d models are also modelled based on ZWO's front plate so whatever focuser the eaf is compatible with, this is also compatible.
Powered by USB C 5V.
Includes a single button for manual focusing (each press changes direction).

<img width="758" height="658" alt="image" src="https://github.com/user-attachments/assets/4cdb19ac-4699-4f49-9c2c-0dc32b65c3b4" />
<img width="617" height="702" alt="image" src="https://github.com/user-attachments/assets/d24b0105-2b52-49c5-b18a-e582102fa68a" />

## 3D Models

There are multiple versions for models.
You can get the motor ZWO used in their older models (described below, imaged above)
Or you could use a basic BYJ motor with a simple modification (How to modify: https://www.youtube.com/watch?v=kCoWSqSAGug)
Its great for lenses with suitable reduction!
![WhatsApp Image 2026-03-04 at 00 19 02](https://github.com/user-attachments/assets/f3d446ba-3176-48ff-9d81-0ab848fd1aed)
![WhatsApp Image 2026-01-04 at 11 39 28](https://github.com/user-attachments/assets/8c9f9e35-6b7d-4f7a-be30-97b1d1d1f248)
![WhatsApp Image 2026-03-04 at 00 26 04](https://github.com/user-attachments/assets/4ba3dbff-8b33-45f8-b408-64b82b9ebc21)

Or you could retrofit the motor and front plate of the really old 12V ZWO focuser
![WhatsApp Image 2026-01-10 at 22 27 24](https://github.com/user-attachments/assets/e524d956-3fb2-4f9e-a0f9-98c98d939e50)

All versions can be found in the 3d models folder

## App
The app is a single instance that can communicate with multiple drivers and funnels commands to the focus motor.
The app has many extra controls not possible to implement in control software like nina, sgp etc...
<img width="1451" height="997" alt="image" src="https://github.com/user-attachments/assets/ec4229a1-f2a8-4f94-a811-841edca71723" />


## Disclaimer
This is NOT a beginner friendly project, since its all SMD components.
But this also is not a hard SMD project, just need the right tools.
Mainly a hot plat and\or hot air gun.
This is one of my first SMD projects, and im positive this has a LOT of room for improvement.
Please do feel free to reach out for contribution!

## BOM
For the motor you need to search for this motor: 35YF22GN120R-TF0
I bought it from this Ali Express seller (cheapest i found), but feel free to try and source it yourself:
https://he.aliexpress.com/item/1005004214871047.html

Technically, this should work with any 2 phase 4 wire stepper motor (as long as it doesn't draw too much current).
You'd just need to redesign the 3d model to fit your motor.

I've actually tested on a Nema17 39mm motor, and it seems to work fine, albeit low torque obviously.

The rest of the components are in the kicad folder BOM file.
*note: In the kicad project some components have LCSC part numbers, and some dont.
This is due to me using components i already had, and the rest i ordered from LCSC.
For the components without LCSC part number, you can just search by name in ali express for example.

## Hardware
The connection holes are meant to be fit with m3\4 heatset inserts (these: https://he.aliexpress.com/item/1005007481465353.html)
The PCB and backplate are held with m2 heatset inserts with m2 6mm screws.

## Software
The software is originally based on DarkSkyGeek's OAG focuser project.
A lot of the code has changed, most of it is different.
I'd like to  thank Julian for the inspiration 🙏

In the "Code" Folder you will find the Installer folder, in it the Output, that file will install both Ascom driver and mediator app.

This will allow multiple client connections to the focuser and give extra functionalities like:
- Auto\Manual limits calibration
- Manually settings position, max steps, reverse direction and more..

To flash the firmware on the ESP32, simply go to the arduino firmware folder, open the ino file in arduino IDE, compile and upload.
Make sure to have the ESP32 boards setup (Board manager) and set Xiao ESP32C3 (or S3)
And the following libraries:
- TMCStepper library by teemuatlut.
- EspSoftSerial by Dirk Kaar, Peter Lerup

## Automatic limits calibration
This is very motor and configuration specific.
Not every motor behaves the same since the TMC's way of detecting stalls is back EMF measurement, and your motor might not behave as well as mine with the same configurations.
What can you do to fine tune?
There are multiple parameters in the esp firmware to look for:

```
#define STALL_COUNT_THRESHOLD 2 // How many time should the stall interrupt be raised before we consider real stall
#define STALL_TIME_THRS 300     // In what time frame should these stalls be detected (in milliseconds)
#define STALL_GRACE_PERIOD 1000 // When starting motor, there are many false detections, how long should we ignoe stall detections (in milliseconds)

const uint8_t stall_guard_threshold = 211; // A tmc configuration for stall threshold, higher means less sensitive to detections
```

If limit calibration isn't working for you, try playing around with these parameters according to your situation.
For example:
Lets say the motor hits a hard physical limit but motor still makes noise trying to move (IE stall wasn't detected),
Try changing one of the following one by one (changing multiple parameters might make fine tuning harder):
- stall_guard_threshold = Start with lowering this slowly and see if anything changes
- STALL_TIME_THRS = Increase this to allow slow stall detections to accumulate
- STALL_COUNT_THRESHOLD = You could reduce this, but i suggest not doing that since that might increase false detections

If this doesn't work, you could set DEBUG to 1 and look for stall logs, and see how ofter if stalls are detected at all.
If they are not detected, or they are detected but too slow, reduce stall_guard_threshold.
