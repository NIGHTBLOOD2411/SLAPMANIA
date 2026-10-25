# Slap Mania — Content Manifest

Status: Draft — Last verified 2026-09-19

The shopping list of audio, art, and VFX assets the game needs. Each entry lists the asset category, approximate count, format, and which module consumes it. Placeholder columns track what can be built with free/procedural stand-ins for the grey-box milestone.

## Front-end placeholder art (M10, 2026-10-19)

The V2 mockups' own images, copied from the `Slap Mania Game UI` Design canvas into `Assets/_Project/Art/UI/Mockup/` so the menus match the mockups. **They are placeholders.** Replace them with final art before release and re-run `Tools/mockup2uxml`, which rebuilds the masked copies in `Mockup/Masked/`.

| Asset | Size | Used by | Replace with |
|---|---|---|---|
| `Portrait_Slasher.png`, `Portrait_Buck.png`, `Portrait_Glorp.png` | ~240-300 px | Headers, roster/pack cards, tale of the tape, rankings | Fighter portraits from the roster pipeline (M08) |
| `Bg_ArenaNight.jpg`, `Bg_CrowdPov.jpg`, `Bg_FaceOff.jpg`, `Bg_SlasherFull.jpg`, `Bg_SlapContact.jpg`, `Bg_MatchPov.jpg` | 576x1024 (MatchPov 430x932) | Title, loading, menu tiles, search, walkout, replay | Captures of the real arena and fighters (M14-M17) |
| `Logo_SlapMania.png` | 973x451 | Title, store pack | Final logo lock-up |

The V1 page (2026-10-20) reuses these images; it adds no art of its own.

## Fonts (M10)

All SIL Open Font License 1.1. Keep each folder's `OFL.txt` with the fonts if they ship.

| Font | Files | Source | Used by |
|---|---|---|---|
| Barlow, Barlow Condensed | `Art/Fonts/Barlow/*.ttf` + TMP SDF assets | google/fonts | V2 HUD, V2 front-end screens, arena signage |
| Cabin 400/500/600/700 | `Art/Fonts/Cabin/Cabin-{Regular,Medium,SemiBold,Bold}.ttf` (~74.5 KB each) | Google Fonts static TTFs (fonts.gstatic.com, Cabin v35), the files the V1 mockups render with; licence from google/fonts `ofl/cabin` | V1 front-end screens (body text) |
| Archivo Black 400 | `Art/Fonts/ArchivoBlack/ArchivoBlack-Regular.ttf` (88 KB) | Google Fonts static TTF (Archivo Black v23); licence from google/fonts `ofl/archivoblack` | V1 front-end screens (display text, `.dsp`) |

## Audio (~485 assets, ~15.7 MB)

### Music

| Asset | Count | Format | Duration | Size est. | Module | Placeholder |
|---|---|---|---|---|---|---|
| Core stem (drums + bass) sections | 4–6 | Ogg Vorbis 128 kbps, 48 kHz mono | 8–16 bars each | ~2.5 MB total | M11 | Royalty-free loop |
| Melody stem sections | 4–6 | Ogg Vorbis 128 kbps, 48 kHz mono | 8–16 bars each | ~2.5 MB total | M11 | Silence (stem fades in at intensity) |
| Hype stem sections | 4–6 | Ogg Vorbis 128 kbps, 48 kHz mono | 8–16 bars each | ~2.5 MB total | M11 | Silence |
| Victory stinger | 1 | Ogg Vorbis | ~3 s | ~50 KB | M11 | Short synth chord |
| Defeat stinger | 1 | Ogg Vorbis | ~3 s | ~50 KB | M11 | Short low tone |

### VO (Announcer)

| Asset | Count | Format | Duration | Size est. | Module | Placeholder |
|---|---|---|---|---|---|---|
| Carrier lines ("And the challenger is...", "Round N!", etc.) | ~20 | Ogg Vorbis 96 kbps, 48 kHz mono | 1–3 s each | ~400 KB total | M11 | TTS or silence |
| Fighter name clips | ~20 | Ogg Vorbis 96 kbps, 48 kHz mono | 0.5–1 s each | ~150 KB total | M11 | TTS |
| Reaction lines ("What a hit!", "Incredible!", etc.) | ~30 | Ogg Vorbis 96 kbps, 48 kHz mono | 1–2 s each | ~400 KB total | M11 | TTS |
| Finisher call ("FINISH HIM/HER!") | ~5 | Ogg Vorbis 96 kbps, 48 kHz mono | 1–2 s each | ~80 KB total | M11 | TTS |

