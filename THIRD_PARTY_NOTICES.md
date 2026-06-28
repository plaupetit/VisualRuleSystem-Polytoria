# Third-Party Notices

This file records known third-party licenses for the VisualRuleSystem Polytoria
C# source tree.

This is a practical attribution file, not legal advice. The main project source
is licensed under MPL-2.0; third-party components remain under their own
licenses.

## Project License

| Component | License | Notes |
| --- | --- | --- |
| VisualRuleSystem Polytoria C# source code | MPL-2.0 | Fan/hobby visual scripting tool for Polytoria 2.0. See `LICENSE`. |

## Polytoria

This project targets Polytoria 2.0 and uses public Polytoria concepts, APIs,
object names, and file/script workflows.

| Component | License / status | Notes |
| --- | --- | --- |
| Polytoria 2.0 source repository | MPL-2.0 unless otherwise noted | Official repository: `https://github.com/Polytoria/polytoria-game`. If any file in this project is copied or modified from Polytoria source, keep that file under MPL-2.0 and preserve notices. |
| Polytoria brand assets, logos, names, and trademarks | Not licensed for reuse by the source license | Do not treat Polytoria branding as open-source assets. This project is unofficial and not endorsed by Polytoria. |
| Polytoria third-party assets/native binaries | Separate licenses | Check the relevant upstream package or repository before copying assets or binaries. |

The public Polytoria 2.0 API documentation is used as interoperability
documentation. The repository does not intentionally include Polytoria brand
assets or bundled Polytoria runtime binaries.

## AI-Assisted Development

This project was created with AI assistance. The project license remains
MPL-2.0 for this repository's source code, and third-party materials remain
under their original licenses. The maintainer's hobby/non-commercial intent is
project context only and is not an additional license restriction.

## Icons And Local Assets

Only the selected SVG icons used by the app should be committed. Full downloaded
icon source snapshots are local selection material and are ignored by
`.gitignore`.
No Polytoria logo, icon, or official brand asset is used by the app icon set.

| Component | Version / source | License | Included notice |
| --- | --- | --- | --- |
| Phosphor Icons | `https://github.com/phosphor-icons/core` | MIT | `src/Vrs.App/Assets/Icons/PolytoriaLike/LICENSE-Phosphor.txt` |
| Tabler Icons | `https://github.com/tabler/tabler-icons` | MIT | `src/Vrs.App/Assets/Icons/PolytoriaLike/LICENSE-Tabler.txt` |

## NuGet Dependencies

Resolved package metadata was checked from the local NuGet packages used by the
solution. Most dependencies are restored at build time and are not committed as
source.

| Package | Observed version | License | Role |
| --- | ---: | --- | --- |
| Avalonia | 12.0.4 | MIT | Desktop UI framework |
| Avalonia.Controls.ColorPicker | 12.0.4 | MIT | Color picker UI |
| Avalonia.Desktop | 12.0.4 | MIT | Desktop app host |
| Avalonia.Fonts.Inter | 12.0.4 | MIT | Bundled Inter font support |
| Avalonia.Themes.Fluent | 12.0.4 | MIT | UI theme |
| Avalonia.FreeDesktop | 12.0.4 | MIT | Transitive Avalonia support |
| Avalonia.FreeDesktop.AtSpi | 12.0.4 | MIT | Transitive Avalonia support |
| Avalonia.HarfBuzz | 12.0.4 | MIT | Text shaping integration |
| Avalonia.Native | 12.0.4 | MIT | Native Avalonia support |
| Avalonia.Remote.Protocol | 12.0.4 | MIT | Avalonia diagnostics/runtime support |
| Avalonia.Skia | 12.0.4 | MIT | Skia rendering backend |
| Avalonia.Win32 | 12.0.4 | MIT | Windows backend |
| Avalonia.X11 | 12.0.4 | MIT | Linux X11 backend |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-like ANGLE license file | Windows native rendering dependency; preserve package license when distributing binaries. |
| Avalonia.BuildServices | 11.3.2 | MIT | Build-time Avalonia support |
| AvaloniaUI.DiagnosticsSupport | 2.2.1 | License not declared in local `.nuspec` | Debug-only diagnostics package in this project. Verify upstream terms or remove before publishing debug binaries. |
| CommunityToolkit.Mvvm | 8.4.1 | MIT | MVVM helpers |
| ExCSS | 4.3.1 | MIT | Transitive SVG/CSS parsing |
| HarfBuzzSharp | 8.3.1.3 | MIT | Text shaping |
| HarfBuzzSharp.NativeAssets.Linux | 8.3.1.3 | MIT | Native HarfBuzz assets |
| HarfBuzzSharp.NativeAssets.macOS | 8.3.1.3 | MIT | Native HarfBuzz assets |
| HarfBuzzSharp.NativeAssets.WebAssembly | 8.3.1.3 | MIT | Native HarfBuzz assets |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT | Native HarfBuzz assets |
| MicroCom.Runtime | 0.11.4 | MIT | Transitive Avalonia support |
| Microsoft.CodeCoverage | 17.14.1 | MIT | Test coverage dependency |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0 | MIT | Transitive diagnostics support |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | MIT | Logging abstractions |
| Microsoft.IO.RecyclableMemoryStream | 3.0.1 | MIT | Transitive diagnostics support |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test runner |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT | Test platform |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT | Test host |
| Newtonsoft.Json | 13.0.3 / 13.0.4 | MIT | Test/runtime transitive dependency |
| ShimSkiaSharp | 5.1.1 | MIT | SVG/Skia transitive dependency |
| SkiaSharp | 3.119.4 | MIT | Graphics backend |
| SkiaSharp.NativeAssets.Linux | 3.119.4 | MIT | Native Skia assets |
| SkiaSharp.NativeAssets.macOS | 3.119.4 | MIT | Native Skia assets |
| SkiaSharp.NativeAssets.WebAssembly | 3.119.4 | MIT | Native Skia assets |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | MIT | Native Skia assets |
| Svg.Animation | 5.1.1 | MIT | SVG transitive dependency |
| Svg.Controls.Skia.Avalonia | 12.0.0.13 | MIT | SVG display in Avalonia |
| Svg.Custom | 5.1.1 | MS-PL | SVG transitive dependency |
| Svg.Model | 5.1.1 | MIT | SVG transitive dependency |
| Svg.SceneGraph | 5.1.1 | MIT | SVG transitive dependency |
| Svg.Skia | 5.1.1 | MIT | SVG/Skia integration |
| Tmds.DBus.Protocol | 0.92.0 | MIT | Linux DBus support |
| YamlDotNet | 16.3.0 | MIT | YAML parser for the Polytoria API coverage generator |
| xunit | 2.9.3 | Apache-2.0 | Tests |
| xunit.abstractions | 2.0.3 | xUnit project license URL in package metadata | Test abstraction package |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Test analyzers |
| xunit.assert | 2.9.3 | Apache-2.0 | Tests |
| xunit.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | Tests |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Tests |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Test runner |
| coverlet.collector | 6.0.4 | MIT | Test coverage |

## Binary Distribution Reminder

This repository is prepared as a source repository. If publishing packaged
binaries, include corresponding source availability information for MPL-2.0
covered code and preserve third-party binary license notices from restored
NuGet packages.
