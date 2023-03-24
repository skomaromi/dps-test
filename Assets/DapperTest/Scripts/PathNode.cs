namespace DapperTest
{
    public struct PathNode
    {
        public int x;
        public int y;
        
        // cost to move from start node to current one
        public int gCost;
        // cost to move from current node to end node
        public int hCost;
        // NOTE: both costs do NOT include obstacles when computing cost
        
        // F = G + H
        public int fCost;
        
        public bool isWalkable;
        public int previousNodeIndex;

        public void UpdateFCost()
        {
            fCost = gCost + hCost;
        }
    }
}
