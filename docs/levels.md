# Format des niveaux

Un niveau est un fichier `levels/<id>.json` (l'`id` vaut le nom du fichier s'il est absent). La carte est
dessinée en caractères ; tout le reste est une liste d'objets placés en **coordonnées de tuiles** :
`[3, 4]` est le **centre** de la tuile colonne 3, ligne 4 (les décimales sont permises, `[3.5, 4]` est
le bord entre deux tuiles). L'axe Y descend.

```json
{
  "id": "first_night",
  "nameKey": "level.first_night.name",
  "introKey": "level.first_night.intro",
  "tileSize": 32,
  "legend": { "#": "wall_brick", ".": "floor_wood", "=": "glass" },
  "doorSymbols": { "D": { "tile": "floor_wood", "door": "door_wood", "hinge": "start", "locked": false } },
  "map": [
    "##########",
    "#....#...#",
    "#....D...#",
    "#....=...#",
    "##########"
  ],
  "player": { "definition": "player", "position": [2, 2], "facingDegrees": 0, "weaponId": "none" },
  "actors": [
    { "id": "boss", "definition": "guard", "position": [7, 2], "facingDegrees": 180, "weaponId": "shotgun", "tag": "ambush" },
    { "definition": "thug", "position": [7, 1], "behavior": "patrol", "patrol": [[7, 1], [8, 3]] }
  ],
  "weapons": [ { "definition": "bat", "position": [3, 1], "rotationDegrees": 30, "ammo": null } ],
  "doors":   [ { "id": "gate", "definition": "door_metal", "tile": [5, 3], "axis": "vertical", "hinge": "end", "locked": true } ],
  "props":   [ { "sprite": "prop.crate", "position": [3, 3], "size": [1, 1], "blocksMovement": true, "blocksProjectiles": true, "blocksVision": true } ],
  "decor":   [ { "sprite": "prop.car", "position": [1, 1], "rotationDegrees": 0, "scale": 1, "layer": "floor" } ],
  "lights":  [ { "position": [4, 2], "radius": 4, "color": "#ffffff", "intensity": 1, "flicker": 0.3 } ],
  "items":   [ { "id": "briefcase", "sprite": "prop.crate", "nameKey": "item.briefcase.name", "position": [8, 1] } ],
  "interactables": [ { "id": "switch", "sprite": "prop.generator", "promptKey": "prompt.switch", "position": [1, 3], "once": true } ],
  "zones":   { "exit": { "x": 1, "y": 1, "width": 2, "height": 2 } },
  "objectives": [
    { "type": "eliminateAll", "textKey": "objective.eliminateAll" },
    { "type": "reachZone", "zone": "exit", "textKey": "objective.reachCar" }
  ],
  "triggers": [
    { "on": "enterZone", "subject": "exit", "once": true,
      "actions": [ { "type": "alertTag", "tag": "ambush" }, { "type": "message", "textKey": "msg.trap", "duration": 3 } ] }
  ],
  "music": "music.action",
  "ambience": null
}
```

## Carte

- `legend` associe un caractère à une tuile. L'espace est le vide (infranchissable, invisible).
- `doorSymbols` : un caractère qui pose une porte sur une tuile de sol. Son axe est déduit des murs
  voisins (murs à gauche et à droite → porte horizontale). `hinge` : `start` (charnière en haut ou à
  gauche) ou `end`.
- Les portes peuvent aussi être listées dans `doors` (utile pour leur donner un `id`, par exemple pour
  les déverrouiller par un déclencheur).
- L'apparence (`sprite`) et la collision (`size`, `blocks*`) des `props` sont indépendantes. Le `decor`
  n'a jamais de collision.

## Ennemis

`behavior` remplace le comportement au repos de la définition (`guard`, `patrol`, `wander`, `dormant`).
`patrol` liste les points de ronde. `weaponId: "none"` retire l'arme par défaut. `tag` regroupe des
ennemis pour les déclencheurs.

## Objectifs

Ils s'enchaînent dans l'ordre ; le niveau est gagné quand le dernier est rempli.

| `type` | Paramètres | Rempli quand… |
|---|---|---|
| `eliminateAll` | `tag` (facultatif) | tous les ennemis (du tag) sont morts ; un ennemi à terre compte encore |
| `reachZone` | `zone` | le joueur est dans la zone (elle est signalée à l'écran) |
| `collect` | `target` (id d'item) | l'objet est ramassé (en marchant dessus) |
| `survive` | `duration` | le joueur a survécu la durée indiquée |
| `activate` | `target` (id d'interactable) | le joueur l'a utilisé (Espace) |

## Déclencheurs

`on` : `enterZone` (`subject` = zone), `objectiveCompleted` (`subject` = index de l'objectif, `"0"` pour
le premier), `activated` (`subject` = id d'interactable). `once` (vrai par défaut).

Actions : `activateTag` (réveille des ennemis `dormant`), `alertTag` (les lance sur le joueur),
`message` (`textKey`, `duration`), `unlockDoor` (`door`), `playSound` (`sound`).

## Validation

Au chargement, chaque niveau est vérifié : caractères absents de la légende, tuiles, définitions et
armes inconnues, zones et cibles d'objectifs inexistantes, portes hors carte, objets placés dans un mur
ou hors de la carte, tags jamais utilisés. Un niveau en erreur n'est pas proposé dans le menu et la
raison est affichée dans le diagnostic du contenu.
