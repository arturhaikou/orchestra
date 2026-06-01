/**
 * Dark Factory Theme Tokens
 * 
 * Centralized color system for the Orchestra UI.
 * These tokens map to CSS custom properties and Tailwind extended colors.
 * 
 * Categories:
 * - Backgrounds: Primary application backgrounds
 * - Surfaces: Cards, modals, containers
 * - Borders: Dividers and frame elements
 * - Text: Typography colors
 * - Semantic: Status, priority, accent colors
 * - Effects: Gradients and shadows
 */

// ============================================================================
// RGB Color Values (for CSS custom properties)
// ============================================================================
// Format: "r g b" (space-separated) for use with rgb(var(--variable))

export const colorTokensRGB = {
  // Core Backgrounds
  bgApp: '2 6 23',          // #020617 - Deepest slate (app background)
  bgSurface: '10 10 10',     // #0a0a0a - Floating surfaces
  bgSurfaceAlt: '15 23 42',  // #0f172a - Nested containers (base for 60% opacity)

  // Borders
  borderBase: '30 41 59',    // #1e293b - Subtle borders
  borderElevated: '51 65 85', // #334155 - Interactive borders

  // Text
  textPrimary: '255 255 255',    // #ffffff - Headings, active data
  textSecondary: '148 163 184',  // #94a3b8 - Body, descriptions
  textTertiary: '100 116 139',   // #64748b - Muted, inactive

  // Semantic Accent Colors
  purple: '168 85 247',      // #a855f7 - purple-500
  cyan: '34 211 238',        // #22d3ee - cyan-500
  emerald: '16 185 129',     // #10b981 - emerald-500
  yellow: '234 179 8',       // #eab308 - yellow-500
  red: '239 68 68',          // #ef4444 - red-500

  // Semantic Lighter Shades (for text)
  purpleLight: '216 180 254',     // #d8b4fe - purple-200
  cyanLight: '165 243 252',       // #a5f3fc - cyan-200
  emeraldLight: '167 243 208',    // #a7f3d0 - emerald-200
  yellowLight: '254 243 62',      // #fef33e - yellow-200
  redLight: '254 202 202',        // #fecaca - red-200

  // Semantic Medium Shades (for text on dark)
  purpleMed: '192 132 250',       // #c084fa - purple-400
  cyanMed: '34 211 238',          // #22d3ee - cyan-400
  yellowMed: '234 179 8',         // #eab308 - yellow-400
  emeraldMed: '52 211 153',       // #34d399 - emerald-400
};

// ============================================================================
// Hex Color Values (for inline styles, diagrams, etc.)
// ============================================================================

export const colorTokensHex = {
  bgApp: '#020617',
  bgSurface: '#0a0a0a',
  bgSurfaceAlt: '#0f172a',
  borderBase: '#1e293b',
  borderElevated: '#334155',
  textPrimary: '#ffffff',
  textSecondary: '#94a3b8',
  textTertiary: '#64748b',
  purple: '#a855f7',
  cyan: '#22d3ee',
  emerald: '#10b981',
  yellow: '#eab308',
  red: '#ef4444',
  
  // Extended UI Colors (for complex components like WorkflowBuilder, Dashboard)
  uiBg1: '#13141f',        // Slightly lighter surface for layered UI
  uiBg2: '#1e2030',        // Mid-tone surface for interactive elements
  uiBorder1: '#2d3148',    // Subtle dividers
  uiBorder2: '#3a3d5a',    // Elevated borders
  uiText1: '#c0c4d8',      // Slightly muted primary text
  uiText2: '#8b8fa8',      // Secondary muted text
  uiText3: '#5a5d7a',      // Tertiary muted text
  uiAccent: '#6366f1',     // Primary accent (indigo)
  uiAccentPurple: '#a78bfa', // Light purple accent
  
  // Complementary colors
  uiSuccess: '#10b981',    // Emerald for success
  uiWarning: '#fbbf24',    // Amber for warning
  uiError: '#f87171',      // Light red for error
};

// ============================================================================
// CSS Custom Property Names
// ============================================================================
// Map these to the Tailwind extended colors config in index.html

export const cssVariableNames = {
  // Backgrounds
  background: '--background',
  surface: '--surface',
  surfaceAlt: '--surface-alt',

  // Borders
  borderBase: '--border-base',
  borderElevated: '--border-elevated',

  // Text
  textPrimary: '--text-primary',
  textSecondary: '--text-secondary',
  textTertiary: '--text-tertiary',

  // Semantic Colors
  accentPurple: '--accent-purple',
  accentCyan: '--accent-cyan',
  accentYellow: '--accent-yellow',
  accentEmerald: '--accent-emerald',
  accentRed: '--accent-red',

  // Semantic Lighter
  accentPurpleLight: '--accent-purple-light',
  accentCyanLight: '--accent-cyan-light',
  accentYellowLight: '--accent-yellow-light',
  accentEmeraldLight: '--accent-emerald-light',
  accentRedLight: '--accent-red-light',
};

