# Nep Size plugins
This repository contains the source code of the size mods I developed for the following games:
- Neptunia: Sisters Vs Sisters
- Neptunia Game Maker R:Evolution
- Neptunia Riders VS Dogoos
- Super Neptunia RPG

Prebuilt versions including user instructions will follow soon.

## NepSizeCore
This contains some core functionality which is shared in all games. 
All mods also spawn a web server on a preset port - this one has a websocket based communication layer to the respective mod as well.
This one is built against .NET 3.5 - simply as it's the lowest used version in Unity contexts - and needed by the Super Neptunia RPG mod.

All interactions with Unity are done in the game specific mods - as the games all use different Unity versions (in case of SvS it even uses Mono rather than IL2CPP) this project only contains communication layers - even if the Main plugins are also using pretty similar code.

## Game specific projects:

- **NepSizeSVSMono**: This is for the [SvSFix](https://github.com/KingKrouch/SvSFix) version of Sisters vs Sisters. As of such this is only working on the Steam version of the game, BepInEx 6 does not like the IL2CPP version of this game. The default webserver port is 8989.
- **NepSizeGMRE**: This mod is a BepInEx 6.0-pre-2 based mod for the Xbox PC version of Neptunia Game Maker R:Evolution. It is very likely this will also work on the Steam version. The default webserver port is 7979.
- **NepSizeNepRiders**: And this one is for Neptunia Riders - this is also BepInEx 6.0-pre-2 based. The default webserver port is 9898.
- **NepSizeYuushaNeptune**: And now we have Super Neptunia RPG (Yuusha Neptune in Japan) - this one uses a very old version of Unity so it is compiled using .NET 3.5 standards. The default webserver port is 7878.

# Compilation instruction
All these tools can be built using normal dotnet console client or using VS 2022. The UI tool has some dependencies to WebView2 but NuGet should auto resolve those.

You will need to supply the BepInEx interop libraries for each project yourself.

# Licenses
- [BepinEx](https://github.com/BepInEx/BepInEx) is licensed under the GNU Lesser General Public License v2.1.
- [websocket-sharp](https://github.com/sta/websocket-sharp) This project uses websocket-sharp and includes code of it in NepSizeCore. Websocket-sharp is licensed under the MIT license.
- [Deli.Newtonsoft.Json](https://www.nuget.org/packages/Deli.Newtonsoft.Json/12.0.3) This is a fork of the popular Newtonsoft.Json library to function in Unity contexts. It has been built using the build.cmd inside the package with HAVE_CAS disabled and a simple fix in in JsonTypeReflector.cs. The library is included in *NepSizeCore/Libary/Deli.Newtonsoft.Json.dll* in a compiled form. [The modifications are provided on this fork repository](https://github.com/haolink/Deli.Newtonsoft.Json). It is licensed under the MIT license.

This project is licensed under the BSD-3 license. See LICENSE for more details.