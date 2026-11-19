import React, { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, useWindowDimensions, View, ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import SportIcon from '../../components/SportIcon';
import VenueCard from '../../components/VenueCard';
import LeafletMap from '../../components/map/LeafletMap';
import { MapMarker } from '../../components/map/types';
import SearchFilterSheet, { countFilters, emptyFilters, SearchFilters } from '../../components/SearchFilterSheet';
import { colors } from '../../constants/theme';
import { webInputReset } from '../../constants/webStyles';
import { amenityLabels, sportLabels, sports } from '../../constants/sports';
import { PlayerTabRoutes, RootRoutes } from '../../navigation/types';
import { playerService } from '../../services/playerService';
import { useLocationStore } from '../../store/useLocationStore';
import { Sport, VenueSearchItem, VenueSearchResult } from '../../types/player';
import { formatDay, formatPrice, formatShortPrice, formatTime } from '../../utils/format';

type Props = CompositeScreenProps<BottomTabScreenProps<PlayerTabRoutes, 'Search'>, NativeStackScreenProps<RootRoutes>>;

export default function SearchScreen({ navigation, route }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 960;
  const [sport, setSport] = useState<Sport | undefined>(route.params?.sport);
  const [text, setText] = useState('');
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState<SearchFilters>(emptyFilters);
  const [filterOpen, setFilterOpen] = useState(false);
  const [view, setView] = useState<'list' | 'map'>('list');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [result, setResult] = useState<VenueSearchResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [defaultRadius, setDefaultRadius] = useState(5);
  const [searchFocused, setSearchFocused] = useState(false);
  const { position, locating, error: gpsError, locate, clear } = useLocationStore();

  // Chọn môn từ Trang chủ.
  useEffect(() => { if (route.params?.sport) setSport(route.params.sport); }, [route.params?.sport]);

  // Gõ xong 400 ms mới tìm, tránh gọi API theo từng ký tự.
  useEffect(() => {
    const timer = setTimeout(() => setQuery(text), 400);
    return () => clearTimeout(timer);
  }, [text]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    playerService.searchVenues({
      q: query, sport, radiusKm: filters.radiusKm ?? undefined, lat: position?.lat, lng: position?.lng,
      sheltered: filters.sheltered, amenities: filters.amenities,
      date: filters.date, startHour: filters.startHour, hours: filters.hours,
    }, controller.signal)
      .then(data => {
        setResult(data);
        setLoading(false);
        // Khi chưa chọn bán kính, bán kính API trả về chính là mặc định của hệ thống.
        if (filters.radiusKm === null && data.radiusKm !== null) setDefaultRadius(data.radiusKm);
      })
      .catch(reason => {
        if (controller.signal.aborted) return;
        setError(reason instanceof Error ? reason.message : 'Chưa tìm được sân. Vui lòng thử lại.');
        setLoading(false);
      });
    return () => controller.abort();
  }, [query, sport, filters, position, reloadKey]);

  const venues = result?.venues ?? [];
  const hasLocation = !!result && result.location.lat !== null;
  const markers = useMemo<MapMarker[]>(() => venues.map(item => ({
    id: item.venue.id, lat: item.venue.lat, lng: item.venue.lng, vip: item.venue.isVip, selected: item.venue.id === selectedId,
    label: item.slotPrice !== null ? formatShortPrice(item.slotPrice) : item.venue.minPricePerHour !== null ? `từ ${formatShortPrice(item.venue.minPricePerHour)}` : item.venue.name,
  })), [venues, selectedId]);
  const userPoint = result && result.location.lat !== null && result.location.lng !== null ? { lat: result.location.lat, lng: result.location.lng } : null;
  const selected = venues.find(item => item.venue.id === selectedId) ?? null;
  const filterCount = countFilters(filters);

  // Đang lọc theo giờ chơi thì mở chi tiết với khung giờ đó được chọn sẵn.
  const openVenue = (item: VenueSearchItem) => navigation.navigate('VenueDetail', {
    venueId: item.venue.id, name: item.venue.name, sport,
    ...(result?.slot && filters.date && filters.startHour !== undefined ? { date: filters.date, startHour: filters.startHour, hours: filters.hours } : {}),
  });
  const highlightFor = (item: VenueSearchItem) => item.freeCourts !== null && result?.slot
    ? `Còn ${item.freeCourts} sân trống${item.slotPrice !== null ? ` · ${formatPrice(item.slotPrice)} cho ${result.slot.hours} giờ` : ''}`
    : undefined;

  const map = (
    <View style={[styles.mapWrap, wide && styles.mapWrapWide]}>
      <LeafletMap markers={markers} user={userPoint} radiusKm={result?.radiusKm} onSelect={setSelectedId} style={styles.map} />
      {selected && (
        <View style={[styles.mapCard, wide && styles.mapCardWide]}>
          <Pressable accessibilityRole="button" accessibilityLabel="Đóng thẻ sân" onPress={() => setSelectedId(null)} style={styles.mapCardClose} hitSlop={8}>
            <Ionicons name="close" size={16} color={colors.ink} />
          </Pressable>
          <VenueCard venue={selected.venue} preferredSport={sport} highlight={highlightFor(selected)} onPress={() => openVenue(selected)} />
        </View>
      )}
    </View>
  );

  const list = (
    <ScrollView contentContainerStyle={[styles.list, wide && styles.listWide]} keyboardShouldPersistTaps="handled">
      <ResultHeader result={result} loading={loading} />
      {error && (
        <View accessibilityRole="alert" style={styles.error}>
          <Ionicons name="alert-circle-outline" size={20} color={colors.error} />
          <Text style={styles.errorText}>{error}</Text>
          <Pressable accessibilityRole="button" onPress={() => setReloadKey(k => k + 1)} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
        </View>
      )}
      {!error && !loading && venues.length === 0 && (
        <View style={styles.empty}>
          <Ionicons name="search-outline" size={26} color={colors.green} />
          <Text style={styles.emptyTitle}>Không có cụm sân phù hợp</Text>
          <Text style={styles.muted}>Thử tăng khoảng cách, chọn giờ khác hoặc bỏ bớt bộ lọc.</Text>
        </View>
      )}
      <View style={styles.grid}>
        {venues.map(item => (
          <View key={item.venue.id} style={[styles.cell, cellWidth(wide ? (width >= 1500 ? 2 : 1) : width >= 700 ? 2 : 1)]}>
            <VenueCard venue={item.venue} preferredSport={sport} highlight={highlightFor(item)} selected={wide && item.venue.id === selectedId} onPress={() => openVenue(item)} />
          </View>
        ))}
      </View>
    </ScrollView>
  );

  return (
    <SafeAreaView style={styles.safe} edges={wide ? [] : ['top']}>
      <View style={[styles.top, wide && styles.topWide]}>
        <View style={styles.titleRow}>
          <Text accessibilityRole="header" style={styles.title}>Tìm sân</Text>
          <Pressable accessibilityRole="button" disabled={locating}
            onPress={() => (position ? clear() : void locate())}
            style={[styles.gps, position && styles.gpsOn]}>
            {locating ? <ActivityIndicator size="small" color={colors.green} /> : <Ionicons name={position ? 'navigate' : 'navigate-outline'} size={15} color={position ? colors.white : colors.green} />}
            <Text style={[styles.gpsText, position && styles.gpsTextOn]}>{position ? 'Đang dùng vị trí hiện tại' : 'Dùng vị trí hiện tại'}</Text>
          </Pressable>
        </View>
        <Text style={styles.locationText} numberOfLines={1}>
          {gpsError ?? (!result ? ' ' : result.location.source === 'device' ? 'Khoảng cách tính từ vị trí hiện tại của bạn.'
            : result.location.source === 'profile' ? `Khoảng cách tính từ ${result.location.address ?? 'địa chỉ trong hồ sơ'}.`
            : 'Chưa có vị trí: bật vị trí hiện tại để xem sân gần bạn.')}
        </Text>

        <View style={styles.searchRow}>
          <View style={[styles.searchBox, searchFocused && styles.searchBoxFocused]}>
            <Ionicons name="search" size={18} color={colors.muted} />
            <TextInput value={text} onChangeText={setText} placeholder="Tên cụm sân hoặc khu vực, ví dụ Cầu Giấy" placeholderTextColor="#A0A59D"
              style={[styles.searchInput, webInputReset]} autoCorrect={false} returnKeyType="search" accessibilityLabel="Tìm theo tên hoặc khu vực"
              onFocus={() => setSearchFocused(true)} onBlur={() => setSearchFocused(false)} />
            {!!text && <Pressable accessibilityLabel="Xóa tìm kiếm" onPress={() => setText('')} hitSlop={8}><Ionicons name="close-circle" size={18} color="#B5BAB0" /></Pressable>}
          </View>
          <Pressable accessibilityRole="button" accessibilityLabel={`Bộ lọc${filterCount ? `, ${filterCount} đang bật` : ''}`} onPress={() => setFilterOpen(true)} style={[styles.iconButton, filterCount > 0 && styles.iconButtonOn]}>
            <Ionicons name="options-outline" size={20} color={filterCount > 0 ? colors.white : colors.ink} />
            {filterCount > 0 && <View style={styles.badge}><Text style={styles.badgeText}>{filterCount}</Text></View>}
          </Pressable>
          {!wide && (
            <Pressable accessibilityRole="button" accessibilityLabel={view === 'list' ? 'Xem bản đồ' : 'Xem danh sách'} onPress={() => setView(v => (v === 'list' ? 'map' : 'list'))} style={styles.iconButton}>
              <Ionicons name={view === 'list' ? 'map-outline' : 'list-outline'} size={20} color={colors.ink} />
            </Pressable>
          )}
        </View>

        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.sports}>
          <SportChip label="Tất cả" active={!sport} onPress={() => setSport(undefined)} />
          {sports.map(s => <SportChip key={s} sport={s} label={sportLabels[s]} active={sport === s} onPress={() => setSport(s === sport ? undefined : s)} />)}
        </ScrollView>

        <ActiveFilters filters={filters} onChange={setFilters} defaultRadius={defaultRadius} hasLocation={hasLocation} />
      </View>

      {wide
        ? <View style={styles.split}><View style={styles.splitList}>{list}</View>{map}</View>
        : view === 'list' ? list : map}

      <SearchFilterSheet visible={filterOpen} value={filters} defaultRadiusKm={defaultRadius}
        hasLocation={hasLocation} onClose={() => setFilterOpen(false)} onApply={next => { setFilters(next); setFilterOpen(false); }} />
    </SafeAreaView>
  );
}

