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

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            this.chunkSize = api.World.BlockAccessor.ChunkSize;

            this.api.RegisterCommand("treasure", "Place a treasure chest with random items", "", PlaceTreasureChest, Privilege.controlserver);

            //TODO: Uncomment when we are ready to load on chunk generation
            //this.api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.TerrainFeatures);
        }

        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ)
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

                            if (IsValidChestLocation(chunk, x, y, z))
                            {
                                PlaceTreasureChest(blockPos);
                            }
                        }
                    }
                }
            }
        }

        private bool IsValidChestLocation(IServerChunk chunk, int x, int y, int z)
        {
            int sunlight = GetSunlight(chunk, x, y, z);
            if (sunlight < 9)
            {
                Block block = GetBlock(chunk, x, y, z);

                //TODO: add more conditions later, like are we beside a wall, on the floor
                return true;
            }
            return false;
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
            AddItemStacks(chest, MakeItemStacks());
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