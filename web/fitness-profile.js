export const FITNESS_DIMENSIONS = Object.freeze([
  "cardiorespiratoryEndurance", "muscularStrength", "muscularEndurance",
  "flexibility", "agility", "balance", "coordination", "power", "reactionTime", "speed",
]);
export const FITNESS_LABELS = Object.freeze([
  ["cardio"], ["strength"], ["muscle", "endurance"], ["flexibility"],
  ["agility"], ["balance"], ["coordination"], ["power"], ["reaction"], ["speed"],
]);
export const FITNESS_MAXIMUM_LEVEL = 5;
export const FITNESS_WEB_BOUNDARY_LEVEL = 4;
export const FITNESS_EVIDENCE_VERSION = 4;

export function roundFitnessLevel(level) {
  return !Number.isFinite(level) || level <= 0 ? 0
    : Math.min(FITNESS_MAXIMUM_LEVEL, Math.max(1, Math.floor(level + 0.5)));
}

export function formatFitnessMean(mean) {
  // Do not round a mean just below a half up across the chart's rounding boundary.
  const shown = Math.floor(mean * 100 + 1e-9) / 100;
  return String(shown) + (Math.abs(mean - shown) > 1e-9 ? "…" : "");
}

export const FITNESS_UNAVAILABLE_MESSAGE = "No complete workout fits your current training zone with these settings.";

export function fitnessZones(profile) {
  return profile.map(axis => axis.mean > 0 ? [...axis.bestLevels] : []);
}

export function isFitnessEligible(levels, zones, trainingCharacteristics = null) {
  if (!zones.some(zone => zone.length > 0)) return true;
  if (!levels || trainingCharacteristics && trainingCharacteristics.length !== levels.length) return false;
  if (trainingCharacteristics && !trainingCharacteristics.some(Boolean))
    return zones.every((zone, dimension) => !zone.length || levels[dimension]?.some(level => zone.includes(level)));
  // Explore genuinely trained axes without user evidence. An unknown axis
  // cannot bypass a mismatch on another assessed training axis.
  if (trainingCharacteristics && !trainingCharacteristics.some((trained, dimension) =>
    trained && zones[dimension]?.length)) return levels.every(set => Array.isArray(set) && set.length > 0);
  return levels.some((set, dimension) => set.length > 0 &&
    (!trainingCharacteristics || trainingCharacteristics[dimension]) &&
    set.some(level => zones[dimension]?.includes(level)));
}

export function isFitnessSequenceEligible(members, zones, trainingCharacteristics = null) {
  if (trainingCharacteristics && (trainingCharacteristics.length !== members.length || trainingCharacteristics.some(claim => !claim)))
    return false;
  return members.every((levels, index) => isFitnessEligible(levels, zones, trainingCharacteristics?.[index]));
}

export function orderFitnessCandidates(candidates, movementId, tier, random) {
  const draw = () => {
    const unit = random();
    if (!Number.isFinite(unit) || unit < 0 || unit >= 1)
      throw new RangeError("Invalid selection draw.");
    return unit;
  };
  const families = new Map();
  candidates.forEach((item, index) => {
    const id = movementId(item);
    if (!families.has(id)) families.set(id, []);
    families.get(id).push({ item, index, tier: tier(item) });
  });
  return [...families.values()].flatMap(variants => {
    const familyKey = draw();
    return variants.map(entry => ({ ...entry, familyKey, familyIndex: variants[0].index,
      variantKey: variants.length === 1 ? 0 : draw() }));
  }).sort((a, b) => a.tier - b.tier || a.familyKey - b.familyKey ||
    a.familyIndex - b.familyIndex || a.variantKey - b.variantKey || a.index - b.index)
    .map(entry => entry.item);
}

export function fitnessProfileRadiusFraction(level) {
  return level / FITNESS_MAXIMUM_LEVEL;
}

