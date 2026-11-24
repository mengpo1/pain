# Analyse vidéo de la référence et comparaison avec PAIN (livrable A)

Ce rapport décrit ce qu'une vidéo de gameplay du jeu de référence (Hotline Miami, 2012) montre
réellement, ce qu'on a pu y mesurer, et l'écart avec le pack action de PAIN (`Content/Base`). Les
corrections réalisées sont détaillées dans [`review/calibration/README.md`](review/calibration/README.md)
(livrable B).

**Légende de certitude** : **O** = observé directement ; **E** = estimé (mesure indirecte, marge
indiquée) ; **I** = impossible à déterminer avec cette vidéo. Les chiffres sont des **mesures sur une
vidéo compressée**, pas des valeurs officielles du jeu.

## 1. La source

| Caractéristique | Valeur |
|---|---|
| Durée | 14:10.5 (850,5 s) |
| Image | 426 × 240, image utile 376 × 240 (bandes noires à gauche et à droite) |
| Cadence | 29,97 images/s : **une image = 33,4 ms**, c'est la résolution temporelle de toutes les mesures |
| Codecs | AV1, AAC stéréo 44,1 kHz |
| Emplacement | document de travail, **hors du dépôt** (ni la vidéo ni aucune image extraite ne sont publiées) |

La vidéo a pu être lue entièrement (décodeur AV1 de FFmpeg). Rien n'a été déduit de sources extérieures.

**Limites générales**
- À 240 lignes, un personnage fait 10 à 15 px de large : les poses principales se lisent, les images
  intermédiaires d'animation presque jamais.
- La capture à 30 images/s cache tout ce qui dure moins de 33 ms. Le jeu original peut tourner plus vite.
- Les touches pressées par le joueur ne sont pas visibles : quand une action dépend d'une touche (relance,
  achèvement), le délai mesuré inclut le temps de réaction du joueur.
- La musique couvre les bruitages (voir § 4.9).

**Masques choisis.** Le jeu propose avant chaque chapitre un masque qui modifie certaines règles.
- Chapitre 1 : vraisemblablement « Richard », sans effet affiché (00:59–01:00, **E**). C'est la référence de base.
- Chapitre 2 : « Don Juan — LETHAL DOORS » (04:06.9–04:07.5, **O**). Les portes y tuent, ce qui n'est pas le cas de base.
- Chapitre 3 : « Tony — FASTER EXECUTIONS » (08:44.1, **O**). Les achèvements y sont plus rapides qu'en temps normal.

Les durées d'achèvement sont donc tirées du chapitre 1, et le comportement des portes des chapitres 1 et 3.

## 2. Méthode

- **Images exactes.** FFmpeg décode chaque image à son horodatage. Un recalage contre un décodage
  complet depuis le début du fichier montre au plus une image d'écart. Les timecodes sont donc précis à
  ±1 image (±33 ms).
- **Planches.** Pour chaque séquence étudiée : planches légendées de 2 à 30 images/s et agrandissements
  ×6 à ×14 avec une grille de pixels.
- **Mouvements de caméra.** Corrélation de phase (FFT) d'une image à la suivante, bandeau du HUD exclu.
  On obtient le défilement en px/image.
- **Vitesse du joueur.** On suit sa position à l'écran (centroïde des pixels clairs) et on y ajoute le
  défilement de la caméra. Le bruit est d'environ ±1 px par image.
- **Unité commune.** Toutes les distances sont rapportées à la **largeur d'un personnage**, mesurée dans
  la même vidéo. On compare aussi en **hauteurs d'écran par seconde**, ce que le joueur perçoit.
- **Catalogue.** Les trois chapitres ont été catalogués événement par événement, à 2 images/s puis à
  10–30 images/s autour de chaque événement. Chaque timecode cité a été relevé sur ces planches.
- **Audio.** Recherche d'attaques sonores par flux spectral au-dessus de 1,5 kHz (voir § 4.9).

## 3. Structure de la vidéo

