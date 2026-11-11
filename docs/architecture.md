# Architecture

## Trois projets

```text
Game.Desktop ──► Game.Core
Game.Tests   ──► Game.Core
tools/PlaceholderArt (générateur de ressources, indépendant)
```

- **Game.Core** : règles et simulation, chargement et validation du contenu. Aucune dépendance à
  MonoGame : tout se teste sans fenêtre.
- **Game.Desktop** : fenêtre, rendu isométrique, interface, entrées, sons. Il lit l'état de la
  simulation et lui envoie des commandes ; il ne modifie jamais l'état lui-même.
- **Game.Tests** : tests xUnit des règles, du chargement, des mods et des traductions.

## Les cinq notions, et où elles vivent

| Notion | Exemple | Où |
|---|---|---|
| Définitions (ce qu'est une chose) | `ItemDefinition`, `TilePartDefinition` | `Core/Definitions`, chargées du JSON, immuables |
| État (ce qui existe) | `Battle`, `BattleUnit`, `BattleItem`, `Tile` | `Core/Battle` |
| Règles (ce qui est permis, comment c'est résolu) | `MovementRules`, `CombatRules`, `DamageRules`, `ReactionFire`, `VisionSystem` | `Core/Battle/*` (classes statiques sans état) |
| Commandes (intentions) | `MoveCommand`, `AttackCommand`, `EndTurnCommand` | `Core/Battle/Commands.cs` |
| Présentation | `BattleRenderer`, `BattleHud`, `BattlePlayback` | `Desktop/Tactical` |

## Déroulement d'une bataille

1. `BattleFactory.Create(règles, mission, graine)` construit la carte (bloc fixe ou terrain généré),
   place les unités et leur équipement, calcule la vision.
2. `Battle.Execute(commande)` **valide** la commande (tour, UT, ligne de tir…) et renvoie
   `CommandResult` (accepté, ou clé de traduction expliquant le refus). Une commande acceptée
   ajoute des actions à une pile.
3. `Battle.Advance()` exécute **un pas atomique** : une case de marche, un tir, un impact. Les
   tirs de réaction sont empilés au-dessus de l'action interrompue, comme la pile d'états
   d'OpenXcom. Chaque pas produit des `BattleEvent` (une simple liste, pas de bus).
4. L'interface anime chaque événement puis demande le pas suivant. Les tests et l'IA appellent
   `RunUntilIdle()`.

Le joueur et l'IA passent par les mêmes commandes et les mêmes règles. L'IA (`AiController`) ne
lit que ce que son camp a le droit de savoir (ennemis vus récemment, à leur dernière position vue).

## Déterminisme

Tous les tirages passent par `GameRandom` (SplitMix64, état sur un entier, bornes incluses comme
`RNG::generate`). Même graine et mêmes commandes donnent exactement la même bataille ; les tests
le vérifient. L'état du générateur est prévu pour être sauvegardé.

## Contenu

`ContentLoader` lit un **pack de jeu** (`Content/<pack>/`) puis les **mods** activés
(`Mods/<mod>/`), dans un ordre déterministe (dépendances d'abord, fichiers triés par nom). Les
définitions de même identifiant sont fusionnées propriété par propriété ; `$replace` et `$delete`
sont explicites. `ContentValidator` vérifie toutes les références et produit des messages qui
citent le fichier ou la définition fautive. Une mission invalide est désactivée, le reste du jeu
reste jouable.

## Choix techniques

- **Pas de pipeline de contenu MonoGame** : textures, sons et polices sont chargés à l'exécution,
  ce qui permet aux mods de remplacer n'importe quel fichier sans recompiler.
- **FontStashSharp** pour les polices TrueType à l'exécution (Unicode complet).
- **Formes voxel en boîtes** plutôt que les gabarits LOFT d'OpenXcom : plus simples à écrire et à
  vérifier, les graphismes de remplacement sont dessinés à partir des mêmes boîtes.
- **Statistiques par identifiant** (`StatBlock`) : un pack peut en déclarer d'autres ; elles sont
  stockées et affichées, et n'ont d'effet que si une règle les lit.
- **JSON strict** : une propriété inconnue (faute de frappe) est une erreur.
