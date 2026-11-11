# Format du contenu

Un **pack de jeu** est un dossier de `Content/` ; un **mod** est un dossier de `Mods/`. Les deux ont
la même structure, simplifiée au besoin (un dossier absent est ignoré) :

```text
manifest.json      identité, version, format, dépendances
game.json          titre, langues, missions du menu, constantes de combat, couleurs, sons
stats/             statistiques des unités
damageTypes/       types de dégâts
shapes/            formes voxel des parties de tuiles
tiles/             parties de tuiles (sols, murs, objets)
maps/              blocs de carte
terrains/          terrains pour les cartes générées
items/             armes, munitions, cadavres
armors/            armures (protection, apparence, résistances)
units/             types d'unités (statistiques, équipement, IA)
inventory/         emplacements d'inventaire et coûts de déplacement
missions/          missions tactiques
localization/      textes, un fichier par langue (fr.json, en.json…)
assets.json        identifiants logiques → fichiers
assets/            images, sons, polices
```

Chaque fichier de définitions contient un objet ou un tableau d'objets. Chaque définition a un
`id` stable, indépendant du nom affiché (`nameKey` est une clé de traduction). Les commentaires
`//` et les virgules finales sont acceptés. Une propriété inconnue est une erreur.

## manifest.json

```json
{ "id": "horizon", "name": "Horizon", "version": "0.1.0", "formatVersion": 1, "dependencies": [] }
```

`formatVersion` est la version du format compris par le moteur (1). Un mod liste dans
`dependencies` le pack de jeu (ou les mods) dont il a besoin.

## Statistiques (`stats/`)

```json
{ "id": "firing", "nameKey": "stat.firing", "shortKey": "stat.firing.short", "max": 120, "order": 5 }
```

Les règles lisent `tu`, `stamina`, `health`, `bravery`, `reactions`, `firing`, `throwing`,
`strength`, `melee` : ces statistiques doivent être déclarées. Un pack peut en ajouter.

## Types de dégâts (`damageTypes/`)

Valeurs par défaut : celles du type « perforant » d'OXCE.

| Champ | Défaut | Rôle |
|---|---|---|
| `random` | `standard` | `standard` (puissance ± `damageRangePercent`), `explosion`, `ufo` (0–200 %), `tftd` (50–150 %), `flat`, `fire`, `none` |
| `armorEffectiveness` | 1 | part de l'armure soustraite |
| `toHealth` | 1 | dégâts → santé |
| `toArmor` | 0,1 | usure de l'armure touchée |
| `toStun` / `randomStun` | 0,25 / vrai | étourdissement (aléatoire de 0 à dégâts × facteur) |
| `toWound` | 1 | blessures mortelles (probabilité dégâts / 11) |
| `toTile` | 0,5 | dégâts au terrain |
| `ignoreDirection` | faux | toujours l'armure avant et le torse |

## Formes et parties de tuiles (`shapes/`, `tiles/`)

Une forme est une liste de boîtes `[x0, y0, z0, x1, y1, z1]` dans une tuile de 16 × 16 × 24 voxels.

```json
{ "id": "brick_w", "sprite": "tile.wall_brick_w", "shape": "wallW", "blocksSight": true,
  "armor": 40, "destroyedInto": "brick_ruin_w" }
```

| Champ | Rôle |
|---|---|
| `sprite` | identifiant logique de l'image |
| `shape` | forme voxel (vide = sans matière) |
| `tuWalk` | sols et objets : UT pour entrer (255 = infranchissable) ; murs franchissables : UT ajoutées |
| `walkable` | murs : l'arête peut être franchie |
| `blocksSight` | arrête la vue (les fenêtres laissent voir) |
| `standHeight` | hauteur de la surface où l'on se tient (marches de 8 voxels ; à 16, on accède au niveau supérieur) |
| `armor`, `destroyedInto` | résistance (0 = indestructible) et partie qui la remplace |
| `door`, `openInto`, `closedInto` | `swing` ou `sliding`, et les variantes ouverte/fermée |
| `light` | lumière émise (0–15) |

Le slot (sol, mur ouest, mur nord, objet) est choisi par la carte, pas par la partie.

## Blocs de carte (`maps/`)

```json
{
  "id": "outpost", "size": [24, 24, 2],
  "legend": {
    ".": { "floor": "grass" },
    "W": { "floor": "tiles", "west": "brick_w" },
    "1": { "floor": "grass", "spawn": "player" },
    "z": { "floor": "grass", "zone": "extraction" },
    "p": { "floor": "grass", "patrol": true }
  },
  "levels": [ [ "rangée nord…", "…", "rangée sud" ], [ "niveau 1…" ] ]
}
```

