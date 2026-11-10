# Format du contenu

Un **pack** est un dossier. Le contenu de base est `Content/Base`, chaque mod est un dossier de `Mods/`.
Tous les fichiers sont en JSON (commentaires `//` et virgules finales acceptés). Les noms de propriétés
sont en camelCase. **Une propriété inconnue est une erreur** : les fautes de frappe sont signalées au
lancement (menu « Contenu : n erreur(s) » et `log.txt`).

```text
<pack>/
├── manifest.json        identité du pack (obligatoire)
├── game.json            titre, campagne, thème, règles, caméra, sons génériques
├── assets.json          identifiants logiques → fichiers
├── weapons/*.json       armes
├── characters/*.json    personnages (joueur)
├── enemies/*.json       ennemis (même format que les personnages)
├── tiles/*.json         tuiles
├── doors/*.json         portes
├── modifiers/*.json     modificateurs de partie
├── levels/*.json        niveaux (voir levels.md)
├── locales/<langue>.json textes (voir localization.md)
├── themes/*.json        apparence
└── assets/              images, sons, polices
```

Un fichier peut contenir une définition (objet) ou plusieurs (tableau). Chaque définition a un `id`.
Unités : distances en unités du monde (1 tuile = 32 par défaut), temps en secondes, angles en degrés.

## Ressources (`assets.json`)

Le jeu ne référence **jamais** un chemin de fichier, seulement des identifiants :

```json
{
  "textures": { "weapon.pistol": "assets/sprites/weapons/pistol.png" },
  "sounds":   { "sound.pistol": "assets/audio/pistol.wav" },
  "fonts":    { "font.ui": "assets/fonts/DejaVuSans-Bold.ttf" },
  "animations": {
    "actor.legs": {
      "texture": "actor.legs.sheet", "frameWidth": 32, "frameHeight": 32,
      "frames": [0, 1, 2, 3], "frameDuration": 0.25, "loop": true,
      "origin": [16, 16], "events": { "1": "footstep", "3": "footstep" }
    }
  }
}
```

- Un identifiant de sprite peut désigner une texture ou une animation.
- Les sprites regardent vers la **droite** (+X) et tournent autour de leur centre (ou de `origin`).
- Images PNG, sons WAV (PCM), polices TTF/OTF.
- Fichier manquant : avertissement au chargement et damier magenta à l'écran (ou silence) ; le jeu reste jouable.
- Identifiants utilisés par le moteur lui-même : `fx.blood.1..3`, `fx.blood_drop`, `fx.spark`,
  `fx.shard`, `fx.smoke`, `fx.muzzle`, `fx.bullet`, `ui.crosshair`, `ui.arrow`.

## Armes (`weapons/`)

```json
{
  "id": "pistol", "nameKey": "weapon.pistol.name", "kind": "firearm", "spriteId": "weapon.pistol",
  "damage": 100, "damageType": "bullet", "range": 900, "fireRate": 4, "automatic": false,
  "magazineSize": 12, "pellets": 1, "spreadDegrees": 3, "projectileSpeed": 2300,
  "recoil": 3, "noiseRadius": 650, "muzzleDistance": 20, "holdOffset": [12, 3],
  "whenEmpty": "click", "executionDuration": 0.35,
  "throw": { "speed": 700, "damage": 0, "knockdown": true, "knockdownDuration": 2 },
  "sounds": { "attack": "sound.pistol", "hit": null, "empty": "sound.empty", "pickup": "sound.pickup" }
}
```

| Propriété | Sens |
|---|---|
| `kind` | `melee` ou `firearm` |
| `canDrop` | `false` pour les armes naturelles (poings, griffes) |
| `damage`, `damageType` | dégâts et type (comparés aux résistances des personnages) |
| `range` | allonge de mêlée, ou distance maximale des balles |
| `arcDegrees`, `attackDuration`, `hitDelay`, `maxTargets` | mêlée : angle, durée, instant de l'impact, cibles max |
| `fireRate`, `automatic` | attaques par seconde, tir maintenu |
| `knockdown`, `knockdownDuration`, `knockback` | renversement et recul de la cible |
| `noiseRadius` | rayon dans lequel les ennemis entendent (0 = silencieux) |
| `magazineSize`, `pellets`, `spreadDegrees` | munitions, plombs par tir, dispersion |
| `projectileSpeed` | vitesse des balles ; `0` = tir instantané |
| `muzzleDistance`, `holdOffset` | position du canon et de l'arme dans les mains (avant, droite) |
| `whenEmpty` | `click`, `throw` (lancée automatiquement) ou `drop` |
| `executionDuration` | durée pour achever un ennemi à terre avec cette arme |
| `throw` | comportement une fois lancée (`damage` > 0 : létale, comme un couteau) |

## Personnages et ennemis (`characters/`, `enemies/`)

