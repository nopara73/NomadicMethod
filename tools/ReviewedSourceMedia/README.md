# Preserved human source footage

These are immutable copies of previously packaged human footage, preserved before
correcting a crop, interval or hold selection. They are generation inputs, not
additional runtime media. `provenance.json` identifies the original publisher,
catalog source entry and exact SHA-256 of each file.

The 14 October 2026 locomotion additions also preserve excerpts downloaded from
the named public human demonstrations. Each source descriptor pins the input
bytes, crop and interval. Excerpts retain natural chronology and cadence;
stacked camera views are cropped to one complete human view. The final packaged
media and workout phases receive their own review.

Some publishers no longer serve the old download URL. A correction may use a
preserved clip only when that clip already contains the complete required action.
It cannot recover body parts outside the frame, invent another side, remove
equipment from a movement, or establish an action that is not visible.

The `LocalSourceFile` and `LocalSourceSha256` fields select and verify this source
explicitly. Trim offsets are relative to the preserved file. Ordinary video
normalization then retains natural chronology and cadence. The original URL stays
in the media descriptor as provenance. Rebuilding never silently falls back to a
generated destination or an unverified cached file.

A preserved source is not a passing final review. The resulting packaged clip,
hold frame, sides, directions and workout timer phases still need explicit review.
