mergeInto(LibraryManager.library, {
  KleverPlayBase64Audio: function (base64Ptr, objectNamePtr, volume) {
    var base64Audio = UTF8ToString(base64Ptr);
    var objectName = UTF8ToString(objectNamePtr);

    function notify(method, value) {
      SendMessage(objectName, method, value || '');
    }

    function resumeAudioContexts() {
      try {
        if (typeof WEBAudio !== 'undefined' && WEBAudio.audioContext && WEBAudio.audioContext.state === 'suspended') {
          WEBAudio.audioContext.resume();
        }
        if (window.unityInstance && window.unityInstance.Module && window.unityInstance.Module.WEBAudio && window.unityInstance.Module.WEBAudio.audioContext) {
          if (window.unityInstance.Module.WEBAudio.audioContext.state === 'suspended') {
            window.unityInstance.Module.WEBAudio.audioContext.resume();
          }
        }
        if (window.audioContext && window.audioContext.state === 'suspended') {
          window.audioContext.resume();
        }
      } catch (e) {
        console.warn('[KleverTtsAudio] Error resuming AudioContext:', e);
      }
    }

    function showAudioPrompt() {
      if (document.getElementById('klever-audio-prompt')) return;
      var prompt = document.createElement('div');
      prompt.id = 'klever-audio-prompt';
      prompt.innerHTML = '<div style="display:flex;align-items:center;gap:10px;font-size:14px;font-weight:600;letter-spacing:0.3px;"><span style="font-size:20px;line-height:1;">🔊</span><span>Click anywhere to enable audio</span></div>';
      prompt.style.cssText = 'position:fixed;top:24px;left:50%;transform:translateX(-50%);background:rgba(20,24,35,0.92);color:#ffffff;padding:12px 24px;border-radius:30px;box-shadow:0 8px 24px rgba(0,0,0,0.45);cursor:pointer;z-index:999999;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;border:1px solid rgba(255,255,255,0.25);user-select:none;-webkit-user-select:none;animation:kleverPromptPulse 1.6s infinite alternate;pointer-events:auto;backdrop-filter:blur(6px);';
      
      if (!document.getElementById('klever-audio-prompt-style')) {
        var style = document.createElement('style');
        style.id = 'klever-audio-prompt-style';
        style.textContent = '@keyframes kleverPromptPulse{0%{transform:translateX(-50%) scale(1);box-shadow:0 8px 24px rgba(0,0,0,0.45);}100%{transform:translateX(-50%) scale(1.03);box-shadow:0 10px 28px rgba(66,133,244,0.6);border-color:rgba(66,133,244,0.8);}}';
        document.head.appendChild(style);
      }
      document.body.appendChild(prompt);
    }

    function hideAudioPrompt() {
      var prompt = document.getElementById('klever-audio-prompt');
      if (prompt && prompt.parentNode) {
        prompt.parentNode.removeChild(prompt);
      }
    }

    function release(state) {
      if (!state) return;
      if (state.cleanupGestureListeners) {
        state.cleanupGestureListeners();
        state.cleanupGestureListeners = null;
      }
      hideAudioPrompt();
      state.audio.onplaying = null;
      state.audio.onended = null;
      state.audio.onerror = null;
      state.audio.pause();
      state.audio.removeAttribute('src');
      state.audio.load();
      if (state.url) URL.revokeObjectURL(state.url);
      if (window.KleverTtsAudioState === state) window.KleverTtsAudioState = null;
    }

    var previous = window.KleverTtsAudioState;
    if (previous) {
      var previousObjectName = previous.objectName;
      release(previous);
      SendMessage(previousObjectName, 'OnWebGLAudioStopped', '');
    }

    try {
      var binary = atob(base64Audio);
      var bytes = new Uint8Array(binary.length);
      for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

      var url = URL.createObjectURL(new Blob([bytes], { type: 'audio/mpeg' }));
      var audio = new Audio();
      var state = { audio: audio, url: url, objectName: objectName, started: false, cleanupGestureListeners: null };
      window.KleverTtsAudioState = state;

      function fail(message) {
        if (window.KleverTtsAudioState !== state) return;
        release(state);
        notify('OnWebGLAudioError', message || 'Unknown browser audio error');
      }

      audio.preload = 'auto';
      audio.volume = Math.max(0, Math.min(1, volume));
      audio.onplaying = function () {
        if (state.started || window.KleverTtsAudioState !== state) return;
        state.started = true;
        hideAudioPrompt();
        var duration = Number.isFinite(audio.duration) ? audio.duration : 0;
        notify('OnWebGLAudioStarted', String(duration));
      };
      audio.onended = function () {
        if (window.KleverTtsAudioState !== state) return;
        release(state);
        notify('OnWebGLAudioFinished', '');
      };
      audio.onerror = function () {
        var mediaError = audio.error;
        fail(mediaError ? 'MediaError code ' + mediaError.code : 'The browser could not decode the MP3 audio');
      };
      audio.src = url;

      // Register proactive passive unlock handler once
      if (!window._kleverAudioUnlockInit) {
        window._kleverAudioUnlockInit = true;
        var proactiveEvents = ['pointerdown', 'mousedown', 'touchstart', 'keydown'];
        function proactiveUnlock() {
          resumeAudioContexts();
        }
        proactiveEvents.forEach(function (evt) {
          window.addEventListener(evt, proactiveUnlock, { passive: true });
        });
      }

      function handlePlayFailure(error) {
        var isAutoplayBlocked = error && (
          error.name === 'NotAllowedError' ||
          (error.message && (
            error.message.indexOf('interact') !== -1 ||
            error.message.indexOf('user gesture') !== -1 ||
            error.message.indexOf('NotAllowedError') !== -1 ||
            error.message.indexOf('gesture') !== -1
          ))
        );

        if (isAutoplayBlocked) {
          console.warn('[KleverTtsAudio] Browser autoplay policy blocked audio. Waiting for user interaction...');
          showAudioPrompt();

          var gestureEvents = ['pointerdown', 'mousedown', 'touchstart', 'touchend', 'keydown'];
          function onUserGesture() {
            cleanup();
            hideAudioPrompt();
            resumeAudioContexts();

            if (window.KleverTtsAudioState !== state || !state.audio) return;

            var retryPromise = state.audio.play();
            if (retryPromise && retryPromise.catch) {
              retryPromise.catch(function (retryErr) {
                console.error('[KleverTtsAudio] Audio retry failed after user gesture:', retryErr);
                fail(retryErr && retryErr.message ? retryErr.message : String(retryErr));
              });
            }
          }

          function cleanup() {
            gestureEvents.forEach(function (evt) {
              window.removeEventListener(evt, onUserGesture, true);
              document.removeEventListener(evt, onUserGesture, true);
            });
            var canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
            if (canvas) {
              gestureEvents.forEach(function (evt) {
                canvas.removeEventListener(evt, onUserGesture, true);
              });
            }
            state.cleanupGestureListeners = null;
          }

          state.cleanupGestureListeners = cleanup;
          gestureEvents.forEach(function (evt) {
            window.addEventListener(evt, onUserGesture, { capture: true, once: true });
            document.addEventListener(evt, onUserGesture, { capture: true, once: true });
          });
          var canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
          if (canvas) {
            gestureEvents.forEach(function (evt) {
              canvas.addEventListener(evt, onUserGesture, { capture: true, once: true });
            });
          }
        } else {
          fail(error && error.message ? error.message : String(error));
        }
      }

      var playPromise = audio.play();
      if (playPromise && playPromise.catch) {
        playPromise.catch(handlePlayFailure);
      }
    } catch (error) {
      notify('OnWebGLAudioError', error && error.message ? error.message : String(error));
    }
  },

  KleverStopBase64Audio: function () {
    var state = window.KleverTtsAudioState;
    if (!state) return;

    var objectName = state.objectName;
    if (state.cleanupGestureListeners) {
      state.cleanupGestureListeners();
      state.cleanupGestureListeners = null;
    }
    var prompt = document.getElementById('klever-audio-prompt');
    if (prompt && prompt.parentNode) {
      prompt.parentNode.removeChild(prompt);
    }
    state.audio.onplaying = null;
    state.audio.onended = null;
    state.audio.onerror = null;
    state.audio.pause();
    state.audio.removeAttribute('src');
    state.audio.load();
    if (state.url) URL.revokeObjectURL(state.url);
    window.KleverTtsAudioState = null;
    SendMessage(objectName, 'OnWebGLAudioStopped', '');
  },

  KleverPauseBase64Audio: function () {
    var state = window.KleverTtsAudioState;
    if (state && state.audio && !state.audio.paused) {
      state.audio.pause();
    }
  },

  KleverResumeBase64Audio: function () {
    var state = window.KleverTtsAudioState;
    if (state && state.audio && state.audio.paused && state.started) {
      var p = state.audio.play();
      if (p && p.catch) {
        p.catch(function (e) {
          console.warn('[KleverTtsAudio] Failed to resume audio:', e);
        });
      }
    }
  }
});
