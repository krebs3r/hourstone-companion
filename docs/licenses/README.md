# Dependency license sources

The bundled notices preserve the license text of the dependency versions used by
this project. Packaging also includes license and third-party notice files supplied
by the restored .NET runtime packages.

| File | Upstream source |
| --- | --- |
| Velopack-1.2.0.txt | [Velopack at its NuGet repository commit](https://github.com/velopack/velopack/blob/f2edcbcafb81da5b3c884aaea330e225ad91d8b6/LICENSE) |
| SQLitePCLRaw-2.1.12.txt | [SQLitePCL.raw v2.1.12](https://github.com/ericsink/SQLitePCL.raw/blob/v2.1.12/LICENSE.TXT) |
| Microsoft-10.0.12.txt | LICENSE.TXT from the restored [Microsoft.NETCore.App.Runtime.win-x64 10.0.12 package](https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/10.0.12) |

Dependency upgrades must review the corresponding license files. The packaging
script refuses dependencies without a bundled license text. The generated
CycloneDX inventory records package names, versions, license expressions and package
URLs without local filesystem paths.
