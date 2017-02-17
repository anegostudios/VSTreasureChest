using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Interfaces;

namespace Vintagestory.TreasureChest
{
    public class TreasureChestMod : ModBase
    {
        private int minItems = 3;
        private int maxItems = 10;
        private ICoreServerAPI api;
        private int chunkSize;

        private ISet<string> treeTypes;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            this.chunkSize = api.World.BlockAccessor.ChunkSize;

            this.api.RegisterCommand("treasure", "Place a treasure chest with random items", "", PlaceTreasureChest, Privilege.controlserver);

            this.treeTypes = new HashSet<string>();
            WorldProperty treetypes = api.Assets.TryGet("worldproperties/block/wood.json").ToObject<WorldProperty>();
            foreach (WorldPropertyVariant variant in treetypes.Variants)
            {
                treeTypes.Add("log-" + variant.Code + "-ud");
            }

            //TODO: Uncomment when we are ready to load on chunk generation
            this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Vegetation);
        }

        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            if(ShouldPlaceChest())
            {
                BlockPos blockPos = new BlockPos();

                for (int i = 0; i < chunks.Length; i++)
                {
                    IServerChunk chunk = chunks[i];
                    for (int x = 0; x < chunks.Length; x++)
                    {
                        for (int z = 0; z < chunks.Length; z++)
                        {
                            for (int y = 0; y < chunks.Length; y++)
                            {
                                blockPos.X = x;
                                blockPos.Y = y;
                                blockPos.Z = z;

                                BlockPos chestLocation = GetChestLocation(chunk, x, y, z);
                                if(chestLocation != null)
                                {
                                    PlaceTreasureChest(ToWorldCoordinates(chunk, chestLocation));
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        //TODO: Need to figure out how to convert local chunk coordinates to world coordinates.(Need chunk x,z coordinates);
        private BlockPos ToWorldCoordinates(IServerChunk chunk, BlockPos chunkCoordinates)
        {
            int chunkX = 0;
            int chunkZ = 0;
            return new BlockPos(chunkX * chunkSize + chunkCoordinates.X, chunkCoordinates.Y, chunkZ * chunkSize + chunkCoordinates.Z);
        }

        //TODO: Always places a single chest in a chunk for testing.
        private bool ShouldPlaceChest()
        {
            return true;
            //int randomNumber = api.World.Rand.Next(0, 100);
            //return randomNumber > 1 && randomNumber <= 6;//5% chance
        }

        private BlockPos GetChestLocation(IServerChunk chunk, int x, int y, int z)
        {
            Block block = GetBlock(chunk, x, y, z);
            if(IsTree(block))
            {
                for(int i = y; i > 0; i--)
                {
                    Block underBlock = GetBlock(chunk, x, i, z);
                    if(!IsTree(underBlock))
                    {
                        return new BlockPos(x, i + 1, z);
                    }
                }
            }
            return null;
        }

        private bool IsTree(Block block)
        {
            return treeTypes.Contains(block.Code);            
        }

        private Block GetBlock(IServerChunk chunk, int x, int y, int z)
        {
            int index = (y * chunkSize + z) * chunkSize + x;
            ushort blockId = chunk.Blocks[index];
            return api.World.GetBlock(blockId);
        }

        private int GetSunlight(IServerChunk chunk, int x, int y, int z)
        {
            chunk.Unpack();
            int index3d = ((x % chunkSize) * chunkSize + (z % chunkSize)) * chunkSize + (x % chunkSize);
            return chunk.Light[index3d] & 31;
        }

        private int GetBlockLight(IServerChunk chunk, int x, int y, int z)
        {
            chunk.Unpack();
            int index3d = ((x % chunkSize) * chunkSize + (z % chunkSize)) * chunkSize + (x % chunkSize);
            return (chunk.Light[index3d] >> 5) & 31;
        }

        private void PlaceTreasureChest(IServerPlayer player, int groupId, CmdArgs args)
        {
            PlaceTreasureChest(player.Entity.Pos.HorizontalAheadCopy(2).AsBlockPos);
        }

        private void PlaceTreasureChest(BlockPos pos)
        {
            ushort blockID = api.WorldManager.GetBlockId("chest-south");
            api.World.BlockAccessor.SetBlock(blockID, pos);
            IBlockEntityContainer chest = (IBlockEntityContainer)api.World.BlockAccessor.GetBlockEntity(pos);       
            if(chest == null)
            {
                System.Diagnostics.Debug.WriteLine("Chest was null at " + pos.ToString(), new object[] { });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Treasure chest at " + pos.ToString(), new object[] { });
                AddItemStacks(chest, MakeItemStacks());
            }
            
        }

        private void AddItemStacks(IBlockEntityContainer chest, IEnumerable<ItemStack> itemStacks)
        {
            int slotNumber = 0;
            foreach (ItemStack itemStack in itemStacks)
            {
                if (slotNumber > chest.Inventory.QuantitySlots)
                {
                    slotNumber = chest.Inventory.QuantitySlots - 1;
                }
                IItemSlot slot = chest.Inventory.GetSlot(slotNumber);
                slot.Itemstack = itemStack;
                slotNumber++;
            }
        }

        private IEnumerable<ItemStack> MakeItemStacks()
        {
            ShuffleBag<string> shuffleBag = MakeShuffleBag();
            Dictionary<string, ItemStack> itemStacks = new Dictionary<string, ItemStack>();
            int grabCount = api.World.Rand.Next(minItems, maxItems);
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