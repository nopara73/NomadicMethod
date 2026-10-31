import { spawnSync } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const packageName = "com.local.nomadicmethod";

export function inspectAndroidStartup({ xml, log, processRunning }) {
  const nodes = [...xml.matchAll(/<node\b[^>]*>/g)].map(([tag]) =>
    Object.fromEntries([...tag.matchAll(/([\w-]+)="([^"]*)"/g)].map(([, key, value]) => [key, value])));
  const has = (id, enabled = false) => nodes.some(node =>
    node.package === packageName && node["resource-id"] === `${packageName}:id/${id}` &&
    (!enabled || node.enabled === "true"));
  const setup = has("duration_minutes_value") && has("begin_workout_button", true);
  const workout = has("exercise_name") && ["start_button", "playback_action", "rest_playback_action"]
    .some(id => has(id, true));
  const issues = [];
  if (!processRunning) issues.push("app process is not running");
  if (/\bunavailable\b/i.test(xml)) issues.push("the visible UI reports unavailable");
  if (/FATAL EXCEPTION|Fatal signal|No implementation found for|UnsatisfiedLinkError|Unhandled exception/i.test(log))
    issues.push("fatal runtime failure");
  if (/E\s+NomadicMethod\s*:.*(?:Exception|invalid)/i.test(log) &&
      /InitializeApplication|ReadAndValidateBundledCatalog|ExerciseCatalogValidator/.test(log))
    issues.push("caught application startup exception");
  if (!setup && !workout) issues.push("no enabled setup or workout action is visible");
  return { ok: issues.length === 0, screen: setup ? "setup" : workout ? "workout" : "unconfirmed", issues };
}

async function main() {
  const args = process.argv.slice(2);
  const option = name => args[args.indexOf(name) + 1];
  if (!args.includes("--serial") || !option("--serial"))
    throw new Error("Use --serial with the connected phone's explicit ADB serial.");
  const serial = option("--serial");
  const output = path.resolve(args.includes("--out") ? option("--out") : ".artifacts/android-startup");
  await mkdir(output, { recursive: true });
  const adb = (command, { binary = false, allowFailure = false } = {}) => {
    const result = spawnSync("adb", ["-s", serial, ...command], {
      encoding: binary ? null : "utf8", timeout: 15_000, maxBuffer: 16 * 1024 * 1024, windowsHide: true,
    });
    if (!allowFailure && (result.error || result.status !== 0))
      throw new Error(result.error?.message || String(result.stderr) || `ADB exited ${result.status}`);
    return result.stdout || (binary ? Buffer.alloc(0) : "");
  };
  const packageInfo = adb(["shell", "dumpsys", "package", packageName]);
  const version = packageInfo.match(/versionName=(\S+)/)?.[1];
  if (args.includes("--expected-version") && version !== option("--expected-version"))
    throw new Error(`Expected version ${option("--expected-version")}; the phone has ${version}.`);
  adb(["shell", "run-as", packageName, "id"]);
  adb(["shell", "am", "start", "-n", `${packageName}/.MainActivity`]);
  let last = { ok: false, screen: "unconfirmed", issues: ["startup not checked"] };
  const deadline = Date.now() + 60_000;
  do {
    const pid = adb(["shell", "pidof", "-s", packageName], { allowFailure: true }).trim();
    const log = /^\d+$/.test(pid)
      ? adb(["logcat", "-d", "-v", "threadtime", `--pid=${pid}`, "-t", "1000"], { allowFailure: true }) : "";
    const xml = adb(["exec-out", "uiautomator", "dump", "/dev/tty"], { allowFailure: true });
    last = inspectAndroidStartup({ xml, log, processRunning: /^\d+$/.test(pid) });
    await writeFile(path.join(output, "screen.xml"), xml);
    await writeFile(path.join(output, "startup.log"), log);
    if (last.ok || last.issues.some(issue => /exception|failure|unavailable/.test(issue))) break;
    await new Promise(resolve => setTimeout(resolve, 1000));
  } while (Date.now() < deadline);
  await writeFile(path.join(output, "screen.png"), adb(["exec-out", "screencap", "-p"], { binary: true }));
  const result = { ...last, version, serial, checkedAt: new Date().toISOString(),
    screenshot: path.join(output, "screen.png") };
  await writeFile(path.join(output, "result.json"), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
  if (!last.ok) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href)
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
