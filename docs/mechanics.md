# Mécaniques : état de la reproduction d'OpenXcom Extended

Ce document dit, mécanique par mécanique, ce qui est **reproduit**, **partiel** ou **manquant**, et
d'où viennent les règles. Il est tenu à jour à chaque lot.

- ✅ **Reproduit** : règle relue dans les sources d'OXCE (branche `oxce-plus` de
  [MeridianOXC/OpenXcom](https://github.com/MeridianOXC/OpenXcom)), réimplémentée et testée.
- 🟡 **Partiel** : comportement présent mais simplifié ; l'écart est décrit.
- ❌ **Manquant** : pas encore implémenté.

Les sources d'OpenXcom sont sous licence GPL v3. Aucune ligne de code n'a été copiée : les règles
ont été lues, décrites en français, puis réécrites en C# avec une structure propre à ce projet.
Les noms des fonctions OXCE sont cités pour permettre la vérification.

## Lot 1 — combat tactique

### Carte et terrain

| Mécanique | État | Détails |
|---|---|---|
| Grille 3D, tuiles à 4 parties (sol, mur ouest, mur nord, objet) | ✅ | Comme les MCD d'OpenXcom. Les murs sont sur les arêtes. |
| Voxels 16 × 16 × 24 par tuile | ✅ | Tirs et lignes de vue tracés voxel par voxel (Bresenham 3D avec couverture des pas latéraux, `calculateLineVoxel`). |
| Formes des parties | 🟡 | OXCE utilise les gabarits LOFTEMPS (12 tranches de 16 × 16 bits). Ici : boîtes alignées sur les axes (`shapes/`). Couvertures partielles, fenêtres et intérêt de s'agenouiller sont conservés ; les silhouettes fines sont perdues. |
| Unités dans l'espace voxel | 🟡 | Cylindre de rayon 4 voxels, de la hauteur de l'unité (debout / à genou), au lieu des gabarits d'unité. |
| Portes battantes et coulissantes | ✅ | Ouverture en traversant (variante « ouverte » d'une partie, `Alt_MCD`) ; les coulissantes se referment en fin de tour si l'embrasure est libre. |
| Destruction par tir direct | ✅ | `TileEngine::hit` méthode 1 : la tuile reçoit 50–150 % de la puissance × `toTile` (0,5) et perd une couche si ce total atteint son armure. Remplacement par `destroyedInto` (`Die_MCD`). |
| Armure 255 « indestructible » | 🟡 | Ici, `armor: 0` signifie indestructible. |
| Explosions sur le terrain | ✅ | Voir « Grenades et explosions ». |
| Feu et fumée | ❌ | La fumée est déjà prise en compte par la vision si une case en contient ; ni création ni propagation pour l'instant. |
| Chute (gravité) | ❌ | Les sols sont indestructibles dans les packs de démonstration. |

### Déplacements (`Pathfinding::getTUCost`, `UnitWalkBState`)

| Mécanique | État | Détails |
|---|---|---|
| Coût d'un pas | ✅ | UT du sol + UT de l'objet (non compté s'il sert de surface, comme un escalier), 4 si le total est nul, +1 pour monter, × 1,5 tronqué en diagonale, puis moyenne des UT des murs franchis. |
| Énergie | ✅ | 50 % du coût en UT, tronqué (OXCE `moveCost`, marche). |
| Coins en diagonale | ✅ | Bloqué si un des 4 murs du carré 2 × 2 bloque ; un objet ou une unité à côté du coin ne bloque pas. |
| Marches et escaliers | ✅ | Montée de 8 voxels maximum par pas ; depuis une surface ≥ 16, passage sur le sol du niveau supérieur ; descente sur une surface ≥ 12. |
| Rotation | ✅ | 1 UT par huitième de tour sur place ; gratuite pendant la marche ; ne déclenche jamais de tir de réaction. |
| Genou à terre / debout | ✅ | 4 UT / 8 UT ; marcher à genou fait d'abord se relever (compté dans l'aperçu). Déclenche un test de réaction. |
| Exécution pas à pas | ✅ | UT et énergie payées au début de chaque pas ; arrêt sur la dernière case payable, sur une case occupée par une unité non vue (sans dépense), ou quand le camp repère un nouvel ennemi (sans réaction pour ce pas). |
| Recherche de chemin | ✅ | Chemin le moins cher en UT (Dijkstra ; OXCE : A*, même résultat), sans tenir compte de l'énergie ni des UT restantes. Les unités connues sont contournées. |
| Course, pas latéral, furtivité, vol, ascenseurs | ❌ | Options OXCE non reprises. |
| Réserve d'UT du joueur | ❌ | Lot 2. |

### Précision et tir (`BattleUnit::getFiringAccuracy`, `Projectile::applyAccuracy`)

| Mécanique | État | Détails |
|---|---|---|
| Coût des tirs | ✅ | % des UT max (`max(1, UTmax × % / 100)`) ou valeur fixe (`flatTu`) ; un coût nul = mode absent. |
| Précision affichée | ✅ | Tir × précision de l'arme, puis × 115 % à genou, × 80 % pour une arme à deux mains si les deux mains sont occupées, × `max(10, 25 × PV / PVmax + 75 − 10 × blessures)` %. Divisions entières dans l'ordre d'OXCE. |
| Pas de jet de touche | ✅ | Le point visé est dévié (formule anisotrope d'OXCE, plus serrée le long de x que de y), la ligne est prolongée, et le premier voxel touché reçoit l'impact : un raté peut toucher quelqu'un d'autre. |
| Perte de précision avec la distance | ✅ | Au-delà de `range.aimed` (et sous `range.min`), −`dropoff` points par case ; `range.snap`/`range.auto` seulement avec l'option `extendedRangeAccuracy` (comme `battleUFOExtenderAccuracy`). Portée maximale `range.max`. |
| Rafales | ✅ | UT payées une fois, une munition et un tirage de déviation par coup. |
| Ligne de tir du joueur | ✅ | Premier tir refusé sans coût si la ligne droite touche autre chose que la cible. |
| Point visé | 🟡 | Quelques points du corps de la cible (centre puis décalages), au lieu de l'échantillonnage complet de `canTargetUnit`. |
| Mêlée | 🟡 | Vrai jet de pourcentage (mêlée × précision de l'arme, malus de santé) sur une case adjacente, après rotation payante. Esquive (`meleeDodge`) non reprise. |
| Rechargement | ✅ | 15 UT par défaut (`reloadTu`), chargeur porté par l'unité ; chargeur vide jeté. |
| Lancer, grenades, amorçage | 🟡 | Voir « Grenades et explosions ». |

### Tirs de réaction (`TileEngine::checkReactionFire`)

| Mécanique | État | Détails |
|---|---|---|
| Déclencheurs | ✅ | Fin de chaque pas, genou à terre / debout, fin d'une séquence de tir. Jamais une rotation. Seul le camp qui joue déclenche. |
| Qui réagit | ✅ | Unité active d'un camp adverse, à 20 cases au plus, qui voit l'acteur dans son secteur de 90° (ou qui a été touchée par lui ce tour), avec une arme prête. |
| Score | ✅ | Réactions × UT restantes / UT max (réel). Le réacteur tire tant que son score est ≥ celui de l'acteur (égalité au réacteur) ; chaque tir baisse son score de coût × réactions / UT max ; au plus 10 tirs par déclencheur. |
| Type | ✅ | Coup de mêlée si l'acteur est adjacent devant le réacteur, sinon tir rapide uniquement. Les UT sont revérifiées au moment du tir. |
| Arrêt de la marche | ✅ | Toute réaction interrompt la marche de l'acteur. |

### Dégâts, armure, blessures (`BattleUnit::damage`, `RuleDamageType`)

| Mécanique | État | Détails |
|---|---|---|
| Types de dégâts en données | ✅ | Randomisation (`standard` ±100 % par défaut, `explosion` ±50 %, `ufo`, `tftd`, `flat`, `fire`, `none`), efficacité de l'armure, conversions vers santé, armure, étourdissement, blessures, terrain. |
| Résistances d'armure | ✅ | `damageModifiers` en % par type. |
| Face touchée | ✅ | Octant de l'impact comparé à l'orientation ; impacts obliques sur l'avant ou l'arrière 2 fois sur 3 ; dessous si l'impact est sous le centre. |
| Partie du corps | ✅ | Tête au-dessus de la hauteur de l'unité, bras/torse au-dessus de 4 voxels, jambes en dessous. |
| Armure soustraite puis usée | ✅ | Dégâts − armure × efficacité ; l'armure de la face perd `round(dégâts × 0,1)`. |
| Étourdissement, inconscience | ✅ | 25 % aléatoire des dégâts ; inconscient quand étourdissement ≥ santé ; −1 par tour ; réveil sur place. |
| Blessures mortelles | ✅ | 1 à 3 blessures avec probabilité dégâts/11 (toujours à partir de 11) ; −1 PV par blessure et par tour ; malus de précision (tête, bras) et d'UT (jambes, −10 % chacune) ; énergie (torse). Par défaut seuls les soldats du joueur saignent (`woundable`). |
| Mort | ✅ | PV ≤ 0 : l'inventaire tombe au sol avec le corps (objet `corpse`). |
| Difficulté (multiplicateurs d'armure alien) | ❌ | |

### Moral (`BattleUnit::moraleChange`, `checkForCasualties`)

| Mécanique | État | Détails |
|---|---|---|
| Perte en étant blessé | ✅ | `100 × (PV perdus × ((110 − bravoure) × 10 / 100) / 10) / 100`, et −1 par nouvelle blessure. |
| Morts | ✅ | Alliés de la victime : −12 à bravoure 50 (formule OXCE) ; autres camps : +10 ; tueur : +20 (ennemi) ou pénalité (ami, civil). |
| Grades et commandement | ❌ | Tous les modificateurs valent 100 %. |
| Panique et berserk | ❌ | Lot 2 (test de début de tour `100 − 2 × moral`). |

### Vision (`TileEngine::calculateFOV`, `visible`)

| Mécanique | État | Détails |
|---|---|---|
| Secteur de vue | ✅ | 90° selon l'orientation : ±45° en orientation cardinale (cases latérales adjacentes exclues), quadrant entier en diagonale. |
| Portée | ✅ | 20 cases (disque 2D). |
| Terrain | ✅ | Révélé case par case le long de lignes 3D en cases, arrêtées par les murs et objets opaques et par tout sol entre deux niveaux ; règle « deux voies » en diagonale ; on voit l'objet opaque mais pas au-delà. Lumière et fumée sans effet. |
| Unités | ✅ | Rayon voxel de l'œil au corps ; qualité = portée − longueur − coût de la fumée > 0 ; portée de nuit 9 cases pour le joueur (20 pour les hostiles) si la cible est dans l'ombre. |
| Lumière | ✅ | Éclairage « classique » d'OXCE : ambiante, sources du terrain, lumière portée par les unités du joueur (15), décroissance de 1 par case (un niveau = 1,5 case). Sombre si ombre > 9. |
| Brouillard de guerre | ✅ | Jamais vu (noir), déjà vu (assombri), vu maintenant. Un ennemi repéré pendant le tour du joueur reste affiché jusqu'à la fin de ce tour. |
| Mémoire des camps | 🟡 | Comme OXCE, un camp connaît un ennemi vu depuis moins de `intelligence` tours. **Différence voulue** : l'IA utilise la *dernière position vue*, pas la position réelle comme OXCE, pour ne jamais exploiter d'information cachée. |

### Intelligence artificielle (`AIModule`)

| Mécanique | État | Détails |
|---|---|---|
| Tour de l'IA | 🟡 | Jusqu'à 4 décisions par unité et par tour, unité terminée sous 5 UT. |
| Attaque | 🟡 | Cible connue la plus proche et visible (sans secteur, comme OXCE) ; mode de tir selon la distance (règle par défaut d'OXCE : < 4 cases rafale, > 12 visé, sinon rapide) ; genou à terre avant un tir visé ou une rafale si l'unité le peut. |
| Position de tir | 🟡 | `findFirePoint` : case atteignable dans un carré 11 × 11 en gardant les UT du tir, avec ligne de tir ; score 100 − 10 × ennemis qui la voient + UT restantes + 10 hors du secteur de la cible ; acceptée au-delà de 70. |
| Charge au corps à corps | 🟡 | Case libre adjacente à la cible la moins chère, en gardant les UT de la frappe. |
| Enquête | 🟡 | Marche vers la dernière position connue d'un ennemi. |
| Patrouille | 🟡 | Entre les points de patrouille de la carte, en gardant des UT pour la réaction (agressivité 0 : 50 %, 1 : 40 %, 2+ : 33 % des UT max). Les nœuds RMP (rangs, liens, priorités) ne sont pas repris. |
| Repli | 🟡 | Sous `retreatHealthPercent` de santé, vers la case vue par le moins d'ennemis connus puis la plus éloignée. |
| Modes pondérés (patrouille/embuscade/combat/fuite) | ❌ | Remplacés par les priorités ci-dessus. |
| Grenades, psi, mode « triche » des derniers tours | ❌ | L'IA ne lance pas encore de grenades. |

### Tours et missions

| Mécanique | État | Détails |
|---|---|---|
| Ordre des camps | ✅ | Joueur, hostiles, neutres ; le numéro de tour augmente quand le joueur rejoue. |
| Début de tour | ✅ | UT pleines (−10 % par blessure aux jambes), énergie +30 (−10 % de l'énergie courante par blessure au torse), saignement, étourdissement −1, réveil. |
| Victoire, défaite | ✅ | Défaite si plus aucune unité du joueur n'est active ; victoire si tous les hostiles sont hors de combat (toujours, comme OXCE). |
| Objectifs supplémentaires | ✅ | `survive` (tenir N tours), `reachZone` (N unités dans une zone de la carte), limite de tours. |
| Bilan | 🟡 | Survivants, pertes, ennemis neutralisés. Pas encore d'expérience ni de récupération de matériel. |

### Génération des champs de bataille

| Mécanique | État | Détails |
|---|---|---|
| Cartes fixes | ✅ | Un bloc dessiné niveau par niveau (`maps/`). |
| Cartes assemblées par blocs | 🟡 | Grille de blocs (10 × 10 ou multiples) d'un terrain : blocs obligatoires d'abord, puis tirage pondéré avec maximum par bloc, reproductible par la graine. Pas encore de scripts de carte (routes, bâtiments imposés à un endroit). |
| Déploiement | 🟡 | Marqueurs de départ par camp dans les blocs, puis cases libres voisines. |

### Grenades et explosions (`TileEngine::explode`, `detonate`, `BattlescapeGame::endTurn`)

| Mécanique | État | Détails |
|---|---|---|
| Amorçage et minuterie | ✅ | Amorcer coûte 50 % des UT max, lancer 25 %. Une grenade au sol dont la minuterie vaut 0 explose à la fin du tour du joueur ou des hostiles (jamais du tour neutre), puis les minuteries diminuent. Une grenade restée en main n'explose pas (réglage OXCE par défaut). |
| Portée du lancer | ✅ | Simulation d'OXCE (`getMaxThrowDistance`) : vitesse verticale qui baisse tous les 8 voxels de (50 × poids / force) / 100 ; portée horizontale ≤ (distance + 8) / 16 cases. |
| Trajectoire du lancer | 🟡 | Parabole d'une des trois hauteurs (25 %, 50 %, 90 % de la distance) qui atteint la case ; OXCE augmente la courbure par pas de 0,5. Précision : lancer × modificateur de santé, déviation `applyAccuracy` sans prolongement. Le ciel au-dessus du dernier niveau est libre ; un plafond bloque. |
| Propagation | ✅ | Explosions « plates » (réglage par défaut d'OXCE) : rayons tous les 3°, −10 de puissance par case, − 2 × blocage des murs et objets traversés (`explosionBlock`, comme `HE_Block`, règle des deux chemins en diagonale). |
| Dégâts aux unités | ✅ | Une fois par case : dégâts aléatoires (50–150 % pour le type explosif) ; dessous de l'armure sur la case centrale et ses voisines, sinon la face tournée vers l'explosion. Les corps inconscients au sol sont touchés. |
| Objets au sol | ✅ | Détruits si dégâts × `toItem` > 20. |
| Terrain | ✅ | Chaque case garde la plus forte puissance reçue × `toTile` ; chaque partie (objet, murs, murs est/sud, plafond) perd des couches tant que l'énergie couvre son armure. |
| Munitions explosives | ✅ | Une munition dont le type a un rayon explose à l'impact. |
| Explosions en hauteur, chaînes d'explosions du terrain, fumée et feu | ❌ | |
| Grenades de proximité, choix libre de la minuterie | ❌ | Minuterie 0 ou 1 dans l'interface. |

## Lots suivants

| Domaine | État |
|---|---|
| Inventaire | 🟡 Déplacement d'objets entre mains, ceinture, sac et sol avec les coûts en UT des emplacements (`RuleInventory::getCost`) ; panneau en liste, pas encore la grille d'OXCE ; poids et encombrement non appliqués. |
| Feu, fumée (création, propagation, dégâts) | ❌ Algorithmes relevés dans les sources (`prepareNewTurn`). |
| Panique, berserk, états particuliers | ❌ |
| Expérience et progression | ❌ |
| Sauvegarde de bataille et de campagne | ❌ |
| Geoscape (temps, bases, recherche, fabrication, véhicules, interceptions, économie) | ❌ Lot 3. |
| Campagne, événements, encyclopédie | ❌ Lot 4. |

## Pièges relevés dans les sources (à garder en tête)

- Les divisions entières dans l'ordre exact du code changent les résultats (précision : `tir × arme / 100`, puis `× 115 / 100`, puis `× 80 / 100`, puis `× modificateur / 100`).
- `RNG(0..100)` a 101 valeurs ; le pourcentage affiché n'est pas une probabilité de toucher.
- La dispersion des tirs est deux fois plus serrée le long de l'axe x que de l'axe y : c'est voulu.
- La perte de précision avec la distance compte en voxels 3D (un niveau vaut 1,5 case).
- Égalité de score de réaction : le réacteur tire.
- Un pas qui révèle un ennemi ne déclenche pas de tir de réaction.
