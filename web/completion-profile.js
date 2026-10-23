import { FITNESS_LABELS } from "./fitness-profile.js";

// Missing legacy evidence is different from ten known-to-be-unknown axes.
export function copyProfileSnapshot(values) {
  return Array.isArray(values) && values.length === 10 && values.every(value =>
    Number.isFinite(value) && (value === 0 || (value >= 1 && value <= 5)))
    ? [...values] : null;
}

const format = value => value === 0 ? "—" : (Math.floor(value * 10 + 0.5) / 10).toFixed(1);
const speak = value => value === "—" ? "unknown" : value;

export function completionProfileChanges(before, after) {
  if (!copyProfileSnapshot(before) || !copyProfileSnapshot(after)) return [];
  return FITNESS_LABELS.flatMap((lines, index) => {
    if (before[index] === after[index]) return [];
    const oldValue = format(before[index]), newValue = format(after[index]);
    if (oldValue === newValue) return [];
    const label = lines.join(" ");
    return [{ label, before: oldValue, after: newValue,
      description: `${label}: ${speak(oldValue)} to ${speak(newValue)}` }];
  });
}

export function renderCompletionProfile(host, before, after) {
  const changes = completionProfileChanges(before, after);
  host.replaceChildren(...changes.map(change => {
    const row = document.createElement("li");
    row.setAttribute("aria-label", change.description);
    for (const [className, text] of [["completion-change-label", change.label],
      ["completion-change-before", change.before], ["completion-change-arrow", "→"],
      ["completion-change-after", change.after]]) {
      const part = document.createElement("span");
      part.className = className;
      part.textContent = text;
      part.setAttribute("aria-hidden", "true");
      row.append(part);
    }
    return row;
  }));
  host.hidden = changes.length === 0;
  host.closest(".completion-screen").classList.toggle("has-profile-changes", changes.length > 0);
}
