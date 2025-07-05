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

public class ExileRitualEj : BaseSettingsPlugin<ExileRitualEjSettings>
{
    private readonly HashSet<Entity> _gigantEntities = new HashSet<Entity>();
    private readonly HashSet<Vector2> _gigantSpawnPositions = new HashSet<Vector2>();
    private readonly HashSet<uint> _processedGigantEntityIds = new HashSet<uint>();
    private readonly HashSet<Entity> _ritualRuneEntities = new HashSet<Entity>();
    private readonly List<RitualBlockerInfo> _ritualBlockers = new List<RitualBlockerInfo>();
    public override bool Initialise()
    {
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _gigantEntities.Clear();
        _gigantSpawnPositions.Clear();
        _processedGigantEntityIds.Clear();
        _ritualRuneEntities.Clear();
        _ritualBlockers.Clear();
    }

    public override Job Tick()
    {
        foreach (var entity in _gigantEntities.ToList())
        {
            if (entity?.IsValid == true && !entity.IsDead && !_processedGigantEntityIds.Contains(entity.Id))
            {
                var statsComponent = entity.GetComponent<Stats>();
                var combinedLifePct = 100;
                if (statsComponent?.StatDictionary != null)
                {
                    foreach (var stat in statsComponent.StatDictionary)
                    {
                        if (stat.Key.ToString().Contains("CombinedLifePct"))
                        {
                            combinedLifePct = stat.Value;
                            break;
                        }
                    }
                }
                if (combinedLifePct >= 100)
                {
                    _gigantSpawnPositions.Add(entity.GridPosNum);
                    _processedGigantEntityIds.Add(entity.Id);
                    
                    UpdateRitualBlockerCounts(entity.GridPosNum);
                    
                    LogMessage($"GIGANTIC spawn position saved: {entity.GridPosNum}, Distance: {entity.DistancePlayer}, CombinedLifePct: {combinedLifePct}");
                }
                else
                {
                    LogMessage($"GIGANTIC entity skipped (damaged): ID={entity.Id}, CombinedLifePct: {combinedLifePct}");
                }
            }
        }
        
        _gigantEntities.RemoveWhere(entity => entity?.IsDead == true);
        
        return null;
    }

    public override void Render()
    {
        if (Settings.RenderGiganticName.Value)
        {
            foreach (var entity in _gigantEntities.ToList())
            {
                if (entity?.IsValid != true) continue;
                
                var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                if (screenPos.X > 0 && screenPos.Y > 0)
                {
                    var textPos = new Vector2(screenPos.X - 40, screenPos.Y - 60);
                    
                    var text = "GIGANT";
                    var scale = 8.0f;
                    
                    for (int i = 0; i < text.Length; i++)
                    {
                        var charPos = new Vector2(textPos.X + i * 2 * scale, textPos.Y);
                        var character = text[i].ToString();
                        
                        for (int x = -2; x <= 2; x++)
                        {
                            for (int y = -2; y <= 2; y++)
                            {
                                if (x != 0 || y != 0)
                                {
                                    Graphics.DrawText(character, new Vector2(charPos.X + x, charPos.Y + y), Color.Black);
                                }
                            }
                        }
                        
                        Graphics.DrawText(character, charPos, Color.White);
                    }
                }
            }
        }
        
        if (Settings.RenderGiganticSpawnPositions.Value)
        {
            foreach (var spawnPos in _gigantSpawnPositions)
            {
                if (GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisibleLocal)
                {
                    DrawFilledCircleOnMap(spawnPos, 6f, Settings.GiganticSpawnColor.Value);
                }
                
                DrawFilledCircleOnWorld(spawnPos, 50f, Settings.GiganticSpawnColor.Value);
            }
        }
        
        if (Settings.RenderRitualRadius.Value)
        {
            foreach (var entity in _ritualRuneEntities.ToList())
            {
                if (entity?.IsValid != true) continue;
                
                DrawCircleOnWorld(entity.GridPosNum, Settings.RitualRadius.Value, Settings.RitualRadiusColor.Value, Settings.RitualRadiusThickness.Value);
            }
        }
        
        DrawRitualBlockerCounts();
    }

