import assert from "node:assert/strict";
import { mapConcurrent } from "./map-concurrent.mjs";
import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { frozenWorkoutProtocols } from "../web/workout-protocol-history.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const assets = path.join(root, "NomadicMethod/Assets");
const sha256 = async (directory, file) => createHash("sha256").update(await readFile(path.join(directory, file))).digest("hex");

// Review the whole pose independently of the main action's Alternating label.
// Replacing footage invalidates its human review. A fixed position needs the
// opposite mirror in every owning sequence, including linked members and each
// direction segment. Merely adding a SideSequence string cannot satisfy this.
export function findPositionCoverageDeficiencies(catalog, reviews) {
  const failures = [];
  for (const review of reviews) {
    if (review.coverage !== "OppositeBlocks") continue;
    const owners = catalog.filter(e => e.sequenceBlocks.some(b => b.exerciseId === review.id));
    if (owners.length === 0) failures.push({ id: review.id, reason: "No owning sequence" });
    for (const owner of owners) {
      const blocks = owner.sequenceBlocks.filter(b => b.exerciseId === review.id);
      for (const segment of new Set(blocks.map(b => b.mediaSegment))) {
        const positions = new Set(blocks.filter(b => b.mediaSegment === segment).map(b => b.mirrorMedia));
        if (!positions.has(false) || !positions.has(true))
          failures.push({ id: review.id, ownerId: owner.id, segment, reason: "Missing opposite position" });
      }
    }
  }
  return failures;
}

export async function checkFixedPositionReviews(assetsRoot = assets) {
  const catalog = JSON.parse(await readFile(path.join(assetsRoot, "exercises.json"), "utf8"));
  const review = JSON.parse(await readFile(path.join(root, "tools/ExercisePositionReviews.json"), "utf8"));
  assert.deepEqual(review.exercises.map(e => e.id).sort((a,b)=>a-b), catalog.map(e=>e.id).sort((a,b)=>a-b),
    "Every catalog entry needs an explicit whole-pose review");
  assert.equal(new Set(review.exercises.map(e => e.id)).size, catalog.length);
  const mediaChecks = [];
  for (const row of review.exercises) {
    assert.ok(["Symmetric", "WholeLoop", "OppositeBlocks"].includes(row.coverage), `Unreviewed pose ${row.id}`);
    assert.ok(row.reason, `Missing review reasoning ${row.id}`);
    const exercise = catalog.find(e => e.id === row.id);
    const files = [exercise.video];
    if (exercise.presentation === "Still") files.push(`exercise_hold_frames/exercise_${String(row.id).padStart(4,"0")}.png`);
    if (exercise.directionSequence !== "None") files.push(`exercise_direction_videos/exercise_${String(row.id).padStart(4,"0")}.mp4`);
    assert.deepEqual(Object.keys(row.media), files, `Reviewed media inventory changed for ${row.id}`);
    for (const file of files) mediaChecks.push([assetsRoot, file, row.media[file], `Pose review is stale: ${file}`]);
  }
  assert.deepEqual(findPositionCoverageDeficiencies(catalog, review.exercises), [], "Fixed positions need complete opposite blocks");
  const reaction = JSON.parse(await readFile(path.join(assets, "reaction_drills.json"), "utf8"));
  const responseFiles = reaction.families.flatMap(f=>f.actions.map(a=>a.video));
  assert.deepEqual(Object.keys(review.reactionMedia), responseFiles);
  for (const file of responseFiles) mediaChecks.push([assets, file, review.reactionMedia[file], `Reaction pose review is stale: ${file}`]);
  await mapConcurrent(mediaChecks, 8, async ([directory, file, expected, message]) =>
    assert.equal(await sha256(directory, file), expected, message));
  const history = JSON.parse(await readFile(path.join(assets, "catalog_protocol_history.json"), "utf8"));
  assert.deepEqual(frozenWorkoutProtocols, history, "Web must use the Android protocol history");
  return { exercises: catalog.length, oppositeBlocks: review.exercises.filter(e=>e.coverage==="OppositeBlocks").length,
    reactionClips: responseFiles.length };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const option = process.argv.indexOf("--assets-root");
  console.log(JSON.stringify(await checkFixedPositionReviews(option < 0 ? assets : path.resolve(process.argv[option + 1]))));
}
