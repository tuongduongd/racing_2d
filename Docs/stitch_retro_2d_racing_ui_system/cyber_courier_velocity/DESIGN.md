---
name: Cyber Courier Velocity
colors:
  surface: '#10141a'
  surface-dim: '#10141a'
  surface-bright: '#353940'
  surface-container-lowest: '#0a0e14'
  surface-container-low: '#181c22'
  surface-container: '#1c2026'
  surface-container-high: '#262a31'
  surface-container-highest: '#31353c'
  on-surface: '#dfe2eb'
  on-surface-variant: '#b9cacb'
  inverse-surface: '#dfe2eb'
  inverse-on-surface: '#2d3137'
  outline: '#849495'
  outline-variant: '#3b494b'
  surface-tint: '#00dbe9'
  primary: '#dbfcff'
  on-primary: '#00363a'
  primary-container: '#00f0ff'
  on-primary-container: '#006970'
  inverse-primary: '#006970'
  secondary: '#ffd795'
  on-secondary: '#422c00'
  secondary-container: '#fbb400'
  on-secondary-container: '#694900'
  tertiary: '#fff3f1'
  on-tertiary: '#640d00'
  tertiary-container: '#ffcec4'
  on-tertiary-container: '#b62506'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#7df4ff'
  primary-fixed-dim: '#00dbe9'
  on-primary-fixed: '#002022'
  on-primary-fixed-variant: '#004f54'
  secondary-fixed: '#ffdea9'
  secondary-fixed-dim: '#ffba27'
  on-secondary-fixed: '#271900'
  on-secondary-fixed-variant: '#5e4100'
  tertiary-fixed: '#ffdad3'
  tertiary-fixed-dim: '#ffb4a4'
  on-tertiary-fixed: '#3e0500'
  on-tertiary-fixed-variant: '#8d1600'
  background: '#10141a'
  on-background: '#dfe2eb'
  surface-variant: '#31353c'
typography:
  display-hero:
    fontFamily: Anton
    fontSize: 64px
    fontWeight: '400'
    lineHeight: 68px
    letterSpacing: 0.04em
  display-hero-mobile:
    fontFamily: Anton
    fontSize: 40px
    fontWeight: '400'
    lineHeight: 44px
    letterSpacing: 0.03em
  headline-lg:
    fontFamily: Anton
    fontSize: 36px
    fontWeight: '400'
    lineHeight: 40px
    letterSpacing: 0.03em
  headline-lg-mobile:
    fontFamily: Anton
    fontSize: 28px
    fontWeight: '400'
    lineHeight: 32px
    letterSpacing: 0.02em
  headline-md:
    fontFamily: Anton
    fontSize: 24px
    fontWeight: '400'
    lineHeight: 28px
    letterSpacing: 0.02em
  title-lg:
    fontFamily: Chivo
    fontSize: 20px
    fontWeight: '800'
    lineHeight: 26px
    letterSpacing: 0.01em
  title-md:
    fontFamily: Chivo
    fontSize: 16px
    fontWeight: '700'
    lineHeight: 22px
    letterSpacing: 0.01em
  body-lg:
    fontFamily: Chivo
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Chivo
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-lg:
    fontFamily: Space Mono
    fontSize: 13px
    fontWeight: '700'
    lineHeight: 16px
    letterSpacing: 0.08em
  label-md:
    fontFamily: Space Mono
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.06em
  label-sm:
    fontFamily: Space Mono
    fontSize: 9px
    fontWeight: '700'
    lineHeight: 12px
    letterSpacing: 0.05em
spacing:
  gutter: 1rem
  gutter-desktop: 1.5rem
  margin: 1rem
  margin-desktop: 2.5rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2.5rem
---

## Brand & Style

This design system channels the gritty kinetic urgency of 90s arcade combat racers (such as Road Rash) collided with the relentless, neon-drenched reality of modern Vietnamese street courier culture. The aesthetic bridges Saigon-at-midnight street hustle—decked out in electric dispatch windbreakers, roaring modified mopeds, and LED delivery boxes—with raw cyber-arcade retro-futurism.

### Aesthetic Pillars
- **Neo-Cholon Arcade**: Heavy, raw, angled UI framing juxtaposed with ultra-vibrant safety neon signage and CRT-scanline atmospheric touches.
- **Courier Utility & Street Mod**: Visual styling reflects industrial vinyl wraps, tactical delivery harnesses, reflective high-visibility hazard tapes, and metallic kickplates.
- **High-Velocity Kinetic**: Interfaces convey constant forward motion, split-second reflexes, and competitive tension via slanted chamfers, hard-surface panels, and neon chromatic glare.

## Colors

The palette operates in permanent midnight dark mode, mimicking rain-slicked asphalt under floodlights. Contrast is razor-sharp; functional elements glow like high-output halogen headlights and dispatch HUDs.

### Core Palette Roles
- **Base Surfaces (Asphalt Black)**: `#0d1117` serves as the subterranean canvas. Secondary surface container `#161b22` and elevated chassis panels `#21262d` form mechanical framing.
- **Electric Cyan (`#00f0ff`)**: The primary interactive current. Used for active states, turbo indicators, digital instrumentation, and critical target readouts.
- **Safety Neon Amber (`#ffb703`) & Street Flare (`#fb8500`)**: The secondary hazard pair. Signals warnings, countdown timers, currency multipliers, and delivery priorities.
- **Fleet Faction Accents**:
  - **Electric Cyan-Green (`#00d285`)**: High-voltage fleet tech and stamina gauges.
  - **Shopee Flare (`#ee4d2d`)**: Turbo boost meters, combat impacts, and delivery streak alerts.
  - **Grab Emerald (`#00b14f`)**: Nitro tanks, health pickups, and clean checkpoint milestones.
  - **Ahamove Ignition (`#ff7a00`)**: Urgent side-hustle bounties and rival target tags.