### Crowd

| Asset | Count | Format | Duration | Size est. | Module | Placeholder |
|---|---|---|---|---|---|---|
| Ambient murmur loop | 1 | Ogg Vorbis 96 kbps, 48 kHz mono | ~10 s looping | ~120 KB | M11 | Royalty-free crowd |
| Cheer loop | 1 | Ogg Vorbis 96 kbps, 48 kHz mono | ~10 s looping | ~120 KB | M11 | Royalty-free crowd |
| Reaction one-shots (gasp, ooh, roar) | ~10 | Ogg Vorbis 96 kbps, 48 kHz mono | 1–2 s each | ~150 KB total | M11 | Royalty-free |
| Victory eruption | 1 | Ogg Vorbis 96 kbps, 48 kHz mono | ~3 s | ~40 KB | M11 | Royalty-free |

### SFX

| Asset | Count | Format | Duration | Size est. | Module | Placeholder |
|---|---|---|---|---|---|---|
| Tap tick loop | 1 | Ogg Vorbis, 48 kHz | ~2 s loop at 140 BPM | ~30 KB | M11 | Metronome click loop |
| Tap accent loop | 1 | Ogg Vorbis, 48 kHz | ~2 s loop | ~30 KB | M11 | Higher-pitch click |
| Tap accent one-shots | 2–4 | Ogg Vorbis, 48 kHz | ~0.1 s each | ~10 KB total | M11 | Synthesized blip |
| Impact — body thud (per tier: Normal/Crit/Finisher) | 3 | Ogg Vorbis, 48 kHz | ~0.3 s each | ~20 KB total | M11 | Low drum hit |
| Impact — skin crack (per tier) | 3 | Ogg Vorbis, 48 kHz | ~0.2 s each | ~15 KB total | M11 | Snare snap |
| Impact — sweetener (Crit/Finisher only) | 2 | Ogg Vorbis, 48 kHz | ~0.2 s each | ~10 KB total | M11 | High bell |
| Impact — sub rumble (Crit/Finisher only) | 2 | Ogg Vorbis, 48 kHz | ~0.5 s each | ~15 KB total | M11 | Synth sub drop |
| Impact — reverb tail | 3 | Ogg Vorbis, 48 kHz | ~1 s each | ~30 KB total | M11 | Convolution reverb |
| Timing zone enter sounds (per zone) | 5 | Ogg Vorbis, 48 kHz | ~0.1 s each | ~10 KB total | M11 | Rising pitch tones |
| Timing commit sound | 1 | Ogg Vorbis, 48 kHz | ~0.2 s | ~5 KB | M11 | Click |
| UI button press | 2 | Ogg Vorbis, 48 kHz | ~0.1 s each | ~5 KB total | M11 | Soft click |
| UI screen transition | 2 | Ogg Vorbis, 48 kHz | ~0.3 s each | ~10 KB total | M11 | Whoosh |
| Round start bell | 1 | Ogg Vorbis, 48 kHz | ~1 s | ~15 KB | M11 | Boxing bell |
| KO sound | 1 | Ogg Vorbis, 48 kHz | ~1 s | ~15 KB | M11 | Deep gong |
| Camera shutter (snapshot) | 1 | Ogg Vorbis, 48 kHz | ~0.3 s | ~5 KB | M09/M11 | Click + film advance |

**Audio total:** ~485 clips, ~15.7 MB

---

## 3D Art

