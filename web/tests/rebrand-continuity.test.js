import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../../", import.meta.url);
const source = (file) => readFile(new URL(file, root), "utf8");

test("the rebrand updates the existing Android installation and launcher", async () => {
  const [project, activity, privacy, preferences, database] = await Promise.all([
    source("NomadicMethod/NomadicMethod.csproj"),
    source("NomadicMethod/MainActivity.cs"),
    source("NomadicMethod/RecoveryPrivacyActivity.cs"),
    source("NomadicMethod/Data/SharedPreferencesWorkoutStateStore.cs"),
    source("NomadicMethod/Data/SqliteExerciseDatabase.cs"),
  ]);
  // These are identities from the already-installed app, not derived from its new name.
  assert.match(project, /<ApplicationId>com\.local\.flux<\/ApplicationId>/);
  assert.match(activity, /Name = "crc648a276c800321e548\.MainActivity"/);
  assert.match(privacy, /Name = "com\.local\.flux\.RecoveryPrivacyActivity"/);
  assert.match(preferences, /PreferencesName = "flux_workout_state"/);
  assert.match(database, /DatabaseFileName = "flux_exercises\.db"/);
});

test("the renamed web app and instant controls share existing workout storage", async () => {
  const [app, startup] = await Promise.all([
    source("web/app.js"), source("web/instant-controls.js"),
  ]);
  assert.match(app, /STORAGE_KEY = "flux\.workout\.state\.v1"/);
  assert.match(startup, /storageKey = "flux\.workout\.state\.v1"/);
  assert.match(app, /window\.nomadicMethodStartupControls/);
  assert.match(startup, /window\.nomadicMethodStartupControls = controller/);
});