// ============================================================================
// Status & Priority Color Mapping
// ============================================================================
// Remapped to Dark Factory palette

export const statusColors = {
  OPEN: {
    bg: 'bg-cyan-500/20',
    text: 'text-cyan-400',
    border: 'border-cyan-500/50',
    hex: colorTokensHex.cyan,
  },
  IN_PROGRESS: {
    bg: 'bg-yellow-500/20',
    text: 'text-yellow-400',
    border: 'border-yellow-500/50',
    hex: colorTokensHex.yellow,
  },
  REVIEW: {
    bg: 'bg-purple-500/20',
    text: 'text-purple-400',
    border: 'border-purple-500/50',
    hex: colorTokensHex.purple,
  },
  DONE: {
    bg: 'bg-emerald-500/20',
    text: 'text-emerald-400',
    border: 'border-emerald-500/50',
    hex: colorTokensHex.emerald,
  },
  BLOCKED: {
    bg: 'bg-red-500/20',
    text: 'text-red-400',
    border: 'border-red-500/50',
    hex: colorTokensHex.red,
  },
};

export const priorityColors = {
  LOW: {
    bg: 'bg-slate-500/10',
    text: 'text-slate-400',
    border: 'border-slate-500/20',
    hex: '#64748b',
  },
  MEDIUM: {
    bg: 'bg-cyan-500/10',
    text: 'text-cyan-400',
    border: 'border-cyan-500/20',
    hex: colorTokensHex.cyan,
  },
  HIGH: {
    bg: 'bg-yellow-500/10',
    text: 'text-yellow-400',
    border: 'border-yellow-500/20',
    hex: colorTokensHex.yellow,
  },
  CRITICAL: {
    bg: 'bg-red-500/10',
    text: 'text-red-400',
    border: 'border-red-500/20',
    hex: colorTokensHex.red,
  },
};

// ============================================================================
// Special Effects
// ============================================================================

export const effects = {
  // Pipeline gradient
  pipelineGradient: 'bg-gradient-to-r from-purple-500 via-cyan-500 to-emerald-500',
  pipelineGradientVertical: 'bg-gradient-to-b from-purple-500 via-cyan-500 to-emerald-500',

  // Glow effects
  glowCyan: 'shadow-[0_0_15px_rgba(34,211,238,0.5)]',
  glowPurple: 'shadow-[0_0_15px_rgba(168,85,247,0.5)]',
  glowYellow: 'shadow-[0_0_15px_rgba(234,179,8,0.5)]',
  glowEmerald: 'shadow-[0_0_15px_rgba(16,185,129,0.5)]',

  // Metallic heading effect
  metallicHeading: 'text-transparent bg-clip-text bg-gradient-to-br from-white via-slate-200 to-slate-500',
};

// ============================================================================
// Animation Specs
// ============================================================================

export const animations = {
  standardTransition: 'duration-400 ease-in-out',
  pipelineFlow: 'ease-linear',
  processingPulse: 'animate-pulse', // Use Tailwind's built-in pulse
};

// ============================================================================
// Theme Interface
// ============================================================================
// Used by useThemeTokens hook

export interface ThemeTokens {
  // Backgrounds
  background: string;
  surface: string;
  surfaceAlt: string;

  // Borders
  borderBase: string;
  borderElevated: string;

  // Text
  textPrimary: string;
  textSecondary: string;
  textTertiary: string;

  // Semantic colors
  accentPurple: string;
  accentCyan: string;
  accentYellow: string;
  accentEmerald: string;
  accentRed: string;
}

// ============================================================================
// Export Summary
// ============================================================================
/**
 * Usage in components:
 * 
 * 1. Using Tailwind classes (preferred):
 *    <div className="bg-surface text-text border border-border">
 *    <span className="text-text-muted">Secondary text</span>
 *    </div>
 * 
 * 2. Using the hook:
 *    const tokens = useThemeTokens();
 *    <div style={{ background: tokens.background }} />
 * 
 * 3. Using status/priority colors:
 *    import { statusColors } from './tokens';
 *    <span className={`${statusColors.OPEN.bg} ${statusColors.OPEN.text}`}>
 *      Open
 *    </span>
 * 
 * 4. For inline styles (hex values):
 *    import { colorTokensHex } from './tokens';
 *    style={{ backgroundColor: colorTokensHex.bgSurface }}
 * 
 * 5. For effects:
 *    import { effects } from './tokens';
 *    <div className={effects.pipelineGradient}>...</div>
 */
