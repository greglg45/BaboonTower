# AGENTS – Baboon Tower Wars

Ces instructions guident le travail de Codex dans ce dépôt Unity.

## Contexte du projet
- Tower Defense compétitif **2D top‑down** en LAN.
- Architecture **client/serveur** : le serveur gère toute la logique, les clients ne font que l'affichage et les entrées.
- Langage principal : **C#** sous **Unity**.

## Organisation
- Les assets temporaires sont dans `assets/placeholders/`.
- Toutes les positions et collisions sont alignées sur une grille **30 x 16** de tuiles **64×64**.
- Les statistiques et paramètres sont centralisés côté serveur dans `GameConfig` (JSON).

## Conventions de code
- Respecter les conventions C# : `PascalCase` pour classes et méthodes publiques, `camelCase` pour les variables.
- Séparer clairement la logique serveur et les scripts côté client.
- Documenter le comportement des mercenaires et des tours.

## Workflow
- Toute modification doit être enregistrée dans `CHANGELOG.md`.
- Lancer `dotnet test` (ou les tests Unity équivalents) après chaque changement ; noter s'il n'existe aucun test.
- Les commits et messages sont rédigés en français.

## Système de mise à jour
- À chaque lancement, comparer `version.json` local avec la version distante, télécharger l'archive `.zip` et redémarrer le jeu en cas de mise à jour.

Bon développement !
