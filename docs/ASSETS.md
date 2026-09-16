# Bundled artwork

Hourstone Companion loads its icons from WPF resources. It does not contact an image server at runtime. The exact source, transformation, dimensions, and SHA-256 of each new image are recorded in [assets-manifest.json](assets-manifest.json).

## Class icons

All 13 supported classes use Blizzard's original `classicon_<token>` images from the [World of Warcraft render CDN](https://render.worldofwarcraft.com/us/icons/56/classicon_warrior.jpg). Each 56 x 56 JPEG was decoded to PNG without cropping, recoloring, or changing the decoded RGB pixels. Tokens are the stable WoW class identifiers, including `DEATHKNIGHT`, `DEMONHUNTER`, and `EVOKER`.

## Client icons

| Client | Original source | Bundled image |
| --- | --- | --- |
| Retail | Battle.net `world-of-warcraft.svg`: gold and blue World of Warcraft emblem | 256 x 256 PNG |
| Mists Classic | Battle.net `wow-mop.svg`: jade Classic emblem, corroborated by the [Mists launch press kit](https://blizzard.gamespress.com/en/World-of-Warcraft-Mists-of-Pandaria-Classic-Launch), `WoW_Mists_of_Pandaria_Classic_Icon_01.png` | 256 x 256 PNG |
| TBC Anniversary | Battle.net `wow-classic.svg`: green Burning Crusade Classic emblem | 256 x 256 PNG |
| Classic Era | Native icon from Blizzard's signed Classic Era executable, version 1.15.9.69722 | Original 32 x 32 PNG |

The three launcher SVGs came from Battle.net 2.52.11.17778 and were rasterized with resvg-py 0.5.0, preserving their original shapes, colors, and aspect ratios. TBC Anniversary uses the original Burning Crusade Classic emblem; it is not a separate Anniversary wordmark. Classic Era uses its original game executable icon. Blizzard also uses that desktop icon for other WoW executables, so it is not claimed to be an exclusive Era emblem or pixel-identical to the launcher. The adjacent client name identifies the family.

The images are a deliberate mix of original launcher and game resources. No game artwork was redrawn, recolored, or enlarged with AI. Private acquisition files and installed-game locations are not part of this repository.

## Hourstone artwork

`Heart.png` is a format conversion of the addon's [Heart.tga](https://github.com/krebs3r/hourstone-azeroth-hours/blob/7919c2f505f9d2a723baa5e41ef4c52629ca1234/Hourstone/Media/Heart.tga). Every RGBA pixel is identical to the 32 x 32 source, including the pink color and antialiasing. `Unknown.png` is an original neutral geometric marker used only for an unrecognized class or client. These two images and the existing Hourstone brand artwork remain covered by the project's MIT license.

## Rights and attribution

The 13 class icons and four client icons are original Blizzard artwork. Copyright and trademark rights remain with Blizzard Entertainment, Inc. **These Blizzard images are excluded from the repository's MIT license.** The manifest documents provenance and does not grant a separate license to Blizzard artwork. Availability through a CDN, press kit, or installed application is not a blanket redistribution permission; applicable Blizzard terms and [trademark usage guidelines](https://www.blizzard.com/en-us/legal/38fd0408-8431-469a-99bc-2cd9eb9462c8/blizzard-entertainment-trademark-usage-guidelines) continue to apply.

World of Warcraft and Blizzard Entertainment are trademarks or registered trademarks of Blizzard Entertainment, Inc. Hourstone Companion is an independent community project and is not affiliated with or endorsed by Blizzard Entertainment.

## Offline verification

Run `python tools/verify_assets.py`. It verifies the complete set of 13 classes, four clients, heart, and unknown marker against the manifest, validates PNG chunk checksums and dimensions, and rejects missing, additional, or changed catalog images. It needs only Python's standard library and makes no network requests.
