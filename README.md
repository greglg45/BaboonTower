# 🐒 Baboon Tower Wars

> Tower Defense compétitif en LAN, style cartoon/vectoriel, développé sous **Unity** en mode *vibe coding* avec Codex.

## 🎯 Objectif du jeu

Défends ton château contre des vagues de monstres tout en attaquant tes adversaires avec des mercenaires. Le dernier joueur en vie l’emporte.

## ⚙️ Tech Stack

- 🎮 **Moteur** : Unity
- 🧠 **Codex** : vibe coding collaboratif
- 💻 **Plateforme** : Client Windows
- 📡 **Multijoueur LAN** : modèle client-serveur, sans auto-discovery (IP manuelle)
- 🎨 **Style** : cartoon / vectoriel (inspiration [Kenney](https://kenney.nl/assets/tower-defense-top-down))

## 🧬 Fiche technique

| Élément           | Détail                              |
|-------------------|--------------------------------------|
| Genre             | Tower Defense compétitif temps réel |
| Vue               | 2D top-down                         |
| Mode              | Multijoueur LAN                     |
| Plateforme        | Windows (client lourd)              |
| Public cible      | Joueurs PC / stratégie              |

## 🔁 Boucle de jeu

1. Placement initial (château + tours + or)
2. Vagues de mobs générant de l’or à chaque kill
3. Phase d’achat : tours 🏰 ou mercenaires ⚔️
4. Attaques entre joueurs
5. Élimination si PV château = 0 💔
6. Victoire 🎖️ : dernier joueur survivant

## 🧠 Architecture réseau

- **Serveur** : gère logique métier (vagues, dégâts, timers, synchro)
- **Clients** : uniquement UI et interactions
- **Connexion** : par IP (LAN), nombre de joueurs illimité

## 🧩 GameConfig

- Unique fichier JSON côté serveur
- Contient toutes les stats (mobs, tours, mercenaires)
- Permet équilibrage simple et scalable

## 🎨 Assets & Placeholder

- 📦 `assets/placeholders/` (style Kenney)
- Fonctionnels dès le départ, remplaçables sans toucher au code

## 🧱 Map & Tuiles

- Grille : 64x64 px
- Dimensions standard : `30 x 16` tuiles (1920x1080)
- Snapping obligatoire
- Prête pour pathfinding A*

## 🖥️ HUD & Interface

- Vague actuelle : `Vague #`
- Monstres restants : `👾 x N`
- Or dispo : `💰`
- PV château : `❤️ 100 / 100`
- Menu d’achat tours & mercenaires

## 🧨 Mercenaires spéciaux

| Nom     | Effet |
|---------|-------|
| …       | [cf. GDD complet] |

## 🔧 Auto-updater

- Comparaison de `version.json` local/remote
- Téléchargement `.zip` si update dispo
- Redémarrage automatique
- Script dev : version bump + archive + push GitHub Releases

---

## ✅ TODO (tech & gameplay)

- [ ] Implémentation serveur Unity LAN
- [ ] Interface d’achat en temps réel
- [ ] IA et effets des mercenaires
- [ ] Génération de la map
- [ ] Intégration `GameConfig`
- [ ] Placeholder debug map (30x16)

---

## 🚀 Lancer en dev

```bash
git clone <repo>
cd baboon-tower-wars
# Ouvre Unity, build client & serveur
