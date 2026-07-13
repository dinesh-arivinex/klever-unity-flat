mergeInto(LibraryManager.library, {
  KleverMqttConnect: function (objectPtr, urlPtr, usernamePtr, passwordPtr, clientIdPtr, willTopicPtr, willPayloadPtr, keepalive) {
    var objectName = UTF8ToString(objectPtr);
    var url = UTF8ToString(urlPtr);
    var username = UTF8ToString(usernamePtr);
    var password = UTF8ToString(passwordPtr);
    var clientId = UTF8ToString(clientIdPtr);
    var willTopic = UTF8ToString(willTopicPtr);
    var willPayload = UTF8ToString(willPayloadPtr);
    var encoder = new TextEncoder();
    var decoder = new TextDecoder();
    var packetId = 1;
    var socket;
    var pingTimer;

    function notify(method, value) { SendMessage(objectName, method, value || ''); }
    function join(parts) {
      var length = parts.reduce(function (sum, part) { return sum + part.length; }, 0);
      var result = new Uint8Array(length);
      var offset = 0;
      parts.forEach(function (part) { result.set(part, offset); offset += part.length; });
      return result;
    }
    function u16(value) { return new Uint8Array([(value >> 8) & 255, value & 255]); }
    function text(value) { var bytes = encoder.encode(value); return join([u16(bytes.length), bytes]); }
    function remainingLength(value) {
      var bytes = [];
      do { var digit = value % 128; value = Math.floor(value / 128); if (value > 0) digit |= 128; bytes.push(digit); } while (value > 0);
      return new Uint8Array(bytes);
    }
    function packet(header, body) { return join([new Uint8Array([header]), remainingLength(body.length), body]); }
    function send(bytes) { if (socket && socket.readyState === WebSocket.OPEN) socket.send(bytes); }
    function nextId() { packetId = packetId >= 65535 ? 1 : packetId + 1; return packetId; }
    function connectPacket() {
      // MQTT 3.1.1, persistent session, username/password, retained QoS1 LWT.
      var flags = 0xEC;
      var variableHeader = join([text('MQTT'), new Uint8Array([4, flags]), u16(Math.max(1, keepalive || 30))]);
      var payload = join([text(clientId), text(willTopic), text(willPayload), text(username), text(password)]);
      return packet(0x10, join([variableHeader, payload]));
    }
    function readRemaining(bytes, start) {
      var value = 0, multiplier = 1, index = start, digit;
      do { if (index >= bytes.length) return null; digit = bytes[index++]; value += (digit & 127) * multiplier; multiplier *= 128; } while ((digit & 128) !== 0 && multiplier <= 2097152);
      return { value: value, index: index };
    }
    function handleMessage(event) {
      var bytes = new Uint8Array(event.data);
      var offset = 0;
      while (offset < bytes.length) {
        var header = bytes[offset++];
        var remaining = readRemaining(bytes, offset);
        if (!remaining || remaining.index + remaining.value > bytes.length) return;
        var body = bytes.slice(remaining.index, remaining.index + remaining.value);
        offset = remaining.index + remaining.value;
        var type = header >> 4;
        if (type === 2) {
          if (body.length < 2 || body[1] !== 0) { notify('OnKleverMqttWebGlConnectFailed', String(body.length > 1 ? body[1] : -1)); socket.close(); return; }
          notify('OnKleverMqttWebGlConnected', '');
        } else if (type === 3) {
          if (body.length < 2) continue;
          var topicLength = (body[0] << 8) | body[1];
          var index = 2 + topicLength;
          var qos = (header >> 1) & 3;
          var topic = decoder.decode(body.slice(2, 2 + topicLength));
          if (qos > 0) { if (index + 2 > body.length) continue; var publishId = (body[index] << 8) | body[index + 1]; index += 2; send(new Uint8Array([0x40, 0x02, (publishId >> 8) & 255, publishId & 255])); }
          notify('OnKleverMqttWebGlMessage', JSON.stringify({ topic: topic, payload: decoder.decode(body.slice(index)) }));
        }
      }
    }
    if (window.KleverMqttBridge && window.KleverMqttBridge.disconnect) window.KleverMqttBridge.disconnect(false);
    socket = new WebSocket(url, ['mqtt']);
    socket.binaryType = 'arraybuffer';
    socket.onopen = function () { send(connectPacket()); };
    socket.onmessage = handleMessage;
    socket.onerror = function () { notify('OnKleverMqttWebGlConnectFailed', 'websocket error'); };
    socket.onclose = function () { if (pingTimer) clearInterval(pingTimer); notify('OnKleverMqttWebGlDisconnected', ''); };
    window.KleverMqttBridge = {
      subscribe: function (topic, qos) { var id = nextId(); send(packet(0x82, join([u16(id), text(topic), new Uint8Array([qos || 0])]))); },
      publish: function (topic, payload, qos, retain) { var level = qos || 0; var id = level > 0 ? u16(nextId()) : new Uint8Array(); send(packet(0x30 | (retain ? 1 : 0) | (level << 1), join([text(topic), id, encoder.encode(payload)]))); },
      disconnect: function (announce) { if (socket && socket.readyState === WebSocket.OPEN) { send(new Uint8Array([0xE0, 0x00])); socket.close(); } if (announce) notify('OnKleverMqttWebGlDisconnected', ''); }
    };
    pingTimer = setInterval(function () { send(new Uint8Array([0xC0, 0x00])); }, Math.max(1, keepalive || 30) * 500);
  },
  KleverMqttSubscribe: function (topicPtr, qos) { if (window.KleverMqttBridge) window.KleverMqttBridge.subscribe(UTF8ToString(topicPtr), qos); },
  KleverMqttPublish: function (topicPtr, payloadPtr, qos, retain) { if (window.KleverMqttBridge) window.KleverMqttBridge.publish(UTF8ToString(topicPtr), UTF8ToString(payloadPtr), qos, retain); },
  KleverMqttDisconnect: function () { if (window.KleverMqttBridge) window.KleverMqttBridge.disconnect(false); window.KleverMqttBridge = null; }
});
