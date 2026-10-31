import assert from "node:assert/strict";
import test from "node:test";
import { inspectAndroidStartup } from "../../tools/check-android-startup.mjs";

const setup = `<hierarchy><node package="com.local.nomadicmethod" resource-id="com.local.nomadicmethod:id/duration_minutes_value" text="3"/><node package="com.local.nomadicmethod" resource-id="com.local.nomadicmethod:id/begin_workout_button" enabled="true"/></hierarchy>`;
const exception = `E NomadicMethod: System.InvalidOperationException: The bundled exercise catalog does not satisfy its required invariants.\nE NomadicMethod: at NomadicMethod.MainActivity.InitializeApplicationAsync()`;

test("a live process on the unavailable screen fails startup verification", () => {
  const xml = setup.replace('enabled="true"', 'enabled="false"').replace("</hierarchy>", '<node text="Nomadic Method is unavailable."/></hierarchy>');
  const result = inspectAndroidStartup({ xml, log: exception, processRunning: true });
  assert.equal(result.ok, false);
  assert.ok(result.issues.includes("the visible UI reports unavailable"));
  assert.ok(result.issues.includes("caught application startup exception"));
});

test("caught startup errors fail even when an enabled control is present", () => {
  assert.equal(inspectAndroidStartup({ xml: setup, log: exception, processRunning: true }).ok, false);
});

test("usable setup controls and a clean startup log pass", () => {
  assert.deepEqual(inspectAndroidStartup({ xml: setup, log: "I NomadicMethod: ready", processRunning: true }),
    { ok: true, screen: "setup", issues: [] });
});

test("a loading screen or absent process cannot count as a successful startup", () => {
  assert.equal(inspectAndroidStartup({ xml: "<hierarchy/>", log: "", processRunning: true }).ok, false);
  assert.equal(inspectAndroidStartup({ xml: setup, log: "", processRunning: false }).ok, false);
});
