import React from 'react';
import { Image, Pressable, StyleProp, StyleSheet, Text, View, ViewStyle } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import Svg, { Line, Rect } from 'react-native-svg';
import SportIcon from './SportIcon';
import { colors } from '../constants/theme';
import { sportLabels, sportTints } from '../constants/sports';
import { formatDistance, formatPrice } from '../utils/format';
import { Sport, VenueCard as Venue } from '../types/player';

interface Props {
  venue: Venue;
  onPress: () => void;
  style?: StyleProp<ViewStyle>;
  /** Dòng nổi bật, ví dụ "Còn 2 sân trống · 270.000đ cho 2 giờ" khi tìm theo giờ chơi. */
  highlight?: string;
  selected?: boolean;
  /** Môn đang lọc: ảnh thay thế dùng màu/biểu tượng của môn này nếu cụm sân có. */
  preferredSport?: Sport;
}

export default function VenueCard({ venue, onPress, style, highlight, selected, preferredSport }: Props) {
  const mainSport = preferredSport && venue.sports.includes(preferredSport) ? preferredSport : venue.sports[0] ?? 'tennis';
  const tint = sportTints[mainSport];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${venue.name}${venue.isVip ? ', sân nổi bật' : ''}`}
      onPress={onPress}
      style={({ pressed }) => [styles.card, selected && styles.selected, pressed && styles.pressed, style]}
    >
      <View style={[styles.cover, { backgroundColor: tint.background }]}>
        {venue.photoUrl
          ? <Image source={{ uri: venue.photoUrl }} style={StyleSheet.absoluteFill} resizeMode="cover" accessibilityIgnoresInvertColors />
          : <CourtPattern color={tint.ink} sport={mainSport} />}
        <View style={styles.badges}>
          {venue.isVip
            ? <View style={styles.vip}><Ionicons name="star" size={10} color={colors.ink} /><Text style={styles.vipText}>VIP</Text></View>
            : <View />}
          <View style={[styles.open, !venue.isOpenNow && styles.closed]}>
            <View style={[styles.openDot, !venue.isOpenNow && styles.closedDot]} />
            <Text style={[styles.openText, !venue.isOpenNow && styles.closedText]}>{venue.isOpenNow ? 'Đang mở' : 'Đã đóng'}</Text>
          </View>
        </View>
      </View>

      <View style={styles.body}>
        <Text style={styles.name} numberOfLines={1}>{venue.name}</Text>
        <View style={styles.row}>
          <Ionicons name="location-outline" size={13} color={colors.muted} />
          <Text style={styles.address} numberOfLines={1}>
            {venue.distanceKm !== null ? `${formatDistance(venue.distanceKm)} · ` : ''}{venue.address}
          </Text>
        </View>
        <View style={styles.sports}>
          {venue.sports.map(sport => (
            <View key={sport} style={styles.sport}>
              <SportIcon sport={sport} size={13} color={colors.green} />
              <Text style={styles.sportText}>{sportLabels[sport]}</Text>
            </View>
          ))}
          {venue.hasShelter && (
            <View style={styles.sport}><Ionicons name="umbrella-outline" size={12} color={colors.green} /><Text style={styles.sportText}>Có mái</Text></View>
          )}
        </View>
        {!!highlight && (
          <View style={styles.highlight}><Ionicons name="checkmark-circle" size={14} color={colors.green} /><Text style={styles.highlightText}>{highlight}</Text></View>
        )}
        <View style={styles.footer}>
          <Text style={styles.price}>
            {venue.minPricePerHour !== null ? <>Từ <Text style={styles.priceStrong}>{formatPrice(venue.minPricePerHour)}</Text>/giờ</> : 'Chưa có bảng giá'}
          </Text>
          <Text style={styles.hours}>{venue.openTime}–{venue.closeTime}</Text>
        </View>
      </View>
    </Pressable>
  );
}

// Ảnh thay thế khi cụm sân chưa có ảnh: vạch sân cách điệu theo màu của môn chính.
function CourtPattern({ color, sport }: { color: string; sport: Venue['sports'][number] }) {
  return (
    <View style={StyleSheet.absoluteFill}>
      <Svg width="100%" height="100%" viewBox="0 0 300 130" preserveAspectRatio="xMidYMid slice">
        <Rect x={40} y={18} width={220} height={94} rx={3} stroke={color} strokeOpacity={0.28} strokeWidth={2} fill="none" />
        <Line x1={150} y1={18} x2={150} y2={112} stroke={color} strokeOpacity={0.28} strokeWidth={2} />
        <Line x1={40} y1={65} x2={260} y2={65} stroke={color} strokeOpacity={0.16} strokeWidth={2} />
        <Line x1={95} y1={18} x2={95} y2={112} stroke={color} strokeOpacity={0.16} strokeWidth={2} />
        <Line x1={205} y1={18} x2={205} y2={112} stroke={color} strokeOpacity={0.16} strokeWidth={2} />
      </Svg>
      <View style={styles.patternIcon}><View style={styles.patternDisc}><SportIcon sport={sport} size={26} color={color} /></View></View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: { backgroundColor: colors.white, borderRadius: 16, borderWidth: 1, borderColor: '#E8EAE1', overflow: 'hidden' },
  pressed: { opacity: 0.88 },
  selected: { borderColor: colors.green, borderWidth: 2 },
  highlight: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.greenLight, borderRadius: 8, paddingHorizontal: 9, paddingVertical: 7, marginTop: 2 },
  highlightText: { color: colors.green, fontSize: 12, fontWeight: '600', flexShrink: 1 },
  cover: { height: 118, justifyContent: 'flex-start' },
  patternIcon: { position: 'absolute', left: 0, right: 0, top: 0, bottom: 0, alignItems: 'center', justifyContent: 'center' },
  patternDisc: { width: 46, height: 46, borderRadius: 23, backgroundColor: 'rgba(255,255,255,0.9)', alignItems: 'center', justifyContent: 'center' },
  badges: { flexDirection: 'row', justifyContent: 'space-between', padding: 10 },
  vip: { flexDirection: 'row', alignItems: 'center', gap: 3, backgroundColor: colors.yellow, paddingHorizontal: 7, paddingVertical: 3, borderRadius: 6 },
  vipText: { color: colors.ink, fontSize: 10, fontWeight: '800', letterSpacing: 0.6 },
  open: { flexDirection: 'row', alignItems: 'center', gap: 5, backgroundColor: 'rgba(255,255,255,0.92)', paddingHorizontal: 7, paddingVertical: 3, borderRadius: 6 },
  closed: { backgroundColor: 'rgba(255,255,255,0.8)' },
  openDot: { width: 6, height: 6, borderRadius: 3, backgroundColor: '#3E8E5A' },
  closedDot: { backgroundColor: '#A0A59D' },
  openText: { color: colors.green, fontSize: 10, fontWeight: '600' },
  closedText: { color: colors.muted },
  body: { padding: 14, gap: 7 },
  name: { color: colors.ink, fontSize: 15, fontWeight: '700' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  address: { color: colors.muted, fontSize: 12, flex: 1 },
  sports: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 2 },
  sport: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: '#F2F5EC', borderRadius: 6, paddingHorizontal: 6, paddingVertical: 3 },
  sportText: { color: colors.green, fontSize: 10, fontWeight: '600' },
  footer: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 4, paddingTop: 10, borderTopWidth: 1, borderColor: '#F0F1EA' },
  price: { color: colors.muted, fontSize: 12 },
  priceStrong: { color: colors.ink, fontWeight: '700' },
  hours: { color: colors.muted, fontSize: 11 },
});