function cellWidth(columns: number): ViewStyle {
  return { width: columns === 1 ? '100%' : `${100 / columns}%` as `${number}%` };
}

function ResultHeader({ result, loading }: { result: VenueSearchResult | null; loading: boolean }) {
  return (
    <View style={styles.resultHead}>
      <Text style={styles.resultCount}>{result ? `${result.venues.length} cụm sân` : 'Đang tìm…'}</Text>
      {loading && <ActivityIndicator size="small" color={colors.green} />}
      <Text style={styles.resultNote} numberOfLines={2}>
        {result?.slot ? `Trống ${formatDay(result.slot.startAt)}, ${formatTime(result.slot.startAt)}–${formatTime(result.slot.endAt)} · ` : ''}
        {result && result.location.lat !== null ? 'Gần nhất trước' : 'Theo tên'}
      </Text>
    </View>
  );
}

// Các bộ lọc đang bật, bấm × để bỏ từng cái.
function ActiveFilters({ filters, onChange, defaultRadius, hasLocation }: {
  filters: SearchFilters; onChange: (f: SearchFilters) => void; defaultRadius: number; hasLocation: boolean;
}) {
  const items: { key: string; label: string; remove: () => void }[] = [];
  if (filters.startHour !== undefined && filters.date) {
    items.push({
      key: 'slot',
      label: `${formatDay(`${filters.date}T${String(filters.startHour).padStart(2, '0')}:00:00+07:00`)} · ${String(filters.startHour).padStart(2, '0')}:00, ${filters.hours} giờ`,
      remove: () => onChange({ ...filters, date: undefined, startHour: undefined }),
    });
  }
  if (filters.radiusKm !== null && hasLocation) items.push({ key: 'radius', label: `Trong ${filters.radiusKm} km`, remove: () => onChange({ ...filters, radiusKm: null }) });
  if (filters.sheltered) items.push({ key: 'sheltered', label: 'Có mái che', remove: () => onChange({ ...filters, sheltered: false }) });
  filters.amenities.forEach(code => items.push({ key: code, label: amenityLabels[code] ?? code, remove: () => onChange({ ...filters, amenities: filters.amenities.filter(a => a !== code) }) }));
  if (items.length === 0) return hasLocation ? <Text style={styles.hint}>Trong {String(defaultRadius).replace('.', ',')} km quanh bạn · bấm bộ lọc để chọn giờ chơi</Text> : null;
  return (
    <View style={styles.active}>
      {items.map(item => (
        <Pressable key={item.key} accessibilityRole="button" accessibilityLabel={`Bỏ lọc ${item.label}`} onPress={item.remove} style={styles.activeChip}>
          <Text style={styles.activeText}>{item.label}</Text><Ionicons name="close" size={13} color={colors.green} />
        </Pressable>
      ))}
      <Pressable accessibilityRole="button" onPress={() => onChange(emptyFilters)} hitSlop={6}><Text style={styles.clearAll}>Xóa lọc</Text></Pressable>
    </View>
  );
}

