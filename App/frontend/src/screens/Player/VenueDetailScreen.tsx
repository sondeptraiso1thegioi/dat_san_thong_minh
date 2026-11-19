import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Linking, Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import LeafletMap from '../../components/map/LeafletMap';
import SportIcon from '../../components/SportIcon';
import { ApiError } from '../../api/axiosClient';
import { colors } from '../../constants/theme';
import { amenityLabels, environmentLabels, sportLabels, sportTints } from '../../constants/sports';
import { RootRoutes } from '../../navigation/types';
import { bookingService } from '../../services/bookingService';
import { useLocationStore } from '../../store/useLocationStore';
import { CourtSchedule, SlotCell, VenueDetail } from '../../types/booking';
import { Sport } from '../../types/player';
import { formatDay, formatDistance, formatHour, formatPrice, formatShortPrice, upcomingDays } from '../../utils/format';

type Props = NativeStackScreenProps<RootRoutes, 'VenueDetail'>;
interface Selection { courtId: string; startHour: number; hours: number }

const CELL = 64;
const NAME_COL = 118;
const DAYS = 14;

export default function VenueDetailScreen({ navigation, route }: Props) {
  const { venueId, name } = route.params;
  const { width } = useWindowDimensions();
  const wide = width >= 960;
  const position = useLocationStore(state => state.position);
  const [detail, setDetail] = useState<VenueDetail | null>(null);
  const [date, setDate] = useState<string | undefined>(route.params.date);
  const [sport, setSport] = useState<Sport | undefined>(route.params.sport);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<{ message: string; code?: string } | null>(null);
  const preselect = useRef(route.params.startHour !== undefined
    ? { startHour: route.params.startHour, hours: route.params.hours ?? 1 } : null);

  const load = useCallback(async (day?: string) => {
    setLoading(true);
    setError(null);
    try {
      const result = await bookingService.getVenue(venueId, day, position);
      setDetail(result);
      setSport(current => current && result.venue.sports.includes(current) ? current : result.venue.sports[0]);
      // Khung giờ đã lọc ở Tìm sân: chọn sẵn sân con đầu tiên còn trống cả khung.
      const wanted = preselect.current;
      preselect.current = null;
      if (wanted) {
        const court = result.courts.find(c => (!route.params.sport || c.sport === route.params.sport)
          && rangeFree(c, wanted.startHour, wanted.hours));
        setSelection(court ? { courtId: court.id, ...wanted } : null);
      } else {
        // Giữ lựa chọn nếu vẫn còn trống sau khi tải lại.
        setSelection(current => {
          const court = current && result.courts.find(c => c.id === current.courtId);
          return court && current && rangeFree(court, current.startHour, current.hours) ? current : null;
        });
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa tải được cụm sân. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, [venueId, position, route.params.sport]);

  useEffect(() => { void load(date); }, [load, date]);

  // Quay lại từ màn giữ chỗ (ví dụ đã hủy) thì tải lại lưới giờ.
  const focusedOnce = useRef(false);
  useEffect(() => navigation.addListener('focus', () => {
    if (focusedOnce.current) void load(date);
    focusedOnce.current = true;
  }), [navigation, load, date]);

  const days = useMemo(() => upcomingDays(DAYS), []);
  const courts = useMemo(() => detail?.courts.filter(c => c.sport === sport) ?? [], [detail, sport]);
  const selectedCourt = selection ? detail?.courts.find(c => c.id === selection.courtId) ?? null : null;
  const selectedPrice = selectedCourt && selection
    ? selectedCourt.slots.filter(s => s.hour >= selection.startHour && s.hour < selection.startHour + selection.hours)
      .reduce((sum, s) => sum + (s.price ?? 0), 0)
    : 0;
  const maxHours = detail?.maxHours ?? 3;

  // Đổi ngày/môn thì cuộn lưới một lần, để hôm nay không mở ra toàn ô đã qua.
  const gridScroll = useRef<ScrollView>(null);
  const scrolledFor = useRef<string | null>(null);
  useEffect(() => {
    if (!detail || courts.length === 0) return;
    const key = `${detail.date}|${sport}`;
    if (scrolledFor.current === key) return;
    scrolledFor.current = key;
    // Có khung giờ chọn sẵn (từ Tìm sân) thì cuộn tới đó, không thì tới giờ đầu tiên chưa qua.
    const first = selection ? detail.hours.indexOf(selection.startHour)
      : detail.hours.findIndex(h => courts.some(c => c.slots.find(x => x.hour === h)?.state !== 'past'));
    const timer = setTimeout(() => gridScroll.current?.scrollTo({ x: Math.max(0, first - (selection ? 1 : 0)) * CELL, animated: false }), 0);
    return () => clearTimeout(timer);
  }, [detail, courts, sport, selection]);

  function chooseDate(iso: string) {
    if (iso === (detail?.date ?? date)) return;
    setSelection(null);
    setNotice(null);
    setSubmitError(null);
    setDate(iso);
  }

  function chooseSport(next: Sport) {
    setSport(next);
    setSelection(null);
    setNotice(null);
  }

  function pressCell(court: CourtSchedule, hour: number) {
    setNotice(null);
    setSubmitError(null);
    if (selection && selection.courtId === court.id) {
      const end = selection.startHour + selection.hours;
      if (hour >= selection.startHour && hour < end) {
        // Chạm lại trong khung đang chọn: thu về tới giờ vừa chạm, hoặc bỏ chọn nếu chỉ có 1 giờ.
        setSelection(selection.hours === 1 ? null : { ...selection, hours: hour - selection.startHour + 1 });
        return;
      }
      const start = Math.min(selection.startHour, hour);
      const length = Math.max(end, hour + 1) - start;
      if (length <= maxHours && rangeFree(court, start, length)) {
        setSelection({ courtId: court.id, startHour: start, hours: length });
        return;
      }
      if (length > maxHours) setNotice(`Mỗi lần đặt tối đa ${maxHours} giờ liên tiếp trên một sân.`);
    }
    setSelection({ courtId: court.id, startHour: hour, hours: 1 });
  }

  async function hold() {
    if (!selection || !detail) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      const booking = await bookingService.createHold({ courtId: selection.courtId, date: detail.date, startHour: selection.startHour, hours: selection.hours });
      setSelection(null);
      navigation.navigate('BookingHold', { bookingId: booking.id });
    } catch (reason) {
      const code = reason instanceof ApiError ? reason.code : undefined;
      setSubmitError({ message: reason instanceof Error ? reason.message : 'Chưa giữ được chỗ. Vui lòng thử lại.', code });
      if (code === 'SLOT_TAKEN' || code === 'SLOT_IN_PAST' || code === 'COURT_NOT_FOUND') void load(detail.date);
    } finally {
      setSubmitting(false);
    }
  }

  async function openActiveHold() {
    try {
      const active = await bookingService.getActiveHold();
      if (active) navigation.navigate('BookingHold', { bookingId: active.id });
      else { setSubmitError(null); }
    } catch (reason) {
      setSubmitError({ message: reason instanceof Error ? reason.message : 'Chưa mở được chỗ đang giữ.' });
    }
  }

  const venue = detail?.venue;
  const tint = sportTints[sport ?? venue?.sports[0] ?? 'tennis'];

  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.topBar}>
        <Pressable accessibilityRole="button" accessibilityLabel="Quay lại" hitSlop={8} style={styles.back}
          onPress={() => navigation.canGoBack() ? navigation.goBack() : navigation.navigate('Player')}>
          <Ionicons name="arrow-back" size={20} color={colors.green} />
        </Pressable>
        <Text accessibilityRole="header" style={styles.topTitle} numberOfLines={1}>{venue?.name ?? name}</Text>
        {venue?.isVip && <View style={styles.vip}><Ionicons name="star" size={11} color={colors.ink} /><Text style={styles.vipText}>Nổi bật</Text></View>}
      </View>

      {!detail && loading && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
      {!detail && error && (
        <View style={styles.center}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable accessibilityRole="button" style={styles.retry} onPress={() => void load(date)}><Text style={styles.retryText}>Thử lại</Text></Pressable>
        </View>
      )}

      {detail && venue && (
        <>
          <ScrollView contentContainerStyle={[styles.scroll, selection && styles.scrollWithBar]}>
            <View style={[styles.page, wide && styles.pageWide]}>
              {/* Thông tin cụm sân */}
              <View style={[styles.column, wide && styles.infoWide]}>
                {/* Ảnh thay thế khi chưa có ảnh cụm sân; trên điện thoại bỏ để lưới giờ lên gần hơn. */}
                {wide && (
                  <View style={[styles.hero, { backgroundColor: tint.background }]}>
                    <View style={styles.heroIcons}>
                      {venue.sports.map(s => <SportIcon key={s} sport={s} size={26} color={tint.ink} />)}
                    </View>
                    <Text style={[styles.heroSports, { color: tint.ink }]}>{venue.sports.map(s => sportLabels[s]).join(' · ')}</Text>
                  </View>
                )}
                <View style={styles.card}>
                  <Text style={styles.venueName}>{venue.name}</Text>
                  <View style={styles.row}>
                    <Ionicons name="location-outline" size={15} color={colors.muted} />
                    <Text style={styles.muted}>{venue.address}{venue.distanceKm !== null ? ` · ${formatDistance(venue.distanceKm)}` : ''}</Text>
                  </View>
                  <View style={styles.row}>
                    <Ionicons name="time-outline" size={15} color={colors.muted} />
                    <Text style={styles.muted}>{venue.openTime}–{venue.closeTime} · </Text>
                    <Text style={[styles.openState, !venue.isOpenNow && styles.closedState]}>{venue.isOpenNow ? 'Đang mở cửa' : 'Đã đóng cửa'}</Text>
                  </View>
                  <LeafletMap markers={[{ id: venue.id, lat: venue.lat, lng: venue.lng, label: venue.name, vip: venue.isVip, selected: true }]}
                    user={position} onSelect={() => undefined} style={[styles.map, !wide && styles.mapSmall]} />
                  <Pressable accessibilityRole="link" style={styles.directions}
                    onPress={() => void Linking.openURL(`https://www.google.com/maps/dir/?api=1&destination=${venue.lat},${venue.lng}`)}>
                    <Ionicons name="navigate-outline" size={16} color={colors.green} />
                    <Text style={styles.directionsText}>Chỉ đường</Text>
                  </Pressable>
                  {venue.amenities.length > 0 && (
                    <View style={styles.amenities}>
                      {venue.amenities.map(a => <View key={a} style={styles.amenity}><Text style={styles.amenityText}>{amenityLabels[a] ?? a}</Text></View>)}
                    </View>
                  )}
                  {!!detail.description && <Text style={styles.description}>{detail.description}</Text>}
                </View>
              </View>

              {/* Chọn giờ */}
              <View style={[styles.column, wide && styles.bookWide]}>
                <View style={styles.card}>
                  <Text style={styles.sectionTitle}>Chọn giờ chơi</Text>
                  {venue.sports.length > 1 && (
                    <View style={styles.chips}>
                      {venue.sports.map(s => (
                        <Pressable key={s} accessibilityRole="button" accessibilityState={{ selected: s === sport }} onPress={() => chooseSport(s)}
                          style={[styles.chip, s === sport && styles.chipOn]}>
                          <SportIcon sport={s} size={15} color={s === sport ? colors.white : colors.green} />
                          <Text style={[styles.chipText, s === sport && styles.chipTextOn]}>{sportLabels[s]}</Text>
                        </Pressable>
                      ))}
                    </View>
                  )}
                  <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.days}>
                    {days.map(d => {
                      const active = d.iso === detail.date;
                      return (
                        <Pressable key={d.iso} accessibilityRole="button" accessibilityState={{ selected: active }} accessibilityLabel={`${d.label} ${d.sub}`}
                          onPress={() => chooseDate(d.iso)} style={[styles.day, active && styles.dayOn]}>
                          <Text style={[styles.dayLabel, active && styles.dayTextOn]}>{d.label}</Text>
                          <Text style={[styles.daySub, active && styles.dayTextOn]}>{d.sub}</Text>
                        </Pressable>
                      );
                    })}
                  </ScrollView>

                  <View style={styles.legend}>
                    <Legend style={styles.cellFree} label="Còn trống" />
                    <Legend style={styles.cellSelected} label="Đang chọn" />
                    <Legend style={styles.cellBooked} label="Đã có người đặt" />
                    <Legend style={styles.cellOff} label="Đã qua / chưa có giá" />
                  </View>

                  {courts.length === 0
                    ? <Text style={styles.muted}>Cụm sân chưa có sân con nào nhận đặt cho môn này.</Text>
                    : (
                      <View style={[styles.grid, loading && styles.loadingGrid]}>
                        <View style={styles.nameCol}>
                          <View style={styles.headCell} />
                          {courts.map(c => (
                            <View key={c.id} style={styles.nameCell}>
                              <Text style={styles.courtName} numberOfLines={1}>{c.name}</Text>
                              <Text style={styles.courtEnv} numberOfLines={1}>{environmentLabels[c.environment]}</Text>
                            </View>
                          ))}
                        </View>
                        <ScrollView ref={gridScroll} horizontal showsHorizontalScrollIndicator>
                          <View>
                            <View style={styles.gridRow}>
                              {detail.hours.map(h => <View key={h} style={styles.headCell}><Text style={styles.headText}>{formatHour(h)}</Text></View>)}
                            </View>
                            {courts.map(court => (
                              <View key={court.id} style={styles.gridRow}>
                                {court.slots.map(slot => (
                                  <Cell key={slot.hour} court={court} slot={slot}
                                    selected={!!selection && selection.courtId === court.id && slot.hour >= selection.startHour && slot.hour < selection.startHour + selection.hours}
                                    onPress={() => pressCell(court, slot.hour)} />
                                ))}
                              </View>
                            ))}
                          </View>
                        </ScrollView>
                      </View>
                    )}
                  <Text style={styles.hint}>
                    Chạm một ô để chọn giờ bắt đầu, chạm tiếp ô cùng hàng để chơi tới {maxHours} giờ liên tiếp. Giá đã gồm toàn bộ thời gian chơi.
                  </Text>
                  {!!notice && <Text style={styles.notice}>{notice}</Text>}
                  {/* Lỗi giữ chỗ khi lựa chọn đã bị bỏ (ví dụ khung giờ vừa có người đặt): thanh dưới đã ẩn nên hiện ở đây. */}
                  {!!submitError && !selection && <Text accessibilityRole="alert" style={styles.submitError}>{submitError.message}</Text>}
                  {!!error && <Text style={styles.errorText}>{error}</Text>}
                </View>
              </View>
            </View>
          </ScrollView>

          {selection && selectedCourt && (
            <View style={styles.bar}>
              <View style={[styles.barInner, !wide && styles.barInnerNarrow]}>
                <View style={styles.flex}>
                  <Text style={styles.barTitle} numberOfLines={1}>
                    {selectedCourt.name} · {formatDay(`${detail.date}T00:00:00+07:00`)}
                  </Text>
                  <Text style={styles.barSub}>
                    {formatHour(selection.startHour)}–{formatHour(selection.startHour + selection.hours)} · {selection.hours} giờ · <Text style={styles.barPrice}>{formatPrice(selectedPrice)}</Text>
                  </Text>
                  {submitError && (
                    <View style={styles.barErrorRow}>
                      <Text accessibilityRole="alert" style={styles.barError}>{submitError.message}</Text>
                      {submitError.code === 'HOLD_EXISTS' && (
                        <Pressable accessibilityRole="button" onPress={() => void openActiveHold()}>
                          <Text style={styles.barLink}>Xem chỗ đang giữ</Text>
                        </Pressable>
                      )}
                    </View>
                  )}
                </View>
                <Pressable accessibilityRole="button" disabled={submitting} onPress={() => void hold()} style={[styles.holdButton, submitting && styles.busy]}>
                  {submitting ? <ActivityIndicator color={colors.white} />
                    : <><Ionicons name="lock-closed-outline" size={16} color={colors.white} /><Text style={styles.holdText}>Giữ chỗ {detail.holdMinutes} phút</Text></>}
                </Pressable>
              </View>
            </View>
          )}
        </>
      )}
    </SafeAreaView>
  );
}

