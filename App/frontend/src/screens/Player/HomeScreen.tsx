import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, useWindowDimensions, View, ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import SportIcon from '../../components/SportIcon';
import VenueCard from '../../components/VenueCard';
import { colors } from '../../constants/theme';
import { environmentLabels, sportLabels, sports } from '../../constants/sports';
import { PlayerTabRoutes, RootRoutes } from '../../navigation/types';
import { playerService } from '../../services/playerService';
import { useAuthStore } from '../../store/useAuthStore';
import { useLocationStore } from '../../store/useLocationStore';
import { PlayerHome, UpcomingBooking, VenueCard as Venue } from '../../types/player';
import { formatDay, formatPrice, formatTime, givenName, greeting, minutesUntil } from '../../utils/format';

type Props = CompositeScreenProps<BottomTabScreenProps<PlayerTabRoutes, 'Home'>, NativeStackScreenProps<RootRoutes>>;

export default function HomeScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 960;
  const columns = width >= 1280 ? 3 : width >= 700 ? 2 : 1;
  const user = useAuthStore(state => state.session?.user);
  const [home, setHome] = useState<PlayerHome | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const { position, locating, locate } = useLocationStore();

  const load = useCallback(async () => {
    setError(null);
    try {
      setHome(await playerService.getHome(position));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa tải được trang chủ. Vui lòng thử lại.');
    }
  }, [position]);

  useEffect(() => { void load(); }, [load]);
  // Quay lại từ màn giữ chỗ/chi tiết sân thì tải lại để thẻ "Trận sắp tới" đúng trạng thái.
  const focusedOnce = useRef(false);
  useEffect(() => navigation.addListener('focus', () => {
    if (focusedOnce.current) void load();
    focusedOnce.current = true;
  }), [navigation, load]);

  async function refresh() {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }

  const openVenue = (venue: Venue) => navigation.navigate('VenueDetail', { venueId: venue.id, name: venue.name });
  const gridItem: ViewStyle = { width: columns === 1 ? '100%' : `${100 / columns}%` as `${number}%` };

  return (
    <SafeAreaView style={styles.safe} edges={wide ? [] : ['top']}>
      <ScrollView
        contentContainerStyle={[styles.scroll, wide && styles.scrollWide]}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} tintColor={colors.green} />}
      >
        <View style={styles.page}>
          <View style={styles.header}>
            <View style={styles.headerText}>
              <Text style={styles.greeting}>{greeting()}, {givenName(user?.name ?? '')}</Text>
              <LocationLine home={home} locating={locating} onLocate={() => void locate()} />
            </View>
            <Pressable accessibilityRole="button" accessibilityLabel="Tài khoản" onPress={() => navigation.navigate('Account')} style={styles.avatar}>
              <Text style={styles.avatarText}>{givenName(user?.name ?? '?').charAt(0).toUpperCase()}</Text>
            </Pressable>
          </View>

          <Pressable accessibilityRole="button" onPress={() => navigation.navigate('Suggest')} style={({ pressed }) => [styles.ask, pressed && styles.askPressed]}>
            <View style={styles.askHead}>
              <Ionicons name="sparkles" size={16} color={colors.yellow} />
              <Text style={styles.askKicker}>GỢI Ý THÔNG MINH</Text>
            </View>
            <Text style={styles.askTitle}>Hôm nay bạn muốn ra sân lúc nào?</Text>
            <View style={styles.askBox}>
              <Text style={styles.askPlaceholder} numberOfLines={1}>Ví dụ: tối mai đánh pickleball gần nhà</Text>
              <View style={styles.askButton}><Ionicons name="arrow-forward" size={17} color={colors.ink} /></View>
            </View>
            <Text style={styles.askNote}>Gợi ý sân và khung giờ theo khoảng cách, thời tiết và thói quen của bạn.</Text>
          </Pressable>

          <View style={styles.sports}>
            {sports.map(sport => (
              <Pressable key={sport} accessibilityRole="button" accessibilityLabel={`Tìm sân ${sportLabels[sport]}`} onPress={() => navigation.navigate('Search', { sport })} style={({ pressed }) => [styles.sportChip, pressed && styles.sportPressed]}>
                <View style={styles.sportIcon}><SportIcon sport={sport} size={22} color={colors.green} /></View>
                <Text style={styles.sportText}>{sportLabels[sport]}</Text>
              </Pressable>
            ))}
          </View>

          {!home && !error && <View style={styles.loading}><ActivityIndicator color={colors.green} /><Text style={styles.muted}>Đang tải sân gần bạn…</Text></View>}

          {error && (
            <View accessibilityRole="alert" style={styles.error}>
              <Ionicons name="cloud-offline-outline" size={22} color={colors.error} />
              <Text style={styles.errorText}>{error}</Text>
              <Pressable accessibilityRole="button" onPress={() => void load()} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
            </View>
          )}

          {home && <>
            <Section title="Trận sắp tới">
              {home.upcoming
                ? <UpcomingCard booking={home.upcoming} onPress={() => home.upcoming?.status === 'pending' ? navigation.navigate('BookingHold', { bookingId: home.upcoming.id }) : navigation.navigate('Bookings')} />
                : (
                  <Pressable accessibilityRole="button" onPress={() => navigation.navigate('Search')} style={styles.empty}>
                    <Ionicons name="calendar-clear-outline" size={22} color={colors.green} />
                    <View style={styles.flex}>
                      <Text style={styles.emptyTitle}>Chưa có trận nào sắp tới</Text>
                      <Text style={styles.muted}>Tìm một sân gần bạn và đặt giờ chơi.</Text>
                    </View>
                    <Ionicons name="chevron-forward" size={18} color={colors.muted} />
                  </Pressable>
                )}
            </Section>

            {home.featured.length > 0 && (
              <Section
                title="Sân nổi bật"
                subtitle={home.location.lat !== null ? `Sân VIP trong bán kính ${String(home.radiusKm).replace('.', ',')} km` : 'Sân VIP trên Sun Sport'}
              >
                {columns === 1
                  ? (
                    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.carousel}>
                      {home.featured.map(venue => <VenueCard key={venue.id} venue={venue} onPress={() => openVenue(venue)} style={styles.carouselCard} />)}
                    </ScrollView>
                  )
                  : (
                    <View style={styles.grid}>
                      {home.featured.map(venue => <View key={venue.id} style={[styles.gridCell, gridItem]}><VenueCard venue={venue} onPress={() => openVenue(venue)} /></View>)}
                    </View>
                  )}
              </Section>
            )}

            <Section
              title="Sân gần bạn"
              subtitle={home.location.lat !== null ? 'Sắp xếp theo khoảng cách' : 'Bật vị trí hiện tại để xem khoảng cách'}
              action={{ label: 'Xem tất cả', onPress: () => navigation.navigate('Search') }}
            >
              {home.nearby.length === 0
                ? <Text style={styles.muted}>Chưa có cụm sân nào được duyệt.</Text>
                : (
                  <View style={styles.grid}>
                    {home.nearby.map(venue => <View key={venue.id} style={[styles.gridCell, gridItem]}><VenueCard venue={venue} onPress={() => openVenue(venue)} /></View>)}
                  </View>
                )}
            </Section>
          </>}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

