using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    // autogenerated job struct counterparts carry fields too which cause CS0282
    // warning. pragma statement below disables it.
    // source:
    // https://forum.unity.com/threads/compilation-of-issues-with-0-50.1253973/page-2#post-8512268
    #pragma warning disable 0282
    public partial struct EstablishConsumerProducerConnectionJob : IJobEntity
    {
        private const int StraightMoveCost = 10;
        private const int DiagonalMoveCost = 14;
        private const int InvalidNodeIndex = -1;
        
        // job parameters
        public int2 gridSize;
        public NativeParallelHashMap<int2, TileType> tileMap;
        public NativeArray<Entity> producerEntities;

        // getters
        public ComponentDataFromEntity<GridTranslation> gridTranslationFromEntity;
        public BufferFromEntity<ConsumerSlot> consumerSlotBufferFromEntity;
        public BufferFromEntity<ConsumerProducerPathNode> consumerProducerPathBufferFromEntity;

        private void Execute(Entity consumerEntity, ref Consumer consumer)
        {
            // * find nearest producer
            // * add consumer entity ref to nearest producer for producer round
            //   robin
            // * store path to nearest producer in consumer entity
            //   (stored as positional nodes in consumer's DynamicBuffer)
            // * paint tilemap with road tiles from consumer to nearest producer
            
            int producerEntityCount = producerEntities.Length;

            if (producerEntityCount == 0)
                return;

            GridTranslation consumerGridTranslation = gridTranslationFromEntity[consumerEntity];
            int2 consumerGridPosition = consumerGridTranslation.position;

            int gridWidth = gridSize.x;
            int gridHeight = gridSize.y;
            int gridArea = gridWidth * gridHeight;
            
            // allocate core pathfinding buffers
            NativeArray<PathNode> pathNodes = new NativeArray<PathNode>(gridArea, Allocator.Temp);
            NativeList<int> openNodeIndices = new NativeList<int>(Allocator.Temp);
            NativeList<int> closedNodeIndices = new NativeList<int>(Allocator.Temp);
            NativeArray<int2> neighbourOffsets = GenerateNeighbourOffsets();

            NativeList<int2> producerConsumerPath = new NativeList<int2>(Allocator.Temp);
            
            // seek nearest producer
            int nearestGCost = int.MaxValue;
            int2 nearestProducerGridPosition = default;
            Entity nearestProducerEntity = default;
            
            for (int i = producerEntityCount - 1; i >= 0; i--)
            {
                Entity producerEntity = producerEntities[i];
                
                GridTranslation producerGridTranslation = gridTranslationFromEntity[producerEntity];
                int2 producerGridPosition = producerGridTranslation.position;
                
                FindCandidatePath(
                    consumerGridPosition, producerGridPosition,
                    out PathNode producerNode, 
                    ref neighbourOffsets, 
                    ref openNodeIndices, 
                    ref closedNodeIndices, 
                    ref pathNodes);
                
                if (producerNode.gCost < nearestGCost)
                {
                    nearestGCost = producerNode.gCost;
                    nearestProducerGridPosition = producerGridPosition;
                    nearestProducerEntity = producerEntity;
                    BuildPath(producerNode, ref producerConsumerPath, ref pathNodes);
                }
            }
            
            // producer-consumer setup
            // store consumer ref in nearest producer
            DynamicBuffer<ConsumerSlot> consumerSlotBuffer = consumerSlotBufferFromEntity[nearestProducerEntity];
            ConsumerSlot consumerSlot = new ConsumerSlot() { entity = consumerEntity };
            consumerSlotBuffer.Add(consumerSlot);
            
            // consumer-producer setup
            // store producer ref in consumer
            consumer.associatedProducerEntity = nearestProducerEntity;
            
            // store path to producer in consumer
            DynamicBuffer<ConsumerProducerPathNode> consumerProducerPathBuffer = consumerProducerPathBufferFromEntity[consumerEntity];
            
            // deliberately iterating reverse to get a path from consumer to
            // producer
            int startNodeIndex = ToFlatIndex(consumerGridPosition, gridWidth);
            int endNodeIndex = ToFlatIndex(nearestProducerGridPosition, gridWidth);
            
            for (int i = producerConsumerPath.Length - 1; i >= 0; i--)
            {
                // store to consumer-producer path buffer
                int2 node = producerConsumerPath[i];
                ConsumerProducerPathNode consumerProducerPathNode = new ConsumerProducerPathNode()
                {
                    gridPosition = node
                };
                consumerProducerPathBuffer.Add(consumerProducerPathNode);
                
                // paint road on nodes along the path, except for starting and
                // end nodes, where consumer and producer are located,
                // respectively
                int nodeIndex = ToFlatIndex(node, gridWidth);
                if (nodeIndex != startNodeIndex && nodeIndex != endNodeIndex)
                    tileMap[node] = TileType.Road;
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
                4, 
                Allocator.Temp
            );
            
            neighbourOffsets[0] = new int2(-1, 0);  // left 
            neighbourOffsets[1] = new int2(1, 0);   // right
            neighbourOffsets[2] = new int2(0, 1);   // up
            neighbourOffsets[3] = new int2(0, -1);  // down

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
            
            return 
                tileType == TileType.Blocked || 
                tileType == TileType.Road;
        }
        
        private int GetLowestFCostNodeIndex(ref NativeList<int> openNodeIndices, ref NativeArray<PathNode> pathNodes)
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

        private void FindCandidatePath(
            int2 startPosition,
            int2 endPosition,
            out PathNode endNode,
            ref NativeArray<int2> neighbourOffsets,
            ref NativeList<int> openNodeIndices,
            ref NativeList<int> closedNodeIndices,
            ref NativeArray<PathNode> pathNodes)
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
            
            // when populating isWalkable during initialization, only Blocked
            // and Road are considered walkable. explicitly set isWalkable to
            // true to ensure pathfinding algorithm can stand on both start and
            // end nodes
            PathNode startNode = pathNodes[startNodeIndex];
            startNode.gCost = 0;
            startNode.UpdateFCost();
            startNode.isWalkable = true;
            pathNodes[startNodeIndex] = startNode;

            endNode = pathNodes[endNodeIndex];
            endNode.isWalkable = true;
            pathNodes[endNodeIndex] = endNode;

            openNodeIndices.Add(startNodeIndex);

            while (openNodeIndices.Length > 0)
            {
                int currentNodeIndex = GetLowestFCostNodeIndex(ref openNodeIndices, ref pathNodes);
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

                int2 currentNodePosition = GetPosition(currentNode);

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
            
            endNode = pathNodes[endNodeIndex];
        }
        
        private int2 GetPosition(PathNode node)
        {
            return new int2(node.x, node.y);
        }

        private void BuildPath(
            PathNode endNode, 
            ref NativeList<int2> producerConsumerPath,
            ref NativeArray<PathNode> pathNodes)
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