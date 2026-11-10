# Traductions

Aucun texte affiché n'est écrit dans le code : tout passe par des clés, définies dans
`locales/<langue>.json` de chaque pack (`fr.json`, `en.json`…). Le nom du fichier est le code de langue.

```json
{
  "menu.play": "Jouer",
  "game": { "over": "VOUS ÊTES MORT" },
  "hud.collected": "Récupéré : {item}",
  "stats": {
    "kills": { "one": "{count} élimination", "other": "{count} éliminations" }
  }
}
```

- Les objets imbriqués sont aplatis avec des points : `game.over`, `stats.kills.one`.
- `{nom}` est remplacé par une valeur fournie par le jeu.
- **Pluriel** : si le jeu fournit `count` et que `cle.one` / `cle.other` existent, la forme est choisie
  selon la langue (français : 0 et 1 au singulier ; anglais : 1 seul au singulier ; japonais, chinois,
  coréen : pas de pluriel).
- **Repli** : un texte absent de la langue courante est pris dans `fallbackLanguage` (`game.json`,
  anglais par défaut), puis la clé elle-même est affichée. Chaque absence est écrite une fois dans le
  journal, et le diagnostic du contenu liste les clés manquantes de chaque langue.

## Changer de langue

Menu principal → « Langue », ou `--lang en` au lancement. Le choix est enregistré dans `settings.json`.
La langue par défaut d'un jeu est `defaultLanguage` dans `game.json`.

## Ajouter une langue

Créez `locales/<code>.json` (dans le pack de base ou dans un mod) avec les mêmes clés que `en.json`.
Ajoutez aussi `"language.<code>": "Nom de la langue"` pour l'affichage dans le menu. Les polices
TrueType sont chargées à l'exécution et couvrent l'Unicode ; pour un alphabet que la police du thème ne
contient pas, déclarez une autre police dans `assets.json` et référencez-la dans le thème (`ui.font`).

Les noms des touches affichés dans les invites viennent des clés `input.<Touche>` (`input.Space`,
`input.MouseRight`…) ; à défaut, le nom anglais de la touche est utilisé.
