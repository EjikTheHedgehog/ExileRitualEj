using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ImGuiNET;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = System.Numerics.Vector2;

namespace ExileRitualEj;

public class RitualBlockerInfo
{
    public Vector2 Position { get; set; }
    public int Count { get; set; }
    public uint EntityId { get; set; }
    
    public RitualBlockerInfo(Vector2 position, uint entityId)
    {
        Position = position;
        Count = 0;
        EntityId = entityId;
    }
}

public struct EntityStatsInfo
{
    public int CombinedLifePct { get; set; }
    public int ActorScalePct { get; set; }
}

public class ExileRitualEj : BaseSettingsPlugin<ExileRitualEjSettings>
{
    #region Constants
    // Configuration constants for gigantic entity detection via mods and stats,
    // distance calculations, and metadata identifiers for ritual entities
    
    private const int MIN_COMBINED_LIFE_PCT = 100;
    private const int MIN_ACTOR_SCALE_PCT = 80;
    private const float MIN_BLOCKER_DISTANCE = 5f;
    private const float MAX_BLOCKER_UPDATE_DISTANCE = 105f; // Ritual range is 100, so a bit more for safety
    private const string GIGANTISM_MOD = "MonsterSupporterGigantism1";
    private const string RITUAL_BLOCKER_METADATA = "Metadata/Terrain/Leagues/Ritual/RitualBlocker";
    private const string RITUAL_RUNE_METADATA = "Metadata/Terrain/Leagues/Ritual/RitualRuneObject";
    
    #endregion

    #region Fields
    // Collections for tracking gigantic entities, their spawn positions,
    // ritual runes, blockers(fog while encounter), inside ritual spawn positions, and the rendering component
    
    private readonly HashSet<Entity> _gigantEntities = new HashSet<Entity>();
    private readonly HashSet<Vector2> _gigantSpawnPositions = new HashSet<Vector2>();
    private readonly HashSet<uint> _processedGigantEntityIds = new HashSet<uint>();
    private readonly HashSet<Entity> _ritualRuneEntities = new HashSet<Entity>();
    private readonly List<RitualBlockerInfo> _ritualBlockers = new List<RitualBlockerInfo>();
    private readonly HashSet<Vector2> _insideRitualSpawnPositions = new HashSet<Vector2>();
    
    private ExileRitualEjRenderer _renderer;
    
    #endregion

    #region Override Methods
    // Plugin lifecycle methods: initialization and area change handling
    
    public override bool Initialise()
    {
        _renderer = new ExileRitualEjRenderer(this, Settings, GameController, Graphics);
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _gigantEntities.Clear();
        _gigantSpawnPositions.Clear();
        _processedGigantEntityIds.Clear();
        _ritualRuneEntities.Clear();
        _ritualBlockers.Clear();
        _insideRitualSpawnPositions.Clear();
    }

    #endregion

    #region Tick Processing
    // Main game loop processing: validates gigantic entities, checks their stats,
    // and registers spawn positions when conditions are met
    
    public override Job Tick()
    {
        foreach (var entity in _gigantEntities.ToList())
        {
            if (ShouldProcessGigantEntity(entity))
            {
                ProcessGigantEntity(entity);
            }
        }
        
        _gigantEntities.RemoveWhere(entity => entity?.IsDead == true);
        
        return null;
    }

    private bool ShouldProcessGigantEntity(Entity entity)
    {
        return entity?.IsValid == true && 
               !entity.IsDead && 
               !_processedGigantEntityIds.Contains(entity.Id);
    }

    private void ProcessGigantEntity(Entity entity)
    {
        var entityStats = GetEntityStats(entity);
        
        if (IsGiganticEntity(entity))
        {
            RegisterGigantSpawn(entity, entityStats);
        }
        else
        {
            LogGigantSkipped(entity, entityStats);
        }
    }

