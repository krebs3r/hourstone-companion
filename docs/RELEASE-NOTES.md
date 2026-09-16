# Hourstone Companion 0.1.1

Settings now show unsaved changes and enable **Save changes** only when needed.
Dark, light and Windows appearance remain available. Changes take effect after
saving; failed synchronization does not undo a successful settings save.

Window controls have recognizable Windows symbols, visible hover and focus
states, and a restore symbol when maximized. Maximized windows fit the current
monitor work area so the footer stays above the taskbar. The footer includes the Hourstone
heart and author credit. Character and client rows use original game icons.

Client setup reports each account separately: missing or outdated addon, waiting
for the first in-game save, ready, or unreadable. Sign in to a character with
Hourstone 0.2.0, then log out or use `/reload`. Restarting the Companion alone
does not create the addon source identifier. Last valid observations are kept.

Requires Windows 11 x64 and Hourstone 0.2.0. The first creation of Hourstone_Sync
requires a complete WoW restart; later data refreshes load on login or reload.
Existing 0.1.0 settings, device identity and cached observations remain compatible.

Local preview packages are unsigned. Public signed releases still require the
installation, update and two-device provider checks in Release validation.
