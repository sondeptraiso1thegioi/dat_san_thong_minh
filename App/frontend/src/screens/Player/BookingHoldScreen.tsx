import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import SportIcon from '../../components/SportIcon';
import { colors } from '../../constants/theme';
import { environmentLabels, sportLabels } from '../../constants/sports';
import { RootRoutes } from '../../navigation/types';
import { bookingService } from '../../services/bookingService';
import { BookingDetail, BookingStatus } from '../../types/booking';
import { formatDay, formatPrice, formatTime } from '../../utils/format';

type Props = NativeStackScreenProps<RootRoutes, 'BookingHold'>;
type IconName = React.ComponentProps<typeof Ionicons>['name'];

const statusView: Record<BookingStatus, { title: string; text: string; icon: IconName; color: string; background: string }> = {
  pending: { title: 'Đang giữ chỗ cho bạn', text: 'Hoàn tất thanh toán trước khi hết giờ để xác nhận sân.', icon: 'hourglass-outline', color: colors.green, background: colors.greenLight },
  expired: { title: 'Đã hết thời gian giữ chỗ', text: 'Khung giờ đã được nhả cho người khác. Bạn có thể chọn lại giờ.', icon: 'time-outline', color: '#7A5A10', background: '#FFF4D6' },
  cancelled: { title: 'Đã hủy giữ chỗ', text: 'Khung giờ đã được nhả cho người khác.', icon: 'close-circle-outline', color: colors.error, background: colors.errorBackground },
  confirmed: { title: 'Đã xác nhận đặt sân', text: 'Hẹn gặp bạn ở sân.', icon: 'checkmark-circle', color: colors.green, background: colors.greenLight },
  completed: { title: 'Đã chơi xong', text: 'Cảm ơn bạn đã đặt sân qua Sun Sport.', icon: 'trophy-outline', color: colors.green, background: colors.greenLight },
};

