# Créer un mod

Un mod est un dossier de `Mods/` avec un `manifest.json` et les dossiers de contenu qu'il modifie
(même format que les packs, voir [content.md](content.md)). Exemple complet :
[`Mods/horizon_arsenal`](../Mods/horizon_arsenal).

```json
{ "id": "horizon_arsenal", "name": "Horizon — Arsenal", "version": "1.0.0", "formatVersion": 1,
  "dependencies": ["horizon"] }
```

## Activer un mod

- Menu principal : bouton « Mods » (active ou désactive tous les mods installés).
- Ligne de commande : `--mod horizon_arsenal` (répétable, dans l'ordre de priorité).
- `settings.json` (dossier utilisateur) : `"mods": ["horizon_arsenal"]`.

## Règles de fusion

Le pack de jeu est chargé en premier, puis les mods, chacun après ses dépendances, dans l'ordre
demandé ; les fichiers d'un dossier sont lus par ordre alphabétique. Pour une définition de même
`id` qu'une définition déjà chargée :

| Le mod écrit | Effet |
|---|---|
| quelques propriétés | elles remplacent les anciennes valeurs, le reste est conservé ; les objets imbriqués sont fusionnés, **les tableaux sont remplacés entiers** |
| `"$replace": true` | l'ancienne définition est oubliée, la nouvelle la remplace entièrement |
| `"$delete": true` | la définition est supprimée (les références restantes deviennent des erreurs) |

Les textes et les ressources se fusionnent clé par clé : pour changer l'apparence d'une unité, il
suffit d'associer son identifiant logique (`texture.unit.warden`) à un autre fichier.

## Vérifications

- Un mod qui dépend d'un autre jeu, d'un mod absent, ou d'une dépendance circulaire n'est pas
  chargé ; le motif est écrit dans `log.txt` et compté dans le menu.
- Deux mods qui modifient la même définition sans dépendre l'un de l'autre produisent un
  avertissement : le dernier chargé l'emporte.
- Toutes les références sont vérifiées après chargement (objet, munition, armure, partie de tuile,
  bloc, zone…). Une mission en erreur est désactivée, le reste du jeu reste jouable.

## Ce qui se fait sans toucher au moteur

Ajouter ou modifier des armes, munitions, armures, unités, statistiques, types de dégâts,
parties de tuiles, blocs de carte, terrains, missions, textes, images et sons ; changer les
constantes de combat (`game.json`, section `battle`). Une **nouvelle règle** (un nouveau type
d'objectif, un nouvel effet) demande une extension en C# dans `Game.Core`.
