# DeFocuser-Lite
An open source DIY focus motor with automatic range calibration.

This focuser uses the same motor used by ZWO in the older EAF models (Non pro models)
The 3d models are also modelled based on ZWO's front plate so whatever focuser the eaf is compatible, this is also compatible.
Powered by USB C 5V.
Includes a single button for manual focusing (each press changes direction).

<img width="758" height="658" alt="image" src="https://github.com/user-attachments/assets/4cdb19ac-4699-4f49-9c2c-0dc32b65c3b4" />
<img width="617" height="702" alt="image" src="https://github.com/user-attachments/assets/d24b0105-2b52-49c5-b18a-e582102fa68a" />

## Disclaimer
This is NOT a begginer friendly project, since its all SMD components.
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

The rest of the components are in the kicad folder BOM file.
*note: In the kicad project some components have LCSC part numbers, and some dont.
This is due to me using components i already had, and the rest i ordered from LCSC.

## 3d model
The connection holes are meant to be fit with m3 heatset inserts (these: https://he.aliexpress.com/item/1005007481465353.html)
The PCB and backplate are held with m2 heatset inserts with m2 6mm screws.

# Software
In the software folder you can find the ascom driver installer + standalone app.
In the standalone app you have extra functionality like settings the current\max position, Auto calibration etc...
I'd love to be able to create the ascrom driver app and standalone as one app (like the zwo mount for example), 
but i currently dont have the knowledge or time to implement that (maybe some day).
