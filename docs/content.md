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
- Après `assets.json`, les fichiers `assets.*.json` posés à côté sont lus dans l'ordre des noms (le
  générateur écrit `assets.rig.json` : planches, durées, événements, points d'attache). Une entrée de même
  identifiant remplace la précédente.
- Le détail des animations (durées par image, événements, points d'attache de l'arme) est dans
  [animation.md](animation.md).
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
  "whenEmpty": "click", "woundChance": 0.3,
  "execution": { "mode": "shot", "strikes": 2, "windUp": 0.14, "strikeInterval": 0.16, "recovery": 0.18 },
  "throw": { "speed": 700, "damage": 0, "knockdown": true, "knockdownDuration": 2 },
  "sounds": { "attack": "sound.pistol", "hit": null, "empty": "sound.empty", "pickup": "sound.pickup" }
}
```

| Propriété | Sens |
|---|---|
| `kind` | `melee` ou `firearm` |
| `canDrop` | `false` pour les armes naturelles (poings, griffes) |
| `damage`, `damageType` | dégâts et type (comparés aux résistances des personnages) |
| `range` | allonge de mêlée (du centre de l'attaquant au bord de la cible ; un personnage a un rayon de 10) ou distance maximale des balles |
| `arcDegrees`, `attackDuration`, `hitDelay`, `maxTargets` | mêlée : angle, durée, instant de l'impact, cibles max |
| `fireRate`, `automatic` | attaques par seconde, tir maintenu |
| `knockdown`, `knockdownDuration`, `knockback` | renversement et recul de la cible |
| `stunDuration` | étourdit une cible debout (0 = non) |
| `woundChance` | probabilité (0 à 1) qu'un coup, une balle ou un lancer mortel laisse la cible **blessée** au lieu de morte, si elle peut l'être (`wound`) |
| `dismember` | un coup mortel peut trancher des morceaux du corps : `chance`, `parts` (sortes : `head`, `arm`, `leg`), `count` (morceaux au plus), `maxDistance` (0 = toute distance ; le fusil à pompe seulement de près) |
| `noiseRadius` | rayon dans lequel les ennemis entendent (0 = silencieux) |
| `magazineSize`, `pellets`, `spreadDegrees` | munitions, plombs par tir, dispersion |
| `ammoType`, `reloadTime` | munitions de réserve (`"9mm"`, `"shells"`…) : l'arme se recharge depuis l'inventaire du joueur (R, ou en tirant sur un chargeur vide). Sans `ammoType`, une arme vide ne fait que cliquer ou être lancée |
| `projectileSpeed` | vitesse des balles ; `0` = tir instantané |
| `muzzleDistance`, `holdOffset` | position du canon et de l'arme dans les mains (avant, droite) ; `holdOffset` sert aux personnages sans animations |
| `pose` | famille de gestes pour les animations : `unarmed`, `club`, `blade`, `pistol`, `long`, `heavy`… (clés `idle.club`, `attack.club`, `fire.long`) |
| `heldSprite`, `grip` | image de l'arme **en main**, vue de dessus, et point tenu par la main (pixels depuis le centre de l'image) ; `spriteId` reste l'image posée au sol |
| `whenEmpty` | `click`, `throw` (lancée automatiquement) ou `drop` |
| `execution` | achèvement avec cette arme (voir ci-dessous) |
| `throw` | comportement une fois lancée (`damage` > 0 : létale, comme un couteau) |

### Achèvement (`execution`)

Une seule mécanique pour toutes les armes, paramétrée par arme ; elle sert au joueur comme aux ennemis.

| Propriété | Sens | Exemples du contenu |
|---|---|---|
| `mode` | `strike` (coups) ou `shot` (tir à bout portant ; coups de crosse si l'arme est vide) | armes à feu : `shot` |
| `strikes` | nombre de coups, le dernier tue | poings 3, tuyau 2, batte 1 |
| `windUp` | délai entre l'engagement et le premier coup | couteau 0,08 ; batte 0,16 |
| `strikeInterval` | délai minimal entre deux coups | poings 0,12 |
| `strikeOnInput` | le joueur donne chaque coup avec la touche d'attaque | poings : `true` |
| `autoStrikeDelay` | sans appui, un coup part quand même après ce délai (jamais bloqué) | poings 0,6 |
| `recovery` | temps avant de pouvoir agir après le coup final | 0,1 à 0,25 |
| `noiseRadius` | bruit de chaque coup (le tir utilise le bruit de l'arme) | |
| `animations` | allure de l'achèvement, tirée au hasard dans la liste (voir ci-dessous) | poings : `headBash`, `stomp` |
| `against` | réglages différents contre une anatomie (`strikes`, `animations`) | poings contre `quadruped` : `tearLegs`, `tearHead` |

L'animation décide aussi de la place du bourreau. Un corps au sol a la tête du côté de son
orientation.

| Animation | Placement | Effet |
|---|---|---|
| `headBash` | à cheval sur la poitrine, face à la tête | cogne la tête au sol ; elle éclate au dernier coup |
| `stomp` | debout à côté de la tête | coups de pied dans la tête |
| `smash` | debout à côté de la tête | coup d'arme vers le bas (défaut des armes de mêlée) |
| `stab` | à cheval | coup de couteau |
| `shoot` | debout à côté de la tête | tir à bout portant (défaut des armes à feu chargées) |
| `bite` | au-dessus de la tête | morsure (animaux) |
| `tearLegs` | à genoux sur la bête, face aux pattes | une patte arrachée par coup (`sprites.parts` d'id `leg`) |
| `tearHead` | à cheval sur la bête | tire la tête jusqu'à l'arracher (part d'id `head`) |
| `behead` | debout à côté de la tête | coup de lame qui tranche la tête (machette) |

Toutes les poses à cheval regardent vers la tête : les épaules du bourreau sont en travers du corps.

```json
"execution": {
  "strikes": 3, "strikeOnInput": true,
  "animations": ["headBash", "stomp"],
  "against": { "quadruped": { "animations": ["tearLegs", "tearHead"] } }
}
```

## Personnages et ennemis (`characters/`, `enemies/`)

```json
{
  "id": "guard", "nameKey": "actor.guard.name", "faction": "enemy",
  "health": 100, "radius": 10, "speed": 85, "runSpeed": 175, "acceleration": 3000, "friction": 2200,
  "weaponId": "pistol", "naturalWeaponId": null,
  "knockdownImmune": false, "knockdownResistStun": 0.3, "hitStunDuration": 0.15, "getUpDuration": 0.4,
  "damageMultipliers": { "blunt": 0, "sharp": 0.5 },
  "countsForObjectives": true,
  "anatomy": "human",
  "wound": { "crawlSpeed": 30, "bleedOutTime": 0 },
  "sprites": { "body": "actor.guard", "legs": "actor.legs", "down": "actor.guard.down", "dead": "actor.guard.dead",
               "mount": "actor.guard.mount", "kick": "actor.legs.kick", "crawl": "actor.guard.crawl", "bloodTint": "#ffffff",
               "animationSet": "actor.guard", "animations": { "attack.club": "actor.guard.attack.club" } },
  "sounds": { "death": "sound.death", "hurt": "sound.groan", "alert": null },
  "ai": {
    "behavior": "guard", "visionRange": 430, "visionAngleDegrees": 120, "closeDetectionRadius": 36,
    "hearingMultiplier": 1, "reactionTime": 0.32, "turnSpeedDegrees": 420, "aimErrorDegrees": 3,
    "preferredRangeFactor": 0.35, "loseTargetTime": 2.5, "searchDuration": 4,
    "suspicionDelay": 0.4, "patrolPause": 1.2, "wanderRadiusTiles": 4, "alertAlliesRadius": 160
  }
}
```

- Des factions différentes sont hostiles. Le joueur est de faction `player`. Entre alliés, seules
  les attaques autorisées par `rules.friendlyFire` blessent ; les collisions physiques restent.
- `speed`, `acceleration`, `friction` règlent la réactivité : avec 215 / 9000 / 9000, le joueur
  atteint sa vitesse en 0,03 s et fait demi-tour en 0,05 s.
- `knockdownResistStun` : un personnage `knockdownImmune` frappé par une attaque renversante
  vacille ce temps-là à la place.
- `damageMultipliers` par type de dégâts : `0` = immunisé (le colosse ignore les battes),
  `"execution": 0` empêche d'achever.
- `anatomy` (`human` par défaut, `quadruped` pour le chien) : une arme peut achever chaque anatomie
  différemment (`execution.against`).
- `stamina` (absent = pas de course) : `{ "max": 100, "runDrain": 30, "regen": 24, "regenDelay": 0.6,
  "recoverThreshold": 30, "runNoise": 260 }`. En courant (Maj), le joueur va à `runSpeed`, dépense son
  endurance et fait du bruit ; épuisé, il marche jusqu'à en avoir récupéré `recoverThreshold`.
- `sprites.heat` (caméra thermique) : `{ "rest": 0.2, "active": 1, "pulse": 0.14 }` = luminosité au repos
  (embuscade, sommeil, garde immobile), en mouvement, et respiration lente au repos.
- `sounds.idle` : joué de temps en temps au repos quand le joueur est proche (un indice) ; `sounds.leap` :
  au moment où une créature se ramasse pour bondir.
- `ai.behavior: "ambush"` : immobile et aveugle, se réveille quand le joueur entre à vue dans
  `ai.wakeRadius` (ou fait du bruit tout près), puis chasse comme les autres.
- `ai.leap` : `{ "minRange": 55, "range": 190, "windUp": 0.5, "speed": 600, "distance": 230,
  "recovery": 0.8, "cooldown": 1.6 }`. Entre `minRange` et `range`, chemin dégagé : la créature se ramasse
  (`windUp`, annoncé à l'écran et au son), puis bondit en ligne droite vers l'endroit où était sa cible ;
  un mur l'arrête ; un seul coup au contact (son arme naturelle, factions respectées), puis `recovery`.
- `wound` (absent = jamais blessé) : un coup mortel d'une arme qui a un `woundChance` peut laisser le
  personnage **blessé** : à terre, face contre le sol, il lâche son arme et rampe en s'éloignant du
  joueur (`crawlSpeed`), en laissant une traînée de sang et en gémissant (`sounds.hurt`). Il ne se
  relève jamais, n'attaque plus, compte encore pour « éliminer tout le monde », et s'achève avec
  Espace. Tout coup qui fait des dégâts le tue ; un coup de poing ne le relève pas.
  `bleedOutTime` > 0 : il meurt seul après ce temps (le joueur qui l'a blessé est crédité).
- Images : `down` (au sol, tête vers la droite), `dead`, `mount` (à genoux à cheval sur un corps, pour
  les achèvements ; sinon `body`), `kick` (jambes en train de frapper ; sinon `legs`), `crawl`
  (blessé face contre terre, animation qui avance avec la distance rampée ; sinon `down`).
- Corps démontable (`torso` + `parts`, humains et chien) : le corps au sol sans ses morceaux, puis chaque morceau, chacun
  sur une image **de même taille**, à sa place. Le moteur calcule seul le centre et l'articulation de
  chaque morceau à partir des pixels. `over: true` dessine le morceau au-dessus du torse (une tête).
  Les morceaux sont arrachés dans l'ordre de la liste.

  ```json
  "sprites": { "body": "actor.dog", "down": "actor.dog.dead", "dead": "actor.dog.dead",
               "torso": "actor.dog.torso",
               "parts": [ { "id": "leg", "sprite": "actor.dog.leg1" }, { "id": "head", "sprite": "actor.dog.head", "over": true } ] }
  ```
- `ai.behavior` : `guard` (poste fixe, regarde autour), `patrol` (suit la ronde du niveau),
  `wander` (erre), `dormant` (attend un déclencheur). Le combat est le même pour tous.
- Un ennemi « rapide », « résistant » ou « déclenché par un événement » est une variante de données, sans code.

## Tuiles (`tiles/`) et portes (`doors/`)

```json
{ "id": "glass", "spriteId": "tile.glass", "layer": "wall", "thickness": 8,
  "blocksMovement": true, "blocksProjectiles": true, "blocksVision": false,
  "destructible": true, "breaksInto": "glass_broken", "breakSound": "sound.glass", "breakNoiseRadius": 300 }
