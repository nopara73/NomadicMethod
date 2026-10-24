// Prepare every real response before starting. The correct demonstration only
// appears after the decision window; cards preview complete actions in Ready.
export class ReactionDrillView {
  constructor(host, plan, assetUrl, onReady, onError) {
    Object.assign(this, { plan, onReady, onError, ready: false, disposed: false,
      failed: false, mirrored: false, activeIndex: -1, previewEnabled: false, previewIndex: 0 });
    this.prepared = plan.actions.map(() => false);
    this.imagesPrepared = plan.actions.map(() => false);
    this.images = [];
    this.objectUrls = [];
    this.downloads = new AbortController();
    this.labels = [];
    this.numbers = [];
    this.buttons = [];
    this.root = document.createElement('div');
    this.root.className = 'reaction-drill';
    this.context = document.createElement('div');
    this.context.className = 'reaction-drill-context';
    this.stage = document.createElement('div');
    this.stage.className = 'reaction-drill-stage';
    this.cue = document.createElement('div');
    this.cue.className = 'reaction-drill-prompt';
    this.cue.setAttribute('role', 'status');
    const cards = document.createElement('div');
    cards.className = 'reaction-drill-cards';
    this.videos = plan.actions.map((action, index) => {
      const card = document.createElement('button');
      card.type = 'button';
      card.className = 'reaction-drill-card';
      card.setAttribute('aria-label', `Preview ${action.label}`);
      card.onclick = () => this.previewAction(index);
      this.buttons.push(card);
      const image = document.createElement('img');
      image.alt = '';
      image.onload = () => { this.imagesPrepared[index] = image.naturalWidth > 0; this.checkReady(); };
      image.onerror = () => this.fail();
      image.src = assetUrl(action.preview);
      this.images.push(image);
      const number = document.createElement('span');
      number.className = 'reaction-drill-number';
      this.numbers.push(number);
      const label = document.createElement('span');
      this.labels.push(label);
      card.append(image, number, label);
      cards.append(card);
      const video = document.createElement('video');
      Object.assign(video, { muted: true, playsInline: true, preload: 'auto' });
      video.setAttribute('aria-hidden', 'true');
      const prepared = () => {
        if (this.disposed || this.failed || video.readyState < 2 || video.seeking) return;
        this.prepared[index] = true;
        this.checkReady();
      };
      video.onloadedmetadata = () => {
        if (action.durationSeconds > video.duration + 0.1) this.fail();
      };
      video.oncanplay = prepared;
      video.onseeked = prepared;
      video.onerror = () => this.fail();
      // Looping a Ready preview can briefly seek while it restarts. No work is
      // being timed there. A stalled response during a trial still fails closed.
      video.onwaiting = () => { if (!this.previewEnabled && !video.paused) this.fail(); };
      this.stage.append(video);
      this.loadVideo(video, assetUrl(action.video));
      return video;
    });
    this.stage.append(this.cue);
    this.root.append(this.context, this.stage, cards);
    host.append(this.root);
    this.setMirror(false);
    this.render(null);
  }

  async loadVideo(video, url) {
    try {
      const response = await fetch(url, { signal: this.downloads.signal });
      if (!response.ok) throw new Error('Response demonstration unavailable.');
      const blob = await response.blob();
      if (this.disposed) return;
      // A playable first frame does not establish that a whole response is
      // available. Decode from the complete local file, as Android does.
      const objectUrl = URL.createObjectURL(blob);
      this.objectUrls.push(objectUrl);
      video.src = objectUrl;
      video.load();
    } catch { if (!this.disposed) this.fail(); }
  }

  checkReady() {
    if (this.disposed || this.failed) return;
    const wasReady = this.ready;
    this.ready = this.prepared.every(Boolean) && this.imagesPrepared.every(Boolean);
    if (this.ready && !wasReady) {
      this.onReady();
      if (this.previewEnabled && this.activeIndex < 0) this.previewAction(this.previewIndex);
    }
  }

