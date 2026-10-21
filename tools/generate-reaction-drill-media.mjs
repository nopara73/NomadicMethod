import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveReactionDrills } from '../web/reaction-drills.js';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const assets = path.join(root, 'NomadicMethod/Assets');
const definitionPath = path.join(assets, 'reaction_drills.json');
const document = JSON.parse(readFileSync(definitionPath, 'utf8'));
const catalog = JSON.parse(readFileSync(path.join(assets, 'exercises.json'), 'utf8'));
resolveReactionDrills(document, catalog);
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const check = process.argv.includes('--check');
for (const family of document.families) {
  for (const action of family.actions) {
    const source = catalog.find(exercise => exercise.id === action.sourceId);
    const input = path.join(assets, source.video);
    if (sha(readFileSync(input)) !== action.sourceVideoSha256) {
      throw new Error(`Re-review changed reaction source ${action.sourceId}.`);
    }
    if (!Number.isFinite(action.sourceStartSeconds) || action.sourceStartSeconds < 0 ||
        !Number.isFinite(action.previewSeconds) || action.previewSeconds < 0 ||
        action.previewSeconds >= action.durationSeconds) throw new Error('Invalid reviewed reaction segment.');
    const output = path.join(assets, action.video);
    const preview = path.join(assets, action.preview);
    if (check) {
      for (const [file, expected] of [[output, action.videoSha256], [preview, action.previewSha256]]) {
        if (!expected || !existsSync(file) || sha(readFileSync(file)) !== expected) {
          throw new Error(`Reaction asset changed or missing: ${file}`);
        }
      }
    } else {
      mkdirSync(path.dirname(output), { recursive: true });
      mkdirSync(path.dirname(preview), { recursive: true });
      execFileSync('ffmpeg', ['-y', '-v', 'error', '-threads', '1', '-ss', String(action.sourceStartSeconds),
        '-i', input, '-t', String(action.durationSeconds), '-an', '-vf', 'fps=20',
        '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p',
        '-map_metadata', '-1', '-movflags', '+faststart', output]);
      execFileSync('ffmpeg', ['-y', '-v', 'error', '-ss', String(action.previewSeconds),
        '-i', output, '-frames:v', '1', '-update', '1', preview]);
      action.durationSeconds = Number(execFileSync('ffprobe', ['-v', 'error', '-show_entries',
        'format=duration', '-of', 'default=nw=1:nk=1', output], { encoding: 'utf8' }).trim());
      action.videoSha256 = sha(readFileSync(output));
      action.previewSha256 = sha(readFileSync(preview));
    }
    const sourceDuration = Number(execFileSync('ffprobe', ['-v', 'error', '-show_entries',
      'format=duration', '-of', 'default=nw=1:nk=1', input], { encoding: 'utf8' }).trim());
    if (action.sourceStartSeconds + action.durationSeconds > sourceDuration + 0.05) {
      throw new Error('Reviewed reaction segment exceeds its exact source.');
    }
  }
}
if (document.families.length !== 5 || document.plans.length !== 15) throw new Error('Expected five reviewed families and fifteen immutable protocols.');
if (!check) writeFileSync(definitionPath, `${JSON.stringify(document, null, 2)}\n`);
console.log(`Reaction media ${check ? 'verified' : 'generated'}: 5 families, 15 plans, 20 exact responses.`);
