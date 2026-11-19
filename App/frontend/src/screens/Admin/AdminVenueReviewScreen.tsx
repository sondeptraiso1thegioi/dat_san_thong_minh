import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Linking, Modal, Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import LeafletMap from '../../components/map/LeafletMap';
import SportIcon from '../../components/SportIcon';
import { PlainInput } from '../../components/owner/FormControls';
import { ALL_DAYS, WEEKDAYS, WEEKEND, bandsFromPrices, dayShort } from '../../components/owner/priceBands';
import { colors } from '../../constants/theme';
import { amenityLabels, environmentLabels, sportLabels, sports } from '../../constants/sports';
import { RootRoutes } from '../../navigation/types';
import { adminService } from '../../services/adminService';
import { AdminVenueDetail } from '../../types/admin';
import { formatPrice, formatRelative } from '../../utils/format';
import { statusInfo } from '../Owner/OwnerVenuesScreen';

type Props = NativeStackScreenProps<RootRoutes, 'AdminVenueReview'>;
type Action = 'approve' | 'reject' | 'hide';

const quickReasons: Record<'reject' | 'hide', string[]> = {
  reject: ['Vị trí ghim trên bản đồ không khớp địa chỉ', 'Thông tin cụm sân chưa đầy đủ hoặc chưa rõ ràng', 'Bảng giá chưa hợp lý, vui lòng kiểm tra lại'],
  hide: ['Cụm sân đã ngừng hoạt động', 'Vi phạm điều khoản sử dụng của Sun Sport', 'Nhiều phản ánh từ người chơi về chất lượng sân'],
};

/** "Cả tuần", "T2–T6", "T7, CN" hoặc liệt kê ngày. */
function daysLabel(days: number[]): string {
  const same = (set: number[]) => set.length === days.length && set.every(d => days.includes(d));
  if (same(ALL_DAYS)) return 'Cả tuần';
  if (same(WEEKDAYS)) return 'T2–T6';
  if (same(WEEKEND)) return 'T7, CN';
  return ALL_DAYS.filter(d => days.includes(d)).map(d => dayShort[d]).join(', ');
}