    private EntityStatsInfo GetEntityStats(Entity entity)
    {
        var statsComponent = entity.GetComponent<Stats>();
        return new EntityStatsInfo
        {
            CombinedLifePct = GetStatValue(statsComponent, "CombinedLifePct"),
            ActorScalePct = GetStatValue(statsComponent, "ActorScalePct")
        };
    }

    private void RegisterGigantSpawn(Entity entity, EntityStatsInfo stats)
    {
        _gigantSpawnPositions.Add(entity.GridPosNum);
        _processedGigantEntityIds.Add(entity.Id);
        
        bool blockerUpdated = UpdateRitualBlockerCounts(entity.GridPosNum);
        
        // If blocker count was updated, this is an inside ritual spawn position
        if (blockerUpdated)
        {
            _insideRitualSpawnPositions.Add(entity.GridPosNum);
            LogMessage($"Inside RITUAL spawn position registered: {entity.GridPosNum} (blocker count updated)");
        }
        
        LogMessage($"GIGANTIC spawn position saved: {entity.GridPosNum}, Distance: {entity.DistancePlayer}, " +
                  $"CombinedLifePct: {stats.CombinedLifePct}, ActorScalePct: {stats.ActorScalePct}");
    }

    private void LogGigantSkipped(Entity entity, EntityStatsInfo stats)
    {
        LogMessage($"GIGANTIC entity skipped (conditions not met): ID={entity.Id}, " +
                  $"CombinedLifePct: {stats.CombinedLifePct}, ActorScalePct: {stats.ActorScalePct}");
    }

    #endregion

    #region Render
    // Rendering delegation: passes data to the renderer for visual display
    
    public override void Render()
    {
        _renderer.RenderGiganticNames(_gigantEntities);
        _renderer.RenderGiganticSpawnPositions(_gigantSpawnPositions);
        _renderer.RenderRitualRadius(_ritualRuneEntities);
        _renderer.RenderInsideRitualSpawnPositions(_insideRitualSpawnPositions);
        _renderer.DrawRitualBlockerCounts(_ritualBlockers);
    }

    #endregion

    #region Entity Processing
    // Main entity event handler: routes newly added entities to appropriate processors
    // based on their type (ritual blocker fog, gigantic entity, or ritual rune)
    
    public override void EntityAdded(Entity entity)
    {
        if (entity?.IsValid != true) return;

        if (IsRitualBlocker(entity))
        {
            ProcessRitualBlocker(entity);
        }
        else if (IsHostileWithMagicProperties(entity))
        {
            ProcessPotentialGigant(entity);
        }
        else if (IsRitualRune(entity))
        {
            _ritualRuneEntities.Add(entity);
        }
    }

    #endregion

    #region Entity Type Checks
    // Entity classification methods: determine entity types by metadata and components
    
    private bool IsRitualBlocker(Entity entity)
    {
        return entity.IsHostile && entity.Metadata == RITUAL_BLOCKER_METADATA;
    }

    private bool IsHostileWithMagicProperties(Entity entity)
    {
        return entity.IsHostile && entity.HasComponent<ObjectMagicProperties>();
    }

    private bool IsRitualRune(Entity entity)
    {
        return entity.Metadata == RITUAL_RUNE_METADATA;
    }

    #endregion

    #region Ritual Blocker Processing
    // Ritual blocker management: handles distance validation, prevents duplicates,
    // and maintains blocker count state for ritual tracking
    
    private void ProcessRitualBlocker(Entity entity)
    {
        if (IsTooCloseToExistingBlocker(entity.GridPosNum))
        {
            return;
        }

        var blockerInfo = CreateRitualBlockerInfo(entity);
        _ritualBlockers.Add(blockerInfo);
        
        LogMessage($"RitualBlocker entity added: ID={entity.Id}, Position={entity.GridPosNum}, " +
                  $"InitialCount={blockerInfo.Count} (total previous was {-blockerInfo.Count})");
    }

