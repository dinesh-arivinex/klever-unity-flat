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

test('pause blocks iframe, toolbar and keyboard focus; resume preserves the loaded document', () => {
  const { library, document, elements, show } = setup();
  library.NWV_Init('NativeBridge');
  show();
  const iframe = elements.get('native-webview-plugin-iframe');
  const toolbar = elements.get('native-webview-plugin-toolbar');
  iframe.focus();
  library.NWV_SetSessionPaused(1);
  assert.equal(iframe.inert, true);
  assert.equal(iframe.style.visibility, 'hidden');
  assert.equal(toolbar.inert, true);
  assert.equal(toolbar.style.visibility, 'hidden');
  const shield = elements.get('native-webview-plugin-pause-shield');
  assert.equal(shield.style.pointerEvents, 'auto');
  assert.equal(document.activeElement, shield);
  let prevented = false, stopped = false;
  shield.onkeydown({ preventDefault() { prevented = true; }, stopPropagation() { stopped = true; } });
  assert.ok(prevented && stopped);
  library.NWV_SetSessionPaused(1);
  assert.equal(elements.get('native-webview-plugin-pause-shield'), shield);
  library.NWV_SetSessionPaused(0);
  assert.equal(iframe.inert, false);
  assert.equal(toolbar.inert, false);
  assert.equal(iframe.style.visibility, '');
  assert.equal(elements.has('native-webview-plugin-pause-shield'), false);
  assert.equal(elements.get('native-webview-plugin-iframe'), iframe);
  assert.equal(iframe.src, 'https://simulation.example/');
});

test('pause before initialization also shields a newly opened or rebuilt simulation', () => {
  const { library, elements, show } = setup();
  library.NWV_SetSessionPaused(1);
  library.NWV_Init('NativeBridge');
  show();
  const original = elements.get('native-webview-plugin-iframe');
  assert.equal(original.inert, true);
  show();
  const replacement = elements.get('native-webview-plugin-iframe');
  assert.notEqual(replacement, original);
  assert.equal(replacement.inert, true);
  assert.equal(replacement.style.visibility, 'hidden');
  assert.ok(elements.has('native-webview-plugin-pause-shield'));
  library.NWV_SetSessionPaused(0);
  assert.equal(replacement.inert, false);
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
  assert.equal(elements.get('native-webview-plugin-iframe').inert, false);
});
