using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DapperTest
{
    public struct GridInitializationJob : IJob
    {
        public GameSettings settings;
        public Entity settingsEntity;
            
        public NativeParallelHashMap<int2, TileType> tileMap;
        public EntityCommandBuffer commandBuffer;
        public Random random;

        public void Execute()
        {
            WalkMap(settings, ref tileMap, ref random);
            
            // TODO: split in subregions, parallelize and chain work before and
            // after?
            FillUnpopulatedTiles(settings, ref tileMap);
            
            PlaceTiles(TileType.Producer, settings.producerCount, ref tileMap, settings, ref random);
            PlaceTiles(TileType.Consumer, settings.consumerCount, ref tileMap, settings, ref random);
                
            // TODO: NOT optimal. use flags
            GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Empty);
            GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Producer);
            GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Consumer);

            // add tag to mark grid as initialized
            commandBuffer.AddComponent<GridInitializedTag>(settingsEntity);
        }
        
        private static Direction GetRandomDirection(ref Random random)
        {
            return (Direction)random.NextInt(0, (int)Direction.Left);
        }
        
        private static int2 GetNextInt2(ref Random random, int minX, int minY, int maxX, int maxY)
        {
            return random.NextInt2(
                new int2(minX, minY), 
                new int2(maxX, maxY));
        }
        
        private static int2 GetRandomPointInGrid(ref Random random, GameSettings settings)
        {
            return GetNextInt2(ref random, 0, 0, settings.gridSize.x, settings.gridSize.y);
        }
        
        private static int2 MoveInDirection(int2 position, Direction direction)
        {
            int2 newPosition = position;
            
            switch (direction)
            {
                case Direction.Up:
                {
                    newPosition.y++;
                    break;
                }
                
                case Direction.Right:
                {
                    newPosition.x++;
                    break;
                }

                case Direction.Down:
                {
                    newPosition.y--;
                    break;
                }
                
                case Direction.Left:
                {
                    newPosition.x--;
                    break;
                }
            }
            
            return newPosition;
        }
        
        private static bool IsWithinGrid(int2 position, int2 gridSize)
        {
            return
                position.x >= 0 && position.x < gridSize.x &&
                position.y >= 0 && position.y < gridSize.y;
        }
        
        private static bool IsViableMove(int2 position, GameSettings settings, Direction direction)
        {
            return IsWithinGrid(MoveInDirection(position, direction), settings.gridSize);
        }
        
        private static int GetViableDirectionCount(int2 position, GameSettings settings)
        {
            int viableDirectionCount = 0;

            if (IsViableMove(position, settings, Direction.Up))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Right))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Down))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Left))
                viableDirectionCount++;

            return viableDirectionCount;
        }
        
        private static Direction GetViableDirection(int directionIndex, int2 position, GameSettings settings)
        {
            int candidateIndex = -1;

            bool CheckNext(Direction direction)
            {
                if (!IsViableMove(position, settings, direction)) 
                    return false;
                
                candidateIndex++;
                return candidateIndex == directionIndex;
            }

            if (CheckNext(Direction.Up))
                return Direction.Up;
            
            if (CheckNext(Direction.Right))
                return Direction.Right;
            
            if (CheckNext(Direction.Down))
                return Direction.Down;
            
            return Direction.Left;
        }
        
        // TODO: make more efficient. IsViableMove could directly compute destination position and check if hits
        // the corresponding wall (e.g. for `up` just check incremented y against gridSize)
        private static Direction GetRandomViableDirection(ref Random random, int2 position, GameSettings settings)
        {
            int viableDirectionCount = GetViableDirectionCount(position, settings);
            int directionIndex = random.NextInt(0, viableDirectionCount);
            return GetViableDirection(directionIndex, position, settings);
        }
        
        private static void WalkMap(GameSettings settings, ref NativeParallelHashMap<int2, TileType> tileMap,
            ref Random random)
        {
            int2 gridSize = settings.gridSize;

            int gridMinAxis = math.min(gridSize.x, gridSize.y);
            int gridArea = gridSize.x * gridSize.y;

            int stepCount = random.NextInt(gridMinAxis, gridArea);

            Direction direction = GetRandomDirection(ref random);
            int remainingSteps = stepCount;

            int2 startingPosition = GetRandomPointInGrid(ref random, settings);
            int2 position = startingPosition;

            int emptyTiles = gridArea;
            int maxEmptyTiles = (int)(settings.maxEmptyTilesFactor * gridArea);

            int moveLimit = (int)(gridArea * settings.moveLimitFactor);
            int totalMoves = 0;

            bool forceChangeDirection = false;

            while (true)
            {
                if (remainingSteps <= 0)
                {
                    // minimum step count is met, but is the empty tiles condition met as well?
                    if (emptyTiles > maxEmptyTiles)
                    {
                        // empty tiles count still greater than maximum allowed, don't break yet
                        // ... but what if already running for too long?
                        if (totalMoves >= moveLimit)
                            break;
                    }
                    else
                    {
                        // all steps exhausted and empty tile count is less than maximum allowed, can break
                        break;
                    }
                }

                totalMoves++;

                if (tileMap.TryAdd(position, TileType.Blocked)) 
                    emptyTiles--;

                // TODO: might secretly bump head a few times into the same wall, rework logic to change direction
                // the first time it does that
                bool shouldChangeDirection = random.NextBool() || forceChangeDirection;

                if (shouldChangeDirection)
                {
                    direction = GetRandomViableDirection(ref random, position, settings);
                    forceChangeDirection = false;
                }

                int2 newPosition = MoveInDirection(position, direction);
                bool newPositionIsOutOfBounds = !IsWithinGrid(newPosition, gridSize);

                if (newPositionIsOutOfBounds)
                {
                    forceChangeDirection = true;
                    continue;
                }

                position = newPosition;
                remainingSteps--;
            }
        }
        
        private static void FillUnpopulatedTiles(GameSettings settings,
            ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            int2 gridSize = settings.gridSize;
            
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int2 position = new int2(x, y);
                    
                    // if add fails, point already populated by non-Empty TileType
                    tileMap.TryAdd(position, TileType.Empty);
                }
            }
        }
        
        private static bool AnyAdjacentTileIsType(int2 mapPoint, TileType tileType,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings)
        {
            int2 pointAbove = MoveInDirection(mapPoint, Direction.Up);
            if (IsWithinGrid(pointAbove, settings.gridSize) && tileMap[pointAbove] == tileType)
                return true;
            
            int2 pointRight = MoveInDirection(mapPoint, Direction.Right);
            if (IsWithinGrid(pointRight, settings.gridSize) && tileMap[pointRight] == tileType)
                return true;
            
            int2 pointBelow = MoveInDirection(mapPoint, Direction.Down);
            if (IsWithinGrid(pointBelow, settings.gridSize) && tileMap[pointBelow] == tileType)
                return true;
            
            int2 pointLeft = MoveInDirection(mapPoint, Direction.Left);
            if (IsWithinGrid(pointLeft, settings.gridSize) && tileMap[pointLeft] == tileType)
                return true;

            return false;
        }
        
        private static void PlaceTiles(TileType tileType, int tileCount,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings, ref Random random)
        {
            int remainingCount = tileCount;

            while (remainingCount > 0)
            {
                int2 mapPoint = GetRandomPointInGrid(ref random, settings);
                
                if (tileMap[mapPoint] != TileType.Blocked && 
                    !AnyAdjacentTileIsType(mapPoint, TileType.Blocked, ref tileMap, settings))
                    continue;

                tileMap[mapPoint] = tileType;
                remainingCount--;
            }
        }
    }
}
