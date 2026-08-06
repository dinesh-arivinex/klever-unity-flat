mergeInto(LibraryManager.library, {
  KleverPlayBase64Audio: function (base64Ptr, objectNamePtr, volume) {
    var base64Audio = UTF8ToString(base64Ptr);
    var objectName = UTF8ToString(objectNamePtr);

    function notify(method, value) {
      SendMessage(objectName, method, value || '');
    }

    function release(state) {
      if (!state) return;
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
      var state = { audio: audio, url: url, objectName: objectName, started: false };
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

      var playPromise = audio.play();
      if (playPromise && playPromise.catch) {
        playPromise.catch(function (error) {
          fail(error && error.message ? error.message : String(error));
        });
      }
    } catch (error) {
      notify('OnWebGLAudioError', error && error.message ? error.message : String(error));
    }
  },

  KleverStopBase64Audio: function () {
    var state = window.KleverTtsAudioState;
    if (!state) return;

    var objectName = state.objectName;
    state.audio.onplaying = null;
    state.audio.onended = null;
    state.audio.onerror = null;
    state.audio.pause();
    state.audio.removeAttribute('src');
    state.audio.load();
    if (state.url) URL.revokeObjectURL(state.url);
    window.KleverTtsAudioState = null;
    SendMessage(objectName, 'OnWebGLAudioStopped', '');
  }
});
