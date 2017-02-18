﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.Mods.TreasureChest
{
    /// <summary>
    /// Data structure for picking random items
    /// </summary>
    public class ShuffleBag<T>
    {
        private Random random = new Random();
        private List<T> data;

        private T currentItem;
        private int currentPosition = -1;

        private int Capacity { get { return data.Capacity; } }
        public int Size { get { return data.Count; } }

        public ShuffleBag(int initCapacity)
        {
            this.data = new List<T>(initCapacity);
            this.random = new Random();
        }

        public ShuffleBag(int initCapacity, Random random)
        {
            this.random = random;
            this.data = new List<T>(initCapacity);
        }

        /// <summary>
        /// Adds the specified number of the given item to the bag
        /// </summary>
        public void Add(T item, int amount)
        {
            for (int i = 0; i < amount; i++)
                data.Add(item);

            currentPosition = Size - 1;
        }

        /// <summary>
        /// Returns the next random item from the bag
        /// </summary>
        public T Next()
        {
            if (currentPosition < 1)
            {
                currentPosition = Size - 1;
                currentItem = data[0];

                return currentItem;
            }

            var pos = random.Next(currentPosition);

            currentItem = data[pos];
            data[pos] = data[currentPosition];
            data[currentPosition] = currentItem;
            currentPosition--;

            return currentItem;
        }
    }
}