```

`thickness` (unités, tuiles de mur seulement) fait un **mur fin**. Seule une bande de cette largeur,
centrée sur la tuile, est pleine. Elle se prolonge vers les murs voisins (lignes et colonnes, pas les
diagonales) et vers les embrasures des portes. Le reste de la tuile est du sol libre : on y marche, on
voit et on tire au travers, et il prend le sol de la pièce qui le borde (le fond, du côté de l'extérieur).
Une masse de murs reste pleine à l'intérieur. Absent ou 0 : la tuile entière est pleine. Le pack action
utilise 8 unités, le pack thermique des tuiles pleines. Collisions, tirs, vue, chemins et rendu utilisent
la même forme (`TileMap.Solids`).

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
  "campaign": ["first_night", "warehouse", "offices"], "theme": "neon", "menuMusic": "music.menu",
  "rules": {
    "unarmedWeaponId": "unarmed", "pickupRadius": 24, "pickupWhileArmed": "swap",
    "dropWeaponOnKnockdown": true, "dropWeaponOnDeath": true, "doorsKnockDown": true,
    "enemiesHaveInfiniteAmmo": true, "thrownHarmSpeed": 260,
    "friendlyFire": { "melee": false, "projectiles": true, "thrown": false, "explosions": true, "doors": true },
    "execution": { "enabled": true, "range": 24, "durationMultiplier": 1, "cancelByMoving": false,
                   "allowOnStunned": false, "enemiesCanExecute": true },
    "combat": { "inputBuffer": 0.15, "bulletsHitDowned": true, "hitOnStunnedKnocksDown": true },
    "score": { "enabled": true, "comboWindow": 3, "points": { "melee": 400, "firearm": 300, "thrown": 600, "door": 800, "execution": 500 } }
  },
  "camera": { "viewHeight": 400, "zoom": 1, "followSharpness": 9, "lookAhead": 0.22, "lookAheadMax": 90,
              "farLook": 0.6, "farLookMax": 300, "shakeDecay": 4, "maxShake": 12 },
  "sounds": { "uiMove": "...", "uiSelect": "...", "pickup": "...", "throw": "...", "bounce": "...",
              "impact": "...", "footstep": "...", "objectiveComplete": "...", "levelComplete": "...",
              "crush": "...", "tear": "..." },
  "extraLevels": ["arena"],
  "feel": {
    "hit": { "hitstop": 0.02, "shake": 0.06, "punch": 1.5 }, "knockdown": { "hitstop": 0.045, "shake": 0.16, "punch": 2.5 },
    "kill": { "hitstop": 0.06, "shake": 0.22, "punch": 3, "backdrop": 0.25 }, "executionStrike": { "hitstop": 0.045, "shake": 0.4, "punch": 4 },
    "executionFinish": { "hitstop": 0.1, "shake": 0.75, "punch": 9 }, "playerHurt": { "hitstop": 0.06, "shake": 0.45 },
    "maxHitstop": 0.1, "hitFlash": 0.08, "fallDuration": 0.05
  }
}
```

