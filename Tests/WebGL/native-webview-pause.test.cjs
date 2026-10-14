const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function setup() {
  const elements = new Map();
  const document = {
    activeElement: null,
    getElementById: id => elements.get(id) || null,
    querySelector: () => null,
    addEventListener() {},
    createElement(tag) {
      const element = {
        tag, style: {}, children: [], clientWidth: 1280, clientHeight: 720,
        setAttribute() {},
        appendChild(child) {
          this.children.push(child);
          child.parentNode = this;
          if (child.id) elements.set(child.id, child);
        },
        removeChild(child) {
          this.children.splice(this.children.indexOf(child), 1);
          elements.delete(child.id);
          child.parentNode = null;
        },
        focus() { document.activeElement = this; },
        blur() { if (document.activeElement === this) document.activeElement = null; }
      };
      return element;
    }
  };
  document.body = document.createElement('body');
  const library = {};
  const window = {
    addEventListener() {}, requestAnimationFrame(fn) { fn(); },
    history: { pushState() {} }, location: { href: 'https://classroom.example/' }
  };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname,
    '../../Assets/NativeWebViewPlugin/Plugins/WebGL/NativeWebView.jslib'), 'utf8'), {
    window, document, Module: {}, URL, UTF8ToString: value => value,
    LibraryManager: { library }, mergeInto: Object.assign
  });
  const show = () => library.NWV_ShowWebView(JSON.stringify({ url: 'https://simulation.example/' }));
  return { library, window, document, elements, show };
}

test('pause destroys the simulation document and shields input; resume reloads its original URL', () => {
  const { library, document, elements, show } = setup();
  library.NWV_Init('NativeBridge');
  show();
  const iframe = elements.get('native-webview-plugin-iframe');
  iframe.focus();
  library.NWV_SetSessionPaused(1);
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  assert.equal(iframe.parentNode, null);
  assert.equal(elements.has('native-webview-plugin-toolbar'), false);
  const shield = elements.get('native-webview-plugin-pause-shield');
  assert.equal(shield.style.pointerEvents, 'auto');
  assert.equal(document.activeElement, shield);
  let prevented = false, stopped = false;
  shield.onkeydown({ preventDefault() { prevented = true; }, stopPropagation() { stopped = true; } });
  assert.ok(prevented && stopped);
  library.NWV_SetSessionPaused(1);
  assert.equal(elements.get('native-webview-plugin-pause-shield'), shield);
  library.NWV_SetSessionPaused(0);
  const reloaded = elements.get('native-webview-plugin-iframe');
  assert.ok(reloaded);
  assert.notEqual(reloaded, iframe);
  assert.equal(reloaded.src, 'https://simulation.example/');
  assert.equal(elements.has('native-webview-plugin-pause-shield'), false);
  assert.ok(elements.has('native-webview-plugin-toolbar'));
  library.NWV_SetSessionPaused(0);
  assert.equal(elements.get('native-webview-plugin-iframe'), reloaded);
});

test('pause before initialization defers loading, including replacement content while paused', () => {
  const { library, elements, show } = setup();
  library.NWV_SetSessionPaused(1);
  library.NWV_Init('NativeBridge');
  show();
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  library.NWV_ShowWebView(JSON.stringify({ url: 'https://simulation.example/replacement' }));
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  assert.ok(elements.has('native-webview-plugin-pause-shield'));
  library.NWV_SetSessionPaused(0);
  assert.equal(elements.get('native-webview-plugin-iframe').src, 'https://simulation.example/replacement');
});

test('close while paused stays closed on resume and reopening starts interactive', () => {
  const { library, elements, show } = setup();
  library.NWV_Init('NativeBridge');
  show();
  library.NWV_SetSessionPaused(1);
  library.NWV_CloseWebView();
  library.NWV_SetSessionPaused(0);
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  assert.equal(elements.has('native-webview-plugin-pause-shield'), false);
  assert.equal(elements.get('native-webview-plugin-container').style.display, 'none');
  show();
  assert.equal(elements.get('native-webview-plugin-iframe').src, 'https://simulation.example/');
});

test('inline HTML is unloaded on pause and restored only on resume', () => {
  const { library, elements } = setup();
  library.NWV_Init('NativeBridge');
  const html = '<audio autoplay loop src="tone.wav"></audio>';
  library.NWV_ShowWebView(JSON.stringify({ shouldLoadHtmlString: true, htmlString: html }));
  const iframe = elements.get('native-webview-plugin-iframe');
  assert.equal(iframe.srcdoc, html);
  library.NWV_SetSessionPaused(1);
  assert.equal(iframe.parentNode, null);
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  library.NWV_SetSessionPaused(0);
  assert.equal(elements.get('native-webview-plugin-iframe').srcdoc, html);
});

test('late load/error callbacks from an unloaded simulation do not report success or open a tab', () => {
  const { library, window, elements, show } = setup();
  library.NWV_Init('NativeBridge');
  show();
  const iframe = elements.get('native-webview-plugin-iframe');
  const reports = [];
  window.__nativeWebViewBridge.sendToUnity = (...args) => reports.push(args);
  window.open = () => { throw new Error('Unloaded frame must not open a tab'); };
  library.NWV_SetSessionPaused(1);
  iframe.onload();
  iframe.onerror();
  assert.deepEqual(reports, []);
  assert.equal(window.__nativeWebViewBridge.navigationDepth, 0);
});

test('browser back while paused does not discard the pending simulation', () => {
  const { library, window, elements, show } = setup();
  library.NWV_Init('NativeBridge');
  show();
  library.NWV_SetSessionPaused(1);
  const bridge = window.__nativeWebViewBridge;
  assert.equal(bridge.isWebViewOpen(), true);
  bridge.handleBackAction(null, '', 'back');
  library.NWV_SetSessionPaused(0);
  assert.equal(elements.get('native-webview-plugin-iframe').src, 'https://simulation.example/');
});

test('restricted-host fallback cannot open an external tab while already paused', () => {
  const { library, window, elements } = setup();
  library.NWV_Init('NativeBridge');
  const opened = [];
  window.open = url => { opened.push(url); return {}; };
  library.NWV_SetSessionPaused(1);
  library.NWV_ShowWebView(JSON.stringify({ url: 'https://www.google.com/' }));
  assert.deepEqual(opened, []);
  assert.equal(elements.has('native-webview-plugin-iframe'), false);
  library.NWV_SetSessionPaused(0);
  assert.deepEqual(opened, ['https://www.google.com/']);
});