  fail() {
    if (this.disposed || this.failed) return;
    this.failed = true;
    this.ready = false;
    this.videos?.forEach(video => video.pause());
    this.onError();
  }

  play(video) {
    const index = this.videos.indexOf(video);
    try {
      video.play()?.catch(error => {
        // Switching previews, pausing, or leaving the exercise intentionally
        // cancels the old play request. Reject real failures of the active one.
        if (this.disposed || (error?.name === 'AbortError' && this.activeIndex !== index)) return;
        this.fail();
      });
    } catch { this.fail(); }
  }

  setMirror(mirrored) {
    this.mirrored = mirrored;
    this.plan.actions.forEach((action, index) => {
      const transform = mirrored !== action.mirror ? 'scaleX(-1)' : '';
      this.videos[index].style.transform = transform;
      this.images[index].style.transform = transform;
    });
  }

  setPreviewEnabled(enabled) {
    if (this.disposed || this.previewEnabled === enabled) return;
    this.previewEnabled = enabled;
    this.buttons.forEach(button => { button.disabled = !enabled; });
    if (enabled) this.previewAction(this.previewIndex);
    else this.reset(false);
  }

  previewAction(index) {
    if (!this.previewEnabled || !this.ready || this.disposed) return;
    this.previewIndex = index;
    this.activeIndex = index;
    this.videos.forEach((video, position) => {
      video.classList.toggle('show', position === index);
      video.loop = position === index;
      if (position === index) this.play(video);
      else video.pause();
    });
    this.cue.hidden = true;
  }

  render(drill) {
    if (this.previewEnabled) this.setPreviewEnabled(false);
    this.buttons.forEach(button => { button.disabled = true; });
    const reverse = drill?.reverse === true;
    this.context.textContent = this.plan.protocol === 'StopOnly' ? '3 · 2 · 1 → action; STOP → stay'
      : this.plan.protocol === 'ContextChoice'
        ? (reverse ? '⇄  Reverse' : '→  In order') : 'Number → action';
    this.labels.forEach((label, index) => {
      const number = reverse ? this.plan.actions.length - index : index + 1;
      label.textContent = this.plan.actions[index].label;
      this.numbers[index].textContent = String(number);
      this.buttons[index].setAttribute('aria-label', `Preview ${number}: ${this.plan.actions[index].label}`);
    });
    const active = drill?.demoActionIndex ?? -1;
    const previous = this.activeIndex;
    this.activeIndex = active;
    this.videos.forEach((video, index) => {
      const shown = active === index;
      video.classList.toggle('show', shown);
      video.loop = false;
      if (shown && previous !== active) this.play(video);
      else if (!shown) video.pause();
    });
    this.cue.hidden = active >= 0;
    this.cue.textContent = drill?.cue ?? 'Ready';
    this.cue.classList.toggle('number', ['Prompt', 'Countdown'].includes(drill?.phase));
    this.cue.classList.toggle('stop', drill?.phase === 'Stop');
  }

  freeze() {
    this.previewEnabled = false;
    this.buttons.forEach(button => { button.disabled = true; });
    this.videos.forEach(video => video.pause());
  }

  reset(render = true) {
    this.activeIndex = -1;
    this.previewEnabled = false;
    this.videos.forEach((video, index) => {
      video.pause();
      video.loop = false;
      video.classList.remove('show');
      if (Math.abs(video.currentTime) > 0.02) {
        this.prepared[index] = false;
        this.ready = false;
        video.currentTime = 0;
      }
    });
    if (render) this.render(null);
  }

  dispose() {
    this.disposed = true;
    this.downloads.abort();
    this.videos.forEach(video => {
      video.pause();
      video.removeAttribute('src');
      video.load();
    });
    this.objectUrls.forEach(url => URL.revokeObjectURL(url));
    this.root.remove();
  }
}