export default function AdminVenueReviewScreen({ navigation, route }: Props) {
  const { venueId } = route.params;
  const { width } = useWindowDimensions();
  const wide = width >= 1100;
  const [venue, setVenue] = useState<AdminVenueDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<Action | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try { setVenue(await adminService.getVenue(venueId)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa tải được cụm sân.'); }
  }, [venueId]);
  useEffect(() => { void load(); }, [load]);

  const marker = useMemo(() => (venue ? [{ id: venue.id, lat: venue.lat, lng: venue.lng, label: venue.name, vip: false, selected: true }] : []), [venue]);

  function open(next: Action) {
    setAction(next);
    setReason('');
    setActionError(null);
  }

  async function confirm() {
    if (!action || !venue) return;
    const text = reason.trim();
    if (action !== 'approve' && (text.length < 5 || text.length > 500)) {
      setActionError('Nhập lý do từ 5 đến 500 ký tự để chủ sân biết cần sửa gì.');
      return;
    }
    setBusy(true);
    setActionError(null);
    try {
      const result = await adminService.act(venue.id, action, action === 'approve' ? undefined : text);
      setVenue(result.venue);
      setAction(null);
      setDone(action === 'approve' ? 'Đã duyệt. Cụm sân bắt đầu xuất hiện trong tìm kiếm và gợi ý của người chơi.'
        : action === 'reject' ? 'Đã từ chối. Chủ sân sẽ thấy lý do trong danh sách cụm sân của họ.'
          : `Đã ẩn cụm sân. ${result.cancelledBookings} booking sắp tới đã bị hủy${result.refundsRequested ? `, ${result.refundsRequested} khoản cần hoàn tiền` : ''}.`);
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : 'Chưa xử lý được. Vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  }

  const info = venue ? statusInfo[venue.status] : null;
  const activeSports = venue ? sports.filter(s => venue.courts.some(c => c.isActive && c.sport === s)) : [];
  const bands = venue ? bandsFromPrices(venue.prices) : [];

  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.topBar}>
        <Pressable accessibilityRole="button" accessibilityLabel="Quay lại" onPress={() => navigation.goBack()} hitSlop={8} style={styles.back}>
          <Ionicons name="arrow-back" size={20} color={colors.green} />
        </Pressable>
        <Text accessibilityRole="header" style={styles.topTitle} numberOfLines={1}>{venue?.name ?? 'Xem xét cụm sân'}</Text>
      </View>

      {!venue && !error && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
      {error && <View style={styles.center}><Text style={styles.errorText}>{error}</Text></View>}

      {venue && info && (
        <>
          <ScrollView contentContainerStyle={styles.scroll}>
            <View style={styles.page}>
              <View style={[styles.banner, { backgroundColor: info.background }]}>
                <Ionicons name={info.icon} size={18} color={info.color} />
                <View style={styles.flex}>
                  <Text style={[styles.bannerTitle, { color: info.color }]}>{info.label} · gửi {formatRelative(venue.createdAt)}</Text>
                  {!!venue.rejectReason && <Text style={[styles.bannerText, { color: info.color }]}>Lý do: {venue.rejectReason}</Text>}
                </View>
              </View>
              {done && <View style={styles.done}><Ionicons name="checkmark-circle" size={18} color={colors.green} /><Text style={styles.doneText}>{done}</Text></View>}

              <View style={[styles.columns, wide && styles.columnsWide]}>
                <View style={[styles.column, wide && styles.columnWide]}>
                  <Card title="Vị trí">
                    <LeafletMap markers={marker} onSelect={() => undefined} style={styles.map} />
                    <Text style={styles.value}>{venue.address}</Text>
                    <View style={styles.rowBetween}>
                      <Text style={styles.muted}>{venue.lat.toFixed(5)}, {venue.lng.toFixed(5)}</Text>
                      <Pressable accessibilityRole="link" onPress={() => void Linking.openURL(`https://www.google.com/maps/search/?api=1&query=${venue.lat},${venue.lng}`)} style={styles.link}>
                        <Ionicons name="open-outline" size={14} color={colors.green} /><Text style={styles.linkText}>Đối chiếu trên Google Maps</Text>
                      </Pressable>
                    </View>
                  </Card>
                  <Card title="Thông tin">
                    <Row label="Giờ mở cửa" value={`${venue.openTime}–${venue.closeTime}`} />
                    <Row label="Tiện ích" value={venue.amenities.map(a => amenityLabels[a] ?? a).join(', ') || 'Không có'} />
                    <Row label="Mô tả" value={venue.description || 'Không có'} />
                  </Card>
                </View>

                <View style={[styles.column, wide && styles.columnWide]}>
                  <Card title="Chủ sân">
                    <Text style={styles.value}>{venue.owner.name}</Text>
                    <Text style={styles.muted}>{venue.owner.email}{venue.owner.phone ? ` · ${venue.owner.phone}` : ''}</Text>
                    {venue.owner.status !== 'active' && <Text style={styles.warn}>Tài khoản chủ sân đang bị khóa.</Text>}
                  </Card>
                  <Card title={`Sân con (${venue.courts.filter(c => c.isActive).length} đang hoạt động)`}>
                    {venue.courts.map(court => (
                      <View key={court.id} style={[styles.court, !court.isActive && styles.off]}>
                        <SportIcon sport={court.sport} size={16} color={colors.green} />
                        <Text style={styles.courtName}>{court.name}</Text>
                        <Text style={styles.muted}>{sportLabels[court.sport]} · {environmentLabels[court.environment].toLowerCase()}{court.isActive ? '' : ' · ngừng hoạt động'}</Text>
                      </View>
                    ))}
                  </Card>
                  <Card title="Bảng giá">
                    {activeSports.map(sport => (
                      <View key={sport} style={styles.priceGroup}>
                        <View style={styles.row}><SportIcon sport={sport} size={15} color={colors.green} /><Text style={styles.priceSport}>{sportLabels[sport]}</Text></View>
                        {bands.filter(b => b.sport === sport).map(band => (
                          <View key={band.key} style={styles.priceRow}>
                            <Text style={styles.priceDays}>{daysLabel(band.days)}</Text>
                            <Text style={styles.priceHours}>{String(band.startHour).padStart(2, '0')}:00–{String(band.endHour).padStart(2, '0')}:00</Text>
                            <Text style={styles.priceValue}>{formatPrice(band.price ?? 0)}/giờ</Text>
                          </View>
                        ))}
                        {!bands.some(b => b.sport === sport) && <Text style={styles.warn}>Chưa có bảng giá.</Text>}
                      </View>
                    ))}
                  </Card>
                </View>
              </View>
            </View>
          </ScrollView>

          {venue.status !== 'deleted' && (
            <View style={styles.actions}>
              <View style={styles.actionsInner}>
                {venue.status === 'pending' ? <>
                  <Pressable accessibilityRole="button" onPress={() => open('reject')} style={[styles.button, styles.reject]}><Ionicons name="close-circle-outline" size={18} color={colors.error} /><Text style={styles.rejectText}>Từ chối</Text></Pressable>
                  <Pressable accessibilityRole="button" onPress={() => open('approve')} style={[styles.button, styles.approve]}><Ionicons name="checkmark-circle-outline" size={18} color={colors.white} /><Text style={styles.approveText}>Duyệt cụm sân</Text></Pressable>
                </> : (
                  <Pressable accessibilityRole="button" onPress={() => open('hide')} style={[styles.button, styles.reject]}><Ionicons name="eye-off-outline" size={18} color={colors.error} /><Text style={styles.rejectText}>Ẩn cụm sân</Text></Pressable>
                )}
              </View>
            </View>
          )}

          <Modal visible={action !== null} transparent animationType="fade" onRequestClose={() => setAction(null)}>
            <View style={styles.backdrop}>
              <View style={styles.dialog}>
                <Text accessibilityRole="header" style={styles.dialogTitle}>
                  {action === 'approve' ? 'Duyệt cụm sân?' : action === 'reject' ? 'Từ chối cụm sân' : 'Ẩn cụm sân'}
                </Text>
                <Text style={styles.dialogText}>
                  {action === 'approve' && `"${venue.name}" sẽ xuất hiện ngay trong tìm kiếm và gợi ý của người chơi.`}
                  {action === 'reject' && 'Chủ sân sẽ thấy lý do này, sửa lại rồi gửi duyệt lại.'}
                  {action === 'hide' && 'Cụm sân sẽ bị ẩn khỏi người chơi và chủ sân không sửa được nữa.'}
                </Text>
                {action === 'hide' && venue.upcomingBookings > 0 && (
                  <View style={styles.warnBox}>
                    <Ionicons name="warning-outline" size={18} color="#7A5A10" />
                    <Text style={styles.warnBoxText}>{venue.upcomingBookings} booking sắp tới sẽ bị hủy; các booking đã thanh toán được chuyển sang chờ hoàn tiền.</Text>
                  </View>
                )}
                {action && action !== 'approve' && <>
                  <View style={styles.chips}>
                    {quickReasons[action].map(text => (
                      <Pressable key={text} accessibilityRole="button" onPress={() => setReason(text)} style={[styles.chip, reason === text && styles.chipOn]}>
                        <Text style={[styles.chipText, reason === text && styles.chipTextOn]}>{text}</Text>
                      </Pressable>
                    ))}
                  </View>
                  <PlainInput value={reason} onChangeText={setReason} multiline maxLength={500} placeholder="Lý do (chủ sân sẽ thấy)" accessibilityLabel="Lý do" />
                </>}
                {!!actionError && <Text accessibilityRole="alert" style={styles.dialogError}>{actionError}</Text>}
                <View style={styles.dialogActions}>
                  <Pressable accessibilityRole="button" disabled={busy} onPress={() => setAction(null)} style={[styles.button, styles.cancel]}><Text style={styles.cancelText}>Hủy</Text></Pressable>
                  <Pressable accessibilityRole="button" disabled={busy} onPress={() => void confirm()} style={[styles.button, action === 'approve' ? styles.approve : styles.danger, busy && styles.busy]}>
                    {busy ? <ActivityIndicator color={colors.white} /> : <Text style={styles.approveText}>{action === 'approve' ? 'Duyệt' : action === 'reject' ? 'Từ chối' : 'Ẩn cụm sân'}</Text>}
                  </Pressable>
                </View>
              </View>
            </View>
          </Modal>
        </>
      )}
    </SafeAreaView>
  );
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return <View style={styles.card}><Text style={styles.cardTitle}>{title}</Text>{children}</View>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.infoRow}><Text style={styles.infoLabel}>{label}</Text><Text style={styles.value}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1, minWidth: 0 },
  topBar: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  back: { padding: 4 },
  topTitle: { flex: 1, color: colors.ink, fontSize: 17, fontWeight: '800' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 },
  errorText: { color: colors.error, textAlign: 'center' },
  scroll: { padding: 18, paddingBottom: 110 },
  page: { width: '100%', maxWidth: 1180, alignSelf: 'center', gap: 12 },
  banner: { flexDirection: 'row', gap: 10, padding: 14, borderRadius: 14 },
  bannerTitle: { fontSize: 13, fontWeight: '800' },
  bannerText: { fontSize: 13, marginTop: 3, lineHeight: 19 },
  done: { flexDirection: 'row', gap: 8, alignItems: 'center', padding: 12, borderRadius: 12, backgroundColor: colors.greenLight },
  doneText: { flex: 1, color: colors.green, fontSize: 13, fontWeight: '600', lineHeight: 19 },
  columns: { gap: 12 },
  columnsWide: { flexDirection: 'row', alignItems: 'flex-start' },
  column: { gap: 12 },
  columnWide: { flex: 1, minWidth: 0 },
  card: { backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 16, gap: 8 },
  cardTitle: { color: colors.ink, fontSize: 15, fontWeight: '800', marginBottom: 2 },
  map: { height: 240, borderRadius: 12, overflow: 'hidden', borderWidth: 1, borderColor: colors.line },
  value: { color: colors.ink, fontSize: 14, lineHeight: 20 },
  muted: { color: colors.muted, fontSize: 12, flexShrink: 1 },
  warn: { color: '#7A5A10', fontSize: 12, fontWeight: '600' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  rowBetween: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' },
  link: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingVertical: 4 },
  linkText: { color: colors.green, fontSize: 13, fontWeight: '700' },
  infoRow: { paddingVertical: 6, borderBottomWidth: 1, borderColor: '#F0F1EA' },
  infoLabel: { color: colors.muted, fontSize: 11, fontWeight: '700', letterSpacing: 0.4, marginBottom: 2 },
  court: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingVertical: 6, borderBottomWidth: 1, borderColor: '#F0F1EA', flexWrap: 'wrap' },
  off: { opacity: 0.5 },
  courtName: { color: colors.ink, fontSize: 14, fontWeight: '600' },
  priceGroup: { gap: 6, paddingVertical: 6 },
  priceSport: { color: colors.ink, fontSize: 14, fontWeight: '700' },
  priceRow: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 5, paddingHorizontal: 10, borderRadius: 8, backgroundColor: '#F7F8F2' },
  priceDays: { color: colors.green, fontSize: 12, fontWeight: '700', width: 92 },
  priceHours: { color: colors.ink, fontSize: 13, flex: 1 },
  priceValue: { color: colors.ink, fontSize: 13, fontWeight: '700' },
  actions: { position: 'absolute', left: 0, right: 0, bottom: 0, backgroundColor: colors.white, borderTopWidth: 1, borderColor: colors.line, padding: 14 },
  actionsInner: { flexDirection: 'row', gap: 10, width: '100%', maxWidth: 1180, alignSelf: 'center', justifyContent: 'flex-end' },
  button: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, borderRadius: 12, paddingVertical: 13, paddingHorizontal: 20 },
  approve: { backgroundColor: colors.green },
  approveText: { color: colors.white, fontWeight: '700', fontSize: 14 },
  reject: { borderWidth: 1, borderColor: '#EBC9C3', backgroundColor: colors.errorBackground },
  rejectText: { color: colors.error, fontWeight: '700', fontSize: 14 },
  danger: { backgroundColor: colors.error },
  cancel: { borderWidth: 1, borderColor: colors.line },
  cancelText: { color: colors.ink, fontWeight: '600' },
  busy: { opacity: 0.7 },
  backdrop: { flex: 1, backgroundColor: 'rgba(20, 32, 24, 0.4)', alignItems: 'center', justifyContent: 'center', padding: 20 },
  dialog: { width: '100%', maxWidth: 480, backgroundColor: colors.white, borderRadius: 20, padding: 22, gap: 12 },
  dialogTitle: { color: colors.ink, fontSize: 19, fontWeight: '800' },
  dialogText: { color: colors.muted, fontSize: 13, lineHeight: 20 },
  dialogError: { color: colors.error, fontSize: 13 },
  dialogActions: { flexDirection: 'row', gap: 10, justifyContent: 'flex-end', marginTop: 4 },
  warnBox: { flexDirection: 'row', gap: 8, padding: 12, borderRadius: 12, backgroundColor: '#FFF4D6' },
  warnBoxText: { flex: 1, color: '#7A5A10', fontSize: 12, lineHeight: 18 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  chip: { paddingHorizontal: 10, paddingVertical: 7, borderRadius: 8, borderWidth: 1, borderColor: colors.line, backgroundColor: '#FCFCF8' },
  chipOn: { backgroundColor: colors.greenLight, borderColor: colors.green },
  chipText: { color: colors.ink, fontSize: 12 },
  chipTextOn: { color: colors.green, fontWeight: '600' },
});
