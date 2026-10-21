import assert from "node:assert/strict";
import {readFile} from "node:fs/promises";
import test from "node:test";

const read = path => readFile(new URL(path, import.meta.url), "utf8");
const [activity, layout, html, css] = await Promise.all([
  read("../../NomadicMethod/MainActivity.cs"),
  read("../../NomadicMethod/Resources/layout/screen_duration.xml"),
  read("../index.html"), read("../styles.css"),
]);
const nativeMethod = name => activity.match(new RegExp(`private void ${name}\\([\\s\\S]*?(?=\\n    private )`))[0];
const graphics = method => [...new Set([...nativeMethod(method).matchAll(/Resource\.Drawable\.([a-z_]+)/g)].map(m => m[1]))].sort();

test("Off reuses the first enabled icon; only enabled variants introduce another graphic", () => {
  for (const [name, method, attribute, base, variants, nativeIcons] of [
    ["locomotion", "UpdateLocomotionModifierPresentation", "locomotion-space", "room", ["room", "field"], ["ic_locomotion_room", "ic_locomotion_field"]],
    ["wall", "UpdateWallModifierPresentation", "wall-equipment", "soles-may-touch", ["soles-may-touch", "soles-stay-off"], ["ic_wall", "ic_wall_no_sole"]],
    ["mirror", "UpdateMirrorModifierPresentation", "mirror-equipment", "tall", ["tall", "compact"], ["ic_mirror_tall", "ic_mirror_compact"]],
    ["elevation", "SetSelectedElevationEquipment", "elevation-equipment", "support", ["support", "platform"], ["ic_elevation", "ic_elevation_platform"]],
  ]) {
    const webIcons = [...html.matchAll(new RegExp(`class="[^"\\n]*\\b${name}-glyph-([a-z-]+)[^"\\n]*"`, "g"))].map(m => m[1]);
    assert.deepEqual(webIcons.sort(), [...variants].sort(), `${name}: exactly two web graphics`);
    assert.match(css, new RegExp(`\\[data-${attribute}="none"\\]\\s+\\.${name}-glyph-${base}`), `${name}: Off uses its base graphic`);
    assert.deepEqual(graphics(method), [...nativeIcons].sort(), `${name}: same two Android graphics`);
    assert.doesNotMatch(nativeMethod(method), /ic_[a-z_]*(?:_none|_off)\b/, `${name}: no separate Off graphic`);
  }
  assert.deepEqual(graphics("UpdateFloorModifierPresentation"), ["ic_floor_on"]);
  assert.deepEqual(graphics("UpdateHardFloorModifierPresentation"), ["ic_hard_floor"]);
  assert.doesNotMatch(html, /floor-contact-off|floor-glyph-soft/);
  assert.doesNotMatch(layout, /ic_locomotion_none|ic_wall_off|ic_floor_off|ic_elevation_off/);
  assert.match(css, /\.modifier-tile\[aria-pressed="true"\]/);
});
