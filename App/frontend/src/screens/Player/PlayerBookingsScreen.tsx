import React, { useCallback, useRef, useState } from 'react';
import {
  ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View,
} from 'react-native';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import { BottomTabNavigationProp } from '@react-navigation/bottom-tabs';
import { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import SportIcon from '../../components/SportIcon';
import { colors } from '../../constants/theme';
import { environmentLabels, sportLabels } from '../../constants/sports';
import { PlayerTabRoutes, RootRoutes } from '../../navigation/types';
import { bookingService } from '../../services/bookingService';
import { BookingListItem, BookingListView, BookingStatus } from '../../types/booking';
import { formatDay, formatPrice, formatTime } from '../../utils/format';

type IconName = React.ComponentProps<typeof Ionicons>['name'];

const filters: { value: BookingListView; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'upcoming', label: 'Sắp tới' },
  { value: 'history', label: 'Lịch sử' },
];

const statusView: Record<BookingStatus, { label: string; color: string; background: string; icon: IconName }> = {
  pending: { label: 'Đang giữ chỗ', color: '#966515', background: '#FFF4D6', icon: 'hourglass-outline' },
  expired: { label: 'Đã hết hạn giữ chỗ', color: colors.muted, background: '#F0F1EA', icon: 'time-outline' },
  confirmed: { label: 'Đã xác nhận', color: colors.green, background: colors.greenLight, icon: 'checkmark-circle-outline' },
  cancelled: { label: 'Đã hủy', color: colors.error, background: colors.errorBackground, icon: 'close-circle-outline' },
  completed: { label: 'Đã hoàn thành', color: '#3572A5', background: '#EAF3FC', icon: 'trophy-outline' },
};

const paymentLabels: Record<string, string> = {
  pending: 'Chờ thanh toán',
  paid: 'Đã thanh toán',
  failed: 'Thanh toán thất bại',
  refund_pending: 'Chờ hoàn tiền',
  refunded: 'Đã hoàn tiền',
};

