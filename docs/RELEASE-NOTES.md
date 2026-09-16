# Hourstone Companion 0.1.4

The Clients page now provides **Download addon on CurseForge** before any account
is configured, along with an **Addon on GitHub** source link. Missing and outdated
addons have an install or update action beside their setup instructions. Links
open the Windows default browser; browser-start failures produce a localized hint.

The footer contains the companion GitHub link, version and **with ♥ by krebs3r**.
It stays readable in compact windows while the left-hand hint truncates with a
full tooltip. Selecting any character cell highlights the whole row with one cyan
underline. Selection remains visible when moving to an action, and keyboard focus
has an additional outline in both tracked and removed-character views.

This update keeps protocol 3 and requires Hourstone **0.2.2 or later**. It preserves
existing settings, measurements and removal/restoration controls. Public release
requires a compatible addon to be publicly available on CurseForge as well as the
existing signing and practical validation checks. Local test packages are unsigned.

Automated checks cover fixed link destinations, browser-launch errors, source
readiness actions and selection behavior. Synthetic render checks exercise links,
source states and selected rows with dark/light/system themes, compact windows,
German/English text and 100%, 150% and 200% scaling. Live WoW and two-device provider
checks remain part of the broader release validation.
