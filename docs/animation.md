# Animations des personnages

Un seul système pour tous les personnages (humains, chiens, créatures) : la simulation dit **dans quel état**
est un personnage, le rendu choisit **quelle animation** le montre, et les données disent **à quoi ressemble**
cette animation (planche, images, durées, points d'attache, événements). Remplacer une planche ou redéfinir
une animation se fait sans toucher au C#.

## Ce qui décide, ce qui montre

- `TopDown.Core/Presentation/ActorPose.cs` lit un personnage et renvoie une **pose** : une liste de clés,
  de la plus précise à la plus générale, et un temps. Exemple pour un homme armé d'une batte qui marche :
  `["walk.club", "walk", "idle.club", "idle"]`, temps = distance parcourue / 64.
- Le rendu (`WorldRenderer`) prend la première clé que le personnage sait jouer ; sinon il garde
  l'ancien rendu (une image fixe, des mouvements calculés), si bien qu'un personnage sans animations reste
  affiché.
- **Une animation n'applique jamais un dégât ni ne change un état.** Les coups touchent au moment fixé par
  l'arme (`hitDelay`), les chutes durent ce que dit la simulation. Les tests vérifient qu'interroger les
  poses ne change rien à une partie (`AnimationTests.Asking_for_poses_never_changes_the_game`).

## Clés reconnues

| Situation | Clés essayées dans l'ordre | Temps |
|---|---|---|
| immobile | `idle.{famille}`, `idle` | horloge du monde (boucle) |
| en mouvement | `walk.{famille}`, `walk`, puis idle | distance / 64 (les pieds ne glissent pas) |
| attaque | `attack.{famille}.alt` (une sur deux), `attack.{famille}`, `attack` | **compté depuis l'impact** |
| tir récent (0,25 s) | `fire.{famille}` | depuis le tir |
| étourdi | `hurt.{famille}`, `hurt`, `hit` | depuis le coup |
| touché (créature, 0,22 s) | `hit` | depuis le coup |
| au sol | `knockdown` ; en se relevant `getup` | depuis la chute |
| mort | `death.shot` (balle, feu), `death.cut` (lame), `death.blunt` (le reste), `death`, `knockdown` | depuis la mort ; dernière image si le corps était déjà au sol |
| blessé qui rampe | `crawl` | distance / 20 |
| bond | `leap.crouch`, `leap.air`, `leap.land` | depuis le début de la phase |
| achèvement | `execute.{allure}` (`execute.headbash`, `execute.stomp`, `execute.stab`, `execute.smash`, `execute.behead`, `execute.tearlegs`, `execute.tearhead`), sinon `attack.{famille}` ; un tir d'achèvement rejoue `fire.{famille}` | **compté depuis chaque coup** |
| achevé (victime) | `executed.{allure}`, `executed` ; sans animation : immobile, secoué par les coups | **compté depuis chaque coup du bourreau** (même horloge que lui) |
| embuscade, sommeil | `rest` | horloge |
| réveil (veilleur) | `awaken` | depuis le réveil |

La **famille** vient de l'arme tenue : champ `pose` de l'arme (`unarmed`, `club`, `blade`, `pistol`,
`long`, `heavy`…) ; sans ce champ : `unarmed` pour une arme naturelle, `pistol` pour une arme à feu,
`club` pour le reste.

### Le temps « depuis l'impact »

Pour les attaques et les achèvements (côté bourreau comme côté victime), la pose donne un temps négatif
avant le coup, 0 au coup, positif après. Le rendu le cale sur l'image marquée par l'événement `"impact"` : l'image du contact s'affiche
exactement quand la simulation applique le coup, quelle que soit l'arme. Une batte (`hitDelay` 0,06 s) et
un tuyau (0,05 s) partagent la même animation `attack.club` sans décalage.

## Déclarer les animations d'un personnage

Deux façons, combinables, dans `sprites` :

```json
"sprites": {
  "body": "actor.player",
  "animationSet": "actor.player",
  "animations": { "attack.club": "actor.player.attack.special" }
}
```

- `animationSet` : préfixe. La clé `attack.club` cherche l'animation `actor.player.attack.club`.
- `animations` : clé → identifiant d'animation (ou de texture), prioritaire sur le préfixe.

## Décrire une animation

Dans `assets.json` (ou un `assets.*.json` à côté, lu ensuite dans l'ordre des noms) :

```json
"actor.player.attack.club": {
  "texture": "actor.player.attack.club.sheet",
  "frameWidth": 44, "frameHeight": 44, "loop": false,
  "durations": [0.03, 0.025, 0.045, 0.05, 0.06],
  "events": { "2": "impact" },
  "attach": { "weapon": [[-0.8, 10, 128, 0], [6.2, 9.2, 62, 1], [11.2, 2.6, -10, 1], [7, -6.2, -78, 1], [8, 3, -42, 1]] }
}
```

| Champ | Sens |
|---|---|
| `texture`, `frameWidth`, `frameHeight` | planche et taille d'une image ; les images se lisent de gauche à droite, de haut en bas |
| `frames` | indices des images (vide = toute la planche) |
| `durations` | durée de chaque image (sinon `frameDuration` pour toutes) |
| `loop` | boucle ; sinon l'animation reste sur sa dernière image |
| `origin` | point de rotation en pixels (défaut : centre de l'image) = position du personnage |
| `events` | image → nom (`impact`, `footstep`, `thud`) |
| `attach.weapon` | par image : `[x, y, angle°, dessus]` — où la main tient l'arme, en pixels depuis le centre, personnage tourné vers la droite ; `dessus` = 1 si l'arme passe au-dessus du corps |

L'arme tenue est dessinée avec son image « en main » (`heldSprite`, vue de dessus) placée par son point de
prise (`grip`, pixels depuis le centre de l'image) sur le point d'attache, tournée de l'angle de l'image.

Le validateur refuse une animation dont la texture manque, dont le nombre de durées ou de points d'attache
ne correspond pas au nombre d'images, ou dont une durée est nulle. Des tests vérifient aussi que chaque
planche livrée a bien autant d'images que de durées et que chaque attaque ou achèvement marque son impact.

## Fabriquer des planches : le squelette (`tools/PlaceholderArt/Rig`)

Les personnages livrés sont dessinés par un petit **squelette** vu de dessus, original :

- parties en volume (sphères, cylindres arrondis, ellipsoïdes, blocs) décrites par des champs de distance ;
- poses par cinématique inverse à deux os (mains, pieds) avec vecteurs de pôle (coudes, genoux) ;
- rendu par lancer de rayons vertical, lumière en trois bandes, une couleur de palette par pixel, lignes
  de contact quand une partie passe au-dessus d'une autre, contour d'un pixel (pack action) ;
- clips écrits en poses clés (`HumanClips`, `CreatureClips`), avec durées, événements et angle de l'arme.

Le générateur écrit les planches et le manifeste `assets.rig.json` de chaque pack. On peut le remplacer
par des planches dessinées à la main : il suffit de garder les mêmes identifiants (ou d'en déclarer
d'autres dans `assets.json` et `sprites.animations`).