| Fenêtre | Contenu | Utilisé pour |
|---|---|---|
| 00:00–00:58 | introduction, chapitre 1, choix du masque | masques |
| 00:58–02:46 | chapitre 1 « No Talk » : 5 essais, 4 morts du joueur | achèvements, poings, fusil à pompe, arme automatique, IA |
| 02:46–04:08 | fin de chapitre, scores, cinématique, chapitre 2, masque | — |
| 04:08–07:12 | chapitre 2 « Overdose » : 14 morts du joueur, puis réussite | déplacements, caméra, portes (masque), tirs ennemis, IA |
| 07:12–08:43 | scores, cinématiques, chapitre 3, masque | — |
| 08:43–12:52 | chapitre 3 « Decadence » : 6 morts, achèvements (masque), retour à la voiture | lancers, armes à feu, relevés, portes |
| 12:52–14:10 | scores, nouvelle arme, cinématique, fin | — |

## 4. Observations et mesures

### 4.1 Échelle

| Mesure | Valeur | Où | Cert. |
|---|---|---|---|
| Largeur d'un personnage debout | 10–15 px (≈ 12) | 04:17.0, 05:55.8, 10:10.0 | E |
| Personnage couché (longueur) | ≈ 23 px | chapitre 1 | E |
| Épaisseur des murs (double trait clair) | 2–5 px, soit 0,2–0,45 largeur de personnage | 04:17.0, 10:10.0 | E |
| Hauteur visible | 240 px, soit 16–24 largeurs de personnage | toute la vidéo | O / E |

Dans PAIN, un personnage a des épaules de 22 unités, les murs occupent une tuile de 32 unités (1,45
largeur), et la vue montre 360 lignes en 720p et en 1080p (16,4 largeurs).

### 4.2 Déplacements

| Mesure | Valeur | Où | Cert. |
|---|---|---|---|
| Vitesse de course du joueur | 5,7–6,2 px/image en suivi direct ; 4,2–5,8 px/image (moyenne 5,26) sur 15 fenêtres de défilement régulier. Soit **155–185 px/s**, **0,65–0,77 hauteur d'écran/s**, 10–18 largeurs/s | 05:56.00–05:56.77 ; fenêtres de 0,8 s des trois chapitres | E |
| Mise en mouvement | 0 → pleine vitesse en **1 à 2 images** | 06:00.038–06:00.138 ; 06:03.374–06:03.441 | O (±1 px) |
| Arrêt | pleine vitesse → arrêt en **≤ 2 images** (6,2 → 2,6 → 0,9 px) | 05:57.502–05:57.569 | O |
| Changement de direction | demi-tour en 2–3 images ; une courbe progressive sur 4 images | 05:56.80–05:57.00 ; 05:57.27–05:57.40 | O |
| Inertie | aucune au-delà de 2 images | idem | O |
| Diagonales | 2 fenêtres seulement (5,6 et 6,8 px/image contre 5,26 dans l'axe) : impossible de dire si la diagonale est normalisée | 01:53.1 ; 11:04.0 | I |
| Rotation | le buste suit le réticule ; latence non mesurable à 30 images/s | toute la vidéo | O / I |
| Déplacement pendant une attaque | le joueur repart dans les 2 images qui suivent un coup | chapitre 3 | O |
| Glissement le long des murs | non mesuré | — | I |
| Patrouille ennemie | ≈ 50 px/s, soit 4–5 largeurs/s | 02:07.3–02:07.8 | E |

**Conclusion.** Le mouvement est **instantané**, ni accéléré ni amorti de façon visible. La nervosité ne
vient pas d'une accélération particulière, mais de la vitesse rapportée à l'écran, de la caméra qui se
laisse distancer (§ 4.3) et de la brièveté de toutes les transitions de combat (§ 4.4–4.6).

### 4.3 Caméra

| Mesure | Valeur | Où | Cert. |
|---|---|---|---|
| Suivi | le joueur prend 15–25 px d'avance sur le centre dans le sens de la course (6–10 % de la hauteur d'écran) ; la caméra atteint sa vitesse en 0,25–0,37 s | 05:55.73–05:56.00 ; 06:03.37–06:03.74 | E |
| Après l'arrêt | la caméra continue 0,25–0,6 s en ralentissant | 05:57.53–05:58.10 ; 06:06.87–06:07.24 | E |
| Anticipation vers le curseur | la vue se déplace de 22–26 px (≈ 10 % de l'écran) alors que le joueur est immobile | 02:18.33–02:18.67 ; 07:06.98–07:07.38 | E (probable) |
| Secousses | **aucune** sur les coups d'achèvement (0 px mesuré) ; ±1 px ambigus ailleurs | 01:41.07, 02:09.9, 02:28.9 ; 04:17.383, 06:16.100, 06:48.050 | O / E |
| Rotation de la vue | inclinaisons de quelques degrés par moments | 01:12.47, 01:51.0, 02:32.4, 11:20.4–11:20.6, 12:28.0 | O |
| Zoom | aucun changement | toute la vidéo | O |

### 4.4 Combats

**À mains nues (chapitre 1)**
- Le bras reste tendu 3 à 7 images, puis l'ennemi apparaît au sol, renversé mais vivant : 02:09.06–02:09.162
  et 01:12.18–01:12.406 (**O**). La préparation n'est pas lisible.
- L'ennemi passe de debout à couché **d'une image à la suivante**, sans chute intermédiaire visible
  (01:12.406, 08:49.500, **O**).
- Un ennemi renversé et non achevé se relève après **2,1–3,65 s** (02:17.404 → 02:21.05 ;
  10:11.583 → 10:13.70 ; 11:32.6 → 11:35.9, **O**). Le relevé prend **2–3 images** : couché, assis, debout
  à 10:13.667–10:13.734 et à 11:35.867–11:35.967 (**O**, deux exemples).
- Avec le masque du chapitre 3, les poings tuent net. Rien n'indique si c'est l'effet du masque (**I**).

**Armes de mêlée**
- Une seule catégorie est identifiable : une arme longue et fine, dont le type précis reste illisible à 240p (**I**).
- Un coup tue. Le corps est **projeté d'environ une largeur de personnage en 2–3 images** (06:23.433→.567,
  **O**), souvent jusqu'au mur, où il laisse du sang (01:45.672, 02:21.708, **O**).
