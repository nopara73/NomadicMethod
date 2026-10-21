// Protocol identity is the exercise ID. Existing definitions are immutable so
// restoring an unfinished workout never changes its response task.
export const REACTION_PROMPT_MS = 650;
export const REACTION_RESET_MS = 1200;
export const REACTION_WAIT_MIN_MS = 1200;
export const REACTION_WAIT_MAX_MS = 2800;

export function resolveReactionDrills(document, catalog) {
  if (document?.version !== 1 || !Array.isArray(document.families) ||
      !Array.isArray(document.plans)) throw new Error("Invalid reaction drill asset.");
  const result = new Map();
  const families = new Map(document.families.map(family => [family.id, family]));
  if (families.size !== document.families.length || document.families.some(family =>
    typeof family.id !== 'string' || !family.id || !Array.isArray(family.actions) || family.actions.length !== 4)) {
    throw new Error("Invalid reaction families.");
  }
  for (const plan of document.plans) {
    const family = families.get(plan.familyId);
    const exercise = catalog.find(item => item.id === plan.exerciseId);
    const count = { Initiation: 1, TwoChoice: 2, FourChoice: 4, ContextChoice: 4 }[plan.protocol];
    if (!Number.isInteger(plan.exerciseId) || result.has(plan.exerciseId) || !count ||
        !family || !exercise || exercise.practice !== 'Reaction training' ||
        !(exercise.reactionCueLevel > 0)) throw new Error("Invalid reaction plan.");
    const actions = family.actions.slice(0, count).map(action => {
      const source = catalog.find(item => item.id === action.sourceId);
      if (!source || !action.label || !/^reaction_previews\/[a-z0-9_-]+\.png$/.test(action.preview) ||
          !/^reaction_videos\/[a-z0-9_-]+\.mp4$/.test(action.video) || action.startSeconds !== 0 ||
          !Number.isFinite(action.durationSeconds) || action.durationSeconds <= 0 ||
          action.durationSeconds > 10 || typeof action.mirror !== "boolean") {
        throw new Error("Invalid reaction action.");
      }
      return Object.freeze({ ...action });
    });
    const claims = [exercise.primaryCanonicalGroup, ...exercise.secondaryCanonicalGroups];
    if (actions.some(action => {
      const source = catalog.find(item => item.id === action.sourceId);
      const trained = [source.primaryCanonicalGroup, ...source.secondaryCanonicalGroups];
      return claims.some(group => !trained.includes(group));
    })) throw new Error('Invalid reaction training claim: every response must train each claimed muscle.');
    result.set(plan.exerciseId, Object.freeze({ ...plan, actions: Object.freeze(actions) }));
  }
  if (catalog.some(item => item.practice === 'Reaction training' && !result.has(item.id))) {
    throw new Error('Missing reaction plan.');
  }
  return result;
}

function unit(random) {
  const value = random();
  if (!Number.isFinite(value) || value < 0 || value >= 1) throw new Error("Invalid random value.");
  return value;
}

export class ReactionDrill {
  constructor(plan, random = Math.random) {
    this.plan = plan;
    this.random = random;
    this.phase = "Idle";
    this.reverse = false;
    this.number = 0;
    this.actionIndex = -1;
    this.deadline = 0;
  }

  beginReady(now, remaining) {
    this.number = 0;
    this.actionIndex = -1;
    this.reverse = this.plan.protocol === "ContextChoice" && unit(this.random) >= 0.5;
    const wait = REACTION_WAIT_MIN_MS + Math.floor(unit(this.random) *
      (REACTION_WAIT_MAX_MS - REACTION_WAIT_MIN_MS + 1));
    const longest = Math.max(...this.plan.actions.map(action => action.durationSeconds * 1000));
    this.phase = remaining >= wait + REACTION_PROMPT_MS + longest + REACTION_RESET_MS
      ? "Ready" : "Complete";
    this.deadline = now + wait;
  }

  advance(now, remaining, mediaReady = true) {
    if (!mediaReady) return false;
    const previous = this.phase;
    if (this.phase === "Idle") this.beginReady(now, remaining);
    else if (this.phase !== "Complete" && now >= this.deadline) {
      if (this.phase === "Ready") {
        const longest = Math.max(...this.plan.actions.map(action => action.durationSeconds * 1000));
        if (remaining < REACTION_PROMPT_MS + longest + REACTION_RESET_MS) {
          this.phase = "Complete";
        } else {
          this.number = Math.floor(unit(this.random) * this.plan.actions.length) + 1;
          this.actionIndex = this.reverse ? this.plan.actions.length - this.number : this.number - 1;
          this.phase = "Prompt";
          this.deadline = now + REACTION_PROMPT_MS;
        }
      } else if (this.phase === "Prompt") {
        // No catch-up loop: a delayed callback must not skip the decision window.
        const duration = Math.round(this.plan.actions[this.actionIndex].durationSeconds * 1000);
        if (remaining < duration + REACTION_RESET_MS) this.phase = "Complete";
        else { this.phase = "Feedback"; this.deadline = now + duration; }
      } else if (this.phase === "Feedback") {
        this.phase = "Reset";
        this.deadline = now + REACTION_RESET_MS;
      } else if (this.phase === "Reset") this.beginReady(now, remaining);
    }
    return previous !== this.phase;
  }

  get demoActionIndex() { return this.phase === "Feedback" ? this.actionIndex : -1; }
  get cue() { return this.phase === "Prompt" ? String(this.number) :
    this.phase === "Feedback" ? this.plan.actions[this.actionIndex].label :
      this.phase === "Reset" ? "Reset" : "Ready"; }
}
