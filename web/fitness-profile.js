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

export function roundFitnessLevel(level) {
  return !Number.isFinite(level) || level <= 0 ? 0
    : Math.min(FITNESS_MAXIMUM_LEVEL, Math.max(1, Math.floor(level + 0.5)));
}

export const FITNESS_UNKNOWN_TIER = 6;
export const FITNESS_TIER_COUNT = 10;

// +1, current, -1 through -4, unknown, then +2 through +4 as coverage fallbacks.
export function fitnessSelectionTier(levels, roundedProfile) {
  let tier = Infinity;
  levels?.forEach((set, dimension) => {
    const target = roundedProfile[dimension];
    if (!(target > 0)) return;
    for (const level of set) {
      const difference = level - target;
      tier = Math.min(tier, difference === 1 ? 0
        : difference <= 0 ? 1 - difference : 5 + difference);
    }
  });
  return Number.isFinite(tier) ? tier : FITNESS_UNKNOWN_TIER;
}

export function fitnessSequenceTier(members, roundedProfile) {
  const tiers = members.map(levels => fitnessSelectionTier(levels, roundedProfile))
    .filter(tier => tier !== FITNESS_UNKNOWN_TIER);
  return tiers.length ? Math.min(...tiers) : FITNESS_UNKNOWN_TIER;
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
  if (source?.schemaVersion !== 1 ||
      JSON.stringify(source.dimensions) !== JSON.stringify(FITNESS_DIMENSIONS) ||
      !source.ratings || typeof source.ratings !== "object" || Array.isArray(source.ratings)) {
    throw new Error("Invalid fitness rating schema.");
  }
  const ratings = new Map();
  for (const [key, levels] of Object.entries(source.ratings)) {
    const id = Number(key);
    if (!Number.isSafeInteger(id) || id <= 0 || !Array.isArray(levels) ||
        levels.length !== FITNESS_DIMENSIONS.length || levels.some(set =>
          !Array.isArray(set) || set.some((value, index) =>
            !Number.isInteger(value) || value < 1 || value > 5 ||
            (index > 0 && value <= set[index - 1])))) {
      throw new Error("Invalid exercise fitness ratings.");
    }
    ratings.set(id, levels);
  }
  return ratings;
}

export function calculateFitnessProfile(exercises, state, ratings) {
  const slots = Object.values(state.keptExerciseRootIdsBySelectionGroupId ?? {});
  const kept = new Set(slots.length ? slots.flat() : state.lastKeptExerciseIds ?? []);
  const movements = new Map();
  for (const exercise of exercises) {
    if (!kept.has(exercise.id)) continue;
    const levels = ratings.get(exercise.id);
    if (!levels) continue;
    const strongest = Math.max(0, ...levels.flat());
    const movementId = exercise.sessionMovementId > 0 ? exercise.sessionMovementId : exercise.id;
    if (!movements.has(movementId)) movements.set(movementId, new Array(10).fill(0));
    const values = movements.get(movementId);
    levels.forEach((set, dimension) => {
      if (set.length === 1 && set[0] === strongest)
        values[dimension] = Math.max(values[dimension], set[0]);
    });
  }
  return FITNESS_DIMENSIONS.map((_, dimension) => {
    const values = [...movements.values()].map(row => row[dimension]).filter(value => value > 0);
    return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : 0;
  });
}

let nextProfileId = 0;

export class FitnessProfileView {
  constructor(host) {
    this.host = host;
    this.clipId = "fitness-profile-clip-" + ++nextProfileId;
    this.values = new Array(10).fill(0);
    this.observer = new ResizeObserver(() => this.render());
    this.observer.observe(host);
    this.render();
  }

  setLevels(values) {
    this.values = values.map(roundFitnessLevel);
    this.render();
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
    expertRadius = Math.max(12, expertRadius);
    const labelRadius = expertRadius + labelGap;
    const height = Math.ceil(Math.max(300, labelRadius * 2 + lineHeight * 3));
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
    const svg = make("svg", { viewBox: "0 0 " + width + " " + height, role: "img",
      "aria-label": "Fitness profile from hearted exercises" });
    svg.style.height = height + "px";
    const description = FITNESS_LABELS.map((label, index) =>
      label.join(" ") + ": " + (this.values[index] > 0
        ? this.values[index].toLocaleString() + " of 5"
        : "no qualifying hearted exercise")).join("; ");
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
    FITNESS_LABELS.forEach((lines, index) => {
      const [x, y] = point(index, labelRadius);
      const horizontal = Math.cos(index * Math.PI / 5 - Math.PI / 2);
      const label = make("text", { x, y: y - (lines.length - 1) * lineHeight / 2,
        "text-anchor": Math.abs(horizontal) < 0.01 ? "middle" : horizontal > 0 ? "start" : "end",
        "dominant-baseline": "central" });
      lines.forEach((line, lineIndex) =>
        label.append(make("tspan", { x, dy: lineIndex ? lineHeight : 0 }, line)));
      svg.append(label);
    });
    this.host.replaceChildren(svg);
  }
}
