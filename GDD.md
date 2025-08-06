# Game Design Document — Baboon Tower

## Titre du jeu
### Baboon Tower Wars
Jeu compétitif de type Tower Defense en LAN, avec élimination des joueurs et interactions offensives via mercenaires.

## Objectif du jeu
Chaque joueur doit défendre son château contre des vagues de monstres. Les joueurs peuvent aussi attaquer les autres avec des mercenaires de la DSI.
Le dernier joueur survivant remporte la partie.

## Fiche technique
| Élément | Détail |
| --- | --- |
| Genre | Tower Defense compétitif en temps réel |
| Vue | 2D top-down |
| Style visuel | Cartoon / vectoriel (type Kenney) |
| Mode | Multijoueur LAN |
| Plateforme | Client lourd Windows |
| Moteur | Choix libre (Godot, Unity, autre) |
| Public cible | Joueurs PC / stratégie temps réel |

## Architecture multijoueur
### Modèle : Client-Serveur LAN
- Connexion manuelle par IP (pas d’auto-discovery)
- Nombre de joueurs : illimité

#### Le serveur :
- Pilote toute la logique métier
- Gère les vagues, timers, dégâts, synchronisation, éliminations

#### Les clients :
- Gèrent l’affichage et les entrées (UI, clics)
- Aucune logique métier (ni calcul de dégâts)

## Boucle de jeu
### Début de partie
- Chaque joueur a une carte, un château (PV), un chemin d’ennemis
- Or de départ

### Vagues
- Ennemis apparaissent, de plus en plus puissants
- Chaque kill donne de l’or

### Phase d’achat
- Acheter tours de défense
- Acheter mercenaires de la DSI (chemin vers un joueur ciblé)

### Attaque entre joueurs
- Mercenaires suivent un chemin sur la carte adverse
- Infligent des dégâts au château

### Élimination
- PV château = 0 → joueur éliminé

### Victoire
- Dernier joueur encore vivant = vainqueur

## Les ennemis
### les trashs mob
### les mercenaires de la DSI
- **PBUY**
  - Effet : Boost global de vitesse des ennemis.
  - Pouvoir : "Il va falloir mettre un coup de collier maintenant"
  - Stats :
    - Vitesse : 0.7 (lent)
    - Bonus : tous les ennemis gagnent +30% vitesse pendant 5s
- **YBRA**
  - Effet : Les tours touchées ont X% de chance de rater.
  - Pouvoir : Onde de chant perturbante
  - Stats :
    - Rayon : 3 tuiles
    - Miss chance : 20%
    - Vitesse : normale
- **ABRO**
  - Effet : Téléporte une tour aléatoirement
  - Pouvoir : "Dislocation structurelle"
  - Stats :
    - Déclenchement : à l’entrée sur la carte
    - Vitesse : normale
- **BGIR**
  - Effet : Ralentit les mobs alliés derrière lui
  - Pouvoir : Goutte persistante
  - Stats :
    - Vitesse : très lente
    - Aura : -30% vitesse derrière lui (rayon 2)
- **LCA**
  - Effet : Running gag en développement avec Annabelle (TODO)
  - Statut : À définir
  - Suggestion : interactions aléatoires dans le menu d’achat
- **LCAE**
  - Effet : Change les contrôles du joueur adverse
  - Pouvoir : Glitch utilisateur
  - Stats :
    - Rotation map
    - Inversion de touches
    - Changement aléatoire de touches assignées
- **DINT**
  - Effet : Accélère après une pause café
  - Stats :
    - Vitesse : 0.8 → 1.2 après la moitié du chemin
- **GLIG**
  - Effet : Boost de vitesse des mobs devant
  - Pouvoir : "Brasse du vent"
  - Stats :
    - Vitesse : normale
    - Aura frontale : +20% vitesse (rayon 3)
- **GGEE**
  - Effet : Affiche des écrans d'erreur sur le HUD
  - Pouvoir : DDoS psychologique
  - Stats :
    - Affiche aléatoirement : "connection lost", "network unreachable"
- **NDEY**
  - Effet : À définir (TODO)
  - Suggestion : augmentation de la complexité des actions à faire (ex. mini-jeu forcé ?)