export function parseFitnessRatings(source) {
  if (source?.schemaVersion !== 2 ||
      JSON.stringify(source.dimensions) !== JSON.stringify(FITNESS_DIMENSIONS) ||
      !source.ratings || typeof source.ratings !== "object" || Array.isArray(source.ratings) ||
      !source.trainingCharacteristics || typeof source.trainingCharacteristics !== "object" ||
      Array.isArray(source.trainingCharacteristics)) {
    throw new Error("Invalid fitness rating schema.");
  }
  const ratings = new Map(), trainingCharacteristics = new Map();
  for (const [key, levels] of Object.entries(source.ratings)) {
    const id = Number(key), claims = source.trainingCharacteristics[key];
    if (!Number.isSafeInteger(id) || id <= 0 || !Array.isArray(levels) ||
        levels.length !== FITNESS_DIMENSIONS.length || levels.some(set =>
          !Array.isArray(set) || set.length === 0 || set.some((value, index) =>
            !Number.isInteger(value) || value < 1 || value > 5 ||
            (index > 0 && value <= set[index - 1]))) ||
        !Array.isArray(claims) || claims.length !== FITNESS_DIMENSIONS.length ||
        claims.some(value => typeof value !== "boolean") ||
        levels.some((set, dimension) => !claims[dimension] && (set.length !== 1 || set[0] !== 1))) {
      throw new Error("Every exercise requires ten nonempty fitness ratings from 1 through 5 and reviewed training claims.");
    }
    ratings.set(id, levels);
    trainingCharacteristics.set(id, [...claims]);
  }
  if (Object.keys(source.trainingCharacteristics).length !== ratings.size)
    throw new Error("Training characteristics must match the rated exercises.");
  Object.defineProperty(ratings, "trainingCharacteristics", { value: trainingCharacteristics });
  return ratings;
}

export function serializeFitnessRatings(ratings) {
  if (!(ratings.trainingCharacteristics instanceof Map))
    throw new Error("Reviewed training characteristics are required for worker transport.");
  return { schemaVersion: 2, dimensions: [...FITNESS_DIMENSIONS],
    ratings: Object.fromEntries(ratings),
    trainingCharacteristics: Object.fromEntries(ratings.trainingCharacteristics) };
}

export function hasFitnessTrainingClaim(ratings, exerciseId, dimension) {
  return ratings.trainingCharacteristics
    ? ratings.trainingCharacteristics.get(exerciseId)?.[dimension] === true
    : (ratings.get(exerciseId)?.[dimension]?.length ?? 0) > 0;
}

function strongestTrainingLevel(ratings, exerciseId, levels) {
  return Math.max(0, ...levels.flatMap((set, dimension) =>
    hasFitnessTrainingClaim(ratings, exerciseId, dimension) ? set : []));
}

export function calculateFitnessProfile(exercises, state, ratings) {
  return inspectFitnessProfile(exercises, state, ratings).map(axis => axis.mean);
}

export function inspectFitnessProfile(exercises, state, ratings) {
  const latest = latestFitnessMovementDecisions(exercises, state);
  const profile = FITNESS_DIMENSIONS.map(() => ({ mean: 0, contributions: [], bestLevels: [] }));
  for (const [movementId, { exercise, decision }] of [...latest].sort((a, b) => a[0] - b[0])) {
    const levels = ratings.get(exercise.id);
    if (!levels) continue;
    const strongest = strongestTrainingLevel(ratings, exercise.id, levels);
    levels.forEach((set, dimension) => {
      // Too easy applies to every rated axis; love and hard identify only
      // the strongest demands. Empty cells never imply level zero evidence.
      if (!hasFitnessTrainingClaim(ratings, exercise.id, dimension) ||
          !set.length || decision.outcome !== "easy" && set.at(-1) !== strongest) return;
      const supportedLevels = decision.outcome === "tick" ? [...set]
        : decision.outcome === "easy"
          ? [1, 2, 3, 4, 5].filter(level => level >= Math.min(5, set.at(-1) + 1))
          : [1, 2, 3, 4, 5].filter(level => level <= Math.max(1, set[0] - 1));
      profile[dimension].contributions.push({ movementId, exerciseId: exercise.id,
        name: exercise.name, ratedLevels: [...set], feedback: decision.outcome, supportedLevels });
    });
  }
  for (const axis of profile) {
    if (!axis.contributions.length) continue;
    const candidates = [1, 2, 3, 4, 5];
    const penalties = candidates.map(candidate => axis.contributions.reduce((sum, entry) =>
      sum + Math.min(...entry.supportedLevels.map(level => (candidate - level) ** 2)), 0));
    const minimum = Math.min(...penalties);
    axis.bestLevels = candidates.filter((_, index) => penalties[index] === minimum);
    // Selection retains its discrete best-fit set; drawing keeps the fractional
    // estimate so new feedback need not cross an entire tier to move the chart.
    if (axis.bestLevels.length < FITNESS_MAXIMUM_LEVEL)
      axis.mean = fitFitnessMean(axis.contributions);
  }
  return profile;
}