function SportChip({ sport, label, active, onPress }: { sport?: Sport; label: string; active: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected: active }} onPress={onPress} style={[styles.sportChip, active && styles.sportChipOn]}>
      {sport && <SportIcon sport={sport} size={16} color={active ? colors.white : colors.green} />}
      <Text style={[styles.sportText, active && styles.sportTextOn]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  muted: { color: colors.muted, fontSize: 12, lineHeight: 18, textAlign: 'center' },
  top: { paddingHorizontal: 20, paddingTop: 14, paddingBottom: 10, gap: 10, borderBottomWidth: 1, borderColor: colors.line, backgroundColor: colors.background },
  topWide: { paddingHorizontal: 32, paddingTop: 28 },
  titleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  title: { color: colors.ink, fontSize: 24, fontWeight: '800', letterSpacing: -0.6 },
  gps: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 11, paddingVertical: 7, borderRadius: 9, borderWidth: 1, borderColor: '#D5DCCF', backgroundColor: colors.white },
  gpsOn: { backgroundColor: colors.green, borderColor: colors.green },
  gpsText: { color: colors.green, fontSize: 12, fontWeight: '600' },
  gpsTextOn: { color: colors.white },
  locationText: { color: colors.muted, fontSize: 12, marginTop: -4 },
  searchRow: { flexDirection: 'row', gap: 8 },
  searchBox: { flex: 1, minWidth: 0, flexDirection: 'row', alignItems: 'center', gap: 9, height: 46, paddingHorizontal: 13, borderRadius: 12, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  searchBoxFocused: { borderColor: colors.green },
  searchInput: { flex: 1, minWidth: 0, height: '100%', fontSize: 14, color: colors.ink },
  iconButton: { width: 46, height: 46, borderRadius: 12, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white, alignItems: 'center', justifyContent: 'center' },
  iconButtonOn: { backgroundColor: colors.green, borderColor: colors.green },
  badge: { position: 'absolute', top: -5, right: -5, minWidth: 18, height: 18, borderRadius: 9, backgroundColor: colors.yellow, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 4 },
  badgeText: { color: colors.ink, fontSize: 10, fontWeight: '800' },
  sports: { gap: 8 },
  sportChip: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 8, borderRadius: 999, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  sportChipOn: { backgroundColor: colors.green, borderColor: colors.green },
  sportText: { color: colors.ink, fontSize: 13, fontWeight: '600' },
  sportTextOn: { color: colors.white },
  hint: { color: colors.muted, fontSize: 11 },
  active: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, alignItems: 'center' },
  activeChip: { flexDirection: 'row', alignItems: 'center', gap: 5, backgroundColor: colors.greenLight, borderRadius: 7, paddingHorizontal: 9, paddingVertical: 5 },
  activeText: { color: colors.green, fontSize: 12, fontWeight: '600' },
  clearAll: { color: colors.muted, fontSize: 12, fontWeight: '600', textDecorationLine: 'underline', marginLeft: 4 },
  split: { flex: 1, flexDirection: 'row' },
  splitList: { flex: 1.1, minWidth: 0 },
  list: { padding: 20, paddingBottom: 32 },
  listWide: { padding: 28 },
  resultHead: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 12, flexWrap: 'wrap' },
  resultCount: { color: colors.ink, fontSize: 16, fontWeight: '800' },
  resultNote: { color: colors.muted, fontSize: 12, flexShrink: 1 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', marginHorizontal: -6 },
  cell: { padding: 6 },
  error: { alignItems: 'center', gap: 8, padding: 20, borderRadius: 14, backgroundColor: colors.errorBackground, marginBottom: 12 },
  errorText: { color: colors.error, fontSize: 13, textAlign: 'center', lineHeight: 19 },
  retry: { backgroundColor: colors.white, borderRadius: 9, paddingHorizontal: 16, paddingVertical: 8, borderWidth: 1, borderColor: '#EBC9C3' },
  retryText: { color: colors.error, fontWeight: '600', fontSize: 12 },
  empty: { alignItems: 'center', gap: 6, paddingVertical: 36, paddingHorizontal: 20, borderRadius: 16, borderWidth: 1, borderStyle: 'dashed', borderColor: '#D5DCCF', backgroundColor: colors.white },
  emptyTitle: { color: colors.ink, fontSize: 15, fontWeight: '700' },
  mapWrap: { flex: 1 },
  mapWrapWide: { flex: 1, margin: 20, marginLeft: 0, borderRadius: 18, overflow: 'hidden', borderWidth: 1, borderColor: colors.line },
  map: { flex: 1 },
  mapCard: { position: 'absolute', left: 14, right: 14, bottom: 14 },
  mapCardWide: { left: undefined, width: 340 },
  mapCardClose: { position: 'absolute', top: -12, right: -6, zIndex: 2, width: 28, height: 28, borderRadius: 14, backgroundColor: colors.white, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: colors.line },
});