- Du contact au retour au repos : ≈ 7 images (0,23 s). L'arme reste tendue 3–5 images (04:17.333→.567,
  04:24.200→.300, **O**).
- Les différences entre armes ne sont pas observables (**I**).

**Armes à feu**
- **Arme automatique de 24 coups : un tir toutes les 2 images, soit 15 tirs/s.** Le compteur passe de 24
  à 19 de 02:41.828 à 02:42.095, et de même à 10:21.367–10:22.034 (**O**). Les ennemis tirent au même
  rythme (04:18.07–04:18.304, **O**).
- **Fusil à pompe de 6 coups : 15–16 images entre deux tirs (0,50–0,53 s)** : 01:49.543, 01:50.077, 01:50.577 (**O**).
- Traçantes jaunes ou cyan ; flash jaune net seulement au fusil à pompe (10:08.817). Ailleurs, flash et
  recul ne sont pas lisibles (**O / I**).
- Corps touchés projetés de **15–40 px (1,2–3,5 largeurs)** avec une traînée de sang (10:08.85, 10:30.8, **E**).
- Les vitres éclatent au passage des balles (04:18.07, 04:59.57, 05:09.13, **O**).
- Les traçantes traversent ≈ 130 px en 4–5 images (02:41.8). On ne peut pas savoir si la traçante suit la
  balle ou n'est qu'un effet visuel (**I**).

**Armes lancées**
- Vol de 2–3 images. Mesure image par image : 12–14 px/image, soit 360–420 px/s ou 30–38 largeurs/s
  (10:53.047–10:53.113, **O / E**). Même ordre de grandeur à 08:56.683–08:56.784 et 02:27.781–02:27.881.
- L'ennemi touché est renversé, puis presque toujours achevé (**O**).
- L'écran affiche « NO GUN! » au moment du lancer ; l'arme rebondit ou glisse ensuite (**O**).

**Portes**
- Ouverture en 2–3 images (08:49.37–08:49.47, **O**).
- L'ennemi derrière la porte est renversé 1 à 2 images plus tard (01:20.647, 09:08.017→.050,
  10:08.683, **O**). Il ne meurt qu'avec le masque du chapitre 2.

### 4.5 Achèvements (chapitre 1, masque sans effet affiché)

