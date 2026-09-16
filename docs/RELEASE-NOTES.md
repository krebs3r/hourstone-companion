# Hourstone Companion 0.1.2

The character list now shows each character's last recorded guild. Search matches
both character and guild names. Full guild names remain available in tooltips
when the window is narrow. **No guild** and **Not yet recorded** are distinct,
localized states.

Guild updates have their own server timestamp. A guild change or departure can
update the overview without replacing a newer playtime baseline. Unknown guild
data never clears a known membership. Received guild information is not
republished as a local observation.

Update the WoW addon to **Hourstone 0.2.1** and the companion to **0.1.2** on every
PC. Each character needs a login with the updated addon, then logout or `/reload`
to populate its guild. Offline characters retain their last saved status.
The version 2 sync format carries guilds; earlier version 1 snapshots and cached
observations remain readable. Old companion versions cannot read version 2 files
and retain their last valid received data until updated.

Settings, device identity, selected clients and cached playtime from 0.1.0 and
0.1.1 remain compatible. Local preview packages are unsigned; public releases
still require signing and the practical checks in [Release validation](RELEASING.md).
