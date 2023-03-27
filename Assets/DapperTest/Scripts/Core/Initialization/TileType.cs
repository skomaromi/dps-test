using System;

namespace DapperTest
{
    [Flags]
    public enum TileType
    {
        Empty = 1 << 0,
        Blocked = 1 << 1,
        
        Road = 1 << 2,
        Producer = 1 << 3,
        Consumer = 1 << 4
    }
}
