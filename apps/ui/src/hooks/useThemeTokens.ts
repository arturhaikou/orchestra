import { useMemo } from 'react';
import { ThemeTokens, colorTokensRGB } from '../tokens';

/**
 * useThemeTokens Hook
 * 
 * Provides access to Dark Factory theme tokens in React components.
 * Returns token values as CSS custom property values (space-separated RGB).
 * 
 * Usage:
 *   const tokens = useThemeTokens();
 *   <div style={{ background: `rgb(${tokens.background})` }}>
 */
export const useThemeTokens = (): ThemeTokens => {
  return useMemo(
    () => ({
      // Backgrounds
      background: `rgb(${colorTokensRGB.bgApp})`,
      surface: `rgb(${colorTokensRGB.bgSurface})`,
      surfaceAlt: `rgb(${colorTokensRGB.bgSurfaceAlt})`,

      // Borders
      borderBase: `rgb(${colorTokensRGB.borderBase})`,
      borderElevated: `rgb(${colorTokensRGB.borderElevated})`,

      // Text
      textPrimary: `rgb(${colorTokensRGB.textPrimary})`,
      textSecondary: `rgb(${colorTokensRGB.textSecondary})`,
      textTertiary: `rgb(${colorTokensRGB.textTertiary})`,

      // Semantic colors
      accentPurple: `rgb(${colorTokensRGB.purple})`,
      accentCyan: `rgb(${colorTokensRGB.cyan})`,
      accentYellow: `rgb(${colorTokensRGB.yellow})`,
      accentEmerald: `rgb(${colorTokensRGB.emerald})`,
      accentRed: `rgb(${colorTokensRGB.red})`,
    }),
    [],
  );
};

/**
 * Alternative hook for accessing RGB values directly (for opacity modifiers)
 * 
 * Usage:
 *   const rgbTokens = useThemeTokensRGB();
 *   <div style={{ background: `rgb(${rgbTokens.bgApp} / 0.5)` }}>
 */
export const useThemeTokensRGB = () => {
  return useMemo(() => colorTokensRGB, []);
};
