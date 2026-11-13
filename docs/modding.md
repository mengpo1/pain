# Créer un mod

Un mod est un dossier dans `Mods/` avec la même structure qu'un pack (voir [content.md](content.md)).
Aucun code : uniquement des données et des ressources.

## 1. Le manifeste

```json
{
  "id": "my_mod",
  "name": "Mon mod",
  "version": "1.0.0",
  "formatVersion": 1,
  "description": "Une batte plus lourde et un nouveau niveau.",
  "author": "Moi",
  "dependencies": ["base"]
}
```

`id` doit être unique. `formatVersion` doit valoir la version de format du moteur (1) : sinon le mod
n'est pas chargé et l'erreur est affichée. `dependencies` liste les autres mods nécessaires.

## 2. L'activer

Les **profils** de `Content/profiles.json` disent quels mods charger, dans quel ordre :

```json
{ "profiles": [
    { "id": "action", "nameKey": "profile.action", "mods": [] },
    { "id": "mine", "nameKey": "profile.mine", "mods": ["my_mod"] }
] }
```

Choisissez le profil dans le menu (option « Profil ») ou au lancement : `--profile mine`. Pour essayer
un mod sans toucher aux profils : `--mod my_mod`. Le menu « Mods » liste les mods installés et indique
lesquels sont chargés. `F9` recharge tout depuis le disque.

## 3. Règles de fusion

Le pack de base est chargé, puis les mods dans l'ordre du profil, chaque mod après ses dépendances.

- Une définition avec un **nouvel** `id` est ajoutée.
- Une définition avec un `id` **existant** est **complétée** : seules les propriétés fournies sont
  remplacées, les objets imbriqués sont fusionnés de la même façon, les tableaux et valeurs simples
  sont remplacés en entier.

```json
// Mods/my_mod/weapons/bat.json : seule la cadence change, tout le reste vient du pack de base.
{ "id": "bat", "fireRate": 1.5 }
```

- `assets.json` : un identifiant existant pointe vers le fichier du mod (remplacement d'une image, d'un
  son ou d'une police sans toucher au gameplay) ; un nouvel identifiant ajoute une ressource.
- `locales/` : mêmes clés = textes remplacés ; nouvelles clés ou nouvelle langue = ajoutées.
- `game.json` est fusionné comme une définition : un mod peut changer la campagne, le thème, les
  règles (par exemple `"score": { "enabled": false }`) ou la caméra.

Si deux mods sans lien de dépendance modifient la même définition, le dernier chargé gagne et un
avertissement le signale dans le diagnostic.

## 4. Diagnostic

Fichiers JSON invalides, propriétés inconnues, références manquantes (arme, sprite, son, tuile, zone…),
dépendances absentes et conflits sont listés dans le menu (« Contenu : n erreur(s) ») et dans
`log.txt`. Un fichier manquant n'empêche pas de jouer : un damier magenta le remplace.

## Exemple complet

`Mods/NoctInspired` transforme le jeu d'action en jeu d'horreur sans une ligne de C# : thème
thermique, HUD de surveillance, nouvelles tuiles, armes (`flare_gun`, `hunting_rifle`), ennemis
(`husk`, `stalker`), variantes d'ennemis existants (`guard`), deux niveaux, textes fr/en, musique et
ambiance, score désactivé.

`Mods/NoctPrototype` (« Le dernier signal ») va plus loin, toujours sans code propre au pack : survival
horror complet avec ses propres images thermiques, trois créatures (`scavenger`, `leaper` avec `ai.leap`,
`watcher` avec `ai.behavior: "ambush"`), armes à munitions de réserve, conteneurs à fouiller, transmissions
de l'observateur et une mission décrite dans les données du niveau. Il ne dépend que du pack de base.

## Limites

Pas de scripts ni de DLL dans les mods : un comportement réellement nouveau (par exemple un ennemi qui
se téléporte) demande une extension C# du moteur, ensuite paramétrable par les données.
