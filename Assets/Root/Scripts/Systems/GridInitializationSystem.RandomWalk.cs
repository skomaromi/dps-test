﻿using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Root
{
    public partial class GridInitializationSystem
    {
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
        
        private static bool IsWithinGrid(int2 position, Vector2Int gridSize)
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

        private static void WalkMap(GameSettings settings, ref NativeParallelHashMap<int2, TileType> tileMap,
            ref Random random)
        {
            Vector2Int gridSize = settings.gridSize;

            int gridMinAxis = math.min(gridSize.x, gridSize.y);
            int gridArea = gridSize.x * gridSize.y;

            int stepCount = random.NextInt(gridMinAxis, gridArea);

            Debug.Log($"chose {stepCount} steps, total cell count: {gridArea}");

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
                        {
                            Debug.Log(
                                $"running for too long! totalMoves: {totalMoves}, moveLimit: {moveLimit}");
                            break;
                        }
                    }
                    else
                    {
                        // all steps exhausted and empty tile count is less than maximum allowed, can break
                        break;
                    }
                }

                totalMoves++;
                Debug.Log($"iteration {totalMoves} of WalkMap");

                if (tileMap.TryAdd(position, TileType.Blocked))
                {
                    Debug.Log($"marking tile {position.x}, {position.y} as blocked!");
                    emptyTiles--;
                }

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
                    Debug.Log("next position out of bounds!");
                    forceChangeDirection = true;
                    continue;
                }

                position = newPosition;
                remainingSteps--;
            }
        }
    }
}