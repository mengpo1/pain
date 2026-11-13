# Revue — fidélité visuelle et animations

**Statut : proposé pour revue indépendante. Non validé, non fusionné.**
Ce document accompagne la pull request en brouillon mengpo1/pain#2 (branche `claude/youthful-noether-typ76w`
vers `main`). Il ne se déclare pas validé : l'appréciation revient à l'orchestrateur et au propriétaire.

## 1. Objet et provenance

- **Dépôt et branche** : `mengpo1/pain`, branche `claude/youthful-noether-typ76w`, état revu `56f552e`.
  La PR part de `main`, un commit initial vide : son diff contient donc tout le projet.
- **Mission revue ici** : fidélité visuelle et animations, du commit `dfcc5cc` (fin du lot « Prototype
  Noct ») au commit `56f552e`. Les lots précédents (moteur, combat, IA, mods, prototype Noct) figurent dans
  l'historique de la même PR.
- **Règles respectées** : aucun code, sprite, son ou donnée de Hotline Miami ou de Noct ; tout le contenu
  est original et généré par `tools/PlaceholderArt` (licence CC0, voir `LICENSE.md` de chaque pack).

## 2. Fonctionnalités implémentées (périmètre de la mission visuelle)

### 2.1 Système d'animation commun, piloté par les données (`docs/animation.md`)

- `TopDown.Core/Presentation/ActorPose.cs` (lecture seule) choisit, pour chaque personnage, une liste de
  clés de la plus précise à la plus générale, et un temps :
  - `idle.{famille}`, `walk.{famille}` (temps = distance parcourue / 64) ;
  - `attack.{famille}(.alt)`, `fire.{famille}` ;
  - `hurt`, `hit`, `knockdown`, `getup` ;
  - `death.shot|cut|blunt` selon le dernier type de dégât, `crawl` ;
  - `leap.crouch|air|land` ;
  - `rest`, `awaken` ;
  - `execute.{allure}` (bourreau), `executed.{allure}` (victime).
- Le rendu prend la première clé que le personnage sait jouer ; sinon il garde l'ancien rendu.
- **Synchronisation animation / impact** : les attaques et achèvements sont comptés depuis le coup. Le rendu
  cale ce temps sur l'image marquée `"impact"` : l'image du contact s'affiche à l'instant où la simulation
  applique le coup (`hitDelay`), quelle que soit l'arme. La victime d'un achèvement suit la même horloge que
  son bourreau.
- **Séparation** : une animation n'applique jamais un dégât et ne change jamais un état ; un test vérifie
  qu'interroger les poses ne modifie pas une partie.