function fitFitnessMean(evidence) {
  // Supporting every level provides no preference for an integer over a fraction.
  const informative = evidence.filter(entry => entry.supportedLevels.length < FITNESS_MAXIMUM_LEVEL);
  const boundaries = [...new Set([1, FITNESS_MAXIMUM_LEVEL, ...informative.flatMap(entry =>
    entry.supportedLevels.slice(1).map((right, index) => (entry.supportedLevels[index] + right) / 2))])]
    .sort((a, b) => a - b);
  const candidates = [];
  for (let index = 1; index < boundaries.length; index++) {
    const left = boundaries[index - 1], right = boundaries[index], midpoint = (left + right) / 2;
    // Each piece has fixed nearest supported levels and a quadratic loss. Search
    // all pieces so noncontiguous evidence cannot trap the fit in a local minimum.
    const mean = informative.reduce((sum, entry) => sum + entry.supportedLevels.reduce((a, b) =>
      Math.abs(midpoint - a) <= Math.abs(midpoint - b) ? a : b), 0) / informative.length;
    candidates.push(Math.max(left, Math.min(right, mean)));
  }
  const loss = candidate => informative.reduce((sum, entry) => sum +
    Math.min(...entry.supportedLevels.map(level => (candidate - level) ** 2)), 0);
  const minimum = Math.min(...candidates.map(loss));
  const best = [...new Set(candidates)].filter(candidate => Math.abs(loss(candidate) - minimum) < 1e-9);
  return best.reduce((sum, candidate) => sum + candidate, 0) / best.length;
}

export function latestFitnessMovementDecisions(exercises, state) {
  const byId = new Map(exercises.map(exercise => [exercise.id, exercise]));
  const sessions = [...(state.workoutHistory ?? [])];
  if (state.activeWorkoutSession) sessions.push(state.activeWorkoutSession);
  const latest = new Map();
  // Stable ordering mirrors Android, including duplicate active/history evidence.
  const decisions = sessions.sort((a, b) => (a.sessionId ?? 0) - (b.sessionId ?? 0))
    .flatMap(session => session.decisions ?? [])
    .sort((a, b) => (a.decidedAtUnixMilliseconds ?? 0) - (b.decidedAtUnixMilliseconds ?? 0));
  for (const decision of decisions) {
    if (!["tick", "easy", "hard"].includes(decision.outcome)) continue;
    const exercise = byId.get(decision.rootExerciseId);
    if (!exercise) continue;
    const movementId = exercise.sessionMovementId > 0 ? exercise.sessionMovementId : exercise.id;
    latest.set(movementId, { exercise, decision });
  }
  return latest;
}

export function formatFitnessLevels(levels) {
  const parts = [];
  for (let i = 0; i < levels.length; i++) {
    const start = levels[i];
    let end = start;
    while (i + 1 < levels.length && levels[i + 1] === end + 1) end = levels[++i];
    parts.push(start === end ? String(start) : `${start}–${end}`);
  }
  return parts.join(", ");
}

// Retained for legacy profile fixtures and historical interpretation only.
export function calculateFavouriteFitnessProfile(exercises, state, ratings) {
  return inspectFavouriteFitnessProfile(exercises, state, ratings).map(axis => axis.mean);
}

export function inspectFavouriteFitnessProfile(exercises, state, ratings) {
  const slots = Object.values(state.keptExerciseRootIdsBySelectionGroupId ?? {});
  const kept = new Set(slots.length ? slots.flat() : state.lastKeptExerciseIds ?? []);
  const movements = new Map();
  for (const exercise of exercises) {
    if (!kept.has(exercise.id)) continue;
    const levels = ratings.get(exercise.id);
    if (!levels) continue;
    const strongest = strongestTrainingLevel(ratings, exercise.id, levels);
    const movementId = exercise.sessionMovementId > 0 ? exercise.sessionMovementId : exercise.id;
    if (!movements.has(movementId)) movements.set(movementId, new Array(10).fill(null));
    const values = movements.get(movementId);
    levels.forEach((set, dimension) => {
      if (hasFitnessTrainingClaim(ratings, exercise.id, dimension) &&
          set.length === 1 && set[0] === strongest && (!values[dimension] ||
          set[0] > values[dimension].level ||
          (set[0] === values[dimension].level && exercise.id < values[dimension].exerciseId)))
        values[dimension] = { movementId, exerciseId: exercise.id, name: exercise.name, level: set[0] };
    });
  }
  return FITNESS_DIMENSIONS.map((_, dimension) => {
    const contributions = [...movements.values()].map(row => row[dimension]).filter(Boolean)
      .sort((a, b) => a.movementId - b.movementId);
    const mean = contributions.length
      ? contributions.reduce((sum, value) => sum + value.level, 0) / contributions.length : 0;
    return { mean, contributions };
  });
}

