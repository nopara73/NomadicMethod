import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../../", import.meta.url);
const source = (file) => readFile(new URL(file, root), "utf8");

test("Android uses the Nomadic Method package, activities, and storage", async () => {
  const [project, activity, privacy, preferences, database, strings] = await Promise.all([
    source("NomadicMethod/NomadicMethod.csproj"),
    source("NomadicMethod/MainActivity.cs"),
    source("NomadicMethod/RecoveryPrivacyActivity.cs"),
    source("NomadicMethod/Data/SharedPreferencesWorkoutStateStore.cs"),
    source("NomadicMethod/Data/SqliteExerciseDatabase.cs"),
    source("NomadicMethod/Resources/values/strings.xml"),
  ]);
  assert.match(project, /<ApplicationId>com\.local\.nomadicmethod<\/ApplicationId>/);
  assert.match(activity, /Name = "com\.local\.nomadicmethod\.MainActivity"/);
  assert.match(privacy, /Name = "com\.local\.nomadicmethod\.RecoveryPrivacyActivity"/);
  assert.match(preferences, /PreferencesName = "nomadic_method_workout_state"/);
  assert.match(database, /DatabaseFileName = "nomadic_method_exercises\.db"/);
  assert.match(strings, /name="app_name">Nomadic Method<\/string>/);
});

test("the web app and instant controls share Nomadic Method workout storage", async () => {
  const [app, startup, html] = await Promise.all([
    source("web/app.js"), source("web/instant-controls.js"), source("web/index.html"),
  ]);
  assert.match(app, /STORAGE_KEY = "nomadic-method\.workout\.state\.v1"/);
  assert.match(startup, /storageKey = "nomadic-method\.workout\.state\.v1"/);
  assert.match(app, /window\.nomadicMethodStartupControls/);
  assert.match(startup, /window\.nomadicMethodStartupControls = controller/);
  assert.match(html, /<title>Nomadic Method<\/title>/);
});