- **Données** (aucune modification C# pour remplacer une planche) :
  - planches ;
  - `durations` par image, `loop` ;
  - `origin` ;
  - `events` (`impact`, `footstep`, `thud`) ;
  - `attach.weapon` par image (`[x, y, angle°, dessus]`).
- Côté personnages : `sprites.animationSet` (préfixe) et `sprites.animations` (clé → animation).
- Côté armes : `pose` (famille de gestes), `heldSprite` (image en main, vue de dessus), `grip` (point tenu).
- Les manifestes `assets.*.json` sont lus après `assets.json`.
- Le validateur refuse :
  - une texture manquante ;
  - un nombre de durées ou de points d'attache différent du nombre d'images ;
  - une durée nulle ;
  - un jeu d'animations vide.

### 2.2 Pack d'action (inspiré des mécaniques de Hotline Miami)

- **Personnages** : joueur, garde, voyou et costaud redessinés par un squelette original (`tools/PlaceholderArt/Rig`).
  - Rendu : parties en volume (champs de distance), cinématique inverse à deux os, lumière en trois bandes,
    une couleur de palette par pixel, lignes de contact, contour d'un pixel.
  - Clips par personnage :
    - marche en 8 images, avec les jambes dans une bande à part orientée selon le déplacement ;
    - attente par famille d'arme (mains nues, batte, lame, pistolet, arme longue) ;
    - poing alterné droite / gauche ; swing de batte aller puis retour ; taillade et estoc ;
    - double coup de poing du costaud ;
    - tirs au pistolet et à l'arme longue, avec recul ;
    - étourdissement ; chute ; relevé ; reptation du blessé ;
    - morts par balle, par lame et par choc ;
    - sept achèvements : à cheval tête frappée, piétinement, poignard à cheval, batte, machette, arracher
      les pattes, arracher la tête ;
    - réactions de la victime.
- **Chien** : squelette à quatre pattes avec galop, morsure en bond, recul, chute sur le flanc, relevé, mort,
  morsure d'achèvement et réaction quand on l'achève. Le corps au sol et les morceaux arrachés partent de la
  même pose.
- **Armes tenues** : dix images vues de dessus avec point de prise ; les images de profil restent pour le sol.
- **Rendu net** : `post.pixelArt` dessine le monde à un pixel par unité puis l'agrandit d'un facteur entier
  sans lissage. Le décalage sous-pixel est rendu à l'agrandissement, donc le défilement reste fluide.
- **Décor** :
  - huit nouveaux sols : damier, cuisine, salle de bain, marbre, parquet, moquette rouge, trottoir, asphalte ;
  - un mur de cloison ;
  - biseau des murs réglé par le thème ;
  - vingt meubles et objets en volume : canapés, fauteuil, lit, télé, bureau, chaise, billard, bar, frigo,
    cuisinière, plan de travail, toilettes, baignoire, lampe, plante, borne d'arcade, classeur, benne,
    caisse, table, voiture ;
  - deux tapis.
- **Scène `demo`** : immeuble avec salon, cuisine, billard et bar, hall, salle de bain, bureaux et rue, et
  huit ennemis.

### 2.3 Packs thermiques (`Mods/NoctPrototype`, inspiré de l'atmosphère de Noct ; `Mods/NoctInspired`)

- **Créatures** :
  - **charognard** (squelette à quatre pattes) : allure en diagonale, attaque, réaction, chute, relevé, mort, repos ;
  - **bondisseur** : bond en trois phases (ramassé tremblant, étiré en vol, réception puis reprise),
    course bondissante, attaque, réaction, mort ;
  - **veilleur** (squelette humain aux bras allongés) : repos recroquevillé presque froid ; réveil où la tête
    se lève, puis le corps se met à genoux, se dresse et se réchauffe (`ai.wakeTime` 0,9 s) ; marche,
    balayage à deux bras avec longue préparation, réaction, mort.
  - Les cadavres refroidissent au fil des dernières images.
- **Survivant et humains** des deux mods : les mêmes clips que le pack d'action, en signatures thermiques,
  sous les mêmes identifiants. Ils remplacent les versions en couleur sans toucher aux données de jeu.
- **Structures** :
  - toits par pièce qui s'effacent quand le survivant entre (`roofs`), avec bouches d'aération chaudes ;
  - route ;
  - murs épais en béton ;
  - conteneurs, citernes, tuyauteries d'eau chaude, groupe électrogène chaud, fourgon, gravats, pneus,
    palettes, lampadaires.
- **Caméra** : plus haute (430 unités visibles), suivi paresseux, dérive lente réglable (`camera.sway`).
- **Interface de surveillance** :
  - position en grille et indicateur de liaison montante ;
  - crochets sur les créatures qui chassent, « ? » sur celles qui cherchent ;
  - encoches au bord de l'image pour celles hors champ.
- **Scène `noct_demo`** : cour, route, dépôt sous toit, cabane, deux charognards, un bondisseur, un veilleur.

### 2.4 Moteur (petits ajouts, activés par les données)

- `ai.wakeTime` et l'état `Awakening` ;
- `LevelRoof` et le calque de décor `roof` ;
- `WallStyle` (biseau) ;
- `PostEffects.PixelArt` ;
- `CameraSettings.Sway` / `SwayPeriod` ;
- option `--modifier <id>`, qui a servi à enregistrer les démos avec un modificateur d'invulnérabilité hors dépôt.

## 3. Fichiers modifiés

Fichiers touchés par la mission visuelle (`dfcc5cc` → `56f552e` : 786 fichiers, dont environ 750 images
générées), plus le dossier de revue ajouté ensuite.

**Code moteur**
- `src/TopDown.Core/Presentation/ActorPose.cs` (nouveau).
- Modifiés :
  - `src/TopDown.Core/Ai/EnemyBrain.cs` ;
  - `src/TopDown.Core/Definitions/{ActorDefinition,LevelDefinition,WeaponDefinition}.cs` ;
  - `src/TopDown.Core/World/{Actor,Combat}.cs`.

**Contenu et validation**
- `src/TopDown.Content/{ContentLoader,ContentValidator,Model}.cs`.

**Rendu et jeu**
- `src/TopDown.Desktop/Graphics/{AssetCache,Camera2D,PostProcessor,WorldRenderer}.cs` ;
- `src/TopDown.Desktop/Screens/{MenuScreen,PlayScreen}.cs`, `Ui/Hud.cs` ;
- `GameHost.cs`, `Program.cs`.

**Tests**
- `tests/TopDown.Tests/AnimationTests.cs` (nouveau) ;
- `ShippedContentTests.cs`, `NoctPrototypeTests.cs`.

**Générateur**
- Nouveaux :
  - `tools/PlaceholderArt/Rig/{Sdf,Human,HumanClips,Beasts,RigSheets,Furniture,Industrial}.cs` ;
  - `HeldWeapons.cs`, `Rugs.cs`.
- Modifiés : `BaseArt.cs`, `NoctArt.cs`, `NoctPrototypeArt.cs`, `Tiles.cs`, `Canvas.cs`, `Pack.cs`,
  `Program.cs`, `Actors.cs` (anciens dessins d'humains et de chien retirés).

**Données**
- Pack de base, `Content/Base/` :
  - manifestes : `assets.json`, `assets.rig.json` (nouveau, généré) ;
  - personnages : `characters/player.json`, `enemies/{humans,animals}.json` ;
  - armes : `weapons/{firearms,melee,natural}.json` ;
  - monde : `tiles/tiles.json`, `themes/neon.json`, `game.json`, `levels/demo.json` (nouveau) ;
  - textes : `locales/{fr,en}.json`.
- Mod `Mods/NoctPrototype/` :
  - manifestes : `assets.json`, `assets.rig.json` (nouveau) ;
  - données de jeu : `enemies/creatures.json`, `game.json`, `levels/installation.json`,
    `levels/noct_demo.json` (nouveau), `tiles/tiles.json` ;
  - présentation : `themes/thermal_cam.json`, `locales/{fr,en}.json`.
- Mod `Mods/NoctInspired/` : `assets.json`, `assets.rig.json` (nouveau).

**Documentation**
- Nouveaux : `docs/animation.md`, `docs/art/{action,thermal,visual-review}.md`.
- Modifiés : `docs/content.md`, `docs/levels.md`, `README.md`.

**Revue (ajouté pour cette revue, sans changement de code)**
- `docs/review/` : ce document, `build-test.log`, GIF, planches, vidéos.

## 4. Ressources graphiques et animations créées

| Pack | Animations | Images d'animation | Planches et images ajoutées |
|---|---|---|---|
| `Content/Base` (action) | 158 | 826 | joueur 37, garde 37, voyou 37, costaud 38, chien 9 planches ; 20 meubles ; 9 tuiles ; 10 armes en main |
| `Mods/NoctPrototype` | 172 | 886 | humains thermiques 37–38 planches chacun ; charognard 8, bondisseur 8, veilleur 7 ; 9 objets ; 5 tuiles ; 10 armes en main |
| `Mods/NoctInspired` | 149 | 781 | humains thermiques 37–38 planches chacun ; 10 armes en main |

- Tout est régénérable de façon déterministe :
  `dotnet run --project tools/PlaceholderArt -c Release -- .` (environ 60 s).
- Planches de contrôle : `docs/images/sprites_action.png`, `docs/images/sprites_noct.png`,
  `docs/review/chien_animations.png`, `docs/review/victime_animations.png`.

## 5. Compilation et tests

Voir `docs/review/build-test.log`, exécuté le 13 novembre 2026 sur `56f552e` (Linux x86_64, SDK .NET 10.0.404).

| Commande | Résultat |
|---|---|
| `dotnet build TopDown.slnx -c Release` | réussi, 0 avertissement, 0 erreur |
| `dotnet test tests/TopDown.Tests -c Release --no-build` | 270 réussis, 0 échec |
| jeu, pilote automatique, 600 images : `demo`, `noct_demo`, `installation` | code 0, aucun avertissement, capture enregistrée |

Intégration continue de cette branche sur `56f552e` (run 43304181985) : 5 jobs réussis.
- Compilation et tests sous `macos-latest`, `ubuntu-latest` et `windows-latest`.
- Tous les niveaux joués en pilote automatique sous Linux.
- Jeu lancé et rendu sous macOS (Apple Silicon) sur `first_night` et `installation`.

## 6. Captures et vidéos

Les médias sont dans `docs/review/` et `docs/images/`.

| Fichier | Contenu |
|---|---|
| `hm_demo.gif`, `videos/scene_demo_action.mp4` | scène `demo` : marche, batte, morts, combos, salles meublées |
| `hm_achevements.gif`, `videos/achevements_chien_victime.mp4` | voyou achevé à cheval (réactions de la victime), chien au galop, mis à terre et démembré |
| `noct_creatures.gif`, `videos/scene_demo_noct_creatures.mp4` | thermique : charognards, bondisseur, réveil du veilleur, crochets de détection, toits |
| `videos/scene_demo_noct_tirs.mp4` | `noct_demo` au pistolet : tirs, impacts, morts qui refroidissent |
| `docs/images/action.jpg`, `action_pool.jpg`, `noct.jpg` | captures 1080p (pixels ×3) |

Les vidéos ont été enregistrées dans le jeu réel sous Xvfb, en pilote automatique. Le joueur était
invulnérable grâce à un modificateur de capture, hors dépôt. Pour la vidéo des créatures, le survivant était
en plus désarmé, pour laisser le temps aux créatures de charger, bondir et s'éveiller.

## 7. Limites et points incomplets

- **Références** :
  - seules des captures fixes des pages Steam ont été consultées ;
  - le site officiel a refusé l'accès automatisé ;
  - les vidéos de gameplay (jeuxvideo.com, moddb) n'ont **pas** pu être visionnées, donc pas analysées ;
  - Retro Diffusion n'a pas été utilisé (aucune clé d'API configurée).
- **Non vérifié** : session jouée à la main sur une vraie machine avec carte graphique ; écoute du son ;
  rendu à 144 Hz.
- **Pack d'action** :
  - la marche en crabe n'a pas d'animation de jambes propre ;
  - le relevé finit par un demi-tour rapide un peu raide ;
  - à cheval, le bourreau cache une grande partie de sa victime ;
  - les morceaux d'un chien démembré sont tirés mais pas animés ;
  - pas de cycle de couleurs plein écran, car pas de shader global (choix).
- **Packs thermiques** :
  - à la distance de jeu, les créatures sont petites et le flou thermique efface du détail ;
  - pas de végétation ;
  - les créatures de `NoctInspired` (traqueur, enveloppe) gardent leurs anciens sprites fixes.
- **Démos** : le pilote automatique n'est pas un bon joueur ; sans invulnérabilité il meurt vite dans
  certains niveaux.
- **Poids** : environ 21 Mo pour le projet (images et sons générés compris) et environ 17 Mo de médias de revue.

## 8. Tester sur macOS

1. Installer le SDK .NET 10 : paquet officiel sur https://dotnet.microsoft.com/download, ou
   `brew install --cask dotnet-sdk`. Vérifier avec `dotnet --version` (10.x).
2. Récupérer la branche :

   ```bash
   git clone https://github.com/mengpo1/pain.git
   ```

   ```bash
   cd pain
   ```

   ```bash
   git switch claude/youthful-noether-typ76w
   ```

3. Compiler et tester :

   ```bash
   dotnet build TopDown.slnx
   ```

   ```bash
   dotnet test tests/TopDown.Tests
   ```

4. Jouer les scènes de démonstration (Apple Silicon et Intel) :

   ```bash
   dotnet run --project src/TopDown.Desktop -- --profile action --level demo
   ```

   ```bash
   dotnet run --project src/TopDown.Desktop -- --profile noct --level noct_demo
   ```

   ```bash
   dotnet run --project src/TopDown.Desktop -- --profile noct
   ```

5. Commandes du jeu :
   - ZQSD/WASD ou flèches : se déplacer ; souris : viser ; clic gauche : attaquer ou tirer ;
   - clic droit : ramasser ou lancer l'arme ;
   - Espace : achever un ennemi à terre, puis clic gauche à chaque coup ;
   - R : recommencer ; Échap ou P : pause ;
   - dans le pack thermique : Maj pour courir, R pour recharger, H pour se soigner ;
   - mode debug : F1 à F7 (panneau, hitboxes, zones d'attaque, IA, bruits, collisions, visée) ;
     F9 recharge le contenu JSON sans relancer.
6. Variante autonome :

   ```bash
   dotnet publish src/TopDown.Desktop -c Release -r osx-arm64 --self-contained
   ```

   Utiliser `osx-x64` sur Mac Intel. Les dossiers `Content/` et `Mods/` sont copiés à côté de l'exécutable
   et restent modifiables sans recompiler.

## 9. Pistes de revue (modularité)

- Remplacer une planche : modifier l'entrée dans `assets.rig.json` ou `assets.json` ; aucune
  recompilation. Le validateur signale les incohérences.
- Ajouter une animation à un personnage : nouvelle entrée nommée `{animationSet}.{clé}`.
- Changer l'arme en main : `heldSprite` / `grip` dans le JSON de l'arme ; la famille de gestes, par `pose`.
- Les règles (dégâts, états, timings) restent dans `TopDown.Core` et ses tests. La présentation
  (`ActorPose` et le rendu) ne fait que les lire.
