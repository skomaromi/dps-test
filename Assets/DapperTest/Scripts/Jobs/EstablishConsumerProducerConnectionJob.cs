using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    public partial struct EstablishConsumerProducerConnectionJob : IJobEntity
    {
        private const int StraightMoveCost = 10;
        private const int DiagonalMoveCost = 14;
        private const int InvalidNodeIndex = -1;
        
        // job parameters
        public int2 gridSize;
        public NativeParallelHashMap<int2, TileType> tileMap;
        
        public NativeArray<Entity> producerEntities;
        public ComponentDataFromEntity<GridTranslation> gridTranslationFromEntity;

        private NativeArray<PathNode> pathNodes;
        private NativeList<PathNode> consumerProducerPath;
        private NativeList<PathNode> candidateConsumerProducerPath;

        public void Execute(ref GridTranslation consumerGridTranslation)
        {
            // what do we do here? so - this is a Consumer foreach
            // for each consumer, seek nearest producer
            // nearest producer is sought by pathfinding
            // nearest producer is the one with the least gCost for end node
            
            // * paint tilemap with road tiles when closest producer found
            // * add consumer entity ref to producer
            // * store path to producer in consumer entity (DynamicBuffer)

            int producerEntityCount = producerEntities.Length;
            
            if (producerEntityCount == 0)
                return;
            
            int2 consumerGridPosition = consumerGridTranslation.position;

            int gridArea = gridSize.x * gridSize.y;
            pathNodes = new NativeArray<PathNode>(gridArea, Allocator.Temp);
            consumerProducerPath = new NativeList<PathNode>(Allocator.Temp);
            candidateConsumerProducerPath = new NativeList<PathNode>(Allocator.Temp);
            
            int leastGCost = int.MaxValue;
            Entity closestProducerEntity;

            for (int i = producerEntityCount - 1; i >= 0; i--)
            {
                Entity producerEntity = producerEntities[i];
                GridTranslation producerGridTranslation = gridTranslationFromEntity[producerEntity];
                int2 producerGridPosition = producerGridTranslation.position;
                
                FindCandidatePath(consumerGridPosition, producerGridPosition, out PathNode endNode);

                if (endNode.gCost < leastGCost)
                {
                    closestProducerEntity = producerEntity;
                    CopyPathFromCandidate();
                }
            }

            pathNodes.Dispose();
            consumerProducerPath.Dispose();
            candidateConsumerProducerPath.Dispose();
        }

        private int ToFlatIndex(int x, int y, int gridWidth)
        {
            return y * gridWidth + x;
        }
        
        private int CalculateDistanceCost(int startX, int startY, int endX, int endY)
        {
            int dx = math.abs(endX - startX);
            int dy = math.abs(endY - startY);
            int remainder = math.abs(dy - dx);

            return 
                DiagonalMoveCost * math.min(dx, dy) + 
                StraightMoveCost * remainder;
        }

        private int CalculateDistanceCost(int startX, int startY, int2 end)
        {
            return CalculateDistanceCost(startX, startY, end.x, end.y);
        }
        
        private bool GetIsWalkable(int x, int y)
        {
            int2 position = new int2(x, y);
            TileType tileType = tileMap[position];
            return tileType == TileType.Blocked;
        }

        private void FindCandidatePath(int2 startPosition, int2 endPosition, out PathNode endNode)
        {
            int gridWidth = gridSize.x;
            int gridHeight = gridSize.y;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    int flatIndex = ToFlatIndex(x, y, gridWidth);
                    
                    PathNode pathNode = new PathNode()
                    {
                        x = x,
                        y = y,
                        gCost = int.MaxValue,
                        hCost = CalculateDistanceCost(x, y, endPosition),
                        isWalkable = GetIsWalkable(x, y),
                        previousNodeIndex = InvalidNodeIndex
                    };

                    pathNode.UpdateFCost();

                    pathNodes[flatIndex] = pathNode;
                }
            }
            
            // TODO: remove.
            endNode = default;
        }
        
        private void CopyPathFromCandidate()
        {
            consumerProducerPath.Clear();
            
            // deliberate flip as built path goes from end to start
            for (int i = candidateConsumerProducerPath.Length - 1; i >= 0; i--)
                consumerProducerPath.Add(candidateConsumerProducerPath[i]);
        }
    }
}