let nextProfileId = 0;

export class FitnessProfileView {
  constructor(host) {
    this.host = host;
    this.clipId = "fitness-profile-clip-" + ++nextProfileId;
    this.values = new Array(10).fill(0);
    this.ready = false;
    this.profile = FITNESS_DIMENSIONS.map(() => ({ mean: 0, contributions: [] }));
    this.buttons = FITNESS_LABELS.map((lines, index) => {
      const button = document.createElement("button");
      button.type = "button";
      button.disabled = true;
      button.className = "fitness-axis";
      button.dataset.axis = index;
      button.setAttribute("aria-haspopup", "dialog");
      button.textContent = lines.join(" ");
      button.addEventListener("click", () => this.openDetails(index));
      button.addEventListener("focus", () => this.highlight(index));
      button.addEventListener("blur", () => this.highlight(-1));
      return button;
    });
    this.chart = document.createElement("div");
    this.axes = document.createElement("div");
    this.axes.className = "fitness-axes";
    this.axes.append(...this.buttons);
    const fontProbe = document.createElement("span");
    fontProbe.style.cssText = "position:absolute;width:1em;height:1em;visibility:hidden;pointer-events:none";
    fontProbe.setAttribute("aria-hidden", "true");
    host.append(this.chart, this.axes, fontProbe);
    host.setAttribute("role", "group");
    host.setAttribute("aria-label", "Fitness profile from difficulty feedback");
    host.setAttribute("aria-busy", "true");
    this.observer = new ResizeObserver(() => this.render());
    this.observer.observe(host);
    this.observer.observe(fontProbe);
    this.render();
  }

  setProfile(profile) {
    this.ready = true;
    this.host.setAttribute("aria-busy", "false");
    this.buttons.forEach(button => { button.disabled = false; });
    this.profile = profile;
    this.values = profile.map(axis => axis.mean);
    this.render();
  }

  highlight(index) {
    this.chart.querySelectorAll("[data-axis-ray]").forEach(ray =>
      ray.classList.toggle("is-highlighted", Number(ray.dataset.axisRay) === index));
  }

  openDetails(index) {
    if (!this.ready || this.dialog?.open) return;
    this.dialog?.remove();
    this.dialog = null;
    const previousFocus = document.activeElement;
    const axis = this.profile[index];
    const make = (tag, className, text) => {
      const element = document.createElement(tag);
      element.className = className;
      if (text !== undefined) element.textContent = text;
      return element;
    };
    const dialog = make("dialog", "fitness-details");
    const title = make("h2", "", FITNESS_LABELS[index].join(" "));
    title.id = this.clipId + "-title";
    dialog.setAttribute("aria-labelledby", title.id);
    const close = make("button", "fitness-details-close", "×");
    close.type = "button";
    close.setAttribute("aria-label", "Close fitness details");
    close.autofocus = true;
    close.addEventListener("click", () => dialog.close());
    const header = make("div", "fitness-details-header");
    header.append(title, close);
    const summary = make("p", "fitness-details-summary", axis.mean > 0
      ? "level " + formatFitnessMean(this.values[index]) + " of 5" : "No evidence yet");
    summary.id = this.clipId + "-summary";
    dialog.setAttribute("aria-describedby", summary.id);
    const note = make("p", "fitness-details-note", axis.mean > 0
      ? "Best fit " + formatFitnessLevels(axis.bestLevels) +
        " · " + axis.contributions.length + " rated movement" + (axis.contributions.length === 1 ? "" : "s")
      : axis.contributions.length ? "This feedback still fits every level."
      : "No love/easy/hard feedback has a qualifying rating for this axis.");
    const list = make("ul", "fitness-contributions");
    for (const entry of axis.contributions) {
      const row = make("li", "fitness-contribution");
      row.dataset.movementId = entry.movementId;
      row.dataset.exerciseId = entry.exerciseId;
      const supported = formatFitnessLevels(entry.supportedLevels);
      const level = make("span", "fitness-contribution-level", supported);
      level.setAttribute("aria-label", "supports levels " + supported);
      const name = make("span", "fitness-contribution-name", entry.name);
      name.append(make("small", "fitness-contribution-feedback",
        (entry.feedback === "tick" ? "❤️ love" : entry.feedback === "easy" ? "🥱 too easy" : "💀 too hard") +
          " · rated " + formatFitnessLevels(entry.ratedLevels)));
      row.append(name, level);
      list.append(row);
    }
    dialog.append(header, summary, note, list, make("p", "fitness-details-footnote",
      "Too easy supports levels above the rated range; too hard below it; love within it. Best fit across each movement’s latest response, within 1–5. NM estimates, not measured ability."));
    dialog.addEventListener("click", event => {
      const rect = dialog.getBoundingClientRect();
      if (event.target === dialog && (event.clientX < rect.left || event.clientX > rect.right ||
          event.clientY < rect.top || event.clientY > rect.bottom)) dialog.close();
    });
    dialog.addEventListener("keydown", event => {
      if (event.key === "Tab") { event.preventDefault(); close.focus(); }
    });
    dialog.addEventListener("close", () => {
      const active = document.activeElement;
      const restoreFocus = active === previousFocus || active === document.body || dialog.contains(active);
      dialog.remove();
      // A queued close must not steal focus from a newly selected axis/dialog.
      if (this.dialog !== dialog) return;
      this.dialog = null;
      if (restoreFocus) this.buttons[index].focus({ preventScroll: true });
    }, { once: true });
    this.dialog = dialog;
    document.body.append(dialog);
    dialog.showModal();
  }

