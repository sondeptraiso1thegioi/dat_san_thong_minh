import React, { forwardRef, useState } from 'react';
import { View, Text, TextInput, Pressable, StyleSheet, type TextInputProps, type TextInputInstance } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../constants/theme';
import { webInputReset } from '../constants/webStyles';

interface Props extends TextInputProps {
  label: string;
  icon: React.ComponentProps<typeof Ionicons>['name'];
  error?: string;
  password?: boolean;
}

const AuthField = forwardRef<TextInputInstance, Props>(function AuthField(
  { label, icon, error, password, ...props }, ref,
) {
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);
  return (
    <View style={styles.group}>
      <Text style={styles.label}>{label}</Text>
      <View style={[styles.field, focused && styles.focused, !!error && styles.invalid]}>
        <Ionicons name={icon} size={19} color={focused ? colors.green : colors.muted} />
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          {...props}
          ref={ref}
          accessibilityLabel={label}
          placeholderTextColor="#A0A59D"
          secureTextEntry={password && !visible}
          style={[styles.input, webInputReset]}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
        />
        {password && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
            onPress={() => setVisible(value => !value)}
            hitSlop={8}
            style={styles.eye}
          ><Ionicons name={visible ? 'eye-off-outline' : 'eye-outline'} size={21} color={colors.muted} /></Pressable>
        )}
      </View>
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
    </View>
  );
});

export default AuthField;

const styles = StyleSheet.create({
  group: { marginBottom: 21 },
  label: { color: colors.ink, fontWeight: '600', fontSize: 13, marginBottom: 9 },
  field: { height: 54, borderWidth: 1, borderColor: colors.line, borderRadius: 11, paddingHorizontal: 15, flexDirection: 'row', alignItems: 'center', backgroundColor: '#FCFCF8', gap: 11 },
  focused: { borderColor: colors.green, backgroundColor: colors.white },
  invalid: { borderColor: colors.error },
  input: { flex: 1, minWidth: 0, height: '100%', fontSize: 14, color: colors.ink },
  eye: { padding: 4 },
  error: { fontSize: 12, color: colors.error, marginTop: 7 },
});