| Question | Réponse | Cert. |
|---|---|---|
| 1. Quand un ennemi tombe | après un coup de poing, une porte ou une arme lancée. Les armes de mêlée et à feu tuent directement | O |
| 2. Déclenchement | le joueur rejoint le corps ; la touche ne se voit pas. La montée a lieu 11–23 images après la chute | O / I |
| 3. Positions | joueur à califourchon sur le buste, jambes de part et d'autre ; victime sur le dos | O (01:41.0) |
| 4. Animation | coups à la tête, à peine lisibles à 240p | O / I |
| 5. Nombre de frappes | **rythme très régulier : une frappe toutes les ≈ 10 images (0,30–0,33 s)**, mort à la 3e ou 4e frappe visible. Montée → mort : 28, 27, 20 et 15 images, **soit 0,50–0,93 s** | O : 01:13.51–01:14.44, 01:21.01–01:21.92, 01:40.73–01:41.40, 02:09.66–02:10.20 |
| 6. Réaction de la victime | petites variations de la tête jusqu'à la mort, puis sang projeté sur le mur | O |
| 7. Mort | sur la dernière frappe, avec l'affichage « +600 » | O |
| 8. Reprise du contrôle | 1–13 images après la mort | E |

- **Achèvements avec une arme** : aucun n'est clairement visible avec le masque de base (**I**).
- **Chapitre 3 (masque « faster executions »)** : 0,25–0,85 s de la montée à la mort. Ces durées ne servent pas de référence.

### 4.6 Animations

Ce qui se lit à 240p et 30 images/s :
- les poses clés : debout, bras tendu, arme tendue, au sol bras écartés, à califourchon ;
- des transitions très courtes : chute en ≤ 1 image, relevé en 2–3 images, retour au repos après un coup en ≈ 7 images.

Les cycles de marche, les anticipations et les déformations ne sont pas mesurables (**I**). L'ensemble
confirme que les animations ne retardent jamais l'action : on voit le résultat d'un coup sur l'image
suivante.

### 4.7 Intelligence artificielle

| Comportement | Observation | Où | Cert. |
|---|---|---|---|
| Réaction | se retourne 2 images après la mort d'un allié et tue 4 images plus tard ; un autre se retourne et frappe en 2 images | 01:31.06–01:31.19 ; 02:01.76–02:01.82 | O |
| Tireurs | touchent le joueur 0,13–0,5 s après qu'il s'arrête sur un corps, souvent à travers une vitre | 04:18.07, 04:25.40, 04:44.1, 04:59.47 | O |
| Patrouilles | longent les murs ; des patrouilleurs ignorent le joueur hors de vue ; un ennemi assis ne réagit pas | 04:17, 04:43, 05:15 ; 11:11.5–11:19.5 ; 02:32.4 | O |
| Groupes | deux ou trois ennemis convergent | 05:09.5–05:10.6, 05:53, 08:58, 09:59 | O |
| Poursuite, portes | un tireur suit le joueur sur deux pièces ; les ennemis franchissent une porte l'un après l'autre | 09:41.5–09:43.3 ; 10:23.5, 10:30.5 | O |
| Après la mort du joueur | le tueur s'éloigne | 09:43.47 | O |
| Achever le joueur à terre | jamais visible : le joueur meurt toujours d'un coup | — | I |
| Coups entre ennemis | jamais visible | — | I |

### 4.8 Environnements

- Murs fins à double trait clair, portes en planches, vitres qui éclatent.
- Mobilier dense, tapis, sang et corps qui restent jusqu'à la fin du niveau.
- Autour du bâtiment, un fond uni dont la couleur change lentement (sarcelle, violet, magenta).
- Le seul élément destructible observé est la vitre (**O**).
- Les murs ne semblent pas être des tuiles pleines : ils sont plus fins qu'un personnage (**E**).

### 4.9 Interface, effets, son, mort et relance

