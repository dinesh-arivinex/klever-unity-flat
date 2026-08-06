mergeInto(LibraryManager.library, {
  NWV_Init: function(callbackTargetPtr) {
    var targetName = UTF8ToString(callbackTargetPtr || 0);
    var bridge = window.__nativeWebViewBridge;

    if (!bridge) {
      bridge = {
        callbackTarget: "NativeBridge",
        containerId: "native-webview-plugin-container",
        toolbarId: "native-webview-plugin-toolbar",
        iframeId: "native-webview-plugin-iframe",
        resizeHandlerRegistered: false,
        lastParams: {},
        navigationDepth: 0,
        pendingBackNavigation: false,
        reloadRequested: false,
        backGuardRegistered: false,
        isBackGuardActive: false,

        getCanvasRect: function() {
          var canvas = Module && Module.canvas ? Module.canvas : document.querySelector("canvas");
          if (!canvas) {
            return null;
          }

          return canvas.getBoundingClientRect();
        },

        ensureContainer: function() {
          var container = document.getElementById(this.containerId);
          if (!container) {
            container = document.createElement("div");
            container.id = this.containerId;
            container.style.position = "fixed";
            container.style.overflow = "hidden";
            container.style.pointerEvents = "none";
            container.style.background = "transparent";
            container.style.zIndex = "2147483000";
            container.style.display = "none";
            var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
            var parent = canvas ? canvas.parentNode : document.body;
            parent.appendChild(container);

            if (canvas && !canvas.__fullscreenIntercepted) {
              var origFn = canvas.requestFullscreen || canvas.webkitRequestFullscreen || canvas.mozRequestFullScreen || canvas.msRequestFullscreen;
              if (origFn) {
                var newFn = function(options) {
                  var p = canvas.parentNode;
                  var pFn = p.requestFullscreen || p.webkitRequestFullscreen || p.mozRequestFullScreen || p.msRequestFullscreen;
                  if (pFn) return pFn.call(p, options);
                  return origFn.call(canvas, options);
                };
                canvas.requestFullscreen = newFn;
                canvas.webkitRequestFullscreen = newFn;
                canvas.mozRequestFullScreen = newFn;
                canvas.msRequestFullscreen = newFn;
                canvas.__fullscreenIntercepted = true;
              }
            }
          }

          this.updateContainerBounds();
          return container;
        },

        updateContainerBounds: function() {
          var container = document.getElementById(this.containerId);
          if (!container) return;
          var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
          if (!canvas) return;

          container.style.position = "absolute";
          container.style.left = canvas.offsetLeft + "px";
          container.style.top = canvas.offsetTop + "px";
          container.style.width = canvas.offsetWidth + "px";
          container.style.height = canvas.offsetHeight + "px";
        },

        ensureResizeHandler: function() {
          if (this.resizeHandlerRegistered) {
            return;
          }

          var self = this;
          var handler = function() {
            self.updateContainerBounds();
            self.applyLayout(self.lastParams || {});
          };

          window.addEventListener("resize", handler);
          window.addEventListener("scroll", handler);
          this.resizeHandlerRegistered = true;
        },

        hideContainer: function() {
          var container = document.getElementById(this.containerId);
          if (container) {
            container.style.display = "none";
          }
        },

        removeIframe: function() {
          var iframe = document.getElementById(this.iframeId);
          if (iframe && iframe.parentNode) {
            iframe.parentNode.removeChild(iframe);
          }
        },

        removeToolbar: function() {
          var toolbar = document.getElementById(this.toolbarId);
          if (toolbar && toolbar.parentNode) {
            toolbar.parentNode.removeChild(toolbar);
          }
        },

        closeWebView: function(reason) {
          this.deactivateBackGuard();
          this.removeIframe();
          this.removeToolbar();
          this.hideContainer();
          this.navigationDepth = 0;
          this.pendingBackNavigation = false;
          this.reloadRequested = false;
          this.sendToUnity("OnClosed", reason || "WebView closed");
        },

        isWebViewOpen: function() {
          var container = document.getElementById(this.containerId);
          var iframe = document.getElementById(this.iframeId);
          return !!(container && iframe && container.style.display !== "none");
        },

        ensureBackGuard: function() {
          if (this.backGuardRegistered) {
            return;
          }

          var self = this;
          window.addEventListener("popstate", function() {
            if (!self.isWebViewOpen()) {
              self.isBackGuardActive = false;
              return;
            }

            var iframe = document.getElementById(self.iframeId);
            var didNavigateBack = self.handleBackAction(iframe, "", "WebView closed from browser back");

            // Keep intercepting browser back while the WebView remains open.
            if (self.isWebViewOpen()) {
              self.activateBackGuard();
            } else if (!didNavigateBack) {
              self.isBackGuardActive = false;
            }
          });

          this.backGuardRegistered = true;
        },

        activateBackGuard: function() {
          this.ensureBackGuard();
          if (this.isBackGuardActive) {
            return;
          }

          try {
            window.history.pushState({ __nativeWebViewGuard: true }, "", window.location.href);
            this.isBackGuardActive = true;
          } catch (err) {
            this.isBackGuardActive = false;
          }
        },

        deactivateBackGuard: function() {
          this.isBackGuardActive = false;
        },

        handleBackAction: function(iframe, requestedUrl, closeReason) {
          if (!iframe) {
            this.closeWebView(closeReason || "WebView closed from back button");
            return false;
          }

          if (this.navigationDepth <= 1) {
            this.closeWebView(closeReason || "WebView closed from back button");
            return false;
          }

          var canNavigateBack = false;
          try {
            canNavigateBack = !!(iframe.contentWindow && iframe.contentWindow.history);
          } catch (err) {
            canNavigateBack = true;
          }

          if (!canNavigateBack) {
            this.closeWebView(closeReason || "WebView closed from back button");
            return false;
          }

          this.pendingBackNavigation = true;

          try {
            iframe.contentWindow.history.back();
            this.sendToUnity("OnReloaded", "WebView navigated back");
            return true;
          } catch (err) {
            this.pendingBackNavigation = false;
            this.navigationDepth = 1;
            if (requestedUrl) {
              iframe.src = requestedUrl;
              return true;
            }

            this.closeWebView(closeReason || "WebView closed from back button");
            return false;
          }
        },

        reloadWebView: function(iframe, requestedUrl) {
          if (!iframe) {
            return;
          }

          this.reloadRequested = true;

          try {
            if (iframe.contentWindow && iframe.contentWindow.location) {
              iframe.contentWindow.location.reload();
            } else {
              iframe.src = requestedUrl || iframe.src;
            }
          } catch (err) {
            iframe.src = requestedUrl || iframe.src;
          }

          this.sendToUnity("OnReloaded", "WebView reloaded");
        },

        sendToUnity: function(method, payload) {
          var message = payload || "";

          try {
            if (typeof SendMessage === "function") {
              SendMessage(this.callbackTarget, method, message);
              return;
            }

            if (typeof unityInstance !== "undefined" && unityInstance && typeof unityInstance.SendMessage === "function") {
              unityInstance.SendMessage(this.callbackTarget, method, message);
            }
          } catch (err) {
            console.error("[NativeWebView.jslib] Callback to Unity failed:", err);
          }
        },

        normalizeNumber: function(value, fallback) {
          var parsed = typeof value === "number" ? value : parseFloat(value);
          return isFinite(parsed) ? parsed : fallback;
        },

        nonNegative: function(value) {
          return value < 0 ? 0 : value;
        },

        readPadding: function(params) {
          var padding = {};

          if (params) {
            if (params.padding) {
              padding = params.padding;
            } else if (params.miscellaneous && params.miscellaneous.padding) {
              padding = params.miscellaneous.padding;
            }
          }

          var result = {
            top: this.nonNegative(this.normalizeNumber(padding.top, 0)),
            bottom: this.nonNegative(this.normalizeNumber(padding.bottom, 0)),
            leading: this.nonNegative(this.normalizeNumber(padding.leading, 0)),
            trailing: this.nonNegative(this.normalizeNumber(padding.trailing, 0)),
            enableRoundCorners: !!padding.enableRoundCorners,
            roundCornerRadius: this.nonNegative(this.normalizeNumber(padding.roundCornerRadius, 0))
          };
          
          var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
          if (canvas && canvas.width > 0 && canvas.height > 0) {
            var scaleX = canvas.clientWidth / canvas.width;
            var scaleY = canvas.clientHeight / canvas.height;
            result.top *= scaleY;
            result.bottom *= scaleY;
            result.leading *= scaleX;
            result.trailing *= scaleX;
          }
          return result;
        },

        resolveIcon: function(iconToken, fallback) {
          if (!iconToken || typeof iconToken !== "string") {
            return fallback;
          }

          var token = iconToken.trim();
          if (token.length === 0) {
            return fallback;
          }

          var map = {
            KeyboardArrowLeft: "←",
            ArrowBack: "←",
            arrow_back: "←",
            "chevron.left": "←",
            "arrow.left": "←",
            "arrow.backward": "←",
            Close: "✕",
            close: "✕",
            ExitToApp: "✕",
            xmark: "✕",
            "xmark.circle": "✕",
            "xmark.circle.fill": "✕",
            multiply: "✕",
            times: "✕",
            Refresh: "↻",
            refresh: "↻",
            "arrow.clockwise": "↻",
            "arrow.circlepath": "↻",
            Check: "✓",
            check: "✓",
            Warning: "!",
            warning: "!",
            Lock: "🔒",
            lock: "🔒",
            Person: "👤",
            person: "👤",
            Home: "⌂",
            home: "⌂",
            Settings: "⚙",
            settings: "⚙"
          };

          if (Object.prototype.hasOwnProperty.call(map, token)) {
            return map[token];
          }

          return token;
        },

        isHttpUrl: function(value) {
          return typeof value === "string" && /^https?:\/\//i.test(value);
        },

        mapFontFamily: function(fontToken) {
          if (!fontToken || typeof fontToken !== "string") {
            return "";
          }

          var token = fontToken.trim();
          if (token.length === 0) {
            return "";
          }

          var map = {
            defaultFont: 'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
            Default: 'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
            SansSerif: '"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
            serif: 'Georgia, "Times New Roman", Times, serif',
            Serif: 'Georgia, "Times New Roman", Times, serif',
            monospaced: '"SFMono-Regular", Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
            Monospace: '"SFMono-Regular", Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
            rounded: '"Arial Rounded MT Bold", "SF Pro Rounded", system-ui, sans-serif',
            Cursive: '"Segoe Script", "Bradley Hand", cursive'
          };

          if (Object.prototype.hasOwnProperty.call(map, token)) {
            return map[token];
          }

          return token;
        },

        mapFontWeight: function(weightToken) {
          if (!weightToken || typeof weightToken !== "string") {
            return "";
          }

          var token = weightToken.trim();
          if (token.length === 0) {
            return "";
          }

          var map = {
            black: "900",
            Black: "900",
            heavy: "800",
            ExtraBold: "800",
            bold: "700",
            Bold: "700",
            semibold: "600",
            SemiBold: "600",
            medium: "500",
            Medium: "500",
            regular: "400",
            Normal: "400",
            light: "300",
            Light: "300",
            ultraLight: "200",
            ExtraLight: "200",
            thin: "100",
            Thin: "100"
          };

          if (Object.prototype.hasOwnProperty.call(map, token)) {
            return map[token];
          }

          return token;
        },

        extractHost: function(rawUrl) {
          try {
            return new URL(rawUrl, window.location.href).hostname.toLowerCase();
          } catch (err) {
            return "";
          }
        },

        isLikelyIframeRestrictedHost: function(rawUrl) {
          var host = this.extractHost(rawUrl);
          if (!host) {
            return false;
          }

          return /(^|\.)google\./i.test(host) ||
                 /(^|\.)facebook\.com$/i.test(host) ||
                 /(^|\.)instagram\.com$/i.test(host) ||
                 /(^|\.)x\.com$/i.test(host) ||
                 /(^|\.)twitter\.com$/i.test(host);
        },

        openInNewTab: function(url, reason) {
          this.removeIframe();
          this.removeToolbar();
          this.hideContainer();

          var opened = null;
          try {
            opened = window.open(url, "_blank", "noopener,noreferrer");
          } catch (err) {
            opened = null;
          }

          if (opened) {
            this.sendToUnity("OnSuccess", "Opened in new tab: " + url + " (" + reason + ")");
            return true;
          }

          this.sendToUnity("OnError", "Failed to open new tab (popup blocked): " + url);
          return false;
        },

        createToolbar: function(params, requestedUrl) {
          var toolbarParams = params && params.toolbar ? params.toolbar : {};
          if (toolbarParams.isToolbarVisible === false) {
            return null;
          }

          var container = this.ensureContainer();
          var toolbar = document.createElement("div");
          toolbar.id = this.toolbarId;
          toolbar.style.position = "absolute";
          toolbar.style.display = "flex";
          toolbar.style.alignItems = "center";
          toolbar.style.justifyContent = "flex-start";
          toolbar.style.boxSizing = "border-box";
          toolbar.style.pointerEvents = "auto";
          toolbar.style.userSelect = "none";
          toolbar.style.zIndex = "2";

          toolbar.style.background = toolbarParams.toolbarColor || "#0066b2";

          var miscParams = params && params.miscellaneous ? params.miscellaneous : {};
          var mappedFontFamily = this.mapFontFamily(miscParams.font);
          var mappedFontWeight = this.mapFontWeight(miscParams.fontWeight);
          if (mappedFontFamily) {
            toolbar.style.fontFamily = mappedFontFamily;
          }

          var title = document.createElement("div");
          title.textContent = toolbarParams.title || "";
          title.style.flex = "1 1 auto";
          title.style.minWidth = "0";
          title.style.textAlign = "center";
          title.style.whiteSpace = "nowrap";
          title.style.overflow = "hidden";
          title.style.textOverflow = "ellipsis";
          title.style.color = toolbarParams.titleColor || "#faf9f7";

          var titleSize = this.normalizeNumber(toolbarParams.titleSize, 20);
          title.style.fontSize = this.nonNegative(titleSize) + "px";
          if (mappedFontWeight) {
            title.style.fontWeight = mappedFontWeight;
          }

          var createButton = function(icon, color, size, padding, fontWeight) {
            var safePadding = Math.max(0, padding);
            var button = document.createElement("button");
            button.type = "button";
            button.textContent = icon;
            button.style.border = "0";
            button.style.outline = "none";
            button.style.background = "transparent";
            button.style.cursor = "pointer";
            button.style.padding = "0";
            button.style.margin = "0";
            button.style.color = color;
            button.style.fontSize = size + "px";
            button.style.fontWeight = fontWeight || "600";
            button.style.pointerEvents = "auto";
            button.style.lineHeight = "1";
            button.style.textAlign = "center";
            button.style.display = "inline-flex";
            button.style.alignItems = "center";
            button.style.justifyContent = "center";
            button.style.flex = "0 0 auto";
            button.style.boxSizing = "border-box";
            button.style.overflow = "visible";
            button.style.minWidth = "32px";
            button.style.minHeight = "32px";
            button.style.width = Math.max(32, Math.ceil(size + (safePadding * 2) + 10)) + "px";
            button.style.height = Math.max(32, Math.ceil(size + 10)) + "px";
            button.style.paddingLeft = safePadding + "px";
            button.style.paddingRight = safePadding + "px";
            return button;
          };

          var backIcon = this.resolveIcon(toolbarParams.backButton, "←");
          var backColor = toolbarParams.backButtonColor || title.style.color;
          var backSize = this.nonNegative(this.normalizeNumber(toolbarParams.backButtonSize, titleSize));
          var backPadding = this.nonNegative(this.normalizeNumber(toolbarParams.backButtonPadding, 12));
          var backButton = createButton(backIcon, backColor, backSize, backPadding, mappedFontWeight);

          var reloadIcon = this.resolveIcon(toolbarParams.reloadButton, "↻");
          var reloadColor = toolbarParams.reloadButtonColor || title.style.color;
          var reloadSize = this.nonNegative(this.normalizeNumber(toolbarParams.reloadButtonSize, titleSize));
          var reloadPadding = this.nonNegative(this.normalizeNumber(toolbarParams.reloadButtonPadding, 12));
          var reloadButton = createButton(reloadIcon, reloadColor, reloadSize, reloadPadding, mappedFontWeight);
          if (toolbarParams.isReloadVisible === false) {
            reloadButton.style.visibility = "hidden";
            reloadButton.style.pointerEvents = "none";
          }

          var self = this;
          backButton.onclick = function() {
            var iframe = document.getElementById(self.iframeId);
            self.handleBackAction(iframe, requestedUrl, "WebView closed from back button");
          };

          reloadButton.onclick = function() {
            var iframe = document.getElementById(self.iframeId);
            self.reloadWebView(iframe, requestedUrl);
          };

          toolbar.appendChild(backButton);
          toolbar.appendChild(title);
          toolbar.appendChild(reloadButton);

          container.appendChild(toolbar);
          return toolbar;
        },

        applyLayout: function(params) {
          var container = document.getElementById(this.containerId);
          if (!container) {
            return;
          }

          var iframe = document.getElementById(this.iframeId);
          if (!iframe) {
            return;
          }

          var toolbar = document.getElementById(this.toolbarId);
          var padding = this.readPadding(params || {});

          var contentWidth = this.nonNegative(container.clientWidth - padding.leading - padding.trailing);
          var contentHeight = this.nonNegative(container.clientHeight - padding.top - padding.bottom);

          var toolbarHeight = 0;
          if (toolbar) {
            var toolbarParams = params && params.toolbar ? params.toolbar : {};
            var fallbackHeight = Math.max(40, this.normalizeNumber(toolbarParams.titleSize, 20) + 16);
            toolbarHeight = this.nonNegative(this.normalizeNumber(toolbarParams.toolbarSize, fallbackHeight));

            toolbar.style.left = padding.leading + "px";
            toolbar.style.top = padding.top + "px";
            toolbar.style.width = contentWidth + "px";
            toolbar.style.height = toolbarHeight + "px";

            var titlePadding = this.nonNegative(this.normalizeNumber(toolbarParams.titlePadding, 20));
            if (toolbar.children.length >= 2) {
              toolbar.children[1].style.paddingLeft = titlePadding + "px";
              toolbar.children[1].style.paddingRight = titlePadding + "px";
            }
          }

          var iframeTop = padding.top + toolbarHeight;
          var iframeHeight = this.nonNegative(contentHeight - toolbarHeight);

          iframe.style.left = padding.leading + "px";
          iframe.style.top = iframeTop + "px";
          iframe.style.width = contentWidth + "px";
          iframe.style.height = iframeHeight + "px";

          var cornerRadius = padding.enableRoundCorners ? padding.roundCornerRadius : 0;

          iframe.style.borderRadius = cornerRadius + "px";
          iframe.style.overflow = "hidden";

          if (toolbar) {
            toolbar.style.borderTopLeftRadius = cornerRadius + "px";
            toolbar.style.borderTopRightRadius = cornerRadius + "px";
            toolbar.style.borderBottomLeftRadius = "0px";
            toolbar.style.borderBottomRightRadius = "0px";

            iframe.style.borderTopLeftRadius = "0px";
            iframe.style.borderTopRightRadius = "0px";
          }
        },

        show: function(params) {
          var container = this.ensureContainer();
          this.ensureResizeHandler();
          this.removeIframe();
          this.removeToolbar();
          this.lastParams = params || {};
          this.navigationDepth = 0;
          this.pendingBackNavigation = false;
          this.reloadRequested = false;
          this.activateBackGuard();

          var useHtmlString = !!params.shouldLoadHtmlString;
          var requestedUrl = "";

          if (!useHtmlString) {
            if (params.shouldLoadFromFileUrl) {
              if (this.isHttpUrl(params.filePath)) {
                requestedUrl = params.filePath;
              } else if (this.isHttpUrl(params.url)) {
                requestedUrl = params.url;
              } else if (typeof params.htmlString === "string" && params.htmlString.length > 0) {
                useHtmlString = true;
              } else {
                this.sendToUnity("OnError", "WebGL file mode requires an http/https URL or htmlString");
                return;
              }
            } else if (params.url) {
              requestedUrl = params.url;
            }

            if (!useHtmlString && !requestedUrl) {
              this.sendToUnity("OnError", "No valid URL source provided for WebGL WebView");
              return;
            }

            if (!useHtmlString && this.isLikelyIframeRestrictedHost(requestedUrl)) {
              this.openInNewTab(requestedUrl, "known iframe-restricted domain");
              return;
            }
          }

          var iframe = document.createElement("iframe");
          iframe.id = this.iframeId;
          iframe.style.position = "absolute";
          iframe.style.border = "0";
          iframe.style.pointerEvents = "auto";
          iframe.style.zIndex = "1";

          var self = this;
          iframe.onload = function() {
            if (self.pendingBackNavigation) {
              self.pendingBackNavigation = false;
              if (self.navigationDepth > 1) {
                self.navigationDepth -= 1;
              } else {
                self.navigationDepth = 1;
              }
            } else if (self.reloadRequested) {
              self.reloadRequested = false;
              if (self.navigationDepth < 1) {
                self.navigationDepth = 1;
              }
            } else {
              self.navigationDepth += 1;
              if (self.navigationDepth < 1) {
                self.navigationDepth = 1;
              }
            }

            self.sendToUnity("OnSuccess", "WebView loaded");
          };

          iframe.onerror = function() {
            if (requestedUrl) {
              var opened = self.openInNewTab(requestedUrl, "iframe load error fallback");
              if (opened) {
                return;
              }
            }

            self.sendToUnity("OnError", "Failed to load content in WebGL iframe");
          };

          if (useHtmlString) {
            iframe.srcdoc = params.htmlString || "";
          } else {
            iframe.src = requestedUrl;
          }

          this.updateContainerBounds();
          container.style.display = "block";
          container.appendChild(iframe);
          this.createToolbar(params, requestedUrl);
          this.applyLayout(params);

          var layoutBridge = this;
          window.requestAnimationFrame(function() {
            layoutBridge.applyLayout(params);
          });
        }
      };

      window.__nativeWebViewBridge = bridge;
    }

    bridge.callbackTarget = targetName && targetName.length > 0 ? targetName : "NativeBridge";
    bridge.ensureContainer();
    bridge.ensureResizeHandler();
  },

  NWV_ShowWebView: function(jsonPtr) {
    var bridge = window.__nativeWebViewBridge;
    if (!bridge) {
      return;
    }

    var jsonString = UTF8ToString(jsonPtr || 0);
    var params = {};

    try {
      params = JSON.parse(jsonString || "{}");
    } catch (err) {
      bridge.sendToUnity("OnError", "Invalid JSON payload for WebGL WebView");
      return;
    }

    bridge.show(params);
  }
});