function LocationLine({ home, locating, onLocate }: { home: PlayerHome | null; locating: boolean; onLocate: () => void }) {
  if (!home) return <Text style={styles.location}> </Text>;
  if (home.location.lat === null) {
    return (
      <Pressable accessibilityRole="button" onPress={onLocate} disabled={locating} style={styles.locationRow}>
        {locating ? <ActivityIndicator size="small" color={colors.green} /> : <Ionicons name="navigate-outline" size={14} color={colors.green} />}
        <Text style={[styles.location, styles.locationAdd]}>Chưa có vị trí · Dùng vị trí hiện tại</Text>
      </Pressable>
    );
  }
  return (
    <View style={styles.locationRow}>
      <Ionicons name="location" size={14} color={colors.green} />
      <Text style={styles.location} numberOfLines={1}>
        {home.location.source === 'device' ? 'Theo vị trí hiện tại' : `Gần ${home.location.address ?? 'địa chỉ trong hồ sơ'}`}
      </Text>
    </View>
  );
}

function Section({ title, subtitle, action, children }: {
  title: string; subtitle?: string; action?: { label: string; onPress: () => void }; children: React.ReactNode;
}) {
  return (
    <View style={styles.section}>
      <View style={styles.sectionHead}>
        <View style={styles.flex}>
          <Text accessibilityRole="header" style={styles.sectionTitle}>{title}</Text>
          {!!subtitle && <Text style={styles.sectionSubtitle}>{subtitle}</Text>}
        </View>
        {action && <Pressable accessibilityRole="button" onPress={action.onPress} hitSlop={8}><Text style={styles.sectionAction}>{action.label}</Text></Pressable>}
      </View>
      {children}
    </View>
  );
}