| Élément | Observation | Où | Cert. |
|---|---|---|---|
| Score | en haut à droite (« NNNNpts »), défile sur 8–20 images | partout | O |
| Munitions | en bas à gauche (« 24/24rnds », « NO GUN! ») | partout | O |
| Points à l'impact | « +N PTS » à l'endroit de la victime, couleurs qui changent toutes les 2–4 images (cyan, blanc, rose), ≈ 1–1,3 s. Aussi sur les renversements | 01:12.406, 05:25.034, 10:53.147… | O |
| Combo | « 2X COMBO +700 » ≈ 2 s après le dernier kill | 05:20.7, 07:09.0 | O |
| Flash du fond | le fond autour du niveau alterne avec un rouge vif **une image sur deux pendant 6–10 images**. Vu sur des portes, des tirs et certains coups de mêlée, pas sur les coups de poing ni sur les achèvements. La règle exacte est inconnue | 01:20.647, 01:45.672, 01:49.543, 02:32.419, 04:09.84, 11:24.5 | O / I |
| Mort | « YOU'RE DEAD! » et « R TO RESTART! » une image après la mort | 01:31.191, 04:10.833 | O |
| Relance | fondu au noir de 0,67–1,0 s, retour à l'entrée de l'étage | 01:33.56–01:34.53, 04:11.770–04:12.438 | O |
| Son | les attaques sonores suivent surtout la musique (≈ une toutes les 0,27 s). Une seule coïncide avec un impact (12:33.528) | — | I |

La synchronisation entre son et image ne peut pas être établie avec cette vidéo.

## 5. Comparaison avec PAIN (avant corrections)

Les valeurs « PAIN » sont celles du commit de départ 2285c08. Le type de problème figure en tête de
la colonne « Écart » : **Réglage**, **Logique**, **Animation**, **Graphisme**, **Effet**, **Son**.
Les mesures PAIN viennent des tests `tests/TopDown.Tests/ReferenceCalibrationTests.cs` ou du jeu
enregistré (§ 6).