export default function BookingHoldScreen({ navigation, route }: Props) {
  const { bookingId } = route.params;
  const [booking, setBooking] = useState<BookingDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  // Lệch giữa đồng hồ máy chủ và thiết bị, để đếm ngược theo giờ máy chủ.
  const skew = useRef(0);
  const [now, setNow] = useState(Date.now());

  const load = useCallback(async () => {
    setError(null);
    try {
      const result = await bookingService.getBooking(bookingId);
      skew.current = Date.parse(result.serverNow) - Date.now();
      setNow(Date.now());
      setBooking(result);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa tải được thông tin giữ chỗ.');
    }
  }, [bookingId]);

  useEffect(() => { void load(); }, [load]);

  const counting = booking?.status === 'pending' && !!booking.holdExpiresAt;
  useEffect(() => {
    if (!counting) return;
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [counting]);

  const remainingMs = booking?.holdExpiresAt ? Date.parse(booking.holdExpiresAt) - (now + skew.current) : 0;
  const status: BookingStatus | null = booking
    ? booking.status === 'pending' && remainingMs <= 0 ? 'expired' : booking.status
    : null;

  async function cancel() {
    setCancelling(true);
    setError(null);
    try {
      setBooking(await bookingService.cancelHold(bookingId));
      setConfirmCancel(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa hủy được. Vui lòng thử lại.');
    } finally {
      setCancelling(false);
    }
  }

  function chooseAgain() {
    if (!booking) return;
    // Đi từ màn chi tiết của chính cụm sân này thì quay lại màn đó (tự tải lại lưới giờ),
    // không thì thay màn giữ chỗ bằng màn chi tiết để nút Quay lại về trang trước.
    const previous = navigation.getState().routes.find(r => r.name === 'VenueDetail'
      && (r.params as RootRoutes['VenueDetail'] | undefined)?.venueId === booking.venueId);
    if (previous) navigation.popTo('VenueDetail', previous.params as RootRoutes['VenueDetail']);
    else navigation.replace('VenueDetail', {
      venueId: booking.venueId, name: booking.venueName, sport: booking.sport, date: booking.startAt.slice(0, 10),
    });
  }

  const view = status ? statusView[status] : null;
  const seconds = Math.max(0, Math.ceil(remainingMs / 1000));
  const countdown = `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;

  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.topBar}>
        <Pressable accessibilityRole="button" accessibilityLabel="Quay lại" hitSlop={8} style={styles.back}
          onPress={() => navigation.canGoBack() ? navigation.goBack() : navigation.navigate('Player')}>
          <Ionicons name="arrow-back" size={20} color={colors.green} />
        </Pressable>
        <Text accessibilityRole="header" style={styles.topTitle}>Giữ chỗ</Text>
      </View>

      {!booking && !error && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
      {!booking && error && (
        <View style={styles.center}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable accessibilityRole="button" style={styles.primary} onPress={() => void load()}><Text style={styles.primaryText}>Thử lại</Text></Pressable>
        </View>
      )}

      {booking && view && status && (
        <ScrollView contentContainerStyle={styles.scroll}>
          <View style={styles.page}>
            <View style={[styles.status, { backgroundColor: view.background }]}>
              <Ionicons name={view.icon} size={26} color={view.color} />
              <Text style={[styles.statusTitle, { color: view.color }]}>{view.title}</Text>
              {status === 'pending' && (
                <Text accessibilityRole="timer" accessibilityLabel={`Còn ${Math.floor(seconds / 60)} phút ${seconds % 60} giây`}
                  style={[styles.countdown, seconds < 60 && styles.countdownLow]}>{countdown}</Text>
              )}
              <Text style={[styles.statusText, { color: view.color }]}>{view.text}</Text>
              {status === 'cancelled' && !!booking.cancelReason && <Text style={[styles.statusText, { color: view.color }]}>Lý do: {booking.cancelReason}</Text>}
            </View>

            <View style={styles.card}>
              <Text style={styles.venue}>{booking.venueName}</Text>
              <Text style={styles.muted}>{booking.venueAddress}</Text>
              <View style={styles.divider} />
              <Row icon={<SportIcon sport={booking.sport} size={16} color={colors.green} />}
                label="Sân" value={`${booking.courtName} · ${sportLabels[booking.sport]} · ${environmentLabels[booking.environment].toLowerCase()}`} />
              <Row icon={<Ionicons name="calendar-outline" size={16} color={colors.green} />} label="Ngày" value={formatDay(booking.startAt)} />
              <Row icon={<Ionicons name="time-outline" size={16} color={colors.green} />} label="Giờ"
                value={`${formatTime(booking.startAt)}–${formatTime(booking.endAt)} (${booking.hours} giờ)`} />
              <View style={styles.divider} />
              <View style={styles.totalRow}>
                <Text style={styles.totalLabel}>Tổng tiền</Text>
                <Text style={styles.total}>{formatPrice(booking.price)}</Text>
              </View>
            </View>

            {!!error && <Text accessibilityRole="alert" style={styles.errorText}>{error}</Text>}

            {status === 'pending' && (
              <View style={styles.actions}>
                <Pressable accessibilityRole="button" disabled style={[styles.primary, styles.disabled]}>
                  <Ionicons name="card-outline" size={18} color={colors.white} />
                  <Text style={styles.primaryText}>Thanh toán {formatPrice(booking.price)}</Text>
                </Pressable>
                <Text style={styles.note}>Thanh toán giả lập sẽ có ở bước tiếp theo. Hiện bạn có thể giữ hoặc hủy chỗ.</Text>
                {confirmCancel ? (
                  <View style={styles.confirm}>
                    <Text style={styles.confirmText}>Hủy chỗ đang giữ? Khung giờ sẽ được nhả cho người khác.</Text>
                    <View style={styles.confirmButtons}>
                      <Pressable accessibilityRole="button" disabled={cancelling} onPress={() => setConfirmCancel(false)} style={styles.secondary}>
                        <Text style={styles.secondaryText}>Không</Text>
                      </Pressable>
                      <Pressable accessibilityRole="button" disabled={cancelling} onPress={() => void cancel()} style={[styles.danger, cancelling && styles.disabled]}>
                        {cancelling ? <ActivityIndicator color={colors.white} /> : <Text style={styles.primaryText}>Hủy giữ chỗ</Text>}
                      </Pressable>
                    </View>
                  </View>
                ) : (
                  <Pressable accessibilityRole="button" onPress={() => setConfirmCancel(true)} style={styles.secondary}>
                    <Text style={styles.cancelText}>Hủy giữ chỗ</Text>
                  </Pressable>
                )}
              </View>
            )}

            {(status === 'expired' || status === 'cancelled') && (
              <Pressable accessibilityRole="button" onPress={chooseAgain} style={styles.primary}>
                <Ionicons name="calendar-outline" size={18} color={colors.white} />
                <Text style={styles.primaryText}>Chọn giờ khác</Text>
              </Pressable>
            )}
          </View>
        </ScrollView>
      )}
    </SafeAreaView>
  );
}

function Row({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <View style={styles.row}>
      {icon}
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  topBar: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  back: { padding: 4 },
  topTitle: { flex: 1, color: colors.ink, fontSize: 17, fontWeight: '800' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, gap: 12 },
  scroll: { padding: 18, paddingBottom: 40 },
  page: { width: '100%', maxWidth: 560, alignSelf: 'center', gap: 14 },
  status: { alignItems: 'center', borderRadius: 18, padding: 20, gap: 6 },
  statusTitle: { fontSize: 17, fontWeight: '800', textAlign: 'center' },
  statusText: { fontSize: 13, textAlign: 'center', lineHeight: 19 },
  countdown: { color: colors.ink, fontSize: 44, fontWeight: '800', letterSpacing: 2, fontVariant: ['tabular-nums'], marginVertical: 4 },
  countdownLow: { color: colors.error },
  card: { backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 16, gap: 10 },
  venue: { color: colors.ink, fontSize: 18, fontWeight: '800' },
  muted: { color: colors.muted, fontSize: 13, lineHeight: 19 },
  divider: { height: 1, backgroundColor: '#F0F1EA' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  rowLabel: { color: colors.muted, fontSize: 13, width: 40 },
  rowValue: { color: colors.ink, fontSize: 14, fontWeight: '600', flex: 1 },
  totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  totalLabel: { color: colors.muted, fontSize: 14 },
  total: { color: colors.ink, fontSize: 20, fontWeight: '800' },
  actions: { gap: 10 },
  primary: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: colors.green, borderRadius: 12, paddingVertical: 14, paddingHorizontal: 20 },
  primaryText: { color: colors.white, fontWeight: '700', fontSize: 15 },
  disabled: { opacity: 0.5 },
  note: { color: colors.muted, fontSize: 12, textAlign: 'center', lineHeight: 18 },
  secondary: { alignItems: 'center', justifyContent: 'center', borderRadius: 12, paddingVertical: 13, paddingHorizontal: 20, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  secondaryText: { color: colors.ink, fontWeight: '600' },
  cancelText: { color: colors.error, fontWeight: '700' },
  danger: { alignItems: 'center', justifyContent: 'center', borderRadius: 12, paddingVertical: 13, paddingHorizontal: 20, backgroundColor: colors.error, flex: 1 },
  confirm: { backgroundColor: colors.errorBackground, borderRadius: 14, padding: 14, gap: 10 },
  confirmText: { color: colors.error, fontSize: 13, lineHeight: 19 },
  confirmButtons: { flexDirection: 'row', gap: 10 },
  errorText: { color: colors.error, textAlign: 'center', fontSize: 13 },
});
