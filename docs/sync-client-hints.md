# Client hints

Companion 0.2.1 publishes an optional display hint beside its device snapshot so
that other PCs can label WoW: Forever characters. It is a Companion-only extension:
the WoW addon never reads it, and it is not part of the pinned synchronization
contract. Forever characters keep the `retail` flavor everywhere in that contract;
the pinned addendum only adds the list of Forever sources to the generated data.

## File

`<deviceId>.clients` in the `HourstoneSync` folder, UTF-8 JSON:

```json
{
  "formatVersion": 1,
  "groupId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "deviceId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "foreverSources": ["synthetic-source"]
}
```

A device writes the file while at least one enabled source is a WoW: Forever
installation, recognized by its client folder `_forever_`. Otherwise it deletes its
own file. Source IDs are unique and sorted in ordinal order.

## Reading

Readers ignore their own file and accept a hint only when it has format 1, the
current group ID, the device ID of its file name, at most 128 unique valid source
IDs, no unknown or duplicate properties and at most 64 KiB. A hint labels only the
observations in that device's own snapshot whose `sourceId` it lists.

A copied `WTF` folder keeps the source ID of the database it was copied from. As in
source discovery, the Forever copy yields to the other client: a reader drops a
source ID from its Forever set when one of its enabled sources outside `_forever_`
uses it, or when a peer snapshot carries an observation with it that this peer's
hint does not list, including a peer without a hint. A character is labelled when
its winning playtime observation comes from a source in that set.

Hints are display data. An unreadable, invalid or newer hint keeps that device's
last valid hint and never raises a synchronization issue. Case variants of one
device's file name count once. A device's own hint that cannot be written or
withdrawn is retried in the next cycle, also without an issue. Pausing keeps cached
hints; disconnecting removes them. Hints never change flavor, identity, merging,
totals or revisions. From Companion 0.2.2, the reader's Forever set is also written
into its generated data addon as the optional `foreverSources` list of the
[protocol 4 addendum](sync-protocol-v4-forever.md), so that Hourstone 0.3.7 and
newer label these characters in the game as well.

Older Companions enumerate only `*.json` and ignore the file. Forever characters
from a PC that publishes no hint are labelled Retail.