function UpcomingCard({ booking, onPress }: { booking: UpcomingBooking; onPress: () => void }) {
  const pending = booking.status === 'pending';
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={({ pressed }) => [styles.upcoming, pressed && styles.askPressed]}>
      <View style={styles.dateBlock}>
        <Text style={styles.dateDay}>{formatDay(booking.startAt)}</Text>
        <Text style={styles.dateTime}>{formatTime(booking.startAt)}</Text>
        <Text style={styles.dateEnd}>đến {formatTime(booking.endAt)}</Text>
      </View>
      <View style={styles.flex}>
        <Text style={styles.upcomingVenue} numberOfLines={1}>{booking.venueName}</Text>
        <View style={styles.row}>
          <SportIcon sport={booking.sport} size={14} color={colors.muted} />
          <Text style={styles.muted} numberOfLines={1}>{booking.courtName} · {sportLabels[booking.sport]} · {environmentLabels[booking.environment]}</Text>
        </View>
        <View style={[styles.row, styles.upcomingFoot]}>
          <View style={[styles.status, pending && styles.statusPending]}>
            <Text style={[styles.statusText, pending && styles.statusPendingText]}>
              {pending
                ? `Chờ thanh toán${booking.holdExpiresAt ? ` · còn ${minutesUntil(booking.holdExpiresAt)} phút` : ''}`
                : 'Đã xác nhận'}
            </Text>
          </View>
          <Text style={styles.upcomingPrice}>{formatPrice(booking.price)}</Text>
        </View>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { paddingHorizontal: 20, paddingTop: 16, paddingBottom: 32 },
  scrollWide: { paddingHorizontal: 44, paddingTop: 36, paddingBottom: 48 },
  page: { width: '100%', maxWidth: 1180, alignSelf: 'center' },
  flex: { flex: 1, minWidth: 0 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  muted: { color: colors.muted, fontSize: 12, lineHeight: 18, flexShrink: 1 },

  header: { flexDirection: 'row', alignItems: 'center', gap: 16, marginBottom: 20 },
  headerText: { flex: 1, minWidth: 0 },
  greeting: { color: colors.ink, fontSize: 24, fontWeight: '800', letterSpacing: -0.6 },
  locationRow: { flexDirection: 'row', alignItems: 'center', gap: 5, marginTop: 6 },
  location: { color: colors.muted, fontSize: 13, flexShrink: 1 },
  locationAdd: { color: colors.green, fontWeight: '600' },
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.yellow, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.ink, fontSize: 17, fontWeight: '800' },

  ask: { backgroundColor: colors.green, borderRadius: 20, padding: 20, gap: 10 },
  askPressed: { opacity: 0.92 },
  askHead: { flexDirection: 'row', alignItems: 'center', gap: 7 },
  askKicker: { color: '#CFE0C6', fontSize: 10, fontWeight: '700', letterSpacing: 1.6 },
  askTitle: { color: colors.white, fontSize: 20, fontWeight: '700', letterSpacing: -0.4 },
  askBox: { flexDirection: 'row', alignItems: 'center', gap: 10, backgroundColor: colors.white, borderRadius: 13, paddingLeft: 15, paddingRight: 6, height: 50, marginTop: 4 },
  askPlaceholder: { flex: 1, color: '#9AA096', fontSize: 14 },
  askButton: { width: 38, height: 38, borderRadius: 10, backgroundColor: colors.yellow, alignItems: 'center', justifyContent: 'center' },
  askNote: { color: '#CFE0C6', fontSize: 11, lineHeight: 17 },

  sports: { flexDirection: 'row', gap: 10, marginTop: 16, flexWrap: 'wrap' },
  sportChip: { flexGrow: 1, flexBasis: 140, flexDirection: 'row', alignItems: 'center', gap: 10, backgroundColor: colors.white, borderRadius: 14, borderWidth: 1, borderColor: '#E8EAE1', padding: 10 },
  sportPressed: { backgroundColor: colors.greenLight },
  sportIcon: { width: 36, height: 36, borderRadius: 10, backgroundColor: '#F0F3E9', alignItems: 'center', justifyContent: 'center' },
  sportText: { color: colors.ink, fontSize: 13, fontWeight: '600' },

  loading: { alignItems: 'center', gap: 10, paddingVertical: 48 },
  error: { alignItems: 'center', gap: 10, paddingVertical: 28, paddingHorizontal: 20, marginTop: 24, backgroundColor: colors.errorBackground, borderRadius: 16 },
  errorText: { color: colors.error, fontSize: 13, textAlign: 'center', lineHeight: 20 },
  retry: { backgroundColor: colors.white, borderRadius: 10, paddingHorizontal: 18, paddingVertical: 10, borderWidth: 1, borderColor: '#EBC9C3' },
  retryText: { color: colors.error, fontWeight: '600', fontSize: 13 },

  section: { marginTop: 30 },
  sectionHead: { flexDirection: 'row', alignItems: 'flex-end', gap: 12, marginBottom: 14 },
  sectionTitle: { color: colors.ink, fontSize: 18, fontWeight: '800', letterSpacing: -0.3 },
  sectionSubtitle: { color: colors.muted, fontSize: 12, marginTop: 3 },
  sectionAction: { color: colors.green, fontSize: 13, fontWeight: '700' },

  empty: { flexDirection: 'row', alignItems: 'center', gap: 14, backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', borderStyle: 'dashed', padding: 16 },
  emptyTitle: { color: colors.ink, fontSize: 14, fontWeight: '600', marginBottom: 2 },

  upcoming: { flexDirection: 'row', gap: 16, backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 14, alignItems: 'center' },
  dateBlock: { width: 86, alignItems: 'center', backgroundColor: colors.greenLight, borderRadius: 12, paddingVertical: 10 },
  dateDay: { color: colors.green, fontSize: 11, fontWeight: '700' },
  dateTime: { color: colors.ink, fontSize: 22, fontWeight: '800', marginTop: 2 },
  dateEnd: { color: colors.muted, fontSize: 10 },
  upcomingVenue: { color: colors.ink, fontSize: 15, fontWeight: '700', marginBottom: 4 },
  upcomingFoot: { justifyContent: 'space-between', marginTop: 10 },
  status: { backgroundColor: colors.greenLight, borderRadius: 6, paddingHorizontal: 8, paddingVertical: 4 },
  statusPending: { backgroundColor: '#FFF4D6' },
  statusText: { color: colors.green, fontSize: 11, fontWeight: '600' },
  statusPendingText: { color: '#7A5A10' },
  upcomingPrice: { color: colors.ink, fontSize: 14, fontWeight: '700' },

  carousel: { gap: 12, paddingRight: 20 },
  carouselCard: { width: 264 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', marginHorizontal: -7 },
  gridCell: { padding: 7 },
});
