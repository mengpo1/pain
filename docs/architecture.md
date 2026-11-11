# Architecture

## Trois projets, une direction

```text
TopDown.Desktop ──► TopDown.Content ──► TopDown.Core
        └──────────────────────────────────┘
```

- **Core** ne dépend de rien (pas de MonoGame, pas de fichiers). Il utilise `System.Numerics.Vector2`.
  On y teste une attaque, une collision ou une transition d'état sans fenêtre.
- **Content** lit les dossiers, fusionne les packs, valide les références et produit les définitions
  du Core. Il ne décode jamais d'image ni de son : il sait seulement où sont les fichiers.
- **Desktop** affiche, joue les sons, lit le clavier et la souris. Il ne contient aucune règle de jeu.

## Simulation

`GameWorld.Step(dt, commands)` fait avancer un niveau d'un pas fixe (1/120 s, `PlayScreen.Step`).
L'affichage tourne à la fréquence de l'écran et interpole les positions entre les deux derniers pas :
les vitesses ne dépendent jamais du nombre d'images par seconde.

Ordre d'un pas : minuteries et états → commandes du joueur → bruits entendus → IA → attaques de mêlée
→ exécutions → déplacements et collisions → portes → balles → armes lancées → objets ramassés →
déclencheurs → objectifs → score.

Le joueur et les ennemis sont la même classe `Actor` : seule la source des décisions change
(`PlayerCommands` ou `EnemyBrain`). Les règles de combat s'appliquent donc à tous.

### États d'un personnage

`Active` → `Attacking` (mêlée en cours, déplacement possible) → `Active` ; `Stunned` (étourdi debout,
brièvement) ; `KnockedDown` → `GettingUp` → `Active` ; `Executing` (achève une cible) ;
`BeingExecuted` (cible clouée au sol : c'est la **réservation**, personne d'autre ne peut l'achever et
elle ne se relève pas) ; `Dead` (définitif). Les changements passent par des méthodes de `Actor` qui
empêchent les combinaisons impossibles : un achèvement interrompu (exécuteur touché, renversé ou tué)
remet la cible au sol et libère la réservation.

### Combat et achèvement

Une seule règle de ciblage (`CombatSystem.CanDamage`) pour toutes les attaques : un ennemi peut
toujours être blessé, un allié seulement si `rules.friendlyFire` l'autorise pour ce type d'attaque
(mêlée, projectile, arme lancée, explosion, porte). L'achèvement est la même séquence pour tous :
cible éligible (au sol, en train de se relever, ou étourdie si les règles le permettent), proche, sans
obstacle, choisie de façon déterministe (la plus proche, pénalité si elle n'est pas devant) ; puis
coups successifs décrits par l'arme (`execution`), le dernier tue. L'IA utilise exactement ces
fonctions pour achever le joueur ; quand un allié achève déjà la cible, elle attend à proximité.

### Collisions

- Personnages : cercles. Murs : tuiles. Objets solides : rectangles indépendants de leur image.
  Portes : segments épais qui pivotent.
- Chaque tuile, porte ou objet déclare séparément s'il bloque le **déplacement**, les **projectiles**
  ou la **vision** (une vitre bloque le passage, pas le regard).
- Lancers de rayons exacts sur la grille (Amanatides–Woo) : une balle, même très rapide, ne traverse
  jamais un mur.
- **Origine des tirs** : la trajectoire part du centre du tireur, pas du canon. Collé à un mur, on ne
  tire pas à travers ; une cible qui touche le canon est quand même touchée. La mêlée vérifie aussi la
  ligne entre les centres.

### IA

Une seule machine à états (`EnemyBrain`) pour tous les ennemis : `Dormant`, `Idle` (surveillance),
`Patrol`, `Wander`, `Suspicious`, `Investigate`, `Chase` (poursuite et attaque), `Search`, `Return`.
Les définitions changent les paramètres (portée et angle de vision, ouïe, temps de réaction, vitesse,
précision, alerte des alliés) et le comportement au repos. Les chemins (A* sur la grille, sans couper
les coins) ne sont recalculés que si la cible a bougé ou si l'ennemi est bloqué.

### Communication

La simulation produit des `GameEvent` (tir, impact, mort, porte claquée, objectif atteint…). Le
Desktop les lit après chaque pas pour jouer sons, particules et secousses, puis les efface. Pas de bus
d'événements générique : une simple liste.

## Rendu

`WorldRenderer` dessine par couches (sol, décor, sang, ombres, murs, portes, corps, armes, personnages,
balles, effets). `PostProcessor` compose ensuite : carte de lumière multipliée (obscurité + lumières),
teinte globale, parasites vidéo, lignes de balayage, grain, vignette. Tout est piloté par le thème.

## Choix techniques

- **Pas de content pipeline MonoGame** : textures (`Texture2D.FromStream`) et sons (`SoundEffect.FromFile`)
  sont chargés à l'exécution, ce qui permet aux mods de remplacer n'importe quel fichier.
- **FontStashSharp** (seule dépendance ajoutée) : MonoGame ne sait pas charger une police TrueType sans
  son pipeline ; FontStashSharp le fait à l'exécution et couvre l'Unicode.
- **Pas de shaders compilés** : le compilateur d'effets de MonoGame nécessite Wine hors Windows. Les
  effets sont faits avec des modes de mélange et des textures générées.
- **JSON strict** : une propriété inconnue (faute de frappe) est une erreur signalée au chargement.
