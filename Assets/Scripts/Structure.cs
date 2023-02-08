using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure
{
    /// <summary>
    /// add tree at the given position
    /// </summary>
    /// <param name="position"> the position to the tree to (the trunk is placed here) </param>
    /// <param name="minTrunkHeight"> the minimum height of the tree </param>
    /// <param name="maxTrunkHeight"> the maximum height of the tree </param>
    /// <returns></returns>
    public static Queue<VoxelMod> MakeTree(Vector3 position, int minTrunkHeight, int maxTrunkHeight)
    {
        Queue<VoxelMod> queue = new Queue<VoxelMod>();
        // get random height
        int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector2(position.x, position.z), 250f, 3f));
        // make sure the tree doesn't go below the minimum height
        if (height < minTrunkHeight) height = minTrunkHeight;

        /* BUILD TREE */
        
        for (int x = -2; x < 3; x++)
        {
            for (int z = -2; z < 3; z++)
            {
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 2, position.z + z), 11));
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 3, position.z + z), 11));
            }
        }

        for (int x = -1; x < 2; x++)
        {
            for (int z = -1; z < 2; z++)
            {
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 1, position.z + z), 11));
            }
        }
        
        for (int x = -1; x < 2; x++)
        {
            if (x == 0)
                for (int z = -1; z < 2; z++)
                {
                    queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height, position.z + z), 11));
                }
            else
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height, position.z), 11));
        }

        for (int i = 1; i < height; i++)
        {
            queue.Enqueue(new VoxelMod(new Vector3(position.x, position.y + i, position.z),8));
        }

        return queue;
    }
}
