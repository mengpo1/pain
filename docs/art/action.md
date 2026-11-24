# Charte graphique — pack action (`Content/Base`)

Pixel art net, couleurs saturées, vue strictement de dessus. Tout est original et généré par
`tools/PlaceholderArt` (licence CC0) ; aucune image d'un jeu existant n'a été reprise.

## Échelle et résolution

| Élément | Valeur |
|---|---|
| Unité | 1 pixel de sprite = 1 unité du monde |
| Tuile | 32 × 32 |
| Personnage | rayon de collision 10 ; épaules ≈ 22 px, tête ≈ 10 px ; image 44 × 44 (debout), 52 × 52 (au sol) ; le costaud ×1,3 (56 / 68) |
| Hauteur visible | `camera.viewHeight` 400, arrondi en pixel art au facteur entier le plus proche (360 lignes en 720p et 1080p) |
| Rendu | `post.pixelArt: true` : le monde est dessiné à 1 pixel par unité dans une petite image, puis agrandi d'un facteur entier sans lissage ; le décalage sous-pixel est rendu à l'agrandissement, le défilement reste fluide |
| Filtrage | point (jamais bilinéaire) pour tout ce qui est dans le monde |

## Couleurs et lumière

- Rampe de quatre tons par matière (trait, ombre, base, lumière), avec décalage de teinte : les ombres
  tirent vers le violet, les lumières vers le jaune (`Palette.Ramp`).
- Lumière venant d'en haut à gauche (personnages : relative au personnage ; meubles : fixe).
- Contour d'un pixel `#140c1c` autour de chaque silhouette ; ligne sombre où une partie passe au-dessus
  d'une autre (bras sur le torse, coussins sur un canapé).
- Ombres portées par le thème (`shadow.offset` 3, 4) pour murs et personnages ; biseau des murs
  (`walls` : liseré clair en haut et à gauche, sombre en bas et à droite).
- Sang : trois tons (`#5a0710`, `#870f1c`, `#d0404e`), flaques à bord net.
- Murs fins : bande de 8 unités au milieu de la ligne de tuiles, texture du mur recadrée, liseré et ombre
  sur les faces exposées ; les vitres aussi. Le sol des pièces passe sous la partie libre des tuiles de mur
  et des embrasures ; dehors, c'est le fond.

## Personnages

| Rôle | Silhouette |
|---|---|
| joueur | blouson olive ouvert sur un t-shirt clair, jean, cheveux courts |
| garde | costume blanc, chemise noire, cheveux plaqués |
| voyou | survêtement anthracite à bandes blanches, crâne rasé |
| costaud | débardeur rouge, bras nus, coupe rase, ×1,3 |
| chien | squelette à quatre pattes, pelage brun, museau clair, oreilles tombantes, queue ; image 48 × 48 |

## Règles d'animation

- Le personnage regarde vers +X (droite), son côté droit vers +Y (bas de l'image).
- Les jambes sont une bande à part (`legs`, 8 images) tournée dans le sens du mouvement ; le buste suit
  la visée (comme dans les jeux du genre).
- Marche : 8 images par cycle, 64 unités parcourues par seconde d'animation.
- Attaques : préparation courte (2 images), image de contact marquée `impact`, suite et retour. L'arme
  au contact atteint la portée de l'arme.
- Tirs : recul des mains sur 1 à 2 pixels, l'éclair est un effet du moteur à la bouche du canon.
- Chutes : de face à la personne qui frappe (−X) jusqu'au dos, tête vers +X, en 5 à 6 images ;
  l'image finale est celle du corps au sol et des morceaux démembrés. Le corps touche le sol (`thud`)
  0,05 s après le coup et le relevé est debout en 0,09 s (le costaud : 0,36 s), comme dans la vidéo de
  référence (`docs/analyse-video-hm.md`) : le générateur raccourcit les premières images, pas les poses.
- Morts : `death.shot`, `death.cut`, `death.blunt`, la flaque s'étend sur les 4 dernières images.
- Achèvements : `execute.headbash` (à cheval, tête frappée contre le sol), `execute.stomp`, `execute.stab`,
  `execute.smash` (batte à deux mains), `execute.behead`, `execute.tearlegs`, `execute.tearhead` ; le chien
  `execute.bite` (plonge sur la tête, mord, secoue).
- Victime : `executed` (bras devant le visage, sursaut à chaque coup, spasme, inertie) et `executed.stab`
  (le torse se cambre sous la lame) ; la tête reste en place pour que les blessures dessinées dessus suivent.
- Chien : galop en 8 images, bond et morsure, recul, chute sur le flanc, relevé, mort, réaction à
  l'achèvement ; corps au sol et morceaux arrachés rendus depuis la même pose.

## Ordre d'affichage

Sol → décor au sol (tapis) → zone d'objectif, objets → ombres → murs (+ biseau) → meubles → portes →
corps au sol → armes au sol → personnages debout (jambes puis buste et arme selon `attach`) → projectiles,
effets → décor du dessus → toits → effets additifs.

## Décor

- Sols : parquet en vannerie, carrelage de cuisine, petits carreaux de salle de bain, damier, marbre,
  moquette rouge, trottoir, asphalte, béton, bois, carrelage rose/turquoise.
- Meubles en volume (même rendu que les personnages) : canapés, fauteuil, lit, télé, bureau, chaise,
  billard, bar, frigo, cuisinière, plan de travail, toilettes, baignoire, lampe, plante, borne d'arcade,
  classeur, benne, caisse, table, voiture ; tapis dessinés au pixel.