| Élément | Observation vidéo | Comportement PAIN | Écart | Priorité |
|---|---|---|---|---|
| Vitesse de course | 0,65–0,77 hauteur d'écran/s (05:56.0–05:56.77) | 215 u/s = **0,597** hauteur d'écran/s, aussi mesuré dans le jeu enregistré (`characters/player.json`) | **Réglage** : course 15–25 % trop lente à l'écran | Haute |
| Départ et arrêt | ≤ 2 images (06:03.37, 05:57.50) | 1 image (0,033 s) : accélération et freinage à 9000 u/s² (`Physics.MoveActor`) | aucun | — |
| Suivi de caméra | retard de 6–10 % de l'écran, rattrapage en 0,25–0,37 s (05:55.73, 06:03.37) | `followSharpness` 16 : retard de 3,7 %, rattrapage en 0,19 s (`game.json`, `Camera2D.Update`) | **Réglage** : caméra trop raide, la course se sent moins | Haute |
| Anticipation vers la visée | ≈ 10 % de l'écran (02:18.33, **E**) | jusqu'à 90 unités, soit 25 % de l'écran (`lookAhead`, `lookAheadMax`) | distance du curseur inconnue dans la vidéo : non comparable | — |
| Cadence de l'arme automatique | 15 tirs/s (02:41.828–02:42.095) | fusil : `fireRate` 8. De plus, une cadence d'un nombre entier de pas perd un pas à l'arrondi : 13,3 au lieu de 15 (`Actor.TickState`) | **Réglage** et **Logique** | Haute |
| Cadence du fusil à pompe | 0,50–0,53 s (01:49.543–01:50.577) | 0,8 s (`fireRate` 1,25) | **Réglage** | Haute |
| Corps projetés | 1 largeur en mêlée (06:23.43), 1,2–3,5 largeurs au tir (10:08.85) | batte 0,14, tuyau 0,11, pistolet et fusil 0,06, pompe 0,35 largeur (`Combat.Kill`, `knockback` des armes) | **Réglage** : les coups ne « portent » pas | Haute |
| Chute | debout → couché en ≤ 1 image (01:12.406, 08:49.500) | au sol (`thud`) à 0,14–0,18 s, soit 4–5 images (`assets.rig.json`, `HumanClips`) | **Animation** : la chute retarde la lecture du coup | Haute |
| Temps au sol | 2,1–3,65 s (02:17.4, 10:11.6, 11:32.6) | 2,2 (poings), 2,5 (lancers), 2,6–3 s (portes) | aucun | — |
| Relevé | 2–3 images (10:13.667–.734, 11:35.867–.967) | 0,4 s (`getUpDuration` par défaut), animation de 0,56 s | **Réglage** et **Animation** | Moyenne |
| Achèvement à mains nues | montée → mort en 0,50–0,93 s, une frappe toutes les 0,30–0,33 s (chapitre 1) | **1,83 s** sans clic, **0,36 s** en martelant (`natural.json` : `windUp` 0,1, `strikeInterval` 0,12, `autoStrikeDelay` 0,6) | **Réglage** : trop lent sans clic, trop rapide en martelant | Haute |
| Achèvement avec une arme | non observable (**I**) | une ou deux frappes selon l'arme | non comparable | — |
| Reprise après achèvement | 1–13 images | `recovery` 0,10–0,14 s | aucun | — |
| Lancers | 360–420 px/s, environ 660–840 u/s selon la largeur retenue (10:53.05) | 720–900 u/s (`throw.speed`) | aucun mesurable | — |
| Ouverture des portes | 2–3 images (08:49.37–.47) | 0,142 s, soit 4,2 images, pour 85° (`door_wood.pushSpeedDegrees` 620) | **Réglage** | Moyenne |
| Renversement par une porte | 1–2 images, l'ennemi survit (chapitres 1 et 3) | renversement de 2,6 s (`doorsKnockDown`) | aucun | — |
| Réaction de l'IA | 2–6 images en mêlée ; tir 0,13–0,5 s (01:31.06, 02:01.76, 04:18.07) | voyou 0,25 s (frappe à mi-délai, 0,125 s) ; garde 0,32 s (`humans.json`, `EnemyBrain`) | dans la plage observée | — |
| Patrouille | ≈ 4–5 largeurs/s (02:07.3, **E**) | 85–90 u/s, soit 3,9–4,1 largeurs/s | dans la marge | — |
| Groupes, portes, poursuite | convergence, portes franchies l'une après l'autre (05:09.5, 10:23.5) | contournement des alliés, attente pendant l'achèvement d'un autre, chemins par les portes (`EnemyBrain.SpreadFromAllies`, `UpdateDownedTarget`) | aucun visible | — |
| Achever le joueur, coups entre ennemis | non observable (**I**) | règles `enemiesCanExecute` et `friendlyFire.melee: false`, testées (`GroupFightTests`) | non comparable | — |
| Flash du fond | rouge une image sur deux pendant 6–10 images sur certains kills (01:20.647…) | aucun (`PlayScreen.Draw`) | **Effet** absent | Moyenne |
| Points à l'impact | « +N » à l'endroit de la victime, couleurs changeantes, ≈ 1 s | total et combo dans le HUD seulement (`Hud.DrawScore`) | **Effet** absent | Basse |
| Secousse aux achèvements | aucune (01:41.07, 02:09.9, 02:28.9) | `executionStrike` 0,4 et `executionFinish` 0,75 avec à-coup (`game.json` → `feel`), demandés lors d'une mission précédente | **Réglage** : décision à prendre | À valider |
| Épaisseur des murs | 0,2–0,45 largeur (04:17.0) | tuile pleine de 32 u, soit 1,45 largeur (`TileMap`, `Physics.ResolveStatic`, `WorldRenderer.DrawTiles`) | **Graphisme** et **Collision** : architecture des murs | Moyenne |
| Champ de vue | 16–24 largeurs de haut | 16,4 (360 lignes, arrondi entier du pixel art) | **Graphisme** : au plus 25 % plus serré, contraint par l'échelle entière | Basse |
| Rotation de la vue | quelques degrés par moments (01:12.47…) | aucune | **Effet** absent | Basse |
| Fond qui change de couleur | lent cycle de couleur | fond fixe (`themes/neon.json`) | **Effet** absent | Basse |
| Traçantes | ≈ 870 px/s (02:41.8, **I**) | balles de 2000–2600 u/s | non comparable | — |
| Écran de mort | texte une image après la mort | texte « VOUS ÊTES MORT » (`Hud.DrawDeath`) | aucun | — |
| Relance | fondu de 0,67–1,0 s | instantanée (`PlayScreen.Restart`) | PAIN plus rapide, conservé | — |
| Son | synchronisation non mesurable (**I**) | sons sur les événements de la simulation | non comparable | — |

## 6. Avant et après dans le jeu réel

Mesures prises dans le jeu enregistré : 1280 × 720, pixels ×2, pilote automatique. Scènes de capture
reproductibles décrites dans le livrable B.

