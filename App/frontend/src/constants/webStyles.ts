import { Platform, TextStyle } from 'react-native';

/**
 * Web: trình duyệt tự vẽ khung focus đen quanh ô nhập (outline-style: auto bỏ qua outline-width: 0).
 * Các ô nhập trong app tự đổi viền xanh khi focus nên tắt khung này. Kiểu của React Native không có
 * giá trị 'none' nên phải ép kiểu; trên điện thoại style này bị bỏ qua.
 */
export const webInputReset = Platform.OS === 'web' ? ({ outlineStyle: 'none' } as unknown as TextStyle) : undefined;