/** Các giờ [start, start + hours) của sân con đều còn trống. */
function rangeFree(court: CourtSchedule, start: number, hours: number): boolean {
  for (let h = start; h < start + hours; h++) {
    if (court.slots.find(s => s.hour === h)?.state !== 'free') return false;
  }
  return true;
}

const stateLabels: Record<SlotCell['state'], string> = { free: 'còn trống', booked: 'đã có người đặt', past: 'đã qua', unpriced: 'chưa có giá' };

function Cell({ court, slot, selected, onPress }: { court: CourtSchedule; slot: SlotCell; selected: boolean; onPress: () => void }) {
  const free = slot.state === 'free';
  return (
    <Pressable accessibilityRole="button" disabled={!free}
      accessibilityLabel={`${court.name} ${formatHour(slot.hour)}, ${selected ? 'đang chọn' : stateLabels[slot.state]}${slot.price !== null ? `, ${formatPrice(slot.price)}` : ''}`}
      accessibilityState={{ selected, disabled: !free }} onPress={onPress}
      style={[styles.cell, free && styles.cellFree, selected && styles.cellSelected,
        slot.state === 'booked' && styles.cellBooked, (slot.state === 'past' || slot.state === 'unpriced') && styles.cellOff]}>
      <Text style={[styles.cellText, selected && styles.cellTextOn, !free && styles.cellTextOff]} numberOfLines={1}>
        {slot.state === 'booked' ? 'Đã đặt' : slot.state === 'past' ? 'Đã qua' : slot.state === 'unpriced' ? '—' : slot.price !== null ? formatShortPrice(slot.price) : ''}
      </Text>
    </Pressable>
  );
}

