export const feedbackLabels = { x: "Meh", tick: "Love it", easy: "Too easy", hard: "Too hard" };
export const feedbackSymbols = { x: "💩", tick: "❤️", easy: "🥱", hard: "💀" };
export function feedbackDirection(x, y, previous = null) {
  const ax = Math.abs(x), ay = Math.abs(y);
  if (Math.max(ax, ay) < 12) return null;
  const horizontal = previous === "x" || previous === "tick";
  const vertical = previous === "easy" || previous === "hard";
  if (horizontal && ay <= ax * 1.2 || ax > ay * 1.2) return x >= 0 ? "tick" : "x";
  if (vertical && ax <= ay * 1.2 || ay > ax * 1.2) return y >= 0 ? "hard" : "easy";
  return null;
}
export const feedbackThreshold = width => width * .36;
export const feedbackDistance = (x, y, direction) => ({ x: -x, tick: x, easy: -y, hard: y })[direction] ?? 0;
export function feedbackVisual(progress, shortEdge) {
  const p = Math.max(0, Math.min(1, progress));
  const travel = p * p * (3 - 2 * p);
  return { travel, size: 248 * (.2 + .84 * travel), others: 0 };
}

// The accepted Stamp preview, expressed in milliseconds and CSS px / Android dp.
// Absolute phase times keep anticipation, impact and hold independent of easing.
export const stampTiming = { automatic: 1270, stamp: 1700, reveal: 420, reduced: 900, reducedReveal: 180 };
const clamp = p => Math.max(0, Math.min(1, p));
const ease = p => 1 - (1 - clamp(p)) ** 3;
const mix = (a, b, p) => a + (b - a) * p;
export function automaticMehX(t, width) {
  if (t < 240) return 13 * Math.sin(clamp(t / 240) * Math.PI);
  const p = clamp((t - 240) / 850);
  return -width * .39 * p * p * (3 - 2 * p);
}
export function stampFrame(t, width, initial = { x: 0, y: 0, scale: 1.04 }) {
  const base = Math.max(.78, Math.min(1, width / 360));
  let x = 0, y = 0, sx = base, sy = base, rotation = -7;
  if (t < 270) {
    const p = ease(t / 270); x = mix(initial.x, 0, p); y = mix(initial.y, -25, p);
    sx = sy = mix(initial.scale, 1.32 * base, p); rotation = -12 * p;
  } else if (t < 390) {
    const p = (t - 270) / 120; y = mix(-25, 12, p * p);
    sx = mix(1.32, 1.12, p) * base; sy = mix(1.32, .88, p) * base; rotation = mix(-12, -7, p);
  } else if (t < 610) {
    const p = ease((t - 390) / 220); y = mix(12, 0, p);
    sx = mix(1.12, 1, p) * base; sy = mix(.88, 1, p) * base;
  }
  const j = clamp((t - 390) / 330), settled = ease(t / 440);
  return { x, y, sx, sy, rotation, settled, sheetScale: 1 - .055 * settled,
    shake: t >= 390 && t < 560 ? Math.sin((t - 390) / 170 * Math.PI * 2) * 6 * (1 - (t - 390) / 170) : 0,
    impact: t < 390 ? 0 : Math.sin(Math.PI * j) * .8, impactScale: mix(.68, 1.1, j),
    wash: .12 * ease(t / 400), receipt: ease((t - 620) / 150) };
}

