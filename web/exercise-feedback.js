export const feedbackLabels = { x: "Meh", tick: "Love it", easy: "Too easy", hard: "Too hard" };
export const feedbackSymbols = { x: "💩", tick: "❤️", easy: "↑  too easy", hard: "↓  too hard" };
export function feedbackDirection(x, y, previous = null) {
  const ax = Math.abs(x), ay = Math.abs(y);
  if (Math.max(ax, ay) < 12) return null;
  const horizontal = previous === "x" || previous === "tick";
  const vertical = previous === "easy" || previous === "hard";
  if (horizontal && ay <= ax * 1.2 || ax > ay * 1.2) return x >= 0 ? "tick" : "x";
  if (vertical && ax <= ay * 1.2 || ay > ax * 1.2) return y >= 0 ? "hard" : "easy";
  return null;
}
export const feedbackThreshold = width => Math.max(56, Math.min(88, width * .23));
export const feedbackDistance = (x, y, direction) => ({ x: -x, tick: x, easy: -y, hard: y })[direction] ?? 0;

// Owns presentation and pointer capture only. The session remains the authority
// for pending feedback and the rest clock, including restored workouts.
export class ExerciseFeedbackView {
  constructor(card, footer, hooks) {
    this.mediaCard = card; this.footer = footer; this.hooks = hooks;
    this.stage = card.parentElement;
    const sheet = document.createElement("div"); sheet.className = "feedback-sheet";
    this.caption = document.createElement("div"); this.caption.className = "feedback-caption";
    card.replaceWith(sheet); sheet.append(card, this.caption); this.card = card = sheet;
    this.hints = document.createElement("div");
    this.hints.className = "feedback-hints";
    this.hints.setAttribute("aria-hidden", "true");
    this.hints.innerHTML = Object.keys(feedbackLabels).map(value =>
      `<span class="feedback-hint feedback-${value}" data-feedback="${value}">${feedbackSymbols[value]}</span>`).join("");
    this.stage.append(this.hints);
    this.receipt = footer.querySelector(".feedback-receipt");
    footer.querySelector(".feedback-undo").addEventListener("click", () => {
      if (!this.active || this.busy) return;
      hooks.choose(null); this.render(null); this.card.focus({ preventScroll: true });
    });
    card.addEventListener("pointerdown", event => this.down(event));
    card.addEventListener("pointermove", event => this.move(event));
    card.addEventListener("pointerup", event => this.up(event));
    card.addEventListener("pointercancel", () => this.cancelGesture(true));
    card.addEventListener("lostpointercapture", () => this.cancelGesture(true));
    card.addEventListener("keydown", event => {
      if (!this.active || this.selected || this.busy) return;
      const direction = { ArrowLeft: "x", ArrowRight: "tick", ArrowUp: "easy", ArrowDown: "hard" }[event.key];
      if (direction) { event.preventDefault(); this.commit(direction); }
    });
    // Screen reader / keyboard alternatives are discoverable without a visible
    // button panel. The media is the touch surface.
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
    this.receipt.classList.toggle("emoji", selected === "tick" || selected === "x");
    this.footer.querySelector(".feedback-undo").hidden = !selected;
    this.actions.hidden = !!selected || !this.active;
    this.stage.classList.toggle("feedback-selected", !!selected);
    this.card.setAttribute("aria-label", selected ? `${feedbackLabels[selected]}. Feedback saved. Undo available.` :
      "Exercise feedback. Swipe left for Meh, right for Love it, up for too easy, down for too hard.");
  }
  pose(x = 0, y = 0) {
    return `translate3d(${x}px,${y}px,0) rotate(${this.reduced() ? 0 : Math.max(-7, Math.min(7, x / 45))}deg)`;
  }
  cancelAnimation() { this.animation?.cancel(); this.animation = null; }
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
    event.preventDefault();
    const current = new DOMMatrix(getComputedStyle(this.card).transform);
    this.cancelAnimation(); this.card.style.transform = this.pose(current.m41, current.m42);
    this.gesture = { id: event.pointerId, startX: event.clientX - current.m41, startY: event.clientY - current.m42,
      x: current.m41, y: current.m42, direction: null };
    this.hooks.hold(true); this.card.setPointerCapture(event.pointerId);
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
    for (const hint of this.hints.children) {
      hint.classList.toggle("active", hint.dataset.feedback === direction);
      hint.classList.toggle("ready", hint.dataset.feedback === direction && progress >= 1);
    }
  }
  up(event) {
    const g = this.gesture; if (!g || event.pointerId !== g.id) return;
    this.move(event); this.gesture = null;
    if (this.card.hasPointerCapture(g.id)) this.card.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held"); this.preview(null);
    if (!this.selected && g.direction && feedbackDistance(g.x, g.y, g.direction) >= feedbackThreshold(this.card.clientWidth))
      this.commit(g.direction, g);
    else { this.card.style.transform = ""; if (!this.selected) this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]); }
    this.hooks.hold(false);
  }
  cancelGesture(resume) {
    const g = this.gesture; if (!g) return;
    this.gesture = null;
    if (this.card.hasPointerCapture(g.id)) this.card.releasePointerCapture(g.id);
    this.stage.classList.remove("feedback-held"); this.preview(null);
    this.card.style.transform = "";
    this.animate([{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }]);
    this.hooks.hold(false, resume);
  }
  async commit(value, g = { x: 0, y: 0 }, automatic = false) {
    if (!this.active || this.selected || this.busy) return;
    const epoch = this.epoch; this.busy = true;
    this.hooks.choose(value); this.preview(null); this.render(value);
    this.card.style.transform = "";
    const x = value === "x" ? -70 : value === "tick" ? 70 : 0;
    const y = value === "easy" ? -70 : value === "hard" ? 70 : 0;
    await this.animate(automatic
      ? [{ transform: this.pose() }, { transform: this.pose(x, y), offset: .38 }, { transform: this.pose() }]
      : [{ transform: this.pose(g.x, g.y) }, { transform: this.pose() }], automatic ? 480 : 280);
    if (epoch !== this.epoch || !this.active) return;
    this.busy = false;
    if (automatic) this.hooks.expired();
    else this.hooks.settled?.();
  }
  nudge() {
    if (!this.active || this.selected || this.gesture || this.busy || this.reduced() || !this.hooks.running() || document.hidden) return;
    this.animate([{ transform: this.pose() }, { transform: this.pose(-9, 0), offset: .4 }, { transform: this.pose() }], 650);
  }
  stop() {
    this.epoch = (this.epoch ?? 0) + 1;
    clearInterval(this.hintTimer); this.cancelGesture(false); this.cancelAnimation();
    this.active = false; this.busy = false;
    this.card.style.transform = ""; this.preview(null);
    this.stage.classList.remove("feedback-held", "feedback-stage", "feedback-selected");
  }
}