export default function PlayerBookingsScreen() {
  const navigation = useNavigation<BottomTabNavigationProp<PlayerTabRoutes>>();
  const rootNavigation = navigation.getParent<NativeStackNavigationProp<RootRoutes>>();
  const [view, setView] = useState<BookingListView>('all');
  const [items, setItems] = useState<BookingListItem[]>([]);
  const [page, setPage] = useState(1);
  const pageRef = useRef(1);
  const [totalPages, setTotalPages] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (reset: boolean) => {
    const targetPage = reset ? 1 : pageRef.current + 1;
    if (reset) setRefreshing(true);
    else setLoadingMore(true);
    setError(null);
    try {
      const result = await bookingService.getBookings(view, targetPage, 20);
      setItems(current => reset ? result.items : [...current, ...result.items]);
      pageRef.current = result.page;
      setPage(result.page);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa tải được lịch đặt.');
    } finally {
      setLoading(false);
      setRefreshing(false);
      setLoadingMore(false);
    }
  }, [view]);

  useFocusEffect(useCallback(() => {
    void load(true);
  }, [load]));

  function changeView(next: BookingListView) {
    if (next === view) return;
    setView(next);
    setItems([]);
    pageRef.current = 1;
    setPage(1);
    setTotalPages(0);
    setTotalCount(0);
    setLoading(true);
  }

  function openBooking(id: string) {
    rootNavigation?.navigate('BookingHold', { bookingId: id });
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <View>
          <Text style={styles.eyebrow}>SUN SPORT</Text>
          <Text accessibilityRole="header" style={styles.title}>Lịch đặt của tôi</Text>
          <Text style={styles.subtitle}>{totalCount > 0 ? `${totalCount} lượt đặt sân` : 'Theo dõi các lượt đặt sân của bạn'}</Text>
        </View>
        <View style={styles.headerIcon}><Ionicons name="calendar-clear-outline" size={23} color={colors.green} /></View>
      </View>

      <View style={styles.filterRow}>
        {filters.map(filter => {
          const active = view === filter.value;
          return (
            <Pressable key={filter.value} accessibilityRole="button" accessibilityState={{ selected: active }}
              onPress={() => changeView(filter.value)} style={[styles.filter, active && styles.filterActive]}>
              <Text style={[styles.filterText, active && styles.filterTextActive]}>{filter.label}</Text>
            </Pressable>
          );
        })}
      </View>

      {loading && items.length === 0 ? (
        <View style={styles.center}><ActivityIndicator size="large" color={colors.green} /><Text style={styles.muted}>Đang tải lịch đặt…</Text></View>
      ) : error && items.length === 0 ? (
        <View style={styles.center}>
          <Ionicons name="cloud-offline-outline" size={38} color={colors.muted} />
          <Text style={styles.error}>{error}</Text>
          <Pressable accessibilityRole="button" style={styles.retry} onPress={() => void load(true)}><Text style={styles.retryText}>Thử lại</Text></Pressable>
        </View>
      ) : items.length === 0 ? (
        <View style={styles.center}>
          <View style={styles.emptyIcon}><Ionicons name="calendar-outline" size={34} color={colors.green} /></View>
          <Text style={styles.emptyTitle}>{view === 'upcoming' ? 'Chưa có lịch sân sắp tới' : view === 'history' ? 'Chưa có lịch sử đặt sân' : 'Bạn chưa đặt sân nào'}</Text>
          <Text style={styles.muted}>Khi bạn giữ chỗ hoặc đặt sân, thông tin sẽ xuất hiện tại đây.</Text>
          <Pressable accessibilityRole="button" style={styles.retry} onPress={() => navigation.navigate('Search')}>
            <Text style={styles.retryText}>Tìm sân ngay</Text>
          </Pressable>
        </View>
      ) : (
        <ScrollView contentContainerStyle={styles.list} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.green} />}>
          {items.map(item => <BookingCard key={item.id} item={item} onPress={() => openBooking(item.id)} />)}
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          {page < totalPages && (
            <Pressable accessibilityRole="button" disabled={loadingMore} onPress={() => void load(false)} style={[styles.loadMore, loadingMore && styles.disabled]}>
              {loadingMore ? <ActivityIndicator color={colors.green} /> : <Text style={styles.loadMoreText}>Tải thêm lịch đặt</Text>}
            </Pressable>
          )}
          <Text style={styles.footer}>{items.length} / {totalCount} lượt đặt</Text>
        </ScrollView>
      )}
    </SafeAreaView>
  );
}

