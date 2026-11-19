import React, { useEffect, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Switch, Text, useWindowDimensions, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../constants/theme';
import { amenityLabels } from '../constants/sports';
import { currentVnHour, formatHour, upcomingDays } from '../utils/format';

export interface SearchFilters {
  /** null = bán kính mặc định của hệ thống (ai_settings.default_radius_km). */
  radiusKm: number | null;
  sheltered: boolean;
  amenities: string[];
  /** yyyy-MM-dd theo giờ Việt Nam; đi cùng startHour. */
  date?: string;
  startHour?: number;
  hours: number;
}

export const emptyFilters: SearchFilters = { radiusKm: null, sheltered: false, amenities: [], hours: 1 };

/** Số bộ lọc đang bật (không tính môn và ô tìm kiếm), để hiện trên nút "Bộ lọc". */
export function countFilters(f: SearchFilters): number {
  return (f.radiusKm !== null ? 1 : 0) + (f.sheltered ? 1 : 0) + f.amenities.length + (f.startHour !== undefined ? 1 : 0);
}

const radiusOptions = [1, 2, 5, 10, 20];
const hourOptions = Array.from({ length: 19 }, (_, i) => i + 5); // 05:00–23:00
// Tối đa 3 giờ, khớp với giới hạn khi đặt sân.
const durationOptions = [1, 2, 3];

interface Props {
  visible: boolean;
  value: SearchFilters;
  defaultRadiusKm: number;
  hasLocation: boolean;
  onClose: () => void;
  onApply: (filters: SearchFilters) => void;
}

export default function SearchFilterSheet({ visible, value, defaultRadiusKm, hasLocation, onClose, onApply }: Props) {
  const { width } = useWindowDimensions();
  const wide = width >= 720;
  const [draft, setDraft] = useState(value);
  useEffect(() => { if (visible) setDraft(value); }, [visible, value]);

  const days = upcomingDays(7);
  const nowHour = currentVnHour();
  const isToday = draft.date === days[0]?.iso;
  const radius = draft.radiusKm ?? defaultRadiusKm;

  function chooseDay(iso: string | undefined) {
    setDraft(d => {
      if (!iso) return { ...d, date: undefined, startHour: undefined };
      // Đổi sang hôm nay mà giờ đã chọn đã qua thì bỏ giờ.
      const pastToday = iso === days[0]?.iso && d.startHour !== undefined && d.startHour <= nowHour;
      return { ...d, date: iso, startHour: pastToday ? undefined : d.startHour };
    });
  }

  function chooseHour(hour: number) {
    setDraft(d => {
      // Chọn giờ khi chưa chọn ngày: lấy hôm nay nếu giờ đó chưa qua, không thì ngày mai.
      const date = d.date ?? (hour > nowHour ? days[0]?.iso : days[1]?.iso);
      const hours = Math.min(d.hours, 24 - hour);
      return d.startHour === hour ? { ...d, startHour: undefined } : { ...d, date, startHour: hour, hours };
    });
  }

  const toggleAmenity = (code: string) => setDraft(d => ({
    ...d, amenities: d.amenities.includes(code) ? d.amenities.filter(a => a !== code) : [...d.amenities, code],
  }));

  return (
    <Modal visible={visible} transparent animationType={wide ? 'fade' : 'slide'} onRequestClose={onClose}>
      <View style={[styles.backdrop, wide && styles.backdropWide]}>
        <Pressable accessibilityLabel="Đóng bộ lọc" style={StyleSheet.absoluteFill} onPress={onClose} />
        <View style={[styles.sheet, wide && styles.sheetWide]}>
          <View style={styles.head}>
            <Text accessibilityRole="header" style={styles.title}>Bộ lọc</Text>
            <Pressable accessibilityRole="button" accessibilityLabel="Đóng" onPress={onClose} hitSlop={10}><Ionicons name="close" size={22} color={colors.ink} /></Pressable>
          </View>

          <ScrollView contentContainerStyle={styles.body}>
            <Text style={styles.label}>Khoảng cách tối đa</Text>
            {!hasLocation && <Text style={styles.note}>Cần vị trí (GPS hoặc địa chỉ trong hồ sơ) để lọc theo khoảng cách.</Text>}
            <View style={styles.chips}>
              {radiusOptions.map(km => (
                <Chip key={km} label={`${km} km${km === defaultRadiusKm ? ' (mặc định)' : ''}`} active={hasLocation && radius === km} disabled={!hasLocation}
                  onPress={() => setDraft(d => ({ ...d, radiusKm: km === defaultRadiusKm ? null : km }))} />
              ))}
            </View>

            <Text style={styles.label}>Ngày chơi</Text>
            <View style={styles.chips}>
              <Chip label="Bất kỳ" active={!draft.date} onPress={() => chooseDay(undefined)} />
              {days.map(day => <Chip key={day.iso} label={`${day.label} · ${day.sub}`} active={draft.date === day.iso} onPress={() => chooseDay(day.iso)} />)}
            </View>

            <Text style={styles.label}>Giờ bắt đầu</Text>
            <Text style={styles.note}>Chọn giờ để chỉ hiện cụm sân còn sân trống và xem giá cho khung giờ đó.</Text>
            <View style={styles.chips}>
              {hourOptions.map(hour => (
                <Chip key={hour} label={formatHour(hour)} compact active={draft.startHour === hour}
                  disabled={isToday && hour <= nowHour} onPress={() => chooseHour(hour)} />
              ))}
            </View>

            <Text style={styles.label}>Thời lượng</Text>
            <View style={styles.chips}>
              {durationOptions.map(h => (
                <Chip key={h} label={`${h} giờ`} active={draft.hours === h}
                  disabled={draft.startHour !== undefined && draft.startHour + h > 24}
                  onPress={() => setDraft(d => ({ ...d, hours: h }))} />
              ))}
            </View>

            <View style={styles.switchRow}>
              <View style={styles.flex}>
                <Text style={styles.label}>Chỉ sân có mái che</Text>
                <Text style={styles.note}>Sân trong nhà hoặc có mái, chơi được khi mưa nắng.</Text>
              </View>
              <Switch value={draft.sheltered} onValueChange={sheltered => setDraft(d => ({ ...d, sheltered }))}
                trackColor={{ true: colors.green, false: '#D7DBD1' }} thumbColor={colors.white} />
            </View>

            <Text style={styles.label}>Tiện ích</Text>
            <View style={styles.chips}>
              {Object.entries(amenityLabels).map(([code, label]) => (
                <Chip key={code} label={label} active={draft.amenities.includes(code)} onPress={() => toggleAmenity(code)} />
              ))}
            </View>
          </ScrollView>

          <View style={styles.foot}>
            <Pressable accessibilityRole="button" onPress={() => setDraft(emptyFilters)} style={styles.reset}><Text style={styles.resetText}>Đặt lại</Text></Pressable>
            <Pressable accessibilityRole="button" onPress={() => onApply(draft)} style={styles.apply}><Text style={styles.applyText}>Xem kết quả</Text></Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

function Chip({ label, active, disabled, compact, onPress }: { label: string; active: boolean; disabled?: boolean; compact?: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: active, disabled: !!disabled }}
      disabled={disabled}
      onPress={onPress}
      style={[styles.chip, compact && styles.chipCompact, active && styles.chipActive, disabled && styles.chipDisabled]}
    >
      <Text style={[styles.chipText, active && styles.chipTextActive, disabled && styles.chipTextDisabled]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, backgroundColor: 'rgba(20, 32, 24, 0.38)', justifyContent: 'flex-end' },
  backdropWide: { justifyContent: 'center', alignItems: 'center', padding: 24 },
  sheet: { backgroundColor: colors.white, borderTopLeftRadius: 22, borderTopRightRadius: 22, maxHeight: '88%' },
  sheetWide: { borderRadius: 22, width: '100%', maxWidth: 560, maxHeight: '90%' },
  head: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 22, paddingTop: 20, paddingBottom: 12, borderBottomWidth: 1, borderColor: colors.line },
  title: { color: colors.ink, fontSize: 18, fontWeight: '800' },
  body: { paddingHorizontal: 22, paddingBottom: 20 },
  flex: { flex: 1 },
  label: { color: colors.ink, fontSize: 14, fontWeight: '700', marginTop: 20, marginBottom: 4 },
  note: { color: colors.muted, fontSize: 12, lineHeight: 18, marginBottom: 6 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 6 },
  chip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 9, borderWidth: 1, borderColor: colors.line, backgroundColor: '#FCFCF8' },
  chipCompact: { paddingHorizontal: 9 },
  chipActive: { backgroundColor: colors.green, borderColor: colors.green },
  chipDisabled: { opacity: 0.4 },
  chipText: { color: colors.ink, fontSize: 13, fontWeight: '500' },
  chipTextActive: { color: colors.white, fontWeight: '600' },
  chipTextDisabled: { color: colors.muted },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  foot: { flexDirection: 'row', gap: 12, padding: 16, borderTopWidth: 1, borderColor: colors.line },
  reset: { paddingHorizontal: 20, justifyContent: 'center', borderRadius: 11, borderWidth: 1, borderColor: colors.line },
  resetText: { color: colors.ink, fontWeight: '600' },
  apply: { flex: 1, backgroundColor: colors.green, borderRadius: 11, paddingVertical: 14, alignItems: 'center' },
  applyText: { color: colors.white, fontWeight: '700', fontSize: 14 },
});
