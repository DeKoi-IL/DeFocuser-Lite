# DeFocuser-Lite
An open source DIY focus motor with automatic range calibration.

This focuser uses the same motor used by ZWO in the older EAF models (Non pro models)
The 3d models are also modelled based on ZWO's front plate so whatever focuser the eaf is compatible with, this is also compatible.
Powered by USB C 5V.
Includes a single button for manual focusing (each press changes direction).

<img width="758" height="658" alt="image" src="https://github.com/user-attachments/assets/4cdb19ac-4699-4f49-9c2c-0dc32b65c3b4" />
<img width="617" height="702" alt="image" src="https://github.com/user-attachments/assets/d24b0105-2b52-49c5-b18a-e582102fa68a" />

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

## 3d model
The connection holes are meant to be fit with m3\4 heatset inserts (these: https://he.aliexpress.com/item/1005007481465353.html)
The PCB and backplate are held with m2 heatset inserts with m2 6mm screws.

# Software
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
