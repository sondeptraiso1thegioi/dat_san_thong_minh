import React, { useCallback, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { CompositeScreenProps, useFocusEffect } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import SportIcon from '../../components/SportIcon';
import { colors } from '../../constants/theme';
import { sportLabels } from '../../constants/sports';
import { AdminTabRoutes, RootRoutes } from '../../navigation/types';
import { adminService } from '../../services/adminService';
import { AdminVenueCounts, AdminVenueListItem } from '../../types/admin';
import { VenueStatus } from '../../types/owner';
import { formatRelative } from '../../utils/format';
import { statusInfo } from '../Owner/OwnerVenuesScreen';

type Props = CompositeScreenProps<BottomTabScreenProps<AdminTabRoutes, 'AdminVenues'>, NativeStackScreenProps<RootRoutes>>;

const filters: VenueStatus[] = ['pending', 'approved', 'rejected', 'deleted'];
const emptyNote: Record<VenueStatus, string> = {
  pending: 'Không có cụm sân nào đang chờ duyệt.',
  approved: 'Chưa có cụm sân nào được duyệt.',
  rejected: 'Không có cụm sân nào bị từ chối.',
  deleted: 'Không có cụm sân nào bị ẩn.',
};

export default function AdminVenuesScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 960;
  const [status, setStatus] = useState<VenueStatus>('pending');
  const [venues, setVenues] = useState<AdminVenueListItem[] | null>(null);
  const [counts, setCounts] = useState<AdminVenueCounts | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async (wanted: VenueStatus) => {
    setError(null);
    try {
      const result = await adminService.listVenues(wanted);
      setVenues(result.venues);
      setCounts(result.counts);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Chưa tải được danh sách cụm sân.');
    }
  }, []);

  // Tải lại khi quay về từ màn xem xét (trạng thái có thể vừa đổi).
  useFocusEffect(useCallback(() => { void load(status); }, [load, status]));

  function choose(next: VenueStatus) {
    setStatus(next);
    setVenues(null);
  }

  return (
    <SafeAreaView style={styles.safe} edges={wide ? [] : ['top']}>
      <ScrollView contentContainerStyle={[styles.scroll, wide && styles.scrollWide]}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={async () => { setRefreshing(true); await load(status); setRefreshing(false); }} tintColor={colors.green} />}>
        <View style={styles.page}>
          <Text style={styles.kicker}>QUẢN TRỊ VIÊN</Text>
          <Text accessibilityRole="header" style={styles.title}>Duyệt cụm sân</Text>
          <Text style={styles.subtitle}>Kiểm tra thông tin và vị trí trước khi cụm sân xuất hiện với người chơi.</Text>

          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.filters}>
            {filters.map(s => {
              const info = statusInfo[s];
              const active = s === status;
              return (
                <Pressable key={s} accessibilityRole="tab" accessibilityState={{ selected: active }} onPress={() => choose(s)}
                  style={[styles.filter, active && styles.filterActive]}>
                  <Ionicons name={info.icon} size={15} color={active ? colors.white : info.color} />
                  <Text style={[styles.filterText, active && styles.filterTextActive]}>{info.label}</Text>
                  {counts && (
                    <View style={[styles.count, active && styles.countActive, s === 'pending' && counts.pending > 0 && !active && styles.countAlert]}>
                      <Text style={[styles.countText, active && styles.countTextActive]}>{counts[s]}</Text>
                    </View>
                  )}
                </Pressable>
              );
            })}
          </ScrollView>

          {!venues && !error && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
          {error && (
            <View accessibilityRole="alert" style={styles.error}>
              <Text style={styles.errorText}>{error}</Text>
              <Pressable accessibilityRole="button" onPress={() => void load(status)} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
            </View>
          )}
          {venues && venues.length === 0 && (
            <View style={styles.empty}><Ionicons name="checkmark-done-outline" size={26} color={colors.green} /><Text style={styles.emptyText}>{emptyNote[status]}</Text></View>
          )}

          <View style={styles.list}>
            {venues?.map(venue => (
              <Pressable key={venue.id} accessibilityRole="button" accessibilityLabel={`Xem xét ${venue.name}`}
                onPress={() => navigation.navigate('AdminVenueReview', { venueId: venue.id })}
                style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
                <View style={styles.flex}>
                  <Text style={styles.name} numberOfLines={1}>{venue.name}</Text>
                  <View style={styles.row}><Ionicons name="location-outline" size={13} color={colors.muted} /><Text style={styles.muted} numberOfLines={1}>{venue.address}</Text></View>
                  <View style={styles.row}><Ionicons name="person-outline" size={13} color={colors.muted} /><Text style={styles.muted} numberOfLines={1}>{venue.ownerName} · {venue.ownerEmail}</Text></View>
                  <View style={styles.sports}>
                    {venue.sports.map(sport => (
                      <View key={sport} style={styles.sport}><SportIcon sport={sport} size={13} color={colors.green} /><Text style={styles.sportText}>{sportLabels[sport]}</Text></View>
                    ))}
                    <Text style={styles.small}>{venue.activeCourts} sân con · gửi {formatRelative(venue.createdAt)}</Text>
                  </View>
                  {!!venue.rejectReason && <Text style={styles.reason} numberOfLines={2}>Lý do: {venue.rejectReason}</Text>}
                </View>
                <View style={styles.open}>
                  <Text style={styles.openText}>{status === 'pending' ? 'Xem xét' : 'Chi tiết'}</Text>
                  <Ionicons name="chevron-forward" size={16} color={colors.green} />
                </View>
              </Pressable>
            ))}
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { padding: 20, paddingBottom: 32 },
  scrollWide: { paddingHorizontal: 44, paddingTop: 36 },
  page: { width: '100%', maxWidth: 980, alignSelf: 'center' },
  flex: { flex: 1, minWidth: 0, gap: 5 },
  pressed: { opacity: 0.85 },
  kicker: { color: '#829171', fontSize: 10, fontWeight: '700', letterSpacing: 1.6 },
  title: { color: colors.ink, fontSize: 24, fontWeight: '800', letterSpacing: -0.6, marginTop: 4 },
  subtitle: { color: colors.muted, fontSize: 13, marginTop: 6 },
  filters: { gap: 8, marginTop: 20, paddingBottom: 4 },
  filter: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 9, borderRadius: 11, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.white },
  filterActive: { backgroundColor: colors.green, borderColor: colors.green },
  filterText: { color: colors.ink, fontSize: 13, fontWeight: '600' },
  filterTextActive: { color: colors.white },
  count: { minWidth: 22, height: 20, borderRadius: 10, paddingHorizontal: 6, alignItems: 'center', justifyContent: 'center', backgroundColor: '#EFF0EC' },
  countActive: { backgroundColor: 'rgba(255,255,255,0.22)' },
  countAlert: { backgroundColor: colors.yellow },
  countText: { color: colors.ink, fontSize: 11, fontWeight: '800' },
  countTextActive: { color: colors.white },
  center: { paddingVertical: 48, alignItems: 'center' },
  error: { alignItems: 'center', gap: 10, padding: 20, borderRadius: 14, backgroundColor: colors.errorBackground, marginTop: 16 },
  errorText: { color: colors.error, textAlign: 'center' },
  retry: { backgroundColor: colors.white, borderRadius: 9, paddingHorizontal: 16, paddingVertical: 8, borderWidth: 1, borderColor: '#EBC9C3' },
  retryText: { color: colors.error, fontWeight: '600' },
  empty: { alignItems: 'center', gap: 8, padding: 32, marginTop: 16, borderRadius: 16, borderWidth: 1, borderStyle: 'dashed', borderColor: '#D5DCCF', backgroundColor: colors.white },
  emptyText: { color: colors.muted, fontSize: 13 },
  list: { gap: 10, marginTop: 14 },
  card: { flexDirection: 'row', alignItems: 'center', gap: 14, backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 16 },
  name: { color: colors.ink, fontSize: 16, fontWeight: '700' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  muted: { color: colors.muted, fontSize: 12, flexShrink: 1 },
  small: { color: colors.muted, fontSize: 11 },
  sports: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6, marginTop: 2 },
  sport: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: '#F2F5EC', borderRadius: 6, paddingHorizontal: 6, paddingVertical: 3 },
  sportText: { color: colors.green, fontSize: 10, fontWeight: '600' },
  reason: { color: colors.error, fontSize: 12, marginTop: 2 },
  open: { flexDirection: 'row', alignItems: 'center', gap: 2 },
  openText: { color: colors.green, fontSize: 13, fontWeight: '700' },
});
