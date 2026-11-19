import React, { useEffect, useMemo, useRef, useState } from 'react';
import { View } from 'react-native';
import { WebView, WebViewMessageEvent } from 'react-native-webview';
import { DEFAULT_CENTER, LeafletMapProps, MAP_LIMITS, PIN_CSS, TILE_ATTRIBUTION, TILE_URL, pinHtml } from './types';

// Điện thoại: Leaflet chạy trong WebView, tải thư viện từ unpkg và ô bản đồ từ OpenStreetMap (cần Internet).
const html = `<!DOCTYPE html><html><head>
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1">
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" crossorigin="">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" crossorigin=""></script>
<style>html,body,#map{margin:0;height:100%;background:#EEF1E8}${PIN_CSS}</style></head>
<body><div id="map"></div><script>
var map = L.map('map', { minZoom: ${MAP_LIMITS.minZoom}, maxBounds: [[${MAP_LIMITS.south}, ${MAP_LIMITS.west}], [${MAP_LIMITS.north}, ${MAP_LIMITS.east}]], maxBoundsViscosity: 1 })
  .setView([${DEFAULT_CENTER.lat}, ${DEFAULT_CENTER.lng}], 12);
L.tileLayer('${TILE_URL}', { maxZoom: 19, attribution: '${TILE_ATTRIBUTION}' }).addTo(map);
var group = L.layerGroup().addTo(map);
var lastFit = '';
map.on('click', function (e) { var p = e.latlng.wrap(); window.ReactNativeWebView.postMessage(JSON.stringify({ type: 'press', lat: p.lat, lng: p.lng })); });
window.ssUpdate = function (data) {
  group.clearLayers();
  var points = [];
  if (data.user) {
    L.marker([data.user.lat, data.user.lng], { icon: L.divIcon({ className: '', html: '<div class="ss-me"></div>' }), interactive: false }).addTo(group);
    if (data.radiusKm) L.circle([data.user.lat, data.user.lng], { radius: data.radiusKm * 1000, color: '#28563D', weight: 1, fillOpacity: 0.05 }).addTo(group);
    points.push([data.user.lat, data.user.lng]);
  }
  data.markers.forEach(function (m) {
    L.marker([m.lat, m.lng], { icon: L.divIcon({ className: '', html: m.html }), zIndexOffset: m.selected ? 1000 : (m.vip ? 500 : 0) })
      .on('click', function () { window.ReactNativeWebView.postMessage(JSON.stringify({ type: 'select', id: m.id })); })
      .addTo(group);
    points.push([m.lat, m.lng]);
  });
  if (data.fitKey !== lastFit) {
    lastFit = data.fitKey;
    if (points.length === 0) map.setView([${DEFAULT_CENTER.lat}, ${DEFAULT_CENTER.lng}], 12);
    else if (points.length === 1) map.setView(points[0], 14);
    else map.fitBounds(points, { padding: [40, 40], maxZoom: 15 });
  }
};
window.ReactNativeWebView.postMessage(JSON.stringify({ type: 'ready' }));
</script></body></html>`;

export default function LeafletMap({ markers, user, radiusKm, onSelect, onMapPress, style }: LeafletMapProps) {
  const webView = useRef<WebView<{}>>(null);
  const [ready, setReady] = useState(false);
  const payload = useMemo(() => JSON.stringify({
    markers: markers.map(m => ({ ...m, html: pinHtml(m) })),
    user: user ?? null,
    radiusKm: radiusKm ?? null,
    fitKey: JSON.stringify([markers.map(m => m.id), user, radiusKm]),
  }), [markers, user, radiusKm]);

  useEffect(() => {
    if (ready) webView.current?.injectJavaScript(`window.ssUpdate(${payload}); true;`);
  }, [ready, payload]);

  function onMessage(event: WebViewMessageEvent) {
    try {
      const message = JSON.parse(event.nativeEvent.data) as { type: string; id?: string; lat?: number; lng?: number };
      if (message.type === 'ready') setReady(true);
      if (message.type === 'select' && message.id) onSelect(message.id);
      if (message.type === 'press' && message.lat !== undefined && message.lng !== undefined) onMapPress?.({ lat: message.lat, lng: message.lng });
    } catch { /* Bỏ qua tin nhắn không hợp lệ từ trang bản đồ. */ }
  }

  return (
    <View style={[{ overflow: 'hidden' }, style]}>
      <WebView<{}>
        ref={webView}
        originWhitelist={['*']}
        source={{ html, baseUrl: 'https://sunsport.local/' }}
        onMessage={onMessage}
        javaScriptEnabled
        style={{ flex: 1, backgroundColor: '#EEF1E8' }}
      />
    </View>
  );
}
