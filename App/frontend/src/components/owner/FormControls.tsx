import React, { useState } from 'react';
import { Pressable, StyleProp, StyleSheet, Text, TextInput, TextInputProps, View, ViewStyle } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../../constants/theme';
import { webInputReset } from '../../constants/webStyles';

/** Ô nhập có viền xanh khi đang nhập (thay cho khung focus đen của trình duyệt). */
export function PlainInput({ style, onFocus, onBlur, invalid, ...props }: TextInputProps & { invalid?: boolean }) {
  const [focused, setFocused] = useState(false);
  return (
    <TextInput
      placeholderTextColor="#A0A59D"
      {...props}
      onFocus={event => { setFocused(true); onFocus?.(event); }}
      onBlur={event => { setFocused(false); onBlur?.(event); }}
      style={[styles.input, props.multiline && styles.multiline, focused && styles.inputFocused, invalid && styles.inputError, webInputReset, style]}
    />
  );
}

/** Trường nhập có nhãn, dùng trong form chủ sân. */
export function Field({ label, hint, error, ...props }: TextInputProps & { label: string; hint?: string; error?: string | null }) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <PlainInput {...props} accessibilityLabel={label} invalid={!!error} />
      {!!error && <Text style={styles.error}>{error}</Text>}
      {!error && !!hint && <Text style={styles.hint}>{hint}</Text>}
    </View>
  );
}

/** Nút − giá trị + ; dùng cho giờ mở cửa (bước 30 phút) và khung giờ bảng giá (bước 1 giờ). */
export function Stepper({ label, value, onDecrease, onIncrease, canDecrease, canIncrease, style }: {
  label?: string; value: string; onDecrease: () => void; onIncrease: () => void;
  canDecrease: boolean; canIncrease: boolean; style?: StyleProp<ViewStyle>;
}) {
  return (
    <View style={style}>
      {!!label && <Text style={styles.label}>{label}</Text>}
      <View style={styles.stepper}>
        <Pressable accessibilityRole="button" accessibilityLabel={`Giảm ${label ?? ''}`} disabled={!canDecrease} onPress={onDecrease} style={[styles.stepButton, !canDecrease && styles.disabled]} hitSlop={4}>
          <Ionicons name="remove" size={18} color={colors.ink} />
        </Pressable>
        <Text style={styles.stepValue}>{value}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel={`Tăng ${label ?? ''}`} disabled={!canIncrease} onPress={onIncrease} style={[styles.stepButton, !canIncrease && styles.disabled]} hitSlop={4}>
          <Ionicons name="add" size={18} color={colors.ink} />
        </Pressable>
      </View>
    </View>
  );
}

/** Ô nhập tiền: chỉ nhận chữ số, hiển thị có dấu chấm ngăn cách hàng nghìn. */
export function MoneyInput({ value, onChange, placeholder, accessibilityLabel, invalid }: {
  value: number | null; onChange: (value: number | null) => void; placeholder?: string; accessibilityLabel: string; invalid?: boolean;
}) {
  const text = value === null ? '' : String(value).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  const [focused, setFocused] = useState(false);
  return (
    <View style={[styles.money, focused && styles.inputFocused, invalid && styles.inputError]}>
      <TextInput
        value={text}
        onChangeText={raw => {
          const digits = raw.replace(/\D/g, '').slice(0, 8);
          onChange(digits ? Number(digits) : null);
        }}
        keyboardType="number-pad"
        placeholder={placeholder}
        placeholderTextColor="#A0A59D"
        accessibilityLabel={accessibilityLabel}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        style={[styles.moneyInput, webInputReset]}
      />
      <Text style={styles.moneySuffix}>đ/giờ</Text>
    </View>
  );
}

export function Chip({ label, active, disabled, onPress, icon }: {
  label: string; active: boolean; disabled?: boolean; onPress: () => void; icon?: React.ReactNode;
}) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected: active, disabled: !!disabled }} disabled={disabled} onPress={onPress}
      style={[styles.chip, active && styles.chipActive, disabled && styles.disabled]}>
      {icon}
      <Text style={[styles.chipText, active && styles.chipTextActive]}>{label}</Text>
    </Pressable>
  );
}

/** "06:00" ↔ số phút từ 0h; "24:00" = 1440. */
export const toMinutes = (time: string) => { const [h, m] = time.split(':').map(Number); return (h ?? 0) * 60 + (m ?? 0); };
export const fromMinutes = (minutes: number) => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;

const styles = StyleSheet.create({
  field: { marginBottom: 16 },
  label: { color: colors.ink, fontSize: 13, fontWeight: '600', marginBottom: 7 },
  input: { minHeight: 48, borderWidth: 1, borderColor: colors.line, borderRadius: 11, paddingHorizontal: 14, paddingVertical: 12, fontSize: 14, color: colors.ink, backgroundColor: '#FCFCF8' },
  multiline: { minHeight: 96, textAlignVertical: 'top' },
  inputFocused: { borderColor: colors.green, backgroundColor: colors.white },
  inputError: { borderColor: colors.error },
  error: { color: colors.error, fontSize: 12, marginTop: 6 },
  hint: { color: colors.muted, fontSize: 11, marginTop: 6, lineHeight: 16 },
  stepper: { flexDirection: 'row', alignItems: 'center', borderWidth: 1, borderColor: colors.line, borderRadius: 11, backgroundColor: '#FCFCF8', height: 46 },
  stepButton: { width: 42, height: '100%', alignItems: 'center', justifyContent: 'center' },
  stepValue: { flex: 1, textAlign: 'center', color: colors.ink, fontSize: 15, fontWeight: '700', minWidth: 52 },
  disabled: { opacity: 0.35 },
  money: { flexDirection: 'row', alignItems: 'center', borderWidth: 1, borderColor: colors.line, borderRadius: 11, backgroundColor: '#FCFCF8', height: 46, paddingHorizontal: 12 },
  moneyInput: { flex: 1, minWidth: 0, height: '100%', fontSize: 15, fontWeight: '700', color: colors.ink },
  moneySuffix: { color: colors.muted, fontSize: 12 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 5, paddingHorizontal: 11, paddingVertical: 8, borderRadius: 9, borderWidth: 1, borderColor: colors.line, backgroundColor: '#FCFCF8' },
  chipActive: { backgroundColor: colors.green, borderColor: colors.green },
  chipText: { color: colors.ink, fontSize: 13, fontWeight: '500' },
  chipTextActive: { color: colors.white, fontWeight: '600' },
});