- **SLT**
  - Effet : Le joueur doit résoudre une tâche manuelle (ex: minijeu)
  - Pouvoir : Garde technologique
  - Effet : bloque les actions de construction tant qu’il est présent
- **CPAI**
  - Effet : (TODO)
  - Suggestion : Réplication ou contre-intelligence IA ?
- **GHUE**
  - Effet : Attaque à distance le château
  - Stats :
    - Portée : longue (attaque directe à 3 tuiles)
    - Dégâts : +50%
- **DNF**
  - Effet : inconnu (image uniquement, à définir)
  - Suggestion : boss avec comportement spécial ou visuel glitché
- **DDI**
  - Effet : Buff de PV autour de lui
  - Aura : +20% PV à tous les mobs proches
- **RDN**
  - Effet : Donne une monture à certains mobs (boost vitesse)
  - Stats :
    - 30% de chance d’appliquer un buff de vitesse +20%
- **LFRA**
  - Effet : Boost d'esquive des mobs proches
  - Aura : +X% chance d’éviter une attaque (à définir)

### TODO agents à finaliser
- LCA : running gag avec Annabelle
- NDEY : idée gameplay avec Gina/complexité
- CPAI : design en attente
- DNF : visuel mais pas d’effet

## Configuration centrale (GameConfig)
- Fichier unique (JSON ou script)
- Utilisé uniquement côté serveur
- Contient :
  - Stats des tours, monstres, boss, mercenaires
  - Dégâts, récompenses, timings, courbes de difficulté
- Objectif : modification facile / équilibrage / extension

## Système de mise à jour automatique
### À chaque lancement :
- Lit version.json local
- Récupère la version distante (GitHub Releases)
- Compare les versions

### Si nouvelle version :
- Télécharge l’archive .zip
- Remplace les fichiers
- Redémarre le jeu

### Script dev automatisé :
- Incrémente version
- Génère version.json
- Archive .zip
- Push sur GitHub Releases

## UI / HUD
- Vague actuelle : Vague #
- Monstres restants : 👾 x N
- Or disponible : 💰
- PV château : ❤️ 100 / 100

### Menu d’achat :
#### Tours :
- Portée, Dégâts, Cadence, Coût

#### Mercenaires :
- Type, Vitesse, Dégâts, Ciblage joueur

## Assets et placeholders
- Tous les assets sont dans assets/placeholders/
- Objectif : rendu jouable immédiat sans graphisme final
- Placeholders inspirés du style Kenney (sprites simples, icônes colorées)
- Remplaçables facilement sans toucher au code

## La map
### Format des tuiles
- Toutes les tuiles sont carrées, de 64 x 64 pixels
- La map entière est donc une grille simple, parfaite pour :
  - le placement d’objets et de tours
  - le pathfinding basé sur A*
  - les collisions simples
- Tout positionnement (ennemis, tours, décors) doit être snappé à la grille

### Dimensions de la carte
- La carte d’une arène individuelle mesure 30 tuiles de large x 16 tuiles de haut
- Cela correspond exactement à une résolution Full HD 1920x1080
- Aucune zone de scrolling nécessaire
- Prévoir 1 ou 2 lignes de tuiles (en haut ou en bas) pour l’interface utilisateur (HUD)

#### Extensions possibles :
| Variante | Taille suggérée | Usage |
|---|---|---|
| Standard | 30 x 16 | Affichage plein écran sans scroll |
| Défilement H | 50 x 16 | Stratégie étendue |
| Défilement V | 30 x 24 | Gameplay vertical |
| Carré | 24 x 24 | Format équilibré sans scroll |

### Grille visuelle (dev)
Un visuel de carte à taille réelle (30x16 tiles) a été généré pour référence.

Il montre :
- Le rendu en plein écran
- Le placement des tuiles sur grille
- Un chemin d’ennemis et un château
- Des décorations (rochers, arbres)

Ce visuel peut être utilisé pour créer une base de tilemap ou une tilemap de debug

### Exemple d’asset
https://kenney.nl/assets/tower-defense-top-down
