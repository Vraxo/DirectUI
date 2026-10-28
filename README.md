# 🖥️ DirectUI

DirectUI is a .NET UI library with drawing backends for Direct2D, Raylib, SDL3, and SkiaSharp. This repository also contains desktop applications built with the library.

## 📦 Projects

| Project | Description |
| --- | --- |
| [DirectUI](DirectUI/) | UI library, widgets, layout, and rendering backends |
| [Bankan](Bankan/) | Kanban board |
| [Daw](Daw/) | Digital audio workstation |
| [Sonorize](Sonorize/) | Audio player |
| [Tagra](Tagra/) | Tagging application |
| [CherrisEditor](CherrisEditor/) | Editor for the external Cherris projects |

## 🔧 Build and run

Install the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0). The projects use native graphics and audio dependencies; Windows is the primary target for the included configurations.

Build the library or run an application from the repository root:

```sh
dotnet build DirectUI/DirectUI.csproj
dotnet run --project Bankan/Bankan.csproj
```

Replace `Bankan` with another application project to run it. The solution and CherrisEditor also reference `../Cherris/Cherris/Cherris.csproj` and `../Cherris/Apexverse/Apexverse.csproj`, which are not included in this repository. Those projects must be present at those paths to build the full solution or CherrisEditor.