### Règles de combat

| Réglage | Sens |
|---|---|
| `friendlyFire.melee / projectiles / thrown / explosions / doors` | dégâts entre alliés par type d'attaque (par défaut : mêlée et armes lancées non, balles et portes oui) |
| `execution.range` | écart maximal (bord à bord) pour achever |
| `execution.durationMultiplier` | multiplie tous les temps d'achèvement |
| `execution.cancelByMoving` | une touche de déplacement annule l'achèvement (désactivé : c'est un engagement) |
| `execution.allowOnStunned` | achever un personnage étourdi encore debout |
| `execution.enemiesCanExecute` | les ennemis achèvent le joueur à terre |
| `combat.inputBuffer` | une attaque, un ramassage ou Espace pressé un peu trop tôt est retenu ce temps-là |
| `combat.bulletsHitDowned` | les balles touchent les personnages au sol (indépendant de l'achèvement) |
| `combat.hitOnStunnedKnocksDown` | tout coup sur un personnage étourdi le renverse |

### Ressenti (`feel`)

Présentation uniquement, jamais les règles. Pour chaque type d'impact où le joueur est impliqué :
`hitstop` (micro-arrêt en secondes), `shake` (secousse de 0 à 1) et `punch` (à-coup de la caméra dans
le sens du coup, en unités du monde) et `backdrop` (durée en secondes pendant laquelle le fond autour du
niveau clignote, une image sur deux à 30 Hz, dans la couleur `backdropFlash` du thème ; rien si le thème
n'en a pas ; coupé quand la secousse d'écran est réglée à 0). `playerHurt` remplace les autres
quand c'est le joueur qui est touché. `maxHitstop` plafonne le gel cumulé, `hitFlash` règle le flash
blanc d'un personnage touché, `fallDuration` la chute visible au sol. `extraLevels` liste des niveaux
proposés après la campagne (la salle d'essai `arena`). Sons : `crush` quand une tête éclate, `tear`
quand un morceau est arraché.

`camera.viewHeight` fixe la hauteur de monde visible : tous les écrans voient la même zone.
`camera.followSharpness` règle le suivi (lissage exponentiel) : à vitesse constante, la caméra reste
`vitesse / followSharpness` unités en retard et prend 95 % d'un changement de vitesse en `3 / followSharpness`
secondes. Le pack action est étalonné sur une vidéo de référence (`docs/analyse-video-hm.md`).

Quand le score est actif, chaque élimination par le joueur affiche ses points (« +400 ») à l'endroit où la
victime est tombée (texte `hud.points`, couleurs `ui.secondary`, `ui.text` et `ui.accent` du thème).

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

Couleurs `#rrggbb` ou `#rrggbbaa`. `backdropFlash` (facultatif) : couleur du clignotement du fond
autour du niveau sur les impacts dont le ressenti a un `backdrop` (le pack action : `#b3122e`). `hudStyle` : `default` ou `surveillance` (cadre de caméra, horloge,
position en grille, liaison montante, crochets sur les créatures qui chassent). `post.pixelArt: true` dessine
le monde à un pixel par unité puis l'agrandit d'un facteur entier sans lissage (pixels nets ; la hauteur
visible est arrondie à ce facteur). `walls` : biseau des murs et des toits (`rim`, `rimWidth` côté lumière,
`shade`, `shadeWidth` de l'autre côté ; largeurs en unités, 0 = rien). `hideUnseenActors` cache les
personnages hors du champ de vision du joueur. `heatGlow` (halo autour des corps chauds, selon leur
`sprites.heat`) et `rollingBar` (bande qui défile, vieille vidéo) complètent l'effet caméra thermique
(`Mods/NoctPrototype/themes/thermal_cam.json`). Le thème ne change jamais les règles.

Dans `game.json`, `camera.sway` (unités) et `camera.swayPeriod` (secondes) font dériver lentement la vue,
comme une caméra en vol stationnaire. `"restartWhileAlive": false` réserve la touche de relance à l'après-mort (et à la pause),
pour qu'elle serve aussi à recharger ; `"hud": { "survival": true }` affiche vie, endurance, munitions de
réserve, trousses et objets de mission ; `rules.medkitHeal` règle le soin d'une trousse ; `sounds`
accepte `transmission`, `reload`, `heal`.
