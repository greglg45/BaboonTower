using UnityEngine;
using System.Collections.Generic;

namespace BaboonTower.Game
{
    #region Tower Data Classes
    
    [System.Serializable]
    public class TowerConfig
    {
        public string version;
        public string lastUpdated;
        public string currency;
        public List<TowerData> towers;
        public List<TowerUpgrade> upgrades;
        public GlobalTowerSettings globalSettings;
        public TowerBalanceSettings balanceSettings;
    }

    [System.Serializable]
    public class TowerData
    {
        public string id;
        public string name;
        public string description;
        public string icon;
        public string prefabName;
        public int tier;
        public TowerStats stats;
        public TowerCost cost;
        public TowerTargeting targeting;
        public TowerVisual visual;
        public TowerAudio audio;
        public TowerRequirements requirements;
    }

    [System.Serializable]
    public class TowerStats
    {
        public float damage;
        public float range;
        public float fireRate;
        public float projectileSpeed;
        public float splashDamage;
        public float splashRadius;
        public float slowEffect;
        public float slowDuration;
    }

    [System.Serializable]
    public class TowerCost
    {
        public int gold;
        public string upgradeFromId;
    }

    [System.Serializable]
    public class TowerTargeting
    {
        public string mode; // nearest, strongest, fastest, weakest
        public bool canTargetAir;
        public bool canTargetGround;
        public int maxTargets;
    }

    [System.Serializable]
    public class TowerVisual
    {
        public string towerColor;
        public string projectileColor;
        public string rangeIndicatorColor;
        public float scale;
    }

    [System.Serializable]
    public class TowerAudio
    {
        public string placementSound;
        public string fireSound;
        public string hitSound;
    }

    [System.Serializable]
    public class TowerRequirements
    {
        public int minWave;
        public int minPlayerLevel;
        public List<string> requiredResearch;
    }

    [System.Serializable]
    public class TowerUpgrade
    {
        public string fromTowerId;
        public string toTowerId;
        public int cost;
        public UpgradeRequirements requirements;
    }

    [System.Serializable]
    public class UpgradeRequirements
    {
        public int minWave;
    }

    [System.Serializable]
    public class GlobalTowerSettings
    {
        public int maxTowersPerPlayer;
        public float sellRefundPercentage;
        public int placementGridSize;
        public bool showRangeOnHover;
        public bool showRangeOnPlacement;
        public bool allowTowerStacking;
    }

    [System.Serializable]
    public class TowerBalanceSettings
    {
        public float damageMultiplierPerWave;
        public float costMultiplierPerWave;
        public float rangeBoostNearCastle;
        public float damageBoostNearSpawn;
    }

    #endregion

    #region Mercenary Data Classes

    [System.Serializable]
    public class MercenaryConfig
    {
        public string version;
        public string lastUpdated;
        public string currency;
        public List<MercenaryData> mercenaries;
        public GlobalMercenarySettings globalSettings;
        public MercenaryBalanceSettings balanceSettings;
        public MercenaryTargetingSettings targetingSettings;
    }

    [System.Serializable]
    public class MercenaryData
    {
        public string id;
        public string name;
        public string title;
        public string description;
        public string icon;
        public string prefabName;
        public string category;
        public int tier;
        public MercenaryStats stats;
        public MercenaryCost cost;
        public MercenaryEffect effect;
        public MercenaryVisual visual;
        public MercenaryRequirements requirements;
    }

    [System.Serializable]
    public class MercenaryStats
    {
        public float health;
        public float moveSpeed;
        public float damageTocastle;
        public float effectRadius;
        public float effectDuration;
        public float effectPower;
    }

    [System.Serializable]
    public class MercenaryCost
    {
        public int gold;
        public float cooldown;
    }

    [System.Serializable]
    public class MercenaryEffect
    {
        public string type;
        public string target;
        public float speedMultiplier;
        public float selfSpeedMultiplier;
        public float slowMultiplier;
        public float missChance;
        public float pulseInterval;
        public float initialSpeedMultiplier;
        public float boostedSpeedMultiplier;
        public float coffeeBreakDuration;
        public int maxDisplacement;
        public bool canSwapTowers;
        public bool affectAllies;
        public bool revealGold;
        public bool revealCastleHP;
        public bool revealTowerCount;
        public float duration;
        public string message;
        public string visualEffect;
        public string soundEffect;
    }

    [System.Serializable]
    public class MercenaryVisual
    {
        public string color;
        public string trailEffect;
        public float scale;
    }

    [System.Serializable]
    public class MercenaryRequirements
    {
        public int minWave;
        public int minPlayerLevel;
    }

    [System.Serializable]
    public class GlobalMercenarySettings
    {
        public int maxMercenariesPerWave;
        public float mercenarySpawnDelay;
        public bool allowMultipleSameType;
        public bool showTargetSelector;
        public bool autoTargetIfSingleEnemy;
    }

    [System.Serializable]
    public class MercenaryBalanceSettings
    {
        public float healthMultiplierPerWave;
        public float costReductionWithMultipleBuys;
        public float effectPowerScaling;
        public float cooldownReductionPerWave;
    }

    [System.Serializable]
    public class MercenaryTargetingSettings
    {
        public bool showPlayerNames;
        public bool showPlayerIcons;
        public bool showLastKnownStats;
        public bool allowSelfTarget;
        public bool randomTargetOption;
    }

    #endregion
}