function Legend({ style, label }: { style: object; label: string }) {
  return <View style={styles.legendItem}><View style={[styles.legendBox, style]} /><Text style={styles.legendText}>{label}</Text></View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1, minWidth: 0 },
  topBar: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  back: { padding: 4 },
  topTitle: { flex: 1, color: colors.ink, fontSize: 17, fontWeight: '800' },
  vip: { flexDirection: 'row', alignItems: 'center', gap: 3, backgroundColor: colors.yellow, borderRadius: 999, paddingHorizontal: 8, paddingVertical: 3 },
  vipText: { color: colors.ink, fontSize: 11, fontWeight: '700' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, gap: 12 },
  errorText: { color: colors.error, textAlign: 'center', fontSize: 13 },
  retry: { backgroundColor: colors.green, borderRadius: 10, paddingVertical: 11, paddingHorizontal: 20 },
  retryText: { color: colors.white, fontWeight: '700' },
  scroll: { padding: 16, paddingBottom: 32 },
  scrollWithBar: { paddingBottom: 150 },
  page: { width: '100%', maxWidth: 1200, alignSelf: 'center', gap: 14 },
  pageWide: { flexDirection: 'row', alignItems: 'flex-start' },
  column: { gap: 12, minWidth: 0 },
  infoWide: { flex: 4 },
  bookWide: { flex: 6 },
  hero: { height: 110, borderRadius: 16, alignItems: 'center', justifyContent: 'center', gap: 8 },
  heroIcons: { flexDirection: 'row', gap: 14 },
  heroSports: { fontSize: 13, fontWeight: '700' },
  card: { backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 16, gap: 10 },
  venueName: { color: colors.ink, fontSize: 20, fontWeight: '800' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6, flexWrap: 'wrap' },
  muted: { color: colors.muted, fontSize: 13, flexShrink: 1, lineHeight: 19 },
  openState: { color: colors.green, fontSize: 13, fontWeight: '700' },
  closedState: { color: colors.error },
  map: { height: 220, borderRadius: 12, overflow: 'hidden', borderWidth: 1, borderColor: colors.line },
  mapSmall: { height: 170 },
  directions: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, borderWidth: 1, borderColor: colors.green, borderRadius: 10, paddingVertical: 10 },
  directionsText: { color: colors.green, fontWeight: '700', fontSize: 14 },
  amenities: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  amenity: { backgroundColor: colors.greenLight, borderRadius: 999, paddingHorizontal: 10, paddingVertical: 5 },
  amenityText: { color: colors.green, fontSize: 12, fontWeight: '600' },
  description: { color: colors.ink, fontSize: 14, lineHeight: 21 },
  sectionTitle: { color: colors.ink, fontSize: 16, fontWeight: '800' },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 8, borderRadius: 999, borderWidth: 1, borderColor: colors.line, backgroundColor: '#FCFCF8' },
  chipOn: { backgroundColor: colors.green, borderColor: colors.green },
  chipText: { color: colors.ink, fontSize: 13, fontWeight: '600' },
  chipTextOn: { color: colors.white },
  days: { gap: 8, paddingVertical: 2 },
  day: { width: 74, alignItems: 'center', paddingVertical: 9, borderRadius: 12, borderWidth: 1, borderColor: colors.line, backgroundColor: '#FCFCF8' },
  dayOn: { backgroundColor: colors.green, borderColor: colors.green },
  dayLabel: { color: colors.ink, fontSize: 12, fontWeight: '700' },
  daySub: { color: colors.muted, fontSize: 12, marginTop: 2 },
  dayTextOn: { color: colors.white },
  legend: { flexDirection: 'row', flexWrap: 'wrap', gap: 14 },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  legendBox: { width: 16, height: 16, borderRadius: 4, borderWidth: 1 },
  legendText: { color: colors.muted, fontSize: 12 },
  grid: { flexDirection: 'row' },
  loadingGrid: { opacity: 0.5 },
  nameCol: { width: NAME_COL },
  headCell: { width: CELL, height: 28, alignItems: 'center', justifyContent: 'center' },
  headText: { color: colors.muted, fontSize: 11, fontWeight: '700' },
  nameCell: { height: 52, justifyContent: 'center', paddingRight: 8, marginBottom: 6 },
  courtName: { color: colors.ink, fontSize: 13, fontWeight: '700' },
  courtEnv: { color: colors.muted, fontSize: 11, marginTop: 2 },
  gridRow: { flexDirection: 'row' },
  cell: { width: CELL - 6, height: 52, marginRight: 6, marginBottom: 6, borderRadius: 10, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: 'transparent' },
  cellFree: { backgroundColor: colors.white, borderColor: '#CBD8C9' },
  cellSelected: { backgroundColor: colors.green, borderColor: colors.green },
  cellBooked: { backgroundColor: '#ECEDE7', borderColor: '#ECEDE7' },
  cellOff: { backgroundColor: '#F5F5F1', borderColor: '#F5F5F1' },
  cellText: { color: colors.green, fontSize: 13, fontWeight: '700' },
  cellTextOn: { color: colors.white },
  cellTextOff: { color: '#A3A79F', fontWeight: '500', fontSize: 11 },
  hint: { color: colors.muted, fontSize: 12, lineHeight: 18 },
  submitError: { color: colors.error, fontSize: 13, fontWeight: '600', backgroundColor: colors.errorBackground, padding: 10, borderRadius: 10 },
  notice: { color: '#7A5A10', fontSize: 12, fontWeight: '600', backgroundColor: '#FFF4D6', padding: 10, borderRadius: 10 },
  bar: { position: 'absolute', left: 0, right: 0, bottom: 0, backgroundColor: colors.white, borderTopWidth: 1, borderColor: colors.line, padding: 14 },
  barInner: { flexDirection: 'row', alignItems: 'center', gap: 14, width: '100%', maxWidth: 1200, alignSelf: 'center' },
  barInnerNarrow: { gap: 10 },
  barTitle: { color: colors.ink, fontSize: 15, fontWeight: '800' },
  barSub: { color: colors.muted, fontSize: 13, marginTop: 2 },
  barPrice: { color: colors.ink, fontWeight: '800' },
  barErrorRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 8, marginTop: 4 },
  barError: { color: colors.error, fontSize: 12, flexShrink: 1 },
  barLink: { color: colors.green, fontSize: 12, fontWeight: '700', textDecorationLine: 'underline' },
  holdButton: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.green, borderRadius: 12, paddingVertical: 13, paddingHorizontal: 18 },
  holdText: { color: colors.white, fontWeight: '700', fontSize: 14 },
  busy: { opacity: 0.7 },
});
