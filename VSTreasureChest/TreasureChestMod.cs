using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Interfaces;

namespace Vintagestory.Mods.TreasureChest
{
    /// <summary>
    /// Mod that places chests filled with random items at the base of trees. Also supports a /treasure command for 
    /// placing a chest in front of the player.
    /// </summary>
    public class TreasureChestMod : ModBase
    {
        //The minimum number of items to place in the chest
        private const int MIN_ITEMS = 3;
        
        //The maximum number of items to place in the chest
        private const int MAX_ITEMS = 10;
        
        //The max number of chests to generate per chunk
        private const int MAX_CHESTS_PER_CHUNK = 1;

        //The probability of a chest spawning in a chunk
        private const float CHEST_SPAWN_PROBABILITY = 0.80f;

        //The main interface we will use for interacting with Vintage Story
        private ICoreServerAPI api;

        //Size of chunks. Chunks are cubes so this is the size of the cube.
        private int chunkSize;

        //Stores tree types that will be used for detecting trees for placing our chests
        private ISet<string> treeTypes;

        //Used for accessing blocks during chunk generation
        private IBlockAccessor chunkGenBlockAccessor;

        //Used for accessing blocks after chunk generation
        private IBlockAccessor worldBlockAccessor;

        /// <summary>
        /// This is our starting point. This method will be called by the server.
        /// </summary>
        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            this.worldBlockAccessor = api.World.BlockAccessor;
            this.chunkSize = worldBlockAccessor.ChunkSize;
            this.treeTypes = new HashSet<string>();
            LoadTreeTypes(treeTypes);

            //Registers our command with the system's command registry.
            this.api.RegisterCommand("treasure", "Place a treasure chest with random items", "", PlaceTreasureChestInFrontOfPlayer, Privilege.controlserver);

            //Registers a delegate to be called so we can get a reference to the chunk gen block accessor
            this.api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