  render() {
    const width = Math.round(this.host.getBoundingClientRect().width);
    if (width < 100) return;
    // Fit the web around measured, fixed-size labels instead of reserving a wide margin.
    const style = getComputedStyle(this.host);
    const textSize = parseFloat(style.fontSize);
    const lineHeight = textSize * 1.25;
    const labelGap = Math.max(12, textSize * 0.75);
    const measure = document.createElement("canvas").getContext("2d");
    measure.font = style.font;
    let expertRadius = 180;
    FITNESS_LABELS.forEach((lines, index) => {
      const horizontal = Math.abs(Math.cos(index * Math.PI / 5 - Math.PI / 2));
      if (horizontal < 0.01) return;
      const labelWidth = Math.max(...lines.map(line => measure.measureText(line).width));
      expertRadius = Math.min(expertRadius, (width / 2 - labelWidth - 2) / horizontal - labelGap);
    });
    const bounds = FITNESS_LABELS.map((lines, index) => {
      const horizontal = Math.cos(index * Math.PI / 5 - Math.PI / 2);
      const x = width / 2 + horizontal * (expertRadius + labelGap);
      const w = Math.max(48, ...lines.map(line => measure.measureText(line).width + 8));
      const h = Math.max(48, lineHeight * lines.length + 8);
      const left = Math.max(0, Math.min(width - w, Math.abs(horizontal) < 0.01
        ? x - w / 2 : horizontal > 0 ? x - 4 : x - w + 4));
      return { left, right: left + w, top: Math.sin(index * Math.PI / 5 - Math.PI / 2) *
        (expertRadius + labelGap) - h / 2, height: h };
    });
    const stacked = expertRadius < 40 || bounds.some((a, i) => bounds.slice(i + 1).some(b =>
      a.left < b.right && b.left < a.right && a.top < b.top + b.height && b.top < a.top + a.height));
    this.host.classList.toggle("fitness-stacked", stacked);
    expertRadius = stacked ? Math.min(120, width / 2 - 24) : Math.max(12, expertRadius);
    const labelRadius = expertRadius + labelGap;
    const height = Math.ceil(stacked ? expertRadius * 2 + 32 : Math.max(300, labelRadius * 2 + lineHeight * 3));
    const cx = width / 2, cy = height / 2;
    const point = (index, distance) => {
      const angle = index * Math.PI / 5 - Math.PI / 2;
      return [cx + Math.cos(angle) * distance, cy + Math.sin(angle) * distance];
    };
    const make = (tag, attributes, value) => {
      const element = document.createElementNS("http://www.w3.org/2000/svg", tag);
      for (const [key, val] of Object.entries(attributes ?? {})) element.setAttribute(key, String(val));
      if (value !== undefined) element.textContent = value;
      return element;
    };
    const svg = make("svg", { viewBox: "0 0 " + width + " " + height, "aria-hidden": "true" });
    svg.style.height = height + "px";
    const description = FITNESS_LABELS.map((label, index) =>
      label.join(" ") + ": " + (this.values[index] > 0
        ? formatFitnessMean(this.values[index]) + " of 5"
        : "no qualifying difficulty feedback")).join("; ");
    svg.append(make("desc", {}, description));
    const vertices = this.values.map((value, index) =>
      point(index, expertRadius * fitnessProfileRadiusFraction(value)));
    const shape = vertices.map(vertex => vertex.join(",")).join(" ");
    const appendWeb = (target, className) => {
      for (let level = 1; level <= FITNESS_WEB_BOUNDARY_LEVEL; level++)
        target.append(make("polygon", { points: FITNESS_DIMENSIONS.map((_, index) =>
          point(index, expertRadius * fitnessProfileRadiusFraction(level)).join(",")).join(" "),
          class: className, ...(className === "fitness-grid" ? { "data-web-level": level } : {}) }));
      FITNESS_DIMENSIONS.forEach((_, index) => {
        const [x, y] = point(index, expertRadius * fitnessProfileRadiusFraction(FITNESS_WEB_BOUNDARY_LEVEL));
        target.append(make("line", { x1: cx, y1: cy, x2: x, y2: y, class: className }));
      });
    };
    appendWeb(svg, "fitness-grid");
    svg.append(make("polygon", { points: shape, class: "fitness-area" }));
    const clip = make("clipPath", { id: this.clipId });
    clip.append(make("polygon", { points: shape }));
    const defs = make("defs");
    defs.append(clip);
    svg.append(defs);
    const innerWeb = make("g", { "clip-path": "url(#" + this.clipId + ")" });
    appendWeb(innerWeb, "fitness-inner-grid");
    svg.append(innerWeb);
    svg.append(make("polygon", { points: shape, class: "fitness-shape" }));
    vertices.forEach(([x, y], index) => {
      if (this.values[index] > 0)
        svg.append(make("circle", { cx: x, cy: y, r: 3.5, class: "fitness-point", "data-profile-axis": index }));
    });
    FITNESS_DIMENSIONS.forEach((_, index) => {
      const [x, y] = point(index, expertRadius * fitnessProfileRadiusFraction(FITNESS_WEB_BOUNDARY_LEVEL));
      svg.append(make("line", { x1: cx, y1: cy, x2: x, y2: y,
        class: "fitness-axis-ray" + (this.values[index] ? "" : " is-unknown"), "data-axis-ray": index }));
      if (!this.values[index]) svg.append(make("circle", { cx: x, cy: y, r: 4,
        class: "fitness-unknown", "data-unknown-axis": index }));
      // Touching the actual axis also opens its evidence. Native buttons provide
      // the equivalent keyboard and screen-reader interaction.
      const hitEnd = point(index, expertRadius * fitnessProfileRadiusFraction(Math.max(FITNESS_WEB_BOUNDARY_LEVEL, this.values[index])));
      const hit = make("line", { x1: point(index, 18)[0], y1: point(index, 18)[1],
        x2: hitEnd[0], y2: hitEnd[1], class: "fitness-axis-hit" });
      hit.addEventListener("click", () => this.openDetails(index));
      svg.append(hit);
    });
    FITNESS_LABELS.forEach((lines, index) => {
      const [x, y] = point(index, labelRadius);
      const horizontal = Math.cos(index * Math.PI / 5 - Math.PI / 2);
      const button = this.buttons[index];
      button.classList.toggle("is-unknown", !this.values[index]);
      button.setAttribute("aria-label", lines.join(" ") + ": " + (this.values[index]
        ? "level " + formatFitnessMean(this.values[index]) + " of 5" : "no evidence yet") + ". Show difficulty feedback");
      if (stacked) { button.removeAttribute("style"); return; }
      const labelWidth = Math.max(...lines.map(line => measure.measureText(line).width));
      const buttonWidth = Math.max(48, labelWidth + 8);
      const left = Math.abs(horizontal) < 0.01 ? x - buttonWidth / 2
        : horizontal > 0 ? x - 4 : x - buttonWidth + 4;
      button.style.cssText = `left:${Math.max(0, Math.min(width - buttonWidth, left))}px;top:${y}px;width:${buttonWidth}px;min-height:${Math.max(48, lineHeight * lines.length + 8)}px;`;
    });
    this.chart.replaceChildren(svg);
    const focused = this.buttons.indexOf(document.activeElement);
    if (focused >= 0) this.highlight(focused);
  }
}