```json
{
  "id": "guard", "nameKey": "actor.guard.name", "faction": "enemy",
  "health": 100, "radius": 10, "speed": 85, "runSpeed": 175, "acceleration": 3000, "friction": 2200,
  "weaponId": "pistol", "naturalWeaponId": null,
  "knockdownImmune": false, "hitStunDuration": 0.15, "getUpDuration": 0.4,
  "damageMultipliers": { "blunt": 0, "sharp": 0.5 },
  "countsForObjectives": true,
  "sprites": { "body": "actor.guard", "legs": "actor.legs", "down": "actor.guard.down", "dead": "actor.guard.dead", "bloodTint": "#ffffff" },
  "sounds": { "death": "sound.death", "hurt": null, "alert": null },
  "ai": {
    "behavior": "guard", "visionRange": 430, "visionAngleDegrees": 120, "closeDetectionRadius": 36,
    "hearingMultiplier": 1, "reactionTime": 0.32, "turnSpeedDegrees": 420, "aimErrorDegrees": 3,
    "preferredRangeFactor": 0.35, "loseTargetTime": 2.5, "searchDuration": 4,
    "suspicionDelay": 0.4, "patrolPause": 1.2, "wanderRadiusTiles": 4, "alertAlliesRadius": 160
  }
}
```

- Des factions différentes sont hostiles. Le joueur est de faction `player`.
- `damageMultipliers` par type de dégâts : `0` = immunisé (le colosse ignore les battes),
  `"execution": 0` empêche d'achever.
- `ai.behavior` : `guard` (poste fixe, regarde autour), `patrol` (suit la ronde du niveau),
  `wander` (erre), `dormant` (attend un déclencheur). Le combat est le même pour tous.
- Un ennemi « rapide », « résistant » ou « déclenché par un événement » est une variante de données, sans code.

## Tuiles (`tiles/`) et portes (`doors/`)

```json
{ "id": "glass", "spriteId": "tile.glass", "layer": "wall",
  "blocksMovement": true, "blocksProjectiles": true, "blocksVision": false,
  "destructible": true, "breaksInto": "glass_broken", "breakSound": "sound.glass", "breakNoiseRadius": 300 }
```

```json
{ "id": "door_wood", "spriteId": "door.wood", "thickness": 6, "maxAngleDegrees": 90,
  "pushSpeedDegrees": 620, "damping": 2.2, "closeSpring": 0,
  "knockdownSpeedDegrees": 240, "knockdownDuration": 2.6,
  "blocksVision": true, "blocksProjectiles": true, "hitNoiseRadius": 220, "hitSound": "sound.door" }
```

Une porte poussée assez vite renverse ceux qui se trouvent derrière.

## Modificateurs (`modifiers/`)

```json
{ "id": "brawler", "nameKey": "modifier.brawler.name", "descriptionKey": "modifier.brawler.description",
  "moveSpeed": 1, "damageTaken": 1, "spread": 1, "enemyVision": 1, "executionSpeed": 2,
  "allowFirearms": false, "startingWeaponId": null }
```

Choisis dans le menu, ils s'appliquent au joueur et à la perception des ennemis.

## Jeu (`game.json`)

```json
{
  "titleKey": "game.title", "defaultLanguage": "fr", "fallbackLanguage": "en",
  "campaign": ["first_night", "warehouse", "offices"], "theme": "neon", "menuMusic": "music.action",
  "rules": {
    "friendlyFire": true, "unarmedWeaponId": "unarmed", "pickupRadius": 24, "pickupWhileArmed": "swap",
    "dropWeaponOnKnockdown": true, "dropWeaponOnDeath": true, "doorsKnockDown": true,
    "enemiesHaveInfiniteAmmo": true, "thrownHarmSpeed": 260,
    "execution": { "enabled": true, "range": 20, "durationMultiplier": 1, "cancelByMoving": true },
    "score": { "enabled": true, "comboWindow": 3, "points": { "melee": 400, "firearm": 300, "thrown": 600, "door": 800, "execution": 500 } }
  },
  "camera": { "viewHeight": 400, "zoom": 1, "followSharpness": 10, "lookAhead": 0.22, "lookAheadMax": 90,
              "farLook": 0.6, "farLookMax": 300, "shakeDecay": 4, "maxShake": 12 },
  "sounds": { "uiMove": "...", "uiSelect": "...", "pickup": "...", "throw": "...", "bounce": "...",
              "impact": "...", "footstep": "...", "objectiveComplete": "...", "levelComplete": "..." }
}
```

`camera.viewHeight` fixe la hauteur de monde visible : tous les écrans voient la même zone.

## Thèmes (`themes/`)

```json
{
  "id": "thermal", "background": "#050506",
  "tints": { "floor": "#ffffff", "wall": "#ffffff", "actor": "#ffffff", "weapon": "#ffffff", "decor": "#ffffff", "fx": "#ffffff", "blood": "#ffffff" },
  "shadow": { "enabled": false, "offset": [3, 4], "color": "#0a001880" },
  "ui": { "font": "font.mono", "text": "#e6e6e6", "accent": "#ffffff", "secondary": "#b8b8b8", "danger": "#ffffff",
          "panel": "#000000c0", "shadow": "#000000", "hudStyle": "surveillance", "overlayLabelKey": "noct.camLabel", "titleWave": 0.15 },
  "post": { "vignette": 0.5, "grain": 0.13, "scanlines": 0.2, "flicker": 0.6, "videoNoise": 0.5,
            "darkness": 0.45, "darknessColor": "#000000", "playerLightRadius": 240, "playerLightIntensity": 1,
            "muzzleFlashLight": 1.8, "colorMultiply": "#e8eef2", "hideUnseenActors": true }
}
```

Couleurs `#rrggbb` ou `#rrggbbaa`. `hudStyle` : `default` ou `surveillance`. `hideUnseenActors` cache les
personnages hors du champ de vision du joueur. Le thème ne change jamais les règles.
