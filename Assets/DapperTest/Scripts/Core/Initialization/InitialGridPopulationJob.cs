using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DapperTest
{
    public struct InitialGridPopulationJob : IJob
    {
        public GameSettings settings;
        public NativeParallelHashMap<int2, TileType> tileMap;
        public EntityCommandBuffer commandBuffer;
        public Random random;

        public void Execute()
        {
            WalkMap(settings, ref tileMap, ref random);
            
            FillUnpopulatedTiles(settings, ref tileMap);
            
            PlaceTiles(TileType.Producer, settings.producerCount, ref tileMap, settings, ref random);
            PlaceTiles(TileType.Consumer, settings.consumerCount, ref tileMap, settings, ref random);
            
            GridUtility.SpawnPrefabs(
                settings, 
                ref commandBuffer, 
                ref tileMap, 
                TileType.Empty | TileType.Producer | TileType.Consumer);
        }
        
        private static Direction GetRandomDirection(ref Random random)
        {
            return (Direction)random.NextInt(0, (int)Direction.Left);
        }

        private static int2 GetRandomPointInGrid(ref Random random, GameSettings settings)
        {
            return random.NextInt2(int2.zero, settings.gridSize);
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
        
        private static bool IsViableMove(int2 position, int2 gridSize, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return position.y + 1 < gridSize.y;
                
                case Direction.Right:
                    return position.x + 1 < gridSize.x;
                
                case Direction.Down:
                    return position.y - 1 >= 0;
                
                case Direction.Left:
                    return position.x - 1 >= 0;
                
                default:
                    return false;
            }
        }

        private static int GetViableDirectionCount(int2 position, int2 gridSize)
        {
            int viableDirectionCount = 0;

            if (IsViableMove(position, gridSize, Direction.Up))
                viableDirectionCount++;
            if (IsViableMove(position, gridSize, Direction.Right))
                viableDirectionCount++;
            if (IsViableMove(position, gridSize, Direction.Down))
                viableDirectionCount++;
            if (IsViableMove(position, gridSize, Direction.Left))
                viableDirectionCount++;

            return viableDirectionCount;
        }
        
        private static Direction GetViableDirection(int directionIndex, int2 position, int2 gridSize)
        {
            int candidateIndex = -1;

            bool CheckNext(Direction direction)
            {
                if (!IsViableMove(position, gridSize, direction)) 
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
        
        private static Direction GetRandomViableDirection(ref Random random, int2 position, int2 gridSize)
        {
            int viableDirectionCount = GetViableDirectionCount(position, gridSize);
            int directionIndex = random.NextInt(0, viableDirectionCount);
            return GetViableDirection(directionIndex, position, gridSize);
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
                
                bool shouldChangeDirection = forceChangeDirection || random.NextBool();

                if (shouldChangeDirection)
                {
                    direction = GetRandomViableDirection(ref random, position, gridSize);
                    forceChangeDirection = false;
                }

                int2 newPosition = MoveInDirection(position, direction);
                bool newPositionIsOutOfBounds = !GridUtility.IsWithinGrid(newPosition, gridSize);

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
        
        private static void PlaceTiles(TileType tileType, int tileCount,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings, ref Random random)
        {
            int remainingCount = tileCount;

            while (remainingCount > 0)
            {
                int2 mapPoint = GetRandomPointInGrid(ref random, settings);
                
                if (tileMap[mapPoint] != TileType.Blocked)
                    continue;

                tileMap[mapPoint] = tileType;
                remainingCount--;
            }
        }
    }
}
