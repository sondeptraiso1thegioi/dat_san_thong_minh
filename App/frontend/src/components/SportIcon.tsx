import React from 'react';
import Svg, { Circle, Line, Path, Rect } from 'react-native-svg';
import { Sport } from '../types/player';

interface Props { sport: Sport; size?: number; color: string }

// Ionicons không có biểu tượng cho pickleball, cầu lông, bóng bàn nên vẽ bằng SVG đơn giản.
export default function SportIcon({ sport, size = 22, color }: Props) {
  const stroke = { stroke: color, strokeWidth: 1.7, fill: 'none', strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24">
      {sport === 'tennis' && <>
        <Circle cx={12} cy={12} r={8.5} {...stroke} />
        <Path d="M5.2 7.2c3.1 1.6 3.1 8 0 9.6M18.8 7.2c-3.1 1.6-3.1 8 0 9.6" {...stroke} />
      </>}
      {sport === 'pickleball' && <>
        <Rect x={4} y={3} width={10} height={12} rx={4.5} {...stroke} />
        <Path d="M9 15v5.5" {...stroke} strokeWidth={2.6} />
        <Circle cx={18} cy={17.5} r={3} {...stroke} />
        <Circle cx={17.2} cy={16.8} r={0.5} fill={color} />
        <Circle cx={18.9} cy={18.2} r={0.5} fill={color} />
      </>}
      {sport === 'badminton' && <>
        <Path d="M9 15.5 5 4.5h14l-4 11" {...stroke} />
        <Line x1={12} y1={4.5} x2={12} y2={15.5} {...stroke} />
        <Line x1={7.2} y1={9.5} x2={16.8} y2={9.5} {...stroke} />
        <Path d="M8.5 15.5h7a3.5 3.5 0 0 1-7 0Z" {...stroke} />
      </>}
      {sport === 'table_tennis' && <>
        <Circle cx={10} cy={10} r={6.5} {...stroke} />
        <Path d="m14.6 14.6 4.4 4.4" {...stroke} strokeWidth={2.8} />
        <Circle cx={18.5} cy={6} r={2.2} {...stroke} />
      </>}
    </Svg>
  );
}
