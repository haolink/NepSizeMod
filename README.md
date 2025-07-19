# Nep Size plugins
This repository contains the source code of the size mods I developed for the following games:
- Neptunia: Sisters Vs Sisters
- Neptunia Game Maker R:Evolution
- Neptunia Riders VS Dogoos

It also contains the source code of the UI application to control the scales for each character. The project structure is as follows.

Prebuilt versions including user instructions will follow soon.

## NepSizeCore
This contains some core functionality which is shared in all games. In all games the UI communicates with the mod using Named Pipes - this core contains the pipe server and implements the commands which are sent to it. It also reserves memory for the UI to interact with as the main size changes are done using memory tampering.
Other than the UI all mods also spawn a web server on port 7979 - this one has a websocket based communication layer to the respective mod as well.

All interactions with Unity are done in the game specific mods - as the games all use different Unity versions (in case of SvS it even uses Mono rather than IL2CPP) this project only contains communication layers - even if the Main plugins are also using pretty similar code.

## Game specific projects:

- **NepSizeSVSMono**: This is for the [SvSFix](https://github.com/KingKrouch/SvSFix) version of Sisters vs Sisters. As of such this is only working on the Steam version of the game, BepInEx 6 does not like the IL2CPP version of this game. 
- **NepSizeGMRE**: This mod is a BepInEx 6.0-pre-2 based mod for the Xbox PC version of Neptunia Game Maker R:Evolution. It is very likely this will also work on the Steam version.
- **NepSizeNepRiders**: And this one is for Neptunia Riders - this is also BepInEx 6.0-pre-2 based

## NepSizeUI
This is a project containing the Winforms UI which is currently used to control the mods. In its current form it's untested on Linux and it might not work as I don't know how well Named Pipes work on Linux. I plan to rewrite NepSizeCore to be a Websocket based server at some time at which point it would be possible to make simple HTML+JS based clients.

To work towards fully web based clients, this current version is essentially just using WebView2 for its UI - while it directly tampers with the game memory in its current form NepSizeCore also has options to provide this info using its JSON interface which is currently using Named Pipes.

# Compilation instruction
All these tools can be built using normal dotnet console client or using VS 2022. The UI tool has some dependencies to WebView2 but NuGet should auto resolve those.

You will need to supply the BepInEx interop libraries for each project yourself.

# Licenses
- [BepinEx](https://github.com/BepInEx/BepInEx) is licensed under the GNU Lesser General Public License v2.1.
- [websocket-sharp](https://github.com/sta/websocket-sharp) This project uses websocket-sharp and includes code of it in NepSizeCore. Websocket-sharp is licensed under the MIT license.

This project is licensed under the BSD-3 license. See LICENSE for more details.