using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DapperTest
{
    // as per:
    // https://forum.unity.com/threads/compilation-of-issues-with-0-50.1253973/page-2#post-8512268
    #pragma warning disable 0282
    public partial struct EstablishConsumerProducerConnectionJob : IJobEntity
    {
        private const int StraightMoveCost = 10;
        private const int DiagonalMoveCost = 14;
        private const int InvalidNodeIndex = -1;
        
        // job parameters
        public GameSettings settings;
        // TODO: this might not be necessary if road painting here works
        public int2 gridSize;
        public NativeParallelHashMap<int2, TileType> tileMap;
        public NativeArray<Entity> producerEntities;
        public EntityCommandBuffer commandBuffer;
        
        // getters
        public ComponentDataFromEntity<GridTranslation> gridTranslationFromEntity;
        public BufferFromEntity<ConsumerReference> consumerReferenceBufferFromEntity;
        public BufferFromEntity<ConsumerProducerPathNode> consumerProducerPathBufferFromEntity;

        // buffers
        // pathfinding core
        private NativeArray<PathNode> pathNodes;
        private NativeList<int> openNodeIndices;
        private NativeList<int> closedNodeIndices;
        private NativeArray<int2> neighbourOffsets;
        
        // used in establishing producer-consumer relationship
        private NativeList<int2> producerConsumerPath;

        public void Execute(Entity consumerEntity, ref GridTranslation consumerGridTranslation)
        {
            // what do we do here? so - this is a Consumer foreach
            // for each consumer, seek nearest producer
            // nearest producer is sought by pathfinding
            // nearest producer is the one with the least gCost for end node
            
            // * add consumer entity ref to producer for producer round robin
            // * store path to producer in consumer entity (DynamicBuffer)
            // * paint tilemap with road tiles when closest producer found

            int producerEntityCount = producerEntities.Length;
            
            if (producerEntityCount == 0)
                return;
            
            int2 consumerGridPosition = consumerGridTranslation.position;

            int gridArea = gridSize.x * gridSize.y;
            
            // allocate core pathfinding buffers
            pathNodes = new NativeArray<PathNode>(gridArea, Allocator.Temp);
            openNodeIndices = new NativeList<int>(Allocator.Temp);
            closedNodeIndices = new NativeList<int>(Allocator.Temp);
            neighbourOffsets = GenerateNeighbourOffsets();

            producerConsumerPath = new NativeList<int2>(Allocator.Temp);
            
            // seek nearest producer
            int leastGCost = int.MaxValue;
            Entity nearestProducerEntity = default;
            
            for (int i = producerEntityCount - 1; i >= 0; i--)
            {
                Entity producerEntity = producerEntities[i];
                GridTranslation producerGridTranslation = gridTranslationFromEntity[producerEntity];
                int2 producerGridPosition = producerGridTranslation.position;
                
                FindCandidatePath(consumerGridPosition, producerGridPosition, out PathNode producerNode);

                if (producerNode.gCost < leastGCost)
                {
                    nearestProducerEntity = producerEntity;
                    BuildPath(producerNode);
                }
            }
            
            // store consumer ref in nearest producer
            DynamicBuffer<ConsumerReference> consumerReferenceBuffer = consumerReferenceBufferFromEntity[nearestProducerEntity];
            ConsumerReference consumerReference = new ConsumerReference() { entity = consumerEntity };
            consumerReferenceBuffer.Add(consumerReference);
            
            // store path to producer in consumer
            DynamicBuffer<ConsumerProducerPathNode> consumerProducerPathBuffer = consumerProducerPathBufferFromEntity[consumerEntity];
            
            // deliberately iterating reverse to get a path from consumer to
            // producer
            for (int i = producerConsumerPath.Length - 1; i >= 0; i--)
            {
                // store to consumer-producer path buffer
                int2 node = producerConsumerPath[i];
                ConsumerProducerPathNode consumerProducerPathNode = new ConsumerProducerPathNode()
                {
                    gridPosition = node
                };
                consumerProducerPathBuffer.Add(consumerProducerPathNode);
                
                // paint road on tile map
                tileMap[node] = TileType.Road;
                
                // paint actual road tiles
                // TODO: this won't work, but YOLO
                Entity roadEntityInstance = commandBuffer.Instantiate(settings.roadPrefab);
                float3 tilePosition = new float3(
                    settings.tileSize * node.x,
                    0f,
                    settings.tileSize * node.y);
                Translation translation = new Translation() { Value = tilePosition };
                
                commandBuffer.SetComponent(roadEntityInstance, translation);
            }

            // pathfinding buffers release
            pathNodes.Dispose();
            openNodeIndices.Dispose();
            closedNodeIndices.Dispose();
            neighbourOffsets.Dispose();
            
            producerConsumerPath.Dispose();
        }

        private NativeArray<int2> GenerateNeighbourOffsets()
        {
            NativeArray<int2> neighbourOffsets = new NativeArray<int2>(
                8, 
                Allocator.Temp
            );
            
            neighbourOffsets[0] = new int2(-1, 0);  // left 
            neighbourOffsets[1] = new int2(1, 0);   // right
            neighbourOffsets[2] = new int2(0, 1);   // up
            neighbourOffsets[3] = new int2(0, -1);  // down
            neighbourOffsets[4] = new int2(-1, -1); // left down
            neighbourOffsets[5] = new int2(-1, 1);  // left up
            neighbourOffsets[6] = new int2(1, -1);  // right down
            neighbourOffsets[7] = new int2(1, 1);   // right up

            return neighbourOffsets;
        }

        private int ToFlatIndex(int x, int y, int gridWidth)
        {
            return y * gridWidth + x;
        }
        
        private int ToFlatIndex(int2 a, int gridWidth)
        {
            return ToFlatIndex(a.x, a.y, gridWidth);
        }
        
        private int CalculateDistanceCost(int aX, int aY, int bX, int bY)
        {
            int dx = math.abs(bX - aX);
            int dy = math.abs(bY - aY);
            int remainder = math.abs(dy - dx);

            return 
                DiagonalMoveCost * math.min(dx, dy) + 
                StraightMoveCost * remainder;
        }

        private int CalculateDistanceCost(int aX, int aY, int2 b)
        {
            return CalculateDistanceCost(aX, aY, b.x, b.y);
        }
        
        private int CalculateDistanceCost(int2 a, int2 b)
        {
            return CalculateDistanceCost(a.x, a.y, b.x, b.y);
        }
        
        private bool GetIsWalkable(int x, int y)
        {
            int2 position = new int2(x, y);
            TileType tileType = tileMap[position];
            return tileType == TileType.Blocked;
        }
        
        private int GetLowestFCostNodeIndex()
        {
            PathNode lowestCostNode = pathNodes[openNodeIndices[0]];

            for (int i = 1; i < openNodeIndices.Length; i++)
            {
                int candidateNodeIndex = openNodeIndices[i];
                PathNode candidateNode = pathNodes[candidateNodeIndex];

                if (candidateNode.fCost < lowestCostNode.fCost)
                    lowestCostNode = candidateNode;
            }

            return lowestCostNode.index;
        }

        private void FindCandidatePath(int2 startPosition, int2 endPosition, out PathNode endNode)
        {
            int gridWidth = gridSize.x;
            int gridHeight = gridSize.y;

            // (re)initialize grid with initial data
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    int flatIndex = ToFlatIndex(x, y, gridWidth);
                    
                    PathNode pathNode = new PathNode()
                    {
                        x = x,
                        y = y,
                        index = flatIndex,
                        gCost = int.MaxValue,
                        hCost = CalculateDistanceCost(x, y, endPosition),
                        isWalkable = GetIsWalkable(x, y),
                        previousNodeIndex = InvalidNodeIndex
                    };

                    pathNode.UpdateFCost();

                    pathNodes[flatIndex] = pathNode;
                }
            }

            openNodeIndices.Clear();
            closedNodeIndices.Clear();
            
            int startNodeIndex = ToFlatIndex(startPosition, gridWidth);
            int endNodeIndex = ToFlatIndex(endPosition, gridWidth);

            openNodeIndices.Add(startNodeIndex);
            
            while (openNodeIndices.Length > 0)
            {
                int currentNodeIndex = GetLowestFCostNodeIndex();
                PathNode currentNode = pathNodes[currentNodeIndex];

                if (currentNodeIndex == endNodeIndex)
                {
                    // destination reached
                    break;
                }

                // remove current node from list of open ones
                for (int i = 0; i < openNodeIndices.Length; i++)
                {
                    if (currentNodeIndex == openNodeIndices[i])
                    {
                        openNodeIndices.RemoveAtSwapBack(i);
                        break;
                    }
                }
                
                // ... and add to closed ones
                closedNodeIndices.Add(currentNodeIndex);

                int2 currentNodePosition = new int2(currentNode.x, currentNode.y);

                for (int i = 0; i < neighbourOffsets.Length; i++)
                {
                    int2 neighbourOffset = neighbourOffsets[i];
                    int2 neighbourNodePosition = currentNodePosition + neighbourOffset;

                    if (!GridUtility.IsWithinGrid(neighbourNodePosition, gridSize))
                    {
                        // went out of bounds
                        continue;
                    }

                    int neighbourNodeIndex = ToFlatIndex(neighbourNodePosition, gridWidth);

                    if (closedNodeIndices.Contains(neighbourNodeIndex))
                    {
                        // node already explicitly visited (not merely as part of
                        // neighbour R/W)
                        continue;
                    }

                    PathNode neighbourNode = pathNodes[neighbourNodeIndex];

                    if (!neighbourNode.isWalkable)
                        continue;
                    
                    // if this point is reached, node is: within bounds, **not**
                    // visited and walkable; i.e. should be populated now
                    
                    int newGCostCandidate = 
                        currentNode.gCost + 
                        CalculateDistanceCost(currentNodePosition, neighbourNodePosition);
                    
                    // neighbour node either had infinite G cost (int.MaxValue) or a
                    // greater G cost value due to a prior adjacent visitor
                    if (newGCostCandidate < neighbourNode.gCost)
                    {
                        neighbourNode.previousNodeIndex = currentNode.index;
                        neighbourNode.gCost = newGCostCandidate;
                        neighbourNode.UpdateFCost();
                        pathNodes[neighbourNodeIndex] = neighbourNode;

                        if (!openNodeIndices.Contains(neighbourNodeIndex))
                            openNodeIndices.Add(neighbourNodeIndex);
                    }
                }
            }

            // TODO: remove.
            endNode = default;
        }
        
        private int2 GetPosition(PathNode node)
        {
            return new int2(node.x, node.y);
        }

        private void BuildPath(PathNode endNode)
        {
            producerConsumerPath.Clear();
            
            producerConsumerPath.Add(GetPosition(endNode));
            
            PathNode currentNode = endNode;
            while (currentNode.previousNodeIndex != InvalidNodeIndex)
            {
                PathNode previousNode = pathNodes[currentNode.previousNodeIndex];
                producerConsumerPath.Add(GetPosition(previousNode));
                currentNode = previousNode;
            }
        }
    }
}