function BookingCard({ item, onPress }: { item: BookingListItem; onPress: () => void }) {
  const state = statusView[item.status];
  const paymentLabel = item.paymentStatus ? paymentLabels[item.paymentStatus] ?? item.paymentStatus : null;
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
      <View style={styles.cardTop}>
        <View style={styles.dateBlock}>
          <Text style={styles.dateDay}>{formatDay(item.startAt)}</Text>
          <Text style={styles.dateTime}>{formatTime(item.startAt)}–{formatTime(item.endAt)}</Text>
        </View>
        <View style={[styles.badge, { backgroundColor: state.background }]}>
          <Ionicons name={state.icon} size={13} color={state.color} />
          <Text style={[styles.badgeText, { color: state.color }]}>{state.label}</Text>
        </View>
      </View>
      <View style={styles.divider} />
      <Text style={styles.venue}>{item.venueName}</Text>
      <Text numberOfLines={2} style={styles.address}>{item.venueAddress}</Text>
      <View style={styles.courtRow}>
        <SportIcon sport={item.sport} size={17} color={colors.green} />
        <Text style={styles.court} numberOfLines={1}>{item.courtName} · {sportLabels[item.sport]} · {environmentLabels[item.environment].toLowerCase()}</Text>
      </View>
      <View style={styles.divider} />
      <View style={styles.bottomRow}>
        <View>
          <Text style={styles.priceLabel}>Tổng tiền</Text>
          <Text style={styles.price}>{formatPrice(item.price)}</Text>
          {!!paymentLabel && <Text style={styles.payment}>{paymentLabel}</Text>}
          {!paymentLabel && item.status === 'pending' && <Text style={styles.payment}>Chưa phát sinh thanh toán</Text>}
        </View>
        <View style={styles.detailLink}><Text style={styles.detailText}>Chi tiết</Text><Ionicons name="chevron-forward" size={16} color={colors.green} /></View>
      </View>
      {item.status === 'cancelled' && !!item.cancelReason && <Text style={styles.cancelReason}>Lý do: {item.cancelReason}</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  header: { paddingHorizontal: 18, paddingTop: 10, paddingBottom: 16, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  eyebrow: { color: colors.green, fontSize: 10, fontWeight: '800', letterSpacing: 1.4 },
  title: { color: colors.ink, fontSize: 24, fontWeight: '800', marginTop: 3 },
  subtitle: { color: colors.muted, fontSize: 12, marginTop: 4 },
  headerIcon: { width: 44, height: 44, borderRadius: 14, backgroundColor: colors.greenLight, alignItems: 'center', justifyContent: 'center' },
  filterRow: { flexDirection: 'row', gap: 8, paddingHorizontal: 18, paddingBottom: 14 },
  filter: { borderRadius: 20, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white, paddingVertical: 9, paddingHorizontal: 17 },
  filterActive: { backgroundColor: colors.green, borderColor: colors.green },
  filterText: { fontSize: 13, color: colors.muted, fontWeight: '700' },
  filterTextActive: { color: colors.white },
  list: { paddingHorizontal: 16, paddingBottom: 34, gap: 12 },
  card: { backgroundColor: colors.white, borderRadius: 17, borderWidth: 1, borderColor: '#E8EAE1', padding: 15, gap: 9 },
  pressed: { opacity: 0.82 },
  cardTop: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 },
  dateBlock: { gap: 3 },
  dateDay: { color: colors.ink, fontSize: 14, fontWeight: '800' },
  dateTime: { color: colors.green, fontSize: 13, fontWeight: '700' },
  badge: { flexDirection: 'row', alignItems: 'center', gap: 5, paddingVertical: 5, paddingHorizontal: 8, borderRadius: 20 },
  badgeText: { fontSize: 10, fontWeight: '800' },
  divider: { height: 1, backgroundColor: '#F0F1EA' },
  venue: { color: colors.ink, fontSize: 16, fontWeight: '800' },
  address: { color: colors.muted, fontSize: 12, lineHeight: 17 },
  courtRow: { flexDirection: 'row', alignItems: 'center', gap: 7 },
  court: { color: colors.ink, fontSize: 12, flex: 1 },
  bottomRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  priceLabel: { color: colors.muted, fontSize: 11 },
  price: { color: colors.ink, fontSize: 17, fontWeight: '800', marginTop: 2 },
  payment: { color: colors.muted, fontSize: 11, marginTop: 2 },
  detailLink: { flexDirection: 'row', alignItems: 'center', gap: 2, paddingVertical: 9, paddingLeft: 10 },
  detailText: { color: colors.green, fontSize: 13, fontWeight: '800' },
  cancelReason: { color: colors.error, fontSize: 11, lineHeight: 16 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 28, gap: 12 },
  muted: { color: colors.muted, fontSize: 13, textAlign: 'center', lineHeight: 19 },
  error: { color: colors.error, fontSize: 13, lineHeight: 19, textAlign: 'center' },
  retry: { backgroundColor: colors.green, borderRadius: 12, paddingVertical: 12, paddingHorizontal: 20, marginTop: 4 },
  retryText: { color: colors.white, fontWeight: '800', fontSize: 13 },
  emptyIcon: { width: 70, height: 70, borderRadius: 24, backgroundColor: colors.greenLight, alignItems: 'center', justifyContent: 'center' },
  emptyTitle: { color: colors.ink, fontSize: 17, fontWeight: '800', textAlign: 'center' },
  loadMore: { alignItems: 'center', justifyContent: 'center', padding: 14, borderRadius: 12, borderWidth: 1, borderColor: colors.green, backgroundColor: colors.white },
  loadMoreText: { color: colors.green, fontSize: 13, fontWeight: '800' },
  disabled: { opacity: 0.55 },
  footer: { color: colors.muted, fontSize: 11, textAlign: 'center', paddingTop: 2 },
});