    public override void EntityAdded(Entity entity)
    {
        if (entity?.IsValid == true && entity.IsHostile && entity.Metadata == "Metadata/Terrain/Leagues/Ritual/RitualBlocker")
        {
            const float minDistance = 5f;
            bool tooClose = false;
            
            foreach (var existingBlocker in _ritualBlockers)
            {
                var distance = Vector2.Distance(existingBlocker.Position, entity.GridPosNum);
                if (distance < minDistance)
                {
                    tooClose = true;
                    LogMessage($"RitualBlocker ignored (too close): Distance={distance:F1} to existing blocker at {existingBlocker.Position}");
                    break;
                }
            }
            
            if (tooClose)
            {
                return;
            }
            
            int totalPreviousCount = 0;
            foreach (var prevBlocker in _ritualBlockers)
            {
                totalPreviousCount += prevBlocker.Count;
            }
            
            var blockerInfo = new RitualBlockerInfo(entity.GridPosNum, entity.Id);
            blockerInfo.Count = -totalPreviousCount;
            _ritualBlockers.Add(blockerInfo);
            
            LogMessage($"RitualBlocker entity added: ID={entity.Id}, Position={entity.GridPosNum}, InitialCount={blockerInfo.Count} (total previous was {totalPreviousCount})");
            return;
        }
        
        if (entity?.IsValid == true && entity.IsHostile && entity.HasComponent<ObjectMagicProperties>())
        {
            try
            {
                var magicProperties = entity.GetComponent<ObjectMagicProperties>();
                if (magicProperties?.Mods != null)
                {
                    var hasGigantismMod = magicProperties.Mods.Any(mod =>
                        mod?.Contains("MonsterSupporterGigantism1") == true);

                    if (hasGigantismMod)
                    {
                        var statsComponent = entity.GetComponent<Stats>();
                        var combinedLifePct = 100;
                        if (statsComponent?.StatDictionary != null)
                        {
                            foreach (var stat in statsComponent.StatDictionary)
                            {
                                if (stat.Key.ToString().Contains("CombinedLifePct"))
                                {
                                    combinedLifePct = stat.Value;
                                    break;
                                }
                            }
                        }
                        if (combinedLifePct >= 100)
                        {
                            _gigantEntities.Add(entity);

                            LogMessage($"GIGANTIC entity added: ID={entity.Id}, Distance={entity.DistancePlayer}, IsDead={entity.IsDead}, CombinedLifePct: {combinedLifePct}");
                        }
                        else
                        {
                            LogMessage($"GIGANTIC entity skipped (damaged): ID={entity.Id}, CombinedLifePct: {combinedLifePct}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogError($"Error processing entity in EntityAdded: {ex.Message}");
            }
        }
        
        if (entity?.IsValid == true && entity.Metadata == "Metadata/Terrain/Leagues/Ritual/RitualRuneObject")
        {
            _ritualRuneEntities.Add(entity);
        }
    }

    private void DrawFilledCircleOnMap(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var mapPos = GameController.IngameState.Data.GetGridMapScreenPosition(gridPosition);
        
        for (float r = 1; r <= radius; r += 1f)
        {
            const int segments = 16;
            const float segmentAngle = 2f * (float)Math.PI / segments;

            for (var i = 0; i < segments; i++)
            {
                var angle = i * segmentAngle;
                var currentOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * r;
                var nextOffset = new Vector2((float)Math.Cos(angle + segmentAngle), (float)Math.Sin(angle + segmentAngle)) * r;

                var currentPos = mapPos + currentOffset;
                var nextPos = mapPos + nextOffset;

                Graphics.DrawLine(currentPos, nextPos, 1, color);
            }
        }
    }

    private void DrawFilledCircleOnWorld(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var sharpDxGridPos = new SharpDX.Vector2(gridPosition.X, gridPosition.Y);
        var worldPos2D = sharpDxGridPos.GridToWorld();
        var terrainHeight = GameController.IngameState.Data.GetTerrainHeightAt(gridPosition);
        var worldPos3D = new System.Numerics.Vector3(worldPos2D.X, worldPos2D.Y, terrainHeight);

        if (!IsPositionOnScreen(worldPos3D, radius + 100f))
        {
            return;
        }

        Graphics.DrawFilledCircleInWorld(worldPos3D, radius, color);
    }

    private void DrawCircleOnMap(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var mapPos = GameController.IngameState.Data.GetGridMapScreenPosition(gridPosition);
        
        const int segments = 32;
        const float segmentAngle = 2f * (float)Math.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            var nextOffset = new Vector2((float)Math.Cos(angle + segmentAngle), (float)Math.Sin(angle + segmentAngle)) * radius;

            var currentPos = mapPos + currentOffset;
            var nextPos = mapPos + nextOffset;

            Graphics.DrawLine(currentPos, nextPos, 2, color);
        }
    }

    private bool IsPositionOnScreen(System.Numerics.Vector3 worldPosition, float allowance = 50f)
    {
        var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(worldPosition);
        var screenSize = GameController.Window.GetWindowRectangleTimeCache.Size;
        
        return screenPos.X >= -allowance && 
               screenPos.X <= screenSize.Width + allowance &&
               screenPos.Y >= -allowance && 
               screenPos.Y <= screenSize.Height + allowance;
    }

    private void DrawCircleOnWorld(Vector2 gridPosition, float radius, SharpDX.Color color, int thickness = 3)
    {
        var sharpDxGridPos = new SharpDX.Vector2(gridPosition.X, gridPosition.Y);
        var worldPos2D = sharpDxGridPos.GridToWorld();
        var terrainHeight = GameController.IngameState.Data.GetTerrainHeightAt(gridPosition);
        var worldPos3D = new System.Numerics.Vector3(worldPos2D.X, worldPos2D.Y, terrainHeight);

        if (!IsPositionOnScreen(worldPos3D, radius + 100f))
        {
            return;
        }

        const int segments = 64;
        const float segmentAngle = 2f * (float)Math.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var nextAngle = (i + 1) * segmentAngle;
            
            var currentOffset = new System.Numerics.Vector3(
                (float)Math.Cos(angle) * radius, 
                (float)Math.Sin(angle) * radius, 
                0);
            var nextOffset = new System.Numerics.Vector3(
                (float)Math.Cos(nextAngle) * radius, 
                (float)Math.Sin(nextAngle) * radius, 
                0);

            var currentWorldPos = worldPos3D + currentOffset;
            var nextWorldPos = worldPos3D + nextOffset;

            var currentScreenPos = GameController.Game.IngameState.Camera.WorldToScreen(currentWorldPos);
            var nextScreenPos = GameController.Game.IngameState.Camera.WorldToScreen(nextWorldPos);

            Graphics.DrawLine(new Vector2(currentScreenPos.X, currentScreenPos.Y), 
                            new Vector2(nextScreenPos.X, nextScreenPos.Y), thickness, color);
        }
    }

    private void UpdateRitualBlockerCounts(Vector2 gigantPosition)
    {
        const float maxDistance = 200f;
        
        foreach (var blocker in _ritualBlockers)
        {
            var distance = Vector2.Distance(blocker.Position, gigantPosition);
            if (distance <= maxDistance)
            {
                blocker.Count++;
                LogMessage($"RitualBlocker count updated: Position={blocker.Position}, NewCount={blocker.Count}, Distance={distance:F1}");
            }
        }
    }

    private void DrawRitualBlockerCounts()
    {
        foreach (var blocker in _ritualBlockers)
        {
            var sharpDxGridPos = new SharpDX.Vector2(blocker.Position.X, blocker.Position.Y);
            var worldPos2D = sharpDxGridPos.GridToWorld();
            var terrainHeight = GameController.IngameState.Data.GetTerrainHeightAt(blocker.Position);
            var worldPos3D = new System.Numerics.Vector3(worldPos2D.X, worldPos2D.Y, terrainHeight);

            var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(worldPos3D);
            if (screenPos.X > 0 && screenPos.Y > 0)
            {
                var textPos = new Vector2(screenPos.X - 10, screenPos.Y - 10);
                var countText = blocker.Count.ToString();
                
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x != 0 || y != 0)
                        {
                            Graphics.DrawText(countText, new Vector2(textPos.X + x, textPos.Y + y), Color.Black);
                        }
                    }
                }
                
                Graphics.DrawText(countText, textPos, Color.Yellow);
            }
        }
    }
}