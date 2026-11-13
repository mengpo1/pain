# Charte graphique — pack thermique (`Mods/NoctPrototype`, `Mods/NoctInspired`)

Une caméra de surveillance thermique en vol stationnaire, très haut au-dessus d'une installation :
silhouettes chaudes sur un monde froid, grain, halos, structures sombres. Original, généré par
`tools/PlaceholderArt` ; aucune image du jeu qui l'a inspiré n'a été reprise.

## Échelle et caméra

| Élément | Valeur |
|---|---|
| Unité, tuile | comme le pack action (1 px = 1 unité, tuiles 32) |
| Hauteur visible | `viewHeight` 430 (plus loin que l'action) |
| Suivi | `followSharpness` 5 (paresseux), `sway` 6 unités sur 11 s (dérive de drone) |
| Rendu | pas de pixel art strict : le pack peut adoucir (léger flou de 0,25 à 0,4 px, halo), sans effet sur le pack action |

## Température = luminosité (« blanc chaud »)

| Matière | Niveau |
|---|---|
| visages, mains, gueules, yeux des créatures, échappement, bouches d'aération | 0,9 – 1 |
| corps des créatures (selon l'activité) | 0,5 – 0,75 |
| vêtements isolants | 0,55 – 0,74 |
| murs (béton) | 0,46 |
| toits, dalles, asphalte, gravier | 0,17 – 0,38 |
| métal, verre, caoutchouc, eau | 0,11 – 0,32 |

- Rampe de quatre gris par matière : bords plus froids, cœur plus chaud (lumière « venant de l'objectif »).
- Pas de contour d'encre ; un halo blanc doux autour des corps chauds et le grain du capteur.
- Les créatures se refroidissent en mourant (les 4 dernières images de `death`), le veilleur au repos est
  presque froid et se réchauffe en s'éveillant.
- Sang sombre (teinte `#6e6e6e` multipliée), flaques de chaleur qui s'éteignent.

## Créatures

| Créature | Silhouette | Animations |
|---|---|---|
| charognard | quadrupède trapu, crête de vertèbres, membres écartés, gueule chaude | `idle`, `walk` (allure en diagonale), `attack` (se cabre, mord), `hit`, `knockdown`, `getup`, `death`, `rest` |
| bondisseur | maigre, pattes arrière longues sur les doigts, queue | `idle`, `walk` (bonds), `leap.crouch` (se ramasse, tremble), `leap.air` (étiré), `leap.land` (réception, reprise), `attack`, `hit`, `death` |
| veilleur | humanoïde voûté, bras très longs, crâne lisse, yeux chauds | `rest` (recroquevillé, froid), `awaken` (relève la tête, se met à genoux, se dresse, se réchauffe ; `ai.wakeTime` 0,9 s), `idle`, `walk`, `attack` (balayage à deux bras, longue préparation), `hit`, `death` |

## Structures

- Murs épais en béton (plus chauds que le sol), biseau du thème (`walls`).
- **Toits** (`roofs` des niveaux) au-dessus de chaque pièce : membrane ou tôle ondulée, bouches
  d'aération chaudes posées dessus (`decor` de calque `roof`) ; un toit s'efface quand le survivant entre
  et cache l'intérieur sinon.
- Extérieur : asphalte marqué, gravier, terre, clôture grillagée, conteneurs, citernes, tuyauteries
  (eau chaude), groupe électrogène (chaud), fourgon et voiture abandonnés, gravats, pneus, palettes,
  lampadaires.

## Interface de surveillance

Cadre aux coins, `REC` clignotant, horloge, nom de la caméra ; position en grille et barres de liaison
montante sous l'horloge ; crochets autour des créatures qui chassent (« ? » quand elles cherchent),
encoche au bord de l'image pour celles hors champ ; transmissions de l'observateur dans un panneau.