Chaque caractère d'une rangée est une clé de la légende (espace = case vide). Les rangées vont du
nord au sud, les caractères d'ouest en est, les niveaux du sol vers le haut. Un mur `west` est sur
l'arête ouest de la case, `north` sur l'arête nord : le mur est d'un bâtiment se place donc sur les
cases situées juste à l'est de celui-ci. Marqueurs : `spawn` (`player`, `hostile`, `neutral`),
`patrol`, `zone`.

## Terrains (`terrains/`)

```json
{ "id": "outskirts", "blockSize": 10, "blocks": [
  { "block": "outskirts_lz", "required": 1, "weight": 0 },
  { "block": "outskirts_house", "weight": 2, "max": 2 } ] }
```

## Objets (`items/`)

```json
{
  "id": "k7_rifle", "nameKey": "item.k7_rifle", "kind": "firearm", "twoHanded": true,
  "snap": { "accuracy": 60, "tu": 25 }, "aimed": { "accuracy": 110, "tu": 80 },
  "auto": { "accuracy": 35, "tu": 35, "shots": 3 },
  "ammo": ["k7_clip"], "reloadTu": 15, "range": { "aimed": 200, "dropoff": 2 },
  "floorSprite": "item.rifle", "projectileSprite": "fx.bullet", "fireSound": "sfx.rifle"
}
```

`kind` : `firearm`, `ammo`, `melee`, `grenade`, `corpse`, `none`. Les dégâts (`damage: { power,
type }`) sont portés par la munition, ou par l'arme elle-même si elle n'utilise pas de munition
(`rounds: 0` = illimité). `tu` est un pourcentage des UT max, ou une valeur fixe avec `flatTu`.

## Armures et unités (`armors/`, `units/`)

```json
{ "id": "field_vest", "front": 12, "side": 8, "rear": 5, "under": 2,
  "damageModifiers": { "pulse": 80 }, "stats": { "tu": -4 },
  "sprite": "unit.operative", "corpse": "corpse_operative", "standHeight": 22, "kneelHeight": 14 }
```

```json
{ "id": "operative", "nameKey": "unit.operative", "armor": "field_vest",
  "stats": { "tu": 56, "health": 34, "firing": 58 },
  "loadout": [ { "item": "k7_rifle", "slot": "rightHand", "ammo": "k7_clip" } ],
  "ai": { "aggression": 1, "intelligence": 3, "retreatHealthPercent": 35 } }
```

Feuille de sprites d'une unité : 17 images de 64 × 80 sur une ligne, 0–7 debout par direction
(0 = nord, sens horaire), 8–15 à genou, 16 au sol.

## Missions (`missions/`)

```json
{
  "id": "night_extraction", "nameKey": "…", "briefingKey": "…",
  "map": { "block": "outpost" }, "light": 2,
  "player": [ { "unit": "operative", "count": 3 } ],
  "hostile": [ { "unit": "gaunt", "count": 6 } ],
  "objective": { "kind": "reachZone", "zone": "extraction", "units": 2, "descriptionKey": "…" },
  "turnLimit": 15
}
```

`map` : `{ "block": … }` ou `{ "terrain": …, "blocksX": 3, "blocksY": 3, "levels": 2 }`.
`objective.kind` : `eliminateHostiles`, `survive` (`turns`), `reachZone` (`zone`, `units`).
`light` : 0 (nuit noire) à 15 (plein jour).

## Ressources (`assets.json`)

```json
{
  "textures": { "tile.floor_grass": "assets/tiles/floor_grass.png" },
  "sheets": { "unit.operative": { "texture": "texture.unit.operative", "frameWidth": 64, "frameHeight": 80, "anchor": [32, 48] } },
  "sounds": { "sfx.rifle": "assets/audio/rifle.wav" },
  "fonts": { "font.ui": "assets/fonts/DejaVuSans.ttf" }
}
```

Les images de carte sont des cadres de 64 × 80 dont le pixel (32, 48) est posé sur le coin nord du
losange de la case (`ui.mapSpriteAnchor` dans `game.json` pour changer cette convention). Une case
fait 64 × 32 pixels à l'écran, un niveau 48 pixels de haut. Formats : PNG, WAV, TTF.

## Textes (`localization/`)

```json
{ "ui": { "end_turn": "Fin du tour" }, "error": { "not_enough_tu": "Pas assez d'UT ({value} nécessaires)." } }
```

Les objets imbriqués donnent des clés à points (`ui.end_turn`). Paramètres entre accolades ;
pluriels avec `cle.one` / `cle.other`. Une clé absente utilise la langue de repli
(`fallbackLanguage`), puis la clé elle-même, et est signalée dans le journal.