| Grandeur | Référence | PAIN avant | PAIN après |
|---|---|---|---|
| Défilement en course (hauteurs d'écran/s) | 0,65–0,77 | 0,597 | **0,694** |
| Décalage du joueur au centre en courant vers une cible lointaine (avance de visée moins retard) | non comparable | −21,6 % | −17,6 % (retard de 7,7 % au lieu de 3,7 %) |
| Achèvement à mains nues, montée → mort | 0,50–0,93 s | 1,83 s / 0,36 s | **0,93 s / 0,63 s** (sans clic / en martelant) |
| Arme automatique | 15/s | 8/s (13,3 si réglée à 15, à cause de l'arrondi) | **15/s** |
| Fusil à pompe | 0,50–0,53 s | 0,80 s | **0,53 s** |
| Corps projeté (largeurs) | ≈ 1 en mêlée ; 1,2–3,5 au tir | 0,06–0,35 | batte 0,95, tuyau 0,87, pistolet et fusil 1,80, pompe 2,76 |
| Contact au sol après un coup | ≤ 1 image | 0,14–0,18 s | **0,05 s** |
| Relevé | 2–3 images | 0,40 s | **0,10 s** (une image de 1/120 s d'arrondi en plus) |
| Ouverture d'une porte poussée | 2–3 images | 0,142 s | **≈ 0,085 s** |
| Épaisseur des murs (largeurs de personnage) | 0,2–0,45 | 1,45 (tuile pleine) | **0,36** (bande de 8 unités) |

## 7. Plan priorisé

| # | Lot | Contenu | État |
|---|---|---|---|
| 1 | Réactivité, déplacements | vitesse de course, suivi de caméra | **fait** (JSON) |
| 2 | Dynamique des combats | cadences automatique et pompe, arrondi des temps de recharge, projection des corps, ouverture des portes | **fait** (JSON et 1 correction de logique) |
| 3 | Animations d'attaque et d'impact | chute en 0,05 s, relevé en 0,1 s (générateur et manifeste) | **fait** |
| 4 | Achèvements | rythme à mains nues de 0,2–0,3 s entre frappes | **fait** (JSON) |
| 5 | Ennemis | réaction, patrouille, groupes : dans les plages observées | rien à corriger d'après la vidéo |
| 6 | Collisions | murs fins de 8 unités : collisions, tirs, vue et chemins sur la même forme | **fait** (moteur et JSON) |
| 7 | Fidélité graphique | murs fins dessinés (texture recadrée, liseré, sol des pièces dessous) ; champ de vue (non fait) | **fait** pour les murs |
| 8 | Caméra et effets | flash du fond, points à l'impact (**faits**) ; secousse aux achèvements (à décider) ; rotation de la vue, cycle de couleur du fond (proposés) | en partie |
| 9 | Son | rien de mesurable | — |

Les packs thermiques (`noct`, `signal`) gardent exactement leurs réglages. Les valeurs modifiées dans le
pack de base y sont figées, et un test compare toutes les valeurs qu'ils chargent à celles d'avant
l'étalonnage.

## 8. Ce qui reste et pourquoi

- **Murs fins** : faits ensuite (livrable B, § 11). Il reste :
  - les meubles posés contre un mur, décollés de 12 unités ;
  - le chemin de l'IA, qui ne passe pas par la partie libre des tuiles de mur (prudent, sans effet visible) ;
  - les niveaux à revoir pour profiter de la place gagnée.
- **Secousse aux achèvements.** La vidéo n'en montre aucune. **Décision : on la garde**, comme demandé
  auparavant. Pour s'aligner sur la vidéo, il suffirait de mettre `shake` et `punch` à 0 dans
  `feel.executionStrike` et `feel.executionFinish`.
- **Champ de vue.** Au plus 25 % plus serré que la référence. L'échelle entière du pixel art ne laisse que
  360 ou 540 lignes en 1080p.
- **Points sur les renversements**, combo affiché à la fin de la série, fond qui change de couleur,
  inclinaison de la vue : faisables, de priorité basse.
- **Ce qui ne s'établit pas avec cette vidéo** :
  - les diagonales ;
  - la latence de la visée ;
  - les achèvements armés ;
  - les ennemis qui achèvent le joueur ;
  - la différence entre armes de mêlée ;
  - la nature des traçantes ;
  - la synchronisation du son.

  Il faudrait une autre source pour en décider.