## Typography

The typographic hierarchy relies on three distinct voices:
1. **Anton (Display/Headlines)**: Dense, impactful, tall vintage-arcade display lettering. All major headers and milestone screens render in ALL-CAPS with slight positive letter spacing, evoking vintage arcade coin-op marquee signs and decal slap-tags.
2. **Chivo (Body/Titles)**: Crisp, high-velocity grotesque sans with modern edge geometry. Delivers ultra-readable mission briefs, delivery contracts, and dialogue lines under high-speed play.
3. **Space Mono (HUD Data/Telemetry)**: Monospaced machine telemetry for speedometers, distance to drop-off, cash balances, and gear ratios.

## Layout & Spacing

The layout operates on a fluid, HUD-anchored grid structure optimized for high scanability during gameplay and tactical garage menu navigation.

### Structural Mechanics
- **Safe Boundary Margin**: A strict outer frame prevents peripheral HUD clusters (tachometer, minimap, heat meter) from colliding with screen bezels or notch hardware.
- **Symmetric & Asymmetric Splits**: Garage, livery customizer, and loadout screens adopt a 12-column desktop split (7-column stage view / 5-column modular telemetry stack) collapsing to a persistent bottom drawer or full-bleed horizontal scroll pane on mobile.
- **HUD Peripheral Anchors**: In-game flight/drive interfaces snap modules to the four screen corners, preserving an uncluttered center corridor for road action.

## Elevation & Depth

Visual depth is forged through industrial mechanical layers, tactical metallic trim, and active neon light emission rather than soft natural drop shadows.

### Elevation Hierarchy
- **Level 0 (Asphalt Floor)**: `#0d1117` base texture with subtle 1px procedural gridlines or diagonal hazard hash patterns.
- **Level 1 (Sub-Chassis & Drawers)**: `#161b22` bordered by 1px solid `#30363d` or raw metallic steel hairline borders (`#484f58`).
- **Level 2 (Active Panels & HUD Cards)**: `#21262d` elevated by 1.5px metallic trim with an intense inner top highlight (`rgba(255, 255, 255, 0.08)`).
- **Level 3 (Interactive Cyber Focus & Modals)**: Floating panels rimmed in 1.5px `#00f0ff` or `#ffb703`, paired with a tight, high-intensity neon bloom: `0 0 12px rgba(0, 240, 255, 0.45), 0 0 24px rgba(0, 240, 255, 0.2)`.

## Shapes

The design system adopts a strict, sharp mechanical silhouette (`roundedness: 0`). Curved, bubbly corners are prohibited.

### Form Characteristics
- **Sharp Angles & 45° Chamfers**: Containers, buttons, and HUD badges utilize hard 90-degree corners or 45-degree angled corner clips (`clip-path: polygon(...)`) to simulate cut sheet-metal and molded motorbike fairings.
- **Beveled Borders**: Dialog headers and modal trims feature directional metallic bevels (bright top-left edge, darkened bottom-right edge) to emulate industrial hardware casing.

## Components

### Buttons & Interactive Controls
- **Cyber Primaries**: Chamfered rectangular blocks with solid `#00f0ff` background, pitch-black Anton uppercase text, accompanied by an animated diagonal scanline shine. Hover states trigger an intense cyan glow with a 2px horizontal offset shadow (`#ee4d2d`).
- **Hazard Action Buttons**: Solid `#ffb703` with warning diagonal chevron stripes (`#0d1117`) flanking the button label.
- **Ghost Utility Buttons**: Transparent fill, 1.5px `#484f58` metallic frame, shifting to `#00f0ff` border with text color transition on hover.

### Courier Skin & Mod Selector Cards (Badge Style)
- **Format**: Vertical trading-card form factors styled like physical courier IDs and vehicle license badges.
- **Anatomy**:
  - Top edge: Fleet insignia badge (e.g., Xanh SM neon cyan-green bolt, Shopee flame, Grab winged gear).
  - Center: Full-bleed vehicle sprite on dark asphalt background with neon rim lighting.
  - Bottom: Monospaced stat telemetry matrix (Handling, Nitro, Payload Capacity) featuring segmented segmented-LED bar meters.
- **Active State**: Outer border pulses in faction color, accompanied by illuminated corner bracket crosshairs.

### HUD Dialogs & Transmission Overlays
- **Structure**: Floating, compact chassis panels styled like modified moped handlebar phone mounts and courier dispatch terminals.
- **Visuals**: Header includes a flashing status indicator (`LIVE COMM // DISPATCH`), caller pixel portrait, and high-contrast monospaced dialog text typing in real-time.
- **Actions**: Hard-angled "ACCEPT ORDER" (Cyan/Green glow) and "DECLINE" (Muted crimson) buttons pinned to the lower trim.

### Input Fields & Toggles
- **Fields**: Recessed `#0d1117` wells with sharp 1px borders, accompanied by left-aligned monospaced micro-labels pinned directly across the upper border frame.
- **Checkboxes & Radios**: Angular square diamonds (45° rotated boxes). Checked state triggers an electrified neon core and sharp audio-tactile visual feedback.

### Speedometer & Delivery Gauges
- Segmented digital gauge strips with glowing LED tick marks. Critical overdrive redlines transition dynamically from Safety Amber (`#ffb703`) to Shopee Flare (`#ee4d2d`).