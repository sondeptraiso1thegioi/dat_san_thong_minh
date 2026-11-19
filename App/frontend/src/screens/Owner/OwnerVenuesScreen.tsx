import React, { useCallback, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, useWindowDimensions, View, ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { CompositeScreenProps, useFocusEffect } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import SportIcon from '../../components/SportIcon';
import { colors } from '../../constants/theme';
import { sportLabels } from '../../constants/sports';
import { OwnerTabRoutes, RootRoutes } from '../../navigation/types';
import { ownerService } from '../../services/ownerService';
import { useAuthStore } from '../../store/useAuthStore';
import { OwnerVenueListItem, VenueStatus } from '../../types/owner';
import { givenName } from '../../utils/format';

type Props = CompositeScreenProps<BottomTabScreenProps<OwnerTabRoutes, 'Venues'>, NativeStackScreenProps<RootRoutes>>;

export const statusInfo: Record<VenueStatus, { label: string; icon: React.ComponentProps<typeof Ionicons>['name']; color: string; background: string; note: string }> = {
  pending: { label: 'Chờ duyệt', icon: 'time-outline', color: '#7A5A10', background: '#FFF4D6', note: 'Quản trị viên đang kiểm tra. Sân sẽ hiện với người chơi sau khi được duyệt.' },
  approved: { label: 'Đã duyệt', icon: 'checkmark-circle', color: colors.green, background: colors.greenLight, note: 'Đang hiện trong tìm kiếm và gợi ý của người chơi.' },
  rejected: { label: 'Bị từ chối', icon: 'close-circle', color: colors.error, background: colors.errorBackground, note: 'Sửa theo lý do bên dưới rồi gửi lại.' },
  deleted: { label: 'Đã ẩn', icon: 'eye-off-outline', color: colors.muted, background: '#EFF0EC', note: 'Quản trị viên đã ẩn cụm sân này.' },
};

export default function OwnerVenuesScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 960;
  const columns = width >= 1280 ? 2 : 1;
  const user = useAuthStore(state => state.session?.user);
  const [venues, setVenues] = useState<OwnerVenueListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try { setVenues(await ownerService.listVenues()); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa tải được danh sách cụm sân.'); }
  }, []);

  // Tải lại mỗi khi quay về màn này (ví dụ sau khi lưu ở màn sửa cụm sân).
  useFocusEffect(useCallback(() => { void load(); }, [load]));

  const counts = (venues ?? []).reduce<Record<VenueStatus, number>>((acc, v) => ({ ...acc, [v.status]: acc[v.status] + 1 }),
    { pending: 0, approved: 0, rejected: 0, deleted: 0 });
  const openEditor = (venueId?: string) => navigation.navigate('OwnerVenueEditor', venueId ? { venueId } : undefined);
  const cell: ViewStyle = { width: columns === 1 ? '100%' : '50%' };

  return (
    <SafeAreaView style={styles.safe} edges={wide ? [] : ['top']}>
      <ScrollView
        contentContainerStyle={[styles.scroll, wide && styles.scrollWide]}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={async () => { setRefreshing(true); await load(); setRefreshing(false); }} tintColor={colors.green} />}
      >
        <View style={styles.page}>
          <View style={styles.header}>
            <View style={styles.flex}>
              <Text style={styles.kicker}>CHỦ SÂN · {givenName(user?.name ?? '').toUpperCase()}</Text>
              <Text accessibilityRole="header" style={styles.title}>Cụm sân của tôi</Text>
            </View>
            <Pressable accessibilityRole="button" onPress={() => openEditor()} style={({ pressed }) => [styles.add, pressed && styles.pressed]}>
              <Ionicons name="add" size={20} color={colors.white} />
              <Text style={styles.addText}>Thêm cụm sân</Text>
            </Pressable>
          </View>

          {venues && venues.length > 0 && (
            <View style={styles.stats}>
              {(['approved', 'pending', 'rejected'] as const).map(status => (
                <View key={status} style={styles.stat}>
                  <Text style={[styles.statValue, { color: statusInfo[status].color }]}>{counts[status]}</Text>
                  <Text style={styles.statLabel}>{statusInfo[status].label}</Text>
                </View>
              ))}
            </View>
          )}

          {!venues && !error && <View style={styles.center}><ActivityIndicator color={colors.green} /></View>}
          {error && (
            <View accessibilityRole="alert" style={styles.error}>
              <Text style={styles.errorText}>{error}</Text>
              <Pressable accessibilityRole="button" onPress={() => void load()} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
            </View>
          )}

          {venues && venues.length === 0 && (
            <View style={styles.empty}>
              <View style={styles.emptyIcon}><Ionicons name="business-outline" size={30} color={colors.green} /></View>
              <Text style={styles.emptyTitle}>Bạn chưa có cụm sân nào</Text>
              <Text style={styles.emptyText}>Đăng ký cụm sân đầu tiên: nhập địa chỉ và ghim vị trí, thêm sân con, khai báo bảng giá. Sau khi quản trị viên duyệt, người chơi sẽ tìm thấy và đặt được sân của bạn.</Text>
              <Pressable accessibilityRole="button" onPress={() => openEditor()} style={[styles.add, styles.emptyButton]}>
                <Ionicons name="add" size={20} color={colors.white} /><Text style={styles.addText}>Đăng ký cụm sân</Text>
              </Pressable>
            </View>
          )}

          <View style={styles.grid}>
            {venues?.map(venue => {
              const info = statusInfo[venue.status];
              return (
                <View key={venue.id} style={[styles.cell, cell]}>
                  <Pressable accessibilityRole="button" accessibilityLabel={`${venue.name}, ${info.label}`} onPress={() => openEditor(venue.id)}
                    style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
                    <View style={styles.cardTop}>
                      <View style={[styles.status, { backgroundColor: info.background }]}>
                        <Ionicons name={info.icon} size={13} color={info.color} />
                        <Text style={[styles.statusText, { color: info.color }]}>{info.label}</Text>
                      </View>
                      {venue.isVip && <View style={styles.vip}><Ionicons name="star" size={10} color={colors.ink} /><Text style={styles.vipText}>VIP</Text></View>}
                      <View style={styles.flex} />
                      <Ionicons name="create-outline" size={18} color={colors.muted} />
                    </View>
                    <Text style={styles.name} numberOfLines={1}>{venue.name}</Text>
                    <View style={styles.row}><Ionicons name="location-outline" size={13} color={colors.muted} /><Text style={styles.muted} numberOfLines={1}>{venue.address}</Text></View>
                    <View style={styles.row}><Ionicons name="time-outline" size={13} color={colors.muted} /><Text style={styles.muted}>{venue.openTime}–{venue.closeTime} · {venue.activeCourts} sân con đang hoạt động</Text></View>
                    <View style={styles.sports}>
                      {venue.sports.map(sport => (
                        <View key={sport} style={styles.sport}><SportIcon sport={sport} size={13} color={colors.green} /><Text style={styles.sportText}>{sportLabels[sport]}</Text></View>
                      ))}
                    </View>
                    {(venue.status === 'rejected' || venue.status === 'deleted') && venue.rejectReason
                      ? <View style={styles.reason}><Text style={styles.reasonLabel}>{venue.status === 'deleted' ? 'Lý do ẩn' : 'Lý do từ chối'}</Text><Text style={styles.reasonText}>{venue.rejectReason}</Text></View>
                      : <Text style={styles.note}>{info.note}</Text>}
                  </Pressable>
                </View>
              );
            })}
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
  page: { width: '100%', maxWidth: 1180, alignSelf: 'center' },
  flex: { flex: 1, minWidth: 0 },
  pressed: { opacity: 0.85 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 14, flexWrap: 'wrap' },
  kicker: { color: '#829171', fontSize: 10, fontWeight: '700', letterSpacing: 1.6 },
  title: { color: colors.ink, fontSize: 24, fontWeight: '800', letterSpacing: -0.6, marginTop: 4 },
  add: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.green, borderRadius: 11, paddingHorizontal: 16, paddingVertical: 12 },
  addText: { color: colors.white, fontWeight: '700', fontSize: 14 },
  stats: { flexDirection: 'row', gap: 10, marginTop: 20 },
  stat: { flex: 1, backgroundColor: colors.white, borderRadius: 14, borderWidth: 1, borderColor: '#E8EAE1', padding: 14 },
  statValue: { fontSize: 22, fontWeight: '800' },
  statLabel: { color: colors.muted, fontSize: 12, marginTop: 2 },
  center: { paddingVertical: 48, alignItems: 'center' },
  error: { alignItems: 'center', gap: 10, padding: 20, borderRadius: 14, backgroundColor: colors.errorBackground, marginTop: 20 },
  errorText: { color: colors.error, textAlign: 'center' },
  retry: { backgroundColor: colors.white, borderRadius: 9, paddingHorizontal: 16, paddingVertical: 8, borderWidth: 1, borderColor: '#EBC9C3' },
  retryText: { color: colors.error, fontWeight: '600' },
  empty: { alignItems: 'center', marginTop: 28, padding: 28, borderRadius: 20, backgroundColor: colors.white, borderWidth: 1, borderColor: '#E8EAE1' },
  emptyIcon: { width: 64, height: 64, borderRadius: 18, backgroundColor: colors.greenLight, alignItems: 'center', justifyContent: 'center', marginBottom: 16 },
  emptyTitle: { color: colors.ink, fontSize: 18, fontWeight: '800' },
  emptyText: { color: colors.muted, fontSize: 13, lineHeight: 20, textAlign: 'center', marginTop: 8, maxWidth: 480 },
  emptyButton: { marginTop: 20 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', marginHorizontal: -7, marginTop: 14 },
  cell: { padding: 7 },
  card: { backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', padding: 16, gap: 7 },
  cardTop: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 2 },
  status: { flexDirection: 'row', alignItems: 'center', gap: 5, borderRadius: 7, paddingHorizontal: 8, paddingVertical: 4 },
  statusText: { fontSize: 11, fontWeight: '700' },
  vip: { flexDirection: 'row', alignItems: 'center', gap: 3, backgroundColor: colors.yellow, paddingHorizontal: 7, paddingVertical: 3, borderRadius: 6 },
  vipText: { color: colors.ink, fontSize: 10, fontWeight: '800' },
  name: { color: colors.ink, fontSize: 16, fontWeight: '700' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  muted: { color: colors.muted, fontSize: 12, flexShrink: 1 },
  sports: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 2 },
  sport: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: '#F2F5EC', borderRadius: 6, paddingHorizontal: 6, paddingVertical: 3 },
  sportText: { color: colors.green, fontSize: 10, fontWeight: '600' },
  note: { color: colors.muted, fontSize: 11, lineHeight: 17, marginTop: 4, paddingTop: 10, borderTopWidth: 1, borderColor: '#F0F1EA' },
  reason: { marginTop: 4, padding: 10, borderRadius: 9, backgroundColor: colors.errorBackground },
  reasonLabel: { color: colors.error, fontSize: 10, fontWeight: '800', letterSpacing: 0.6 },
  reasonText: { color: colors.error, fontSize: 13, marginTop: 3, lineHeight: 19 },
});
