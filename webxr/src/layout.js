export const layout = `
<header class="topbar">
  <div class="top-inner">
    <a class="brand" href="/" aria-label="NARA"><span class="brandmark" aria-hidden="true"></span><span>NARA</span></a>
    <span class="top-divider"></span><span class="top-name" data-copy="home"></span>
    <div class="top-actions">
      <button class="button quiet desktop-help" id="help-button" data-copy="helpButton"></button>
      <button class="button quiet" id="settings-button" data-copy="settings"></button>
      <button class="button vr" id="enter-vr" data-copy="enter" disabled></button>
    </div>
  </div>
</header>
<main class="shell">
  <section class="heading">
    <div><h1 data-copy="heading"></h1><p data-copy="description"></p></div>
    <div class="readiness" role="status">
      <span class="mode-tag" id="ai-badge"><i aria-hidden="true"></i><span id="mode-text"></span></span>
      <span class="mode-tag" id="voice-badge"><i aria-hidden="true"></i><span id="voice-state"></span></span>
    </div>
  </section>
  <div class="workspace">
    <div class="left">
      <section class="stage card" id="stage" aria-labelledby="room-tag">
        <div class="stage-toolbar">
          <span class="view-label" data-copy="cameraView"></span>
          <div class="camera-buttons" id="camera-controls" role="group">
            <button class="view-button" type="button" data-view="presenter" data-copy="audience"></button>
            <button class="view-button" type="button" data-view="screen" data-copy="screenView"></button>
            <button class="view-button" type="button" data-view="overview" data-copy="overview"></button>
          </div>
          <button class="expand" id="fullscreen" type="button" aria-pressed="false"><span aria-hidden="true">⛶</span><span id="fullscreen-text"></span></button>
        </div>
        <div class="viewport" id="viewport">
          <div class="stage-wash"></div>
          <div class="viewport-top">
            <span class="room-tag"><i aria-hidden="true"></i><span id="room-tag"></span></span>
            <span class="count-tag" id="count-tag"></span>
          </div>
          <div class="stage-loading" id="stage-loading"><div class="ring" aria-hidden="true"></div><span data-copy="loadingRoom"></span><small id="load-progress"></small></div>
          <div class="line-bubble" id="bubble" hidden><strong></strong><span></span></div>
          <div class="viewport-bottom"><span class="drag-hint" data-copy="cameraHint"></span></div>
        </div>
      </section>
      <section class="deck card">
        <div class="card-top">
          <div><h2 data-copy="presentation"></h2><span class="subtle deck-filename" id="deck-filename"></span></div>
          <button class="button alt" type="button" id="pick-deck" data-copy="newDeck"></button>
        </div>
        <input type="file" id="file" accept=".pptx,.ppt,.pdf,.png,.jpg,.jpeg,.webp,.gif,.bmp,.avif" hidden>
        <div class="deck-body">
          <div class="thumbnails" id="thumbnails" aria-label="Slide"></div>
          <div class="deck-controls">
            <button type="button" class="arrow" id="prev">‹</button>
            <span id="page-count"></span>
            <button type="button" class="arrow" id="next">›</button>
          </div>
        </div>
        <div class="deck-meta">
          <small id="deck-note" class="subtle" role="status"></small>
          <button class="link" type="button" id="use-sample" data-copy="sample" hidden></button>
        </div>
      </section>
    </div>
    <aside class="right">
      <section class="card setup" id="setup">
        <h2 data-copy="rooms"></h2>
        <div class="room-list">
          <button class="room-option" type="button" data-room="university"><span class="room-icon" aria-hidden="true">▦</span><span><b data-copy="university"></b><small data-copy="universityDetail"></small></span><i aria-hidden="true"></i></button>
          <button class="room-option" type="button" data-room="if110"><span class="room-icon" aria-hidden="true">▥</span><span><b data-copy="if110"></b><small data-copy="if110Detail"></small></span><i aria-hidden="true"></i></button>
        </div>
        <div class="form-row">
          <label><span data-copy="duration"></span><select id="minutes"><option value="1">1 min</option><option value="3">3 min</option><option value="5">5 min</option><option value="10">10 min</option></select></label>
          <label><span data-copy="language"></span><select id="language"><option value="id">Indonesia</option><option value="en">English</option></select></label>
        </div>
        <label class="field"><span data-copy="topic"></span><input type="text" id="topic" maxlength="120"></label>
        <details class="notes"><summary data-copy="summary"></summary><textarea id="summary" maxlength="1800" rows="3"></textarea></details>
      </section>
      <section class="card session" id="session" aria-labelledby="phase-title">
        <div class="card-top"><div class="phase"><span id="phase-light"></span><h2 id="phase-title"></h2></div><strong class="timer" id="timer"></strong></div>
        <progress max="180" value="180" id="time-bar" hidden></progress>
        <p class="phase-note" id="phase-note"></p>
        <div class="session-caption" id="session-caption" hidden><strong id="speaker-name"></strong><p id="speech-text"></p></div>
        <form id="write-form" hidden>
          <label for="draft" data-copy="type"></label>
          <textarea id="draft" rows="3" maxlength="1900"></textarea>
          <button class="button alt" id="send" type="submit"></button>
        </form>
        <p class="message" id="session-message" role="status" hidden></p>
        <div class="session-actions">
          <button class="button alt" id="mic" type="button" hidden></button>
          <button class="button alt" id="pause" type="button" hidden></button>
          <button class="button alt" id="replay" type="button" data-copy="replay" hidden></button>
          <button class="button alt" id="skip" type="button" data-copy="skip" hidden></button>
        </div>
        <button class="button primary" id="primary" type="button"></button>
        <ol class="history" id="history" hidden></ol>
      </section>
    </aside>
  </div>
  <footer><span>NARA <span class="subtle">/ WebXR</span></span><a href="/media/credits/ASSETS.md" target="_blank" rel="noopener" data-copy="credits"></a></footer>
</main>
<dialog id="settings" aria-labelledby="settings-title">
  <div class="dialog-body">
    <div class="card-top"><h2 id="settings-title" data-copy="settings"></h2><button class="button quiet" type="button" data-dismiss="settings" data-copy="close"></button></div>
    <section class="setting-group">
      <div class="card-top"><h3 data-copy="mode"></h3><span class="settings-status" id="service-state" role="status"></span></div>
      <select id="mode"><option value="auto" data-copy="autoMode"></option><option value="live" data-copy="live"></option><option value="offline" data-copy="offline"></option></select>
      <p class="subtle" data-copy="modeDetail"></p>
      <details class="credential">
        <summary data-copy="personalKey"></summary>
        <label class="field"><span data-copy="credential"></span><input type="password" id="token" placeholder="sk-…" autocomplete="off" spellcheck="false"></label>
        <div class="inline"><button class="button alt" id="save-key" type="button" data-copy="saveKey"></button><button class="button quiet" id="clear-key" type="button" data-copy="clearKey"></button></div>
        <p class="subtle" id="key-state" role="status"></p>
      </details>
    </section>
    <section class="setting-group">
      <h3 data-copy="soundAndMic"></h3>
      <label class="check"><input type="checkbox" id="audio"><span data-copy="audio"></span></label>
      <label class="check"><input type="checkbox" id="preloadVoice"><span data-copy="preloadVoice"></span></label>
      <div class="mic-readiness"><button class="button alt" id="prepare-mic" type="button" data-copy="prepareMic"></button><p class="subtle" id="mic-settings" role="status"></p></div>
    </section>
    <section class="setting-group">
      <h3 data-copy="stageSettings"></h3>
      <label class="field"><span data-copy="quality"></span><select id="quality"><option value="eco" data-copy="eco"></option><option value="balanced" data-copy="balanced"></option><option value="high" data-copy="high"></option></select></label>
      <label class="check"><input type="checkbox" id="ocr"><span data-copy="imageText"></span></label>
    </section>
  </div>
</dialog>
<dialog id="help" aria-labelledby="help-title"><div class="dialog-body">
  <div class="card-top"><h2 id="help-title" data-copy="helpButton"></h2><button class="button quiet" type="button" data-dismiss="help" data-copy="close"></button></div>
  <p data-copy="controlsIntro"></p><p data-copy="controlsVR"></p><p class="subtle" id="xr-note"></p>
</div></dialog>
`;