// Owns presentation and pointer capture only. The session remains the authority
// for pending feedback and the rest clock, including restored workouts.
export class ExerciseFeedbackView {
  constructor(card, footer, hooks) {
    this.mediaCard = card; this.footer = footer; this.hooks = hooks;
    this.stage = card.parentElement;
    // Input covers the screen; only the single frozen exercise sheet moves.
    this.surface = card.closest(".workout-screen");
    const sheet = document.createElement("div"); sheet.className = "feedback-sheet";
    this.caption = document.createElement("div"); this.caption.className = "feedback-caption";
    card.replaceWith(sheet); sheet.append(card, this.caption); this.card = card = sheet;
    this.hints = document.createElement("div");
    this.hints.className = "feedback-hints";
    this.hints.setAttribute("aria-hidden", "true");
    this.hints.innerHTML = Object.keys(feedbackLabels).map(value =>
      `<span class="feedback-hint feedback-${value}" data-feedback="${value}">${feedbackSymbols[value]}</span>`).join("");
    document.body.append(this.hints);
    this.burst = document.createElement("div");
    this.burst.className = "feedback-burst"; this.burst.hidden = true;
    this.burst.setAttribute("aria-hidden", "true");
    this.burstEmoji = document.createElement("span"); this.burst.append(this.burstEmoji);
    this.burstImpact = document.createElement("div"); this.burstImpact.className = "feedback-impact";
    this.burstImpact.innerHTML = Array.from({ length: 8 }, (_, i) => `<i style="--angle:${[22,68,112,158,202,248,292,338][i]}deg"></i>`).join("");
    this.burstCheck = document.createElement("div"); this.burstCheck.className = "feedback-stamp-check";
    this.burst.append(this.burstImpact, this.burstCheck);
    document.body.append(this.burst);
    this.receipt = footer.querySelector(".feedback-receipt");
    footer.querySelector(".feedback-undo").addEventListener("click", () => {
      if (!this.active || this.busy) return;
      hooks.choose(null); this.render(null); this.card.focus({ preventScroll: true });
    });
    document.addEventListener("pointerdown", event => this.down(event));
    document.addEventListener("pointermove", event => this.move(event));
    document.addEventListener("pointerup", event => this.up(event));
    document.addEventListener("pointercancel", () => this.cancelGesture(true));
    this.surface.addEventListener("lostpointercapture", () => this.cancelGesture(true));
    card.addEventListener("keydown", event => {
      if (!this.active || this.selected || this.busy) return;
      const direction = { ArrowLeft: "x", ArrowRight: "tick", ArrowUp: "easy", ArrowDown: "hard" }[event.key];
      if (direction) { event.preventDefault(); this.commit(direction); }
    });
    // Screen reader / keyboard alternatives are discoverable without a visible
    // button panel. Controls retain their normal touch behavior.
    this.actions = document.createElement("div");
    this.actions.className = "feedback-accessible-actions";
    for (const [value, label] of Object.entries(feedbackLabels)) {
      const button = document.createElement("button"); button.type = "button";
      button.textContent = label; button.addEventListener("click", () => this.commit(value));
      this.actions.append(button);
    }
    footer.append(this.actions);
  }
  reduced() { return matchMedia("(prefers-reduced-motion: reduce)").matches; }
  setActive(active, selected = null) {
    this.stop(); this.active = active;
    document.body.classList.toggle("feedback-input", active);
    this.stage.classList.toggle("feedback-stage", active);
    this.card.tabIndex = active ? 0 : -1;
    if (active) {
      this.card.setAttribute("role", "group");
      this.card.setAttribute("aria-label", "Exercise feedback. Swipe left for Meh, right for Love it, up for too easy, down for too hard.");
      this.caption.textContent = document.getElementById("exercise-name").textContent;
      this.render(selected);
      this.hintTimer = setInterval(() => this.nudge(), 4200);
    } else {
      this.card.removeAttribute("role"); this.card.removeAttribute("aria-label"); this.footer.hidden = true;
    }
  }
  render(selected) {
    this.selected = selected; this.footer.hidden = !this.active;
    this.footer.classList.toggle("has-response", !!selected);
    this.receipt.textContent = selected ? feedbackSymbols[selected] : "";
    this.receipt.setAttribute("aria-label", selected ? `${feedbackLabels[selected]}. Feedback saved.` : "");
    this.receipt.classList.toggle("emoji", !!selected);
    this.footer.querySelector(".feedback-undo").hidden = !selected;
    this.actions.hidden = !!selected || !this.active;
    this.stage.classList.toggle("feedback-selected", !!selected);
    this.card.setAttribute("aria-label", selected ? `${feedbackLabels[selected]}. Feedback saved. Undo available.` :
      "Exercise feedback. Swipe left for Meh, right for Love it, up for too easy, down for too hard.");
  }
  pose(x = 0, y = 0) {
    return `translate3d(${x * .85}px,${y * .85}px,0) rotate(${this.reduced() ? 0 : Math.max(-8, Math.min(8, x / innerWidth * 8))}deg)`;
  }
  cancelAnimation() {
    this.animation?.cancel(); this.animation = null;
    this.nudgeAnimation?.cancel(); this.nudgeAnimation = null;
  }
  async animate(frames, duration = 260, easing = "cubic-bezier(.2,.8,.2,1)") {
    this.cancelAnimation();
    if (this.reduced()) return;
    const animation = this.surface.animate(frames, { duration, easing });
    this.animation = animation;
    try { await animation.finished; } catch { /* A new gesture owns the frame. */ }
    if (this.animation === animation) this.animation = null;
  }
  down(event) {
    if (!this.active || this.busy || this.gesture || !event.isPrimary || event.button !== 0) return;
    if (event.target.closest("button, a, input, select, textarea, [role=button], dialog, [contenteditable=true]")) return;
    event.preventDefault();
    const current = new DOMMatrix(getComputedStyle(this.surface).transform);
    this.cancelAnimation(); this.surface.style.transform = this.pose(current.m41 / .85, current.m42 / .85);
    this.gesture = { id: event.pointerId, startX: event.clientX - current.m41 / .85, startY: event.clientY - current.m42 / .85,
      x: current.m41 / .85, y: current.m42 / .85, direction: null };
    this.hooks.hold(true); this.surface.setPointerCapture(event.pointerId);
    this.stage.classList.add("feedback-held");
  }
  move(event) {
    const g = this.gesture; if (!g || event.pointerId !== g.id) return;
    g.x = event.clientX - g.startX; g.y = event.clientY - g.startY;
    g.direction = feedbackDirection(g.x, g.y, g.direction);
    if (this.selected) return;
    this.surface.style.transform = this.pose(g.x, g.y);
    this.preview(g.direction, feedbackDistance(g.x, g.y, g.direction) / feedbackThreshold(innerWidth));
  }
  preview(direction, progress = 0) {
    this.stage.classList.toggle("feedback-dragging", !!direction);
    const visual = feedbackVisual(this.reduced() ? 0 : progress, Math.min(innerWidth, innerHeight));
    for (const hint of this.hints.children) {
      const active = hint.dataset.feedback === direction;
      hint.style.opacity = !direction ? 0 : active ? clamp(progress * feedbackThreshold(innerWidth) / 22) : visual.others;
      hint.style.zIndex = active ? 2 : 1;
      const vectors = { x: [-1,0], tick: [1,0], easy: [0,-1], hard: [0,1] };
      const [vx, vy] = vectors[hint.dataset.feedback];
      const dx = vx * innerWidth * .32 * (1 - visual.travel);
      const dy = vy * innerHeight * .25 * (1 - visual.travel);
      hint.style.transform = active
        ? `translate(-50%, -50%) translate(${dx}px, ${dy}px) rotate(${-9 * clamp(progress)}deg) scale(${visual.size / 248})` : "";
    }
  }
  up(event) {
    const g = this.gesture; if (!g || event.pointerId !== g.id) return;
    this.move(event); this.gesture = null;
    if (this.surface.hasPointerCapture(g.id)) this.surface.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held");
    if (!this.selected && g.direction && feedbackDistance(g.x, g.y, g.direction) >= feedbackThreshold(innerWidth))
      this.commit(g.direction, g);
    else { this.preview(null); this.surface.style.transform = ""; if (!this.selected) this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]); }
    this.hooks.hold(false);
  }
  cancelGesture(resume) {
    const g = this.gesture; if (!g) return;
    this.gesture = null;
    if (this.surface.hasPointerCapture(g.id)) this.surface.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held"); this.preview(null);
    this.surface.style.transform = "";
    this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]);
    this.hooks.hold(false, resume);
  }
  async commit(value, g = { x: 0, y: 0 }, automatic = false) {
    if (!this.active || this.selected || this.busy) return;
    if (this.hooks.choose(value) === false) return;
    this.render(value);
    await this.finishSelection(g, automatic);
  }
  hintPose() {
    const hint = this.hints.querySelector(`[data-feedback="${this.selected}"]`);
    const rect = hint.getBoundingClientRect(), matrix = new DOMMatrix(getComputedStyle(hint).transform);
    return { x: rect.x + rect.width / 2 - innerWidth / 2,
      y: rect.y + rect.height / 2 - innerHeight / 2, scale: Math.hypot(matrix.m11, matrix.m12) };
  }
  async finishSelection(g = { x: 0, y: 0 }, automatic = false) {
    if (!this.active || !this.selected || this.busy || document.hidden) return;
    const epoch = this.epoch; this.busy = true;
    this.cancelAnimation(); this.footer.style.visibility = "hidden";
    if (automatic && !this.reduced()) {
      const countdown = document.getElementById("rest-countdown");
      if (countdown) countdown.textContent = "0 seconds to meh";
      if (!await this.timeline(stampTiming.automatic, t => {
        g = { x: automaticMehX(t, innerWidth), y: 0 };
        this.surface.style.transform = this.pose(g.x, 0);
        this.preview("x", Math.abs(g.x) / feedbackThreshold(innerWidth));
      })) return;
    }
    const initial = this.hintPose();
    this.preview(null);
    document.body.classList.add("stamp-confirming");
    this.burstEmoji.textContent = feedbackSymbols[this.selected]; this.burst.hidden = false;
    const reduced = this.reduced();
    if (!await this.timeline(reduced ? stampTiming.reduced : stampTiming.stamp, t => {
      const frame = stampFrame(reduced ? stampTiming.stamp : t, Math.min(innerWidth, innerHeight), initial);
      this.burstEmoji.style.transform = `translate(-50%, -50%) translate(${frame.x}px, ${frame.y}px) rotate(${frame.rotation}deg) scale(${frame.sx},${frame.sy})`;
      this.burst.style.backgroundColor = `rgb(211 239 100 / ${frame.wash})`;
      this.burstImpact.style.opacity = reduced ? 0 : frame.impact;
      this.burstImpact.style.transform = `translate(-50%, -50%) scale(${frame.impactScale})`;
      this.burstCheck.style.opacity = frame.receipt;
      this.burstCheck.style.transform = `translate(-50%, ${8 * (1 - frame.receipt)}px)`;
      const x = g.x * .85 * (1 - frame.settled), y = g.y * .85 * (1 - frame.settled) + frame.shake;
      this.surface.style.transform = reduced ? "" : `translate(${x}px,${y}px) rotate(${g.x / innerWidth * 8 * (1 - frame.settled)}deg) scale(${frame.sheetScale})`;
      this.surface.style.opacity = ".8";
    })) return;
    if (epoch !== this.epoch || !this.active || document.hidden) return;
    // Advance only after the entire hold. The outgoing stamp remains above the
    // real next screen during its entrance; no exercise image is duplicated.
    this.departing = true;
    this.hooks.completed();
    this.departing = false;
    const destination = document.querySelector(".app-screen:not([hidden])") ?? this.surface;
    if (await this.timeline(reduced ? stampTiming.reducedReveal : stampTiming.reveal, t => {
      const p = ease(t / (reduced ? stampTiming.reducedReveal : stampTiming.reveal));
      this.burst.style.opacity = String(1 - p);
      destination.style.opacity = String(p);
      destination.style.transform = reduced ? "" : `translateY(${38 * (1 - p)}px)`;
    })) this.clearBurst();
    destination.style.opacity = destination.style.transform = "";
    this.busy = false;
  }
  timeline(duration, draw) {
    this.cancelTimeline();
    return new Promise(resolve => {
      const start = performance.now(); this.finishTimeline = resolve;
      const step = now => {
        const t = Math.min(duration, now - start); draw(t);
        if (t < duration) this.motionFrame = requestAnimationFrame(step);
        else { this.motionFrame = null; this.finishTimeline = null; resolve(true); }
      };
      draw(0); this.motionFrame = requestAnimationFrame(step);
    });
  }
  cancelTimeline() {
    cancelAnimationFrame(this.motionFrame); this.motionFrame = null;
    this.finishTimeline?.(false); this.finishTimeline = null;
  }
  clearBurst() {
    this.cancelTimeline(); this.burst.hidden = true; this.burst.style.opacity = "";
    document.body.classList.remove("stamp-confirming");
    this.footer.style.visibility = "";
    this.surface.style.transform = this.surface.style.opacity = "";
  }
  interrupt() {
    this.epoch = (this.epoch ?? 0) + 1;
    this.cancelGesture(false); this.cancelAnimation();
    if (!this.departing) { this.clearBurst(); this.busy = false; }
    this.surface.style.transform = this.surface.style.opacity = ""; this.preview(null);
  }
  nudge() {
    if (!this.active || this.selected || this.gesture || this.busy || this.reduced() || !this.hooks.running() || document.hidden) return;
    this.animate([{ transform: this.pose(), easing: "cubic-bezier(.2,.8,.2,1)" }, { transform: this.pose(-40, 0), offset: .38 },
      { transform: this.pose(-40, 0), offset: .52, easing: "cubic-bezier(.2,.8,.2,1)" },
      { transform: this.pose(4, 0), offset: .84, easing: "cubic-bezier(.2,.8,.2,1)" },
      { transform: this.pose() }], 960, "linear");
    const hint = this.hints.querySelector('[data-feedback="x"]');
    hint.style.transform = `translate(-50%, -50%) translateX(${-innerWidth * .32}px) scale(.2)`;
    this.nudgeAnimation = hint.animate([{ opacity: 0 }, { opacity: .8, offset: .38 },
      { opacity: .8, offset: .52 }, { opacity: 0, offset: .84 }, { opacity: 0 }], { duration: 960 });
  }
  stop() {
    clearInterval(this.hintTimer); this.interrupt();
    document.body.classList.remove("feedback-input");
    this.active = false; this.busy = false;
    this.surface.style.transform = this.surface.style.opacity = ""; this.preview(null);
    this.stage.classList.remove("feedback-held", "feedback-stage", "feedback-selected");
  }
}
