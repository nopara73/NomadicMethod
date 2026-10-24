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
export const feedbackThreshold = width => Math.max(96, Math.min(144, width * .38));
export const feedbackDistance = (x, y, direction) => ({ x: -x, tick: x, easy: -y, hard: y })[direction] ?? 0;
export function feedbackVisual(progress, shortEdge) {
  const p = Math.max(0, Math.min(1, progress));
  return { travel: p * p * (3 - 2 * p), size: 64 + (shortEdge * .9 - 64) * p * p,
    others: .28 * (1 - p) };
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
    this.stage.append(this.hints);
    this.burst = document.createElement("div");
    this.burst.className = "feedback-burst"; this.burst.hidden = true;
    this.burst.setAttribute("aria-hidden", "true");
    this.burstEmoji = document.createElement("span"); this.burst.append(this.burstEmoji);
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
    return `translate3d(${x}px,${y}px,0) rotate(${this.reduced() ? 0 : Math.max(-7, Math.min(7, x / 45))}deg)`;
  }
  cancelAnimation() {
    this.animation?.cancel(); this.animation = null;
    this.nudgeAnimation?.cancel(); this.nudgeAnimation = null;
  }
  async animate(frames, duration = 260) {
    this.cancelAnimation();
    if (this.reduced()) return;
    const animation = this.card.animate(frames, { duration, easing: "cubic-bezier(.2,.8,.2,1)" });
    this.animation = animation;
    try { await animation.finished; } catch { /* A new gesture owns the frame. */ }
    if (this.animation === animation) this.animation = null;
  }
  down(event) {
    if (!this.active || this.busy || this.gesture || !event.isPrimary || event.button !== 0) return;
    if (event.target.closest("button, a, input, select, textarea, [role=button], dialog, [contenteditable=true]")) return;
    event.preventDefault();
    const current = new DOMMatrix(getComputedStyle(this.card).transform);
    this.cancelAnimation(); this.card.style.transform = this.pose(current.m41, current.m42);
    this.gesture = { id: event.pointerId, startX: event.clientX - current.m41, startY: event.clientY - current.m42,
      x: current.m41, y: current.m42, direction: null };
    this.hooks.hold(true); this.surface.setPointerCapture(event.pointerId);
    this.stage.classList.add("feedback-held");
  }
  move(event) {
    const g = this.gesture; if (!g || event.pointerId !== g.id) return;
    g.x = event.clientX - g.startX; g.y = event.clientY - g.startY;
    g.direction = feedbackDirection(g.x, g.y, g.direction);
    if (this.selected) return;
    this.card.style.transform = this.pose(g.x, g.y);
    this.preview(g.direction, feedbackDistance(g.x, g.y, g.direction) / feedbackThreshold(this.card.clientWidth));
  }
  preview(direction, progress = 0) {
    this.stage.classList.toggle("feedback-dragging", !!direction);
    const visual = feedbackVisual(this.reduced() ? 0 : progress, Math.min(innerWidth, innerHeight));
    const stage = this.hints.getBoundingClientRect();
    for (const hint of this.hints.children) {
      const active = hint.dataset.feedback === direction;
      hint.style.opacity = !direction ? 0 : active ? 1 : visual.others;
      hint.style.zIndex = active ? 2 : 1;
      const dx = innerWidth / 2 - (stage.x + hint.offsetLeft + hint.offsetWidth / 2);
      const dy = innerHeight / 2 - (stage.y + hint.offsetTop + hint.offsetHeight / 2);
      hint.style.transform = active
        ? `translate(${dx * visual.travel}px, ${dy * visual.travel}px) scale(${visual.size / 256})` : "";
    }
  }
  up(event) {
    const g = this.gesture; if (!g || event.pointerId !== g.id) return;
    this.move(event); this.gesture = null;
    if (this.surface.hasPointerCapture(g.id)) this.surface.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held");
    if (!this.selected && g.direction && feedbackDistance(g.x, g.y, g.direction) >= feedbackThreshold(this.card.clientWidth))
      this.commit(g.direction, g);
    else { this.preview(null); this.card.style.transform = ""; if (!this.selected) this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]); }
    this.hooks.hold(false);
  }
  cancelGesture(resume) {
    const g = this.gesture; if (!g) return;
    this.gesture = null;
    if (this.surface.hasPointerCapture(g.id)) this.surface.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held"); this.preview(null);
    this.card.style.transform = "";
    this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]);
    this.hooks.hold(false, resume);
  }
  async commit(value, g = { x: 0, y: 0 }, automatic = false) {
    if (!this.active || this.selected || this.busy) return;
    if (this.hooks.choose(value) === false) return;
    this.render(value);
    await this.finishSelection(g, automatic);
  }
  async finishSelection(g = { x: 0, y: 0 }, automatic = false) {
    if (!this.active || !this.selected || this.busy || document.hidden) return;
    const epoch = this.epoch; this.busy = true;
    const hint = this.hints.querySelector(`[data-feedback="${this.selected}"]`).getBoundingClientRect();
    this.preview(null); this.footer.style.visibility = "hidden";
    this.burstEmoji.textContent = feedbackSymbols[this.selected]; this.burst.hidden = false;
    const dx = hint.x + hint.width / 2 - innerWidth / 2;
    const dy = hint.y + hint.height / 2 - innerHeight / 2;
    const scale = Math.min(innerWidth, innerHeight) * .9 / 256;
    const startScale = hint.width / 320;
    const pose = (x, y, size) => `translate(-50%, -50%) translate(${x}px, ${y}px) scale(${size})`;
    this.card.style.transform = "";
    // One frozen card underneath; the selected emoji travels from its target
    // into a full-screen transition. The clock no longer gates advancement.
    this.animate(automatic
      ? [{ transform: this.pose() }, { transform: this.pose(-144), offset: .55 }, { transform: this.pose() }]
      : [{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }], 260);
    this.burstAnimations = this.reduced() ? [] : [
      this.burstEmoji.animate([
        { transform: pose(dx, dy, startScale), opacity: 1 },
        { transform: pose(0, 0, scale * 1.06), opacity: 1, offset: .45 },
        { transform: pose(0, 0, scale * .94), opacity: .82, offset: .67 },
        { transform: pose(0, 0, scale), opacity: 1, offset: .83 },
        { transform: pose(0, 0, scale), opacity: 1 }
      ], { duration: 640, easing: "cubic-bezier(.2,.8,.2,1)", fill: "forwards" }),
      this.burst.animate([{ backgroundColor: "#f5efe700" }, { backgroundColor: "#f5efe7e6", offset: .45 },
        { backgroundColor: "#f5efe7" }], { duration: 640, fill: "forwards" })
    ];
    try { await Promise.all(this.burstAnimations.map(animation => animation.finished)); }
    catch { return; /* Lifecycle interruption preserves the saved response. */ }
    if (epoch !== this.epoch || !this.active || document.hidden) return;
    this.busy = false;
    this.clearBurst();
    this.hooks.completed();
  }
  clearBurst() {
    for (const animation of this.burstAnimations ?? []) animation.cancel();
    this.burstAnimations = []; this.burst.hidden = true; this.footer.style.visibility = "";
  }
  interrupt() {
    this.epoch = (this.epoch ?? 0) + 1;
    this.cancelGesture(false); this.cancelAnimation(); this.clearBurst(); this.busy = false;
    this.card.style.transform = ""; this.preview(null);
  }
  nudge() {
    if (!this.active || this.selected || this.gesture || this.busy || this.reduced() || !this.hooks.running() || document.hidden) return;
    this.animate([{ transform: this.pose() }, { transform: this.pose(-40, 0), offset: .38 },
      { transform: this.pose(-40, 0), offset: .52 }, { transform: this.pose(4, 0), offset: .84 },
      { transform: this.pose() }], 960);
    const hint = this.hints.querySelector('[data-feedback="x"]');
    this.nudgeAnimation = hint.animate([{ opacity: 0 }, { opacity: .8, offset: .38 },
      { opacity: .8, offset: .52 }, { opacity: 0, offset: .84 }, { opacity: 0 }], { duration: 960 });
  }
  stop() {
    clearInterval(this.hintTimer); this.interrupt();
    document.body.classList.remove("feedback-input");
    this.active = false; this.busy = false;
    this.card.style.transform = ""; this.preview(null);
    this.stage.classList.remove("feedback-held", "feedback-stage", "feedback-selected");
  }
}
