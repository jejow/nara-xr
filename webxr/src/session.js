const linesIn = (text) => String(text || "").trim().replace(/\s+/g, " ");
const cut = (text, limit) => linesIn(text).slice(0, limit);

export class PracticeSession {
  constructor(options) {
    Object.assign(this, options);
    this.reset();
  }

  reset() {
    this.cancel();
    this.phase = "setup";
    this.notes = [];
    this.turns = [];
    this.seen = new Set();
    this.picks = [];
    this.round = 0;
    this.remaining = 0;
    this.floor = null;
    this.caption = null;
    this.feedback = null;
    this.notice = "";
    this.followed = false;
    this.onlineUnavailable = false;
    this.paused = false;
  }

  recordPage(index) {
    if (this.phase === "presenting") this.seen.add(index);
  }

  start(index, roster, options, deck) {
    this.reset();
    this.phase = "presenting";
    this.context = { ...options };
    this.deck = deck;
    this.picks = roster.slice(0, 2);
    this.remaining = options.minutes * 60;
    this.deadline = performance.now() + this.remaining * 1000;
    this.seen.add(index);
  }

  tick() {
    if (this.phase === "presenting" && !this.paused) {
      this.remaining = Math.max(0, Math.ceil((this.deadline - performance.now()) / 1000));
    }
    return this.remaining;
  }

  togglePause() {
    if (this.phase !== "presenting") return;
    if (this.paused) this.deadline = performance.now() + this.remaining * 1000;
    else this.tick();
    this.paused = !this.paused;
  }

  addNotes(value) {
    const text = cut(value, 1900);
    if (this.phase === "presenting" && text) {
      this.notes.push(text);
      if (this.notes.length > 18) this.notes.shift();
      return true;
    }
    return false;
  }

  invite() {
    this.caption = null;
    this.floor = this.picks[this.round];
    if (!this.floor) return this.end();
    this.floorKind = "question";
    this.phase = "invite";
    this.pending = this.request("question", this.floor);
    this.report();
  }

  get language() { return this.context?.language || "id"; }

  local(key, profile) {
    const entry = this.script.find(row => row.key === key);
    return { voice: key, text: (entry?.[this.language] || "").replace("{name}", profile?.label || ""), profile };
  }

  snapshot(kind, who) {
    const material = this.context;
    const seen = Array.from(this.seen);
    const contents = (seen.length ? seen : this.deck.pages.map((_, index) => index)).map(index => cut(this.deck.pages[index]?.text, 1000));
    if (!contents.some(Boolean)) {
      contents.push(...this.deck.pages.map(page => cut(page.text, 370)));
    }
    return {
      kind,
      language: this.language,
      topic: cut(material.topic || this.deck.title, 120),
      summary: cut(material.summary, 1800),
      slides: cut(contents.filter(Boolean).join(" · "), 4200),
      presentation: cut(this.notes.join(" "), 3600),
      conversation: cut(this.turns.map(turn => (turn.line?.text || "") + "\n" + (turn.answer || "")).join("\n"), 3200),
      perspective: who?.profile?.label || ""
    };
  }

  get canAskAI() {
    return this.context?.mode === "live" && (this.context.secret || this.context.managed) && !this.onlineUnavailable;
  }

  request(kind, who) {
    if (!this.canAskAI) return Promise.resolve(null);
    const controller = new AbortController();
    this.controller = controller;
    return fetch("/api/turn", {
      method: "POST",
      headers: { "Content-Type": "application/json",
        ...(this.context.secret ? { "X-Nara-Key": this.context.secret } : {}) },
      body: JSON.stringify(this.snapshot(kind, who)),
      signal: AbortSignal.any([controller.signal, AbortSignal.timeout(18000)])
    }).then(async response => {
      const result = await response.json();
      if (controller.signal.aborted) return null;
      if (response.ok && typeof result.text === "string" && result.text.trim()) {
        const line = { text: cut(result.text, 600), profile: who?.profile };
        this.prepare?.(line, this.language);
        return line;
      }
      this.notice = ["key", "rate", "budget"].includes(result.error) ? result.error :
        response.status === 429 ? "rate" : "offline";
      if (this.notice !== "offline" || result.error === "timeout") this.onlineUnavailable = true;
      return null;
    }).catch(error => {
      if (!controller.signal.aborted) {
        this.notice = "offline";
        if (error.name === "TimeoutError") this.onlineUnavailable = true;
      }
      return null;
    });
  }

  cancel() {
    this.controller?.abort();
    this.serial = (this.serial || 0) + 1;
  }

  async reveal() {
    if (!["invite", "presenting"].includes(this.phase) || !this.floor) return;
    const id = this.serial;
    this.phase = "thinking";
    this.report();
    const question = await this.pending;
    if (this.serial !== id) return;
    this.showQuestion(question);
  }

  useOfflineQuestion() {
    if (this.phase !== "thinking") return;
    this.cancel();
    this.onlineUnavailable = true;
    this.notice = "useScript";
    this.showQuestion(null);
  }

  showQuestion(question) {
    if (!question && this.floorKind === "followup") {
      this.round++;
      return this.invite();
    }
    const line = question || this.local("question_" + (this.round + 1), this.floor.profile);
    this.caption = { ...line, seat: this.floor.seat };
    this.turns.push({ line, seat: this.floor.seat, answer: "", kind: this.floorKind });
    this.phase = "answer";
    this.report();
    this.play(this.caption);
  }

  answer(value, skipped = false) {
    if (this.phase !== "answer") return;
    const answer = cut(value, 1900);
    if (!answer && !skipped) return;
    this.turns.at(-1).answer = answer;
    this.stop();
    if (!skipped && this.round === 0 && !this.followed && this.canAskAI &&
      answer.split(/\s+/).length >= 6) {
      this.followed = true;
      this.floorKind = "followup";
      this.caption = null;
      this.phase = "invite";
      this.pending = this.request("followup", this.floor);
      return this.report();
    }
    this.round++;
    this.invite();
  }

  async end() {
    const id = this.serial;
    this.floor = null;
    this.caption = null;
    this.phase = "done";
    const participant = this.picks.at(-1);
    this.feedback = this.local("feedback", participant?.profile);
    this.report();
    if (this.canAskAI) {
      const response = await this.request("feedback", participant);
      if (id !== this.serial) return;
      if (response) this.feedback = response;
    }
    if (id !== this.serial) return;
    this.report();
    this.finish();
    if (participant) this.play({ ...this.feedback, seat: participant.seat });
  }
}