            //Registers a delegate to be called when a chunk column is generating in the Vegetation phase of generation
            this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation);
        }

        /// <summary>
        /// Our mod only needs to be loaded by the server
        /// </summary>
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        ///<summary>
        /// Loads tree types from worldproperties/block/wood.json. Used for detecting trees for chest placement.
        /// </summary>
        private void LoadTreeTypes(ISet<string> treeTypes)
        {
            WorldProperty treeTypesFromFile = api.Assets.TryGet("worldproperties/block/wood.json").ToObject<WorldProperty>();
            foreach (WorldPropertyVariant variant in treeTypesFromFile.Variants)
            {
                treeTypes.Add("log-" + variant.Code + "-ud");
            }
        }
        /// <summary>
        /// Stores the chunk gen thread's IBlockAccessor for use when generating chests during chunk gen. This callback
        /// is necessary because chunk loading happens in a separate thread and it's important to use this block accessor
        /// when placing chests during chunk gen.
        /// </summary>
        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        /// <summary>
        /// Called when a number of chunks have been generated. For each chunk we first determine if we should place a chest
        /// and if we should we then loop through each block to find a tree. When one is found we place the block at the base
        /// of the tree. At most one chest will be placed per chunk.
        /// </summary>
        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            int chestsPlacedCount = 0;
            for (int i = 0; i < chunks.Length; i++)
            {
                if(ShouldPlaceChest())
                {
                    BlockPos blockPos = new BlockPos();
                    for (int x = 0; x < chunkSize; x++)
                    {
                        for (int z = 0; z < chunkSize; z++)
                        {
                            for (int y = 0; y < worldBlockAccessor.MapSizeY; y++)
                            {
                                if(chestsPlacedCount < MAX_CHESTS_PER_CHUNK)
                                {
                                    blockPos.X = chunkX * chunkSize + x;
                                    blockPos.Y = y;
                                    blockPos.Z = chunkZ * chunkSize + z;

                                    BlockPos chestLocation = TryGetChestLocation(blockPos);
                                    if (chestLocation != null)
                                    {
                                        bool chestWasPlaced = PlaceTreasureChest(chunkGenBlockAccessor, chestLocation);
                                        if (chestWasPlaced)
                                        {
                                            chestsPlacedCount++;
                                        }
                                    }
                                }
                                else//Max chests have been placed for this chunk
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the location to place the chest if the given world coordinates is a tree, null if it's not a tree.
        /// </summary>
        private BlockPos TryGetChestLocation(BlockPos pos)
        {
            Block block = chunkGenBlockAccessor.GetBlock(pos);
            if(IsTreeLog(block))
            {
                for (int posY = pos.Y; posY >= 0; posY--)
                {
                    while (pos.Y-- > 0)
                    {
                        Block underBlock = chunkGenBlockAccessor.GetBlock(pos);
                        if (IsTreeLog(underBlock)) continue;

                        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                        {
                            BlockPos adjacentPos = pos.AddCopy(facing).Up();
                            if (chunkGenBlockAccessor.GetBlock(adjacentPos).Id == 0)
                            {
                                return adjacentPos;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool IsTreeLog(Block block)
        {
            return treeTypes.Contains(block.Code);            
        }

        /// <summary>
        /// Delegate for /treasure command. Places a treasure chest 2 blocks in front of the player
        /// </summary>
        private void PlaceTreasureChestInFrontOfPlayer(IServerPlayer player, int groupId, CmdArgs args)
        {
            PlaceTreasureChest(api.World.BlockAccessor, player.Entity.Pos.HorizontalAheadCopy(2).AsBlockPos);
        }

        /// <summary>
        /// Places a chest filled with random items at the given world coordinates using the given IBlockAccessor
        /// </summary>
        private bool PlaceTreasureChest(IBlockAccessor blockAccessor, BlockPos pos)
        {
            ushort blockID = api.WorldManager.GetBlockId("chest-south");
            Block chest = api.WorldManager.GetBlockType(blockID);
            chest.TryPlaceBlockForWorldGen(blockAccessor, pos, BlockFacing.UP);
            IBlockEntityContainer chestEntity = (IBlockEntityContainer)blockAccessor.GetBlockEntity(pos);
            if (chestEntity != null)
            {
                AddItemStacks(chestEntity, MakeItemStacks());
                System.Diagnostics.Debug.WriteLine("Placed treasure chest at " + pos.ToString(), new object[] { });
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FAILED TO PLACE TREASURE CHEST AT " + pos.ToString(), new object[] { });
                return false;
            }
        }

        private bool ShouldPlaceChest()
        {
            int randomNumber = api.World.Rand.Next(0, 100);
            return randomNumber > 0 && randomNumber <= CHEST_SPAWN_PROBABILITY*100;
        }

        /// <summary>
        /// Makes a list of random ItemStacks to be placed inside our chest
        /// </summary>
private IEnumerable<ItemStack> MakeItemStacks()
{
    ShuffleBag<string> shuffleBag = MakeShuffleBag();
    Dictionary<string, ItemStack> itemStacks = new Dictionary<string, ItemStack>();
    int grabCount = api.World.Rand.Next(MIN_ITEMS, MAX_ITEMS);
    for (int i = 0; i < grabCount; i++)
    {
        string nextItem = shuffleBag.Next();
        Item item = api.World.GetItem(nextItem);
        if (itemStacks.ContainsKey(nextItem))
        {
            itemStacks[nextItem].StackSize++;
        }
        else
        {
            itemStacks.Add(nextItem, new ItemStack(item));
        }
    }
    return itemStacks.Values;
}

        ///<summary>
        ///Adds the given list of ItemStacks to the first slots in the given chest.
        ///</summary>
private void AddItemStacks(IBlockEntityContainer chest, IEnumerable<ItemStack> itemStacks)
{
    int slotNumber = 0;
    foreach (ItemStack itemStack in itemStacks)
    {
        slotNumber = Math.Min(slotNumber, chest.Inventory.QuantitySlots - 1);
        IItemSlot slot = chest.Inventory.GetSlot(slotNumber);
        slot.Itemstack = itemStack;
        slotNumber++;
    }
}

        /// <summary>
        /// Creates our ShuffleBag to pick from when generating items for the chest
        /// </summary>
private ShuffleBag<string> MakeShuffleBag()
{
    ShuffleBag<string> shuffleBag = new ShuffleBag<string>(100, api.World.Rand);
    shuffleBag.Add("ingot-iron", 10);
    shuffleBag.Add("ingot-bismuth", 5);
    shuffleBag.Add("ingot-silver", 5);
    shuffleBag.Add("ingot-zinc", 5);
    shuffleBag.Add("ingot-titanium", 5);
    shuffleBag.Add("ingot-platinum", 5);
    shuffleBag.Add("ingot-chromium", 5);
    shuffleBag.Add("ingot-tin", 5);
    shuffleBag.Add("ingot-lead", 5);
    shuffleBag.Add("ingot-gold", 5);
    return shuffleBag;
}
    }
}