/* Include in hosted simulation <head> BEFORE the game/runtime scripts. */
(function () {
  'use strict';
  if (window.parent === window) return;
  var script = document.currentScript;
  var allowedOrigin;
  try { allowedOrigin = new URL(script && script.getAttribute('data-parent-origin') || document.referrer).origin; } catch (_) { return; }
  if (!/^https?:\/\//.test(allowedOrigin)) return;
  var nativeTimeout = window.setTimeout.bind(window), nativeClear = window.clearTimeout.bind(window);
  var nativeFrame = window.requestAnimationFrame.bind(window), nativeCancel = window.cancelAnimationFrame.bind(window);
  var wallNow = performance.now.bind(performance);
  var paused = false, pausedAt = 0, pausedDuration = 0, nextId = 1073741824;
  var timers = new Map(), frames = new Map(), playingMedia = [], playingAnimations = [], runningAudio = [];
  function scheduleTimer(id, timer) {
    timer.due = wallNow() + timer.remaining;
    timer.nativeId = nativeTimeout(function () {
      timer.nativeId = null;
      if (paused || !timers.has(id)) return;
      if (!timer.interval) timers.delete(id);
      timer.callback.apply(window, timer.args);
      if (timer.interval && timers.has(id)) { timer.remaining = timer.delay; scheduleTimer(id, timer); }
    }, timer.remaining);
  }
  function addTimer(callback, delay, interval, args) {
    // String timers keep native semantics; games should use function callbacks.
    if (typeof callback !== 'function') return interval ? nativeInterval(callback, delay) : nativeTimeout(callback, delay);
    var id = nextId++, duration = Math.max(0, Number(delay) || 0);
    var timer = {callback:callback,args:args,delay:duration,remaining:duration,interval:interval,nativeId:null};
    timers.set(id, timer); if (!paused) scheduleTimer(id, timer); return id;
  }
  var nativeInterval = window.setInterval.bind(window), nativeClearInterval = window.clearInterval.bind(window);
  window.setTimeout = function(callback, delay) { return addTimer(callback,delay,false,Array.prototype.slice.call(arguments,2)); };
  window.setInterval = function(callback, delay) { return addTimer(callback,delay,true,Array.prototype.slice.call(arguments,2)); };
  function clearTimer(id) { var timer=timers.get(id); if(timer) { if(timer.nativeId!==null)nativeClear(timer.nativeId);timers.delete(id); } else {nativeClear(id);nativeClearInterval(id);} }
  window.clearTimeout = window.clearInterval = clearTimer;
  function scheduleFrame(id, frame) {
    frame.nativeId = nativeFrame(function(timestamp) { frame.nativeId=null;if(paused||!frames.has(id))return;frames.delete(id);frame.callback(timestamp-pausedDuration); });
  }
  window.requestAnimationFrame = function(callback) {var id=nextId++,frame={callback:callback,nativeId:null};frames.set(id,frame);if(!paused)scheduleFrame(id,frame);return id;};
  window.cancelAnimationFrame = function(id) {var frame=frames.get(id);if(frame){if(frame.nativeId!==null)nativeCancel(frame.nativeId);frames.delete(id);}else nativeCancel(id);};
  // Keep performance-based simulation clocks consistent with the resumed frames.
  try { Object.defineProperty(performance,'now',{configurable:true,value:function(){return (paused?pausedAt:wallNow())-pausedDuration;}}); } catch (_) { /* Engine adapters can use the pause event below. */ }
  var contexts=[];
  ['AudioContext','webkitAudioContext'].forEach(function(name){
    var Original=window[name];if(!Original)return;
    function Tracked(){var context=Reflect.construct(Original,Array.prototype.slice.call(arguments));contexts.push(context);
      var nativeResume=context.resume.bind(context);
      context.resume=function(){if(paused){if(runningAudio.indexOf(context)<0)runningAudio.push(context);return Promise.resolve();}return nativeResume();};
      if(paused){if(context.state==='running')runningAudio.push(context);context.suspend().catch(function(){});}return context;}
    Tracked.prototype=Original.prototype;Object.setPrototypeOf(Tracked,Original);window[name]=Tracked;
  });
  window.addEventListener('message',function(event){
    if(event.source!==window.parent||event.origin!==allowedOrigin||!event.data||event.data.type!=='klever:session-control'||event.data.version!==1||typeof event.data.paused!=='boolean')return;
    var next=event.data.paused;
    if(next!==paused){
      if(next){
        pausedAt=wallNow();paused=true;
        timers.forEach(function(timer){if(timer.nativeId!==null){timer.remaining=Math.max(0,timer.due-pausedAt);nativeClear(timer.nativeId);timer.nativeId=null;}});
        frames.forEach(function(frame){if(frame.nativeId!==null){nativeCancel(frame.nativeId);frame.nativeId=null;}});
        playingMedia=Array.from(document.querySelectorAll('video,audio')).filter(function(media){return !media.paused;});playingMedia.forEach(function(media){media.pause();});
        playingAnimations=document.getAnimations?document.getAnimations().filter(function(animation){return animation.playState==='running';}):[];playingAnimations.forEach(function(animation){animation.pause();});
        runningAudio=contexts.filter(function(context){return context.state==='running';});runningAudio.forEach(function(context){context.suspend().catch(function(){});});
      }else{
        pausedDuration+=wallNow()-pausedAt;paused=false;
        timers.forEach(function(timer,id){scheduleTimer(id,timer);});frames.forEach(function(frame,id){scheduleFrame(id,frame);});
        playingMedia.forEach(function(media){var result=media.play();if(result&&result.catch)result.catch(function(){});});playingAnimations.forEach(function(animation){animation.play();});
        runningAudio.forEach(function(context){context.resume().catch(function(){});});
        playingMedia=[];playingAnimations=[];runningAudio=[];
      }
      window.dispatchEvent(new CustomEvent('klever:simulation-pause',{detail:{paused:paused}}));
    }
    window.parent.postMessage({type:'klever:simulation-pause-ack',version:1,paused:paused},allowedOrigin);
  });
  window.parent.postMessage({type:'klever:simulation-ready',version:1},allowedOrigin);
})();
