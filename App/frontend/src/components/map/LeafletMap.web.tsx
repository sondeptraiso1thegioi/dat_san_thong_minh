import React, { useEffect, useRef } from 'react';
import { View } from 'react-native';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { DEFAULT_CENTER, LeafletMapProps, MAP_LIMITS, PIN_CSS, TILE_ATTRIBUTION, TILE_URL, pinHtml } from './types';

// Web: dùng Leaflet trực tiếp trong trang (không iframe) để ô bản đồ OSM nhận đúng Referer của trang.
export default function LeafletMap({ markers, user, radiusKm, onSelect, onMapPress, style }: LeafletMapProps) {
  const container = useRef<HTMLDivElement | null>(null);
  const map = useRef<L.Map | null>(null);
  const layer = useRef<L.LayerGroup | null>(null);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;
  const onMapPressRef = useRef(onMapPress);
  onMapPressRef.current = onMapPress;

  useEffect(() => {
    if (!container.current) return;
    if (!document.getElementById('ss-pin-css')) {
      const css = document.createElement('style');
      css.id = 'ss-pin-css';
      css.textContent = PIN_CSS;
      document.head.appendChild(css);
    }
    const instance = L.map(container.current, {
      minZoom: MAP_LIMITS.minZoom,
      maxBounds: [[MAP_LIMITS.south, MAP_LIMITS.west], [MAP_LIMITS.north, MAP_LIMITS.east]],
      maxBoundsViscosity: 1,
    }).setView([DEFAULT_CENTER.lat, DEFAULT_CENTER.lng], 12);
    L.tileLayer(TILE_URL, { maxZoom: 19, attribution: TILE_ATTRIBUTION }).addTo(instance);
    layer.current = L.layerGroup().addTo(instance);
    instance.on('click', event => {
      const point = event.latlng.wrap(); // Kinh độ luôn trong −180…180.
      onMapPressRef.current?.({ lat: point.lat, lng: point.lng });
    });
    map.current = instance;
    const resize = new ResizeObserver(() => instance.invalidateSize());
    resize.observe(container.current);
    return () => { resize.disconnect(); instance.remove(); map.current = null; };
  }, []);

  // Vẽ lại ghim mỗi khi dữ liệu đổi (kể cả ghim đang chọn).
  useEffect(() => {
    const group = layer.current;
    if (!group) return;
    group.clearLayers();
    if (user) {
      L.marker([user.lat, user.lng], { icon: L.divIcon({ className: '', html: '<div class="ss-me"></div>' }), interactive: false }).addTo(group);
      if (radiusKm) L.circle([user.lat, user.lng], { radius: radiusKm * 1000, color: '#28563D', weight: 1, fillOpacity: 0.05 }).addTo(group);
    }
    markers.forEach(marker => {
      L.marker([marker.lat, marker.lng], {
        icon: L.divIcon({ className: '', html: pinHtml(marker) }),
        zIndexOffset: marker.selected ? 1000 : marker.vip ? 500 : 0,
        title: marker.label,
      }).on('click', () => onSelectRef.current(marker.id)).addTo(group);
    });
  }, [markers, user, radiusKm]);

  // Chỉ căn lại khung nhìn khi tập sân hoặc vị trí thay đổi, không phải khi chỉ đổi ghim đang chọn.
  const fitKey = JSON.stringify([markers.map(m => m.id), user, radiusKm]);
  useEffect(() => {
    const instance = map.current;
    if (!instance) return;
    const points: L.LatLngExpression[] = markers.map(m => [m.lat, m.lng]);
    if (user) points.push([user.lat, user.lng]);
    if (points.length === 0) instance.setView([DEFAULT_CENTER.lat, DEFAULT_CENTER.lng], 12, { animate: false });
    else if (points.length === 1) instance.setView(points[0] as L.LatLngExpression, 14, { animate: false });
    else instance.fitBounds(L.latLngBounds(points), { padding: [48, 48], maxZoom: 15, animate: false });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fitKey]);

  return (
    <View style={style}>
      <div ref={container} style={{ position: 'absolute', inset: 0, borderRadius: 'inherit', zIndex: 0 }} />
    </View>
  );
}
