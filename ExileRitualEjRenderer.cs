using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ImGuiNET;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = System.Numerics.Vector2;

namespace ExileRitualEj;

public class ExileRitualEjRenderer
{
    private readonly BaseSettingsPlugin<ExileRitualEjSettings> _plugin;
    private readonly ExileRitualEjSettings _settings;
    private readonly GameController _gameController;
    private readonly Graphics _graphics;

    public ExileRitualEjRenderer(BaseSettingsPlugin<ExileRitualEjSettings> plugin, ExileRitualEjSettings settings, GameController gameController, Graphics graphics)
    {
        _plugin = plugin;
        _settings = settings;
        _gameController = gameController;
        _graphics = graphics;
    }

    public void RenderGiganticNames(IEnumerable<Entity> gigantEntities)
    {
        if (!_settings.RenderGiganticName.Value) return;

        foreach (var entity in gigantEntities.ToList())
        {
            if (entity?.IsValid != true) continue;
            
            var screenPos = _gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            if (screenPos.X > 0 && screenPos.Y > 0)
            {
                DrawGigantText(screenPos);
            }
        }
    }

    private void DrawGigantText(System.Numerics.Vector2 screenPos)
    {
        var textPos = new Vector2(screenPos.X - 40, screenPos.Y - 60);
        var text = "GIGANT";
        var scale = 8.0f;
        
        for (int i = 0; i < text.Length; i++)
        {
            var charPos = new Vector2(textPos.X + i * 2 * scale, textPos.Y);
            var character = text[i].ToString();
            
            // Draw shadow
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        _graphics.DrawText(character, new Vector2(charPos.X + x, charPos.Y + y), Color.Black);
                    }
                }
            }
            
            _graphics.DrawText(character, charPos, Color.White);
        }
    }

    public void RenderGiganticSpawnPositions(IEnumerable<Vector2> gigantSpawnPositions)
    {
        if (!_settings.RenderGiganticSpawnPositions.Value) return;

        foreach (var spawnPos in gigantSpawnPositions)
        {
            if (_gameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisibleLocal)
            {
                DrawFilledCircleOnMap(spawnPos, 6f, _settings.GiganticSpawnColor.Value);
            }
            
            DrawFilledCircleOnWorld(spawnPos, 50f, _settings.GiganticSpawnColor.Value);
        }
    }

    public void RenderRitualRadius(IEnumerable<Entity> ritualRuneEntities)
    {
        if (!_settings.RenderRitualRadius.Value) return;

        foreach (var entity in ritualRuneEntities.ToList())
        {
            if (entity?.IsValid != true) continue;
            
            DrawCircleOnWorld(entity.GridPosNum, _settings.RitualRadius.Value, 
                            _settings.RitualRadiusColor.Value, _settings.RitualRadiusThickness.Value);
        }
    }

    public void DrawRitualBlockerCounts(IEnumerable<RitualBlockerInfo> ritualBlockers)
    {
        foreach (var blocker in ritualBlockers)
        {
            var worldPos3D = GridPositionToWorld3D(blocker.Position);
            var screenPos = _gameController.Game.IngameState.Camera.WorldToScreen(worldPos3D);
            
            if (screenPos.X > 0 && screenPos.Y > 0)
            {
                DrawCountText(screenPos, blocker.Count.ToString());
            }
        }
    }

    private void DrawCountText(System.Numerics.Vector2 screenPos, string countText)
    {
        var textPos = new Vector2(screenPos.X - 10, screenPos.Y - 10);
        
        // Draw shadow
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x != 0 || y != 0)
                {
                    _graphics.DrawText(countText, new Vector2(textPos.X + x, textPos.Y + y), Color.Black);
                }
            }
        }
        
        _graphics.DrawText(countText, textPos, Color.Yellow);
    }

    private void DrawFilledCircleOnMap(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var mapPos = _gameController.IngameState.Data.GetGridMapScreenPosition(gridPosition);
        
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

                _graphics.DrawLine(currentPos, nextPos, 1, color);
            }
        }
    }

    private void DrawFilledCircleOnWorld(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var worldPos3D = GridPositionToWorld3D(gridPosition);

        if (!IsPositionOnScreen(worldPos3D, radius + 100f))
        {
            return;
        }

        _graphics.DrawFilledCircleInWorld(worldPos3D, radius, color);
    }

    private void DrawCircleOnMap(Vector2 gridPosition, float radius, SharpDX.Color color)
    {
        var mapPos = _gameController.IngameState.Data.GetGridMapScreenPosition(gridPosition);
        
        const int segments = 32;
        const float segmentAngle = 2f * (float)Math.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            var nextOffset = new Vector2((float)Math.Cos(angle + segmentAngle), (float)Math.Sin(angle + segmentAngle)) * radius;

            var currentPos = mapPos + currentOffset;
            var nextPos = mapPos + nextOffset;

            _graphics.DrawLine(currentPos, nextPos, 2, color);
        }
    }

    private bool IsPositionOnScreen(System.Numerics.Vector3 worldPosition, float allowance = 50f)
    {
        var screenPos = _gameController.Game.IngameState.Camera.WorldToScreen(worldPosition);
        var screenSize = _gameController.Window.GetWindowRectangleTimeCache.Size;
        
        return screenPos.X >= -allowance && 
               screenPos.X <= screenSize.Width + allowance &&
               screenPos.Y >= -allowance && 
               screenPos.Y <= screenSize.Height + allowance;
    }

    private void DrawCircleOnWorld(Vector2 gridPosition, float radius, SharpDX.Color color, int thickness = 3)
    {
        var worldPos3D = GridPositionToWorld3D(gridPosition);

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

            var currentScreenPos = _gameController.Game.IngameState.Camera.WorldToScreen(currentWorldPos);
            var nextScreenPos = _gameController.Game.IngameState.Camera.WorldToScreen(nextWorldPos);

            _graphics.DrawLine(new Vector2(currentScreenPos.X, currentScreenPos.Y), 
                            new Vector2(nextScreenPos.X, nextScreenPos.Y), thickness, color);
        }
    }

    public void RenderInsideRitualSpawnPositions(IEnumerable<Vector2> insideRitualSpawnPositions)
    {
        foreach (var spawnPos in insideRitualSpawnPositions)
        {
            var worldPos3D = GridPositionToWorld3D(spawnPos);
            var screenPos = _gameController.Game.IngameState.Camera.WorldToScreen(worldPos3D);
            
            if (screenPos.X > 0 && screenPos.Y > 0)
            {
                DrawInsideRitualSpawnText(screenPos);
            }
        }
    }

    private void DrawInsideRitualSpawnText(System.Numerics.Vector2 screenPos)
    {
        var textPos = new Vector2(screenPos.X - 30, screenPos.Y - 15);
        var text = "RITUAL";
        var scale = 6.0f;
        
        for (int i = 0; i < text.Length; i++)
        {
            var charPos = new Vector2(textPos.X + i * 2 * scale, textPos.Y);
            var character = text[i].ToString();
            
            // Draw shadow
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x != 0 || y != 0)
                    {
                        _graphics.DrawText(character, new Vector2(charPos.X + x, charPos.Y + y), Color.Black);
                    }
                }
            }
            
            _graphics.DrawText(character, charPos, Color.Orange);
        }
    }

    private System.Numerics.Vector3 GridPositionToWorld3D(Vector2 gridPosition)
    {
        var sharpDxGridPos = new SharpDX.Vector2(gridPosition.X, gridPosition.Y);
        var worldPos2D = sharpDxGridPos.GridToWorld();
        var terrainHeight = _gameController.IngameState.Data.GetTerrainHeightAt(gridPosition);
        return new System.Numerics.Vector3(worldPos2D.X, worldPos2D.Y, terrainHeight);
    }
}