| Asset | Count | Format | Notes | Module | Placeholder |
|---|---|---|---|---|---|
| Fighter character model (player) | 1 | FBX, <10K tris | Rigged, face blend shapes for slap deformation | M08 | Capsule + face decal |
| Fighter character model (opponent) | 1 | FBX, <10K tris | Different visual, same rig | M08 | Capsule, different color |
| Slap ring / platform | 1 | FBX | Central arena platform | Scene | Cylinder primitive |
| Arena floor + walls | 1 | FBX | The venue enclosure | Scene | Scaled cube |
| Crowd (instanced billboard or low-poly) | 1 set | FBX or sprite atlas | Tiered — High: 3D, Mid: billboards, Low: flat texture | Scene | Colored quads |
| Jumbotron screen mesh | 2–4 | FBX | Flat quads with emission material | M17 | Plane primitives |
| Spotlight fixtures | 4–8 | FBX, <500 tris each | Decorative, with point/spot light children | M14 | Cone primitives |
| Ring ropes / barriers | 1 set | FBX | Ring boundary | Scene | Cylinder primitives |

## 2D Art / UI

| Asset | Count | Format | Notes | Module | Placeholder |
|---|---|---|---|---|---|
| Fighter portraits (menu) | 2 | PNG 512×512 | Used on menu and victory screen headers | M10 | Colored circles |
| Health bar sprites | 3 | PNG (9-slice) | Background, fill, delayed fill | M10 | Unity UI Image |
| Power bar sprites | 3 | PNG (9-slice) | Background, fill, glow | M10 | Unity UI Image |
| Timing bar zone colors | 5 | Solid color | Generated from `TimingZoneLayout` colors | M10 | Flat colors |
| Timing marker | 1 | PNG 64×64 | Arrow or line | M10 | White triangle |
| Damage number font | 1 | TMP font asset | Bold, impact-style | M10 | Default TMP font |
| "YOUR TURN" / "OPPONENT'S TURN" banners | 2 | PNG or TMP text | Animated swap | M10 | TMP text |
| Victory screen frame | 1 | PNG 9-slice | Photo gallery border | M10 | Simple border |
| Menu background | 1 | PNG 1080×1920 | Arena preview or gradient | M10 | Solid gradient |
| App icon | 1 | PNG 1024×1024 | Store icon | Build | Placeholder graphic |

## VFX / Particles

| Asset | Count | Format | Notes | Module | Placeholder |
|---|---|---|---|---|---|
| Impact shockwave | 3 (per tier) | Particle System prefab | Expanding ring, scaled by damage | M15 | Expanding circle sprite |
| Sweat / spit particles | 1 | Particle System prefab | Small droplets on impact | M15 | Small white quads |
| Confetti burst | 1 | Particle System prefab | On crit hits | M15 | Colored quads |
| Victory confetti rain | 1 | Particle System prefab | Sustained fall on victory | M15 | Colored quads |
| Arena haze / fog | 1 | Particle System prefab | Ambient atmosphere | M15 | Transparent quad sheet |
| Light beam cones | 4–8 | Particle or mesh | Volumetric spotlight cones | M14/M15 | Cone mesh with alpha |
| Beat pulse ring | 1 | Particle System or shader | Floor glow on beat | M15 | Animated emissive plane |
| Screen-edge flash | 1 | UI Image | Full-screen flash on impact, fades quickly | M10/M15 | White overlay with fast fade |

## Materials / Shaders

| Asset | Count | Notes | Module | Placeholder |
|---|---|---|---|---|
| Fighter body material | 2 | URP Lit, skin-like | M08 | URP Lit, flat color |
| Arena floor material | 1 | URP Lit or custom, emissive | M14 | URP Lit, dark grey |
| Jumbotron screen material | 1 | Unlit, renders `RenderTexture` | M17 | Unlit, shows atlas |
| Crowd billboard material | 1 | URP Unlit, instanced | Scene | Unlit, flat color |
| Smear material | 1 | Custom vertex displacement | M08 | Standard Lit with offset |
| Snapshot rim light material | 1 | URP Lit with fresnel | M09 | Standard emissive edge |

---

## Summary

| Category | Asset count | Size estimate |
|---|---|---|
| Audio | ~485 | ~15.7 MB |
| 3D Art | ~15–25 | ~5–15 MB (depends on texture resolution) |
| 2D Art / UI | ~15–20 | ~2–5 MB |
| VFX / Particles | ~12–15 | <1 MB (prefabs + textures) |
| Materials / Shaders | ~8 | <1 MB |
| **Total** | **~535–550** | **~24–38 MB** |

Target AAB size: ≤ 80 MB (including engine, scripts, scenes, and all assets).
