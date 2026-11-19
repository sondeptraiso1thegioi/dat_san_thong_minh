import React from 'react';
import Svg, { Circle, Ellipse, G, Line, Path, Rect } from 'react-native-svg';

// Decorative vector illustration; does not imply live court availability.
export default function CourtIllustration() {
  return (
    <Svg width="100%" height="100%" viewBox="0 0 590 360" accessibilityElementsHidden>
      <Circle cx="480" cy="65" r="45" fill="#F7C84B" />
      <Circle cx="480" cy="65" r="59" fill="none" stroke="#E6E4D6" strokeWidth="1" strokeDasharray="3 7" />
      <Ellipse cx="290" cy="298" rx="244" ry="34" fill="#DDE3D6" />
      <G transform="translate(40 60) rotate(-9 245 125)">
        <Rect x="0" y="8" width="490" height="252" rx="12" fill="#1F4232" />
        <Rect width="490" height="252" rx="12" fill="#47704B" />
        <Rect x="38" y="27" width="414" height="198" fill="#72916C" stroke="#F3F1D8" strokeWidth="2.5" />
        <Rect x="70" y="27" width="350" height="198" fill="none" stroke="#F3F1D8" strokeWidth="2" />
        <Rect x="70" y="66" width="350" height="120" fill="none" stroke="#F3F1D8" strokeWidth="2" />
        <Line x1="70" y1="126" x2="420" y2="126" stroke="#F3F1D8" strokeWidth="2" />
        <Line x1="245" y1="19" x2="245" y2="234" stroke="#203C2D" strokeWidth="7" />
        <Line x1="245" y1="20" x2="245" y2="233" stroke="#F6F3DF" strokeWidth="2.5" strokeDasharray="3 3" />
        <Circle cx="244" cy="16" r="5" fill="#F7C84B" />
        <Circle cx="244" cy="236" r="5" fill="#F7C84B" />
        <Path d="M113 182 Q145 108 191 111" fill="none" stroke="#D8DFB6" strokeWidth="2" strokeDasharray="5 6" />
        <Circle cx="191" cy="111" r="8" fill="#F7DB68" />
        <Path d="M185 107 Q191 115 197 115" fill="none" stroke="#FFFFE1" strokeWidth="1.5" />
      </G>
      <G transform="translate(438 270) rotate(28)">
        <Ellipse cx="0" cy="0" rx="23" ry="30" fill="#E8E4C7" stroke="#203C2D" strokeWidth="6" />
        <Path d="M-15 -18 L15 18 M-19 -8 L13 24 M-8 -25 L18 9 M15 -18 L-15 18 M19 -8 L-13 24 M8 -25 L-18 9" stroke="#89947C" strokeWidth="1" />
        <Line x1="0" y1="32" x2="0" y2="64" stroke="#203C2D" strokeWidth="8" />
        <Line x1="0" y1="48" x2="0" y2="66" stroke="#E5AB45" strokeWidth="10" />
      </G>
      <Circle cx="70" cy="303" r="17" fill="#F7C84B" />
      <Path d="M60 290 Q66 304 83 307 M55 307 Q68 305 76 318" fill="none" stroke="#FFFFD5" strokeWidth="2" />
    </Svg>
  );
}