    // The function prevents processing a new fog at the position of the previous fog (in case that ever happens)
    private bool IsTooCloseToExistingBlocker(Vector2 position)
    {
        foreach (var existingBlocker in _ritualBlockers)
        {
            var distance = Vector2.Distance(existingBlocker.Position, position);
            if (distance < MIN_BLOCKER_DISTANCE)
            {
                LogMessage($"RitualBlocker ignored (too close): Distance={distance:F1} to existing blocker at {existingBlocker.Position}");
                return true;
            }
        }
        return false;
    }

    private RitualBlockerInfo CreateRitualBlockerInfo(Entity entity)
    {
        int totalPreviousCount = _ritualBlockers.Sum(blocker => blocker.Count);
        
        var blockerInfo = new RitualBlockerInfo(entity.GridPosNum, entity.Id);
        blockerInfo.Count = -totalPreviousCount;
        return blockerInfo;
    }

    #endregion

    #region Gigant Entity Processing
    // Gigantic entity detection and validation: checks for gigantism mods,
    // validates entity stats, and adds entities to tracking collection
    
    private void ProcessPotentialGigant(Entity entity)
    {
        try
        {
            var magicProperties = entity.GetComponent<ObjectMagicProperties>();
            if (magicProperties?.Mods == null) return;

            if (HasGigantismMod(magicProperties))
            {
                HandleGigantismEntity(entity);
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Error processing entity in EntityAdded: {ex.Message}");
        }
    }

    private bool HasGigantismMod(ObjectMagicProperties magicProperties)
    {
        return magicProperties.Mods.Any(mod => mod?.Contains(GIGANTISM_MOD) == true);
    }

    private void HandleGigantismEntity(Entity entity)
    {
        var entityStats = GetEntityStats(entity);
        
        if (IsGiganticEntity(entity))
        {
            _gigantEntities.Add(entity);
            LogMessage($"GIGANTIC entity added: ID={entity.Id}, Distance={entity.DistancePlayer}, " +
                      $"IsDead={entity.IsDead}, CombinedLifePct: {entityStats.CombinedLifePct}, " +
                      $"ActorScalePct: {entityStats.ActorScalePct}");
        }
        else
        {
            LogMessage($"GIGANTIC entity skipped (conditions not met): ID={entity.Id}, " +
                      $"CombinedLifePct: {entityStats.CombinedLifePct}, ActorScalePct: {entityStats.ActorScalePct}");
        }
    }

    #endregion

    #region Helper Methods
    // Utility functions: stat extraction, entity validation, distance calculations,
    // and ritual blocker count updates based on gigantic entity proximity
    
    private bool UpdateRitualBlockerCounts(Vector2 gigantPosition)
    {
        bool blockerUpdated = false;
        foreach (var blocker in _ritualBlockers)
        {
            var distance = Vector2.Distance(blocker.Position, gigantPosition);
            if (distance <= MAX_BLOCKER_UPDATE_DISTANCE)
            {
                blocker.Count++;
                blockerUpdated = true;
                LogMessage($"RitualBlocker count updated: Position={blocker.Position}, NewCount={blocker.Count}, Distance={distance:F1}");
            }
        }
        return blockerUpdated;
    }

    private int GetStatValue(Stats statsComponent, string statName)
    {
        if (statsComponent?.StatDictionary == null) return 0;
        
        foreach (var stat in statsComponent.StatDictionary)
        {
            if (stat.Key.ToString().Contains(statName))
            {
                return stat.Value;
            }
        }
        return 0;
    }

    private bool IsGiganticEntity(Entity entity)
    {
        var statsComponent = entity.GetComponent<Stats>();
        if (statsComponent?.StatDictionary == null) return false;
        
        var combinedLifePct = GetStatValue(statsComponent, "CombinedLifePct");
        var actorScalePct = GetStatValue(statsComponent, "ActorScalePct");
        
        return combinedLifePct >= MIN_COMBINED_LIFE_PCT && actorScalePct >= MIN_ACTOR_SCALE_PCT;
    }

    #endregion
}