using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.Items;
using System.Text;

namespace DigitalWorldOnline.Commons.Models.Base
{
    public partial class ItemListModel
    {
        private readonly object _itemsLock = new object();

        /// <summary>
        /// Returns the current itens in inventory.
        /// </summary>
        public byte Count
        {
            get
            {
                lock (_itemsLock)
                {
                    return (byte)Items.Count(x => x.ItemId != 0);
                }
            }
        }

        /// <summary>
        /// Return the current free slots amount.
        /// </summary>
        public byte TotalEmptySlots
        {
            get
            {
                lock (_itemsLock)
                {
                    return (byte)Items.Count(x => x.ItemId == 0);
                }
            }
        }

        public int RetrieveEnabled => Count > 0 || Bits > 0 ? 100 : 0;

        /// <summary>
        /// Validates and fixes inventory integrity on load
        /// </summary>
        public void ValidateInventoryOnLoad()
        {
            lock (_itemsLock)
            {
                try
                {
                    // Remove duplicate slots
                    var duplicateSlots = Items
                        .GroupBy(x => x.Slot)
                        .Where(g => g.Count() > 1)
                        .ToList();

                    if (duplicateSlots.Any())
                    {
                        Console.WriteLine($"WARNING: Inventory {Id} has {duplicateSlots.Count} duplicate slots. Fixing...");
                        Items = Items
                            .GroupBy(x => x.Slot)
                            .Select(g => g.First())
                            .OrderBy(x => x.Slot)
                            .ToList();
                    }

                    // Fix invalid items
                    CheckEmptyItemsInternal();

                    // Ensure sequential slots
                    for (int i = 0; i < Items.Count; i++)
                    {
                        if (Items[i].Slot != i)
                        {
                            Console.WriteLine($"WARNING: Inventory {Id} has non-sequential slots. Fixing...");
                            Items[i].Slot = i;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Failed to validate inventory {Id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sort itens by ItemId.
        /// </summary>
        public void Sort()
        {
            lock (_itemsLock)
            {
                try
                {
                    var existingItens = Items
                        .Where(x => x.ItemId > 0)
                        .OrderByDescending(x => x.ItemInfo.Type)
                        .ThenByDescending(x => x.ItemId)
                        .ThenByDescending(x => x.Amount)
                        .ToList();

                    var emptyItens = Items
                        .Where(x => x.ItemId == 0)
                        .ToList();

                    // Combine itens com o mesmo ItemId dentro do limite de sobreposição (Overlap)
                    for (int i = 0; i < existingItens.Count; i++)
                    {
                        for (int j = i + 1; j < existingItens.Count; j++)
                        {
                            if (existingItens[i].ItemId == existingItens[j].ItemId &&
                                existingItens[i].ItemInfo != null &&
                                existingItens[j].ItemInfo != null &&
                                existingItens[i].Amount + existingItens[j].Amount <= existingItens[i].ItemInfo.Overlap)
                            {
                                existingItens[i].IncreaseAmount(existingItens[j].Amount);
                                existingItens[j].SetItemId();
                                existingItens[j].SetAmount();
                            }
                        }
                    }

                    existingItens.AddRange(emptyItens);

                    var slot = 0;
                    foreach (var existingItem in existingItens)
                    {
                        existingItem.Slot = slot;
                        slot++;
                    }

                    Items = existingItens;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Sort failed for inventory {Id}: {ex.Message}");
                }
            }
        }

        public ItemModel FindItemBySlotCheck(int slot)
        {
            if (slot < 0) return null;

            lock (_itemsLock)
            {
                try
                {
                    return Items.FirstOrDefault(x => x.Slot == slot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemBySlotCheck failed: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Increase the current inventory size/slots.
        /// </summary>
        public byte AddSlots(byte amount = 1)
        {
            lock (_itemsLock)
            {
                try
                {
                    for (byte i = 0; i < amount; i++)
                    {
                        var maxSlot = Items.Any() ? Items.Max(x => x.Slot) : -1;
                        var newItemSlot = new ItemModel(maxSlot)
                        {
                            ItemListId = Id
                        };

                        Items.Add(newItemSlot);
                        Size++;
                    }

                    return Size;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddSlots failed: {ex.Message}");
                    return Size;
                }
            }
        }

        /// <summary>
        /// Increase the current inventory size.
        /// </summary>
        public ItemModel AddSlot()
        {
            lock (_itemsLock)
            {
                try
                {
                    var maxSlot = Items.Any() ? Items.Max(x => x.Slot) : -1;
                    var newItemSlot = new ItemModel(maxSlot)
                    {
                        ItemListId = Id
                    };

                    Items.Add(newItemSlot);
                    Size++;

                    return newItemSlot;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddSlot failed: {ex.Message}");
                    return null;
                }
            }
        }

        public int CountItensById(int itemId)
        {
            lock (_itemsLock)
            {
                try
                {
                    var total = 0;
                    var items = FindItemsByIdInternal(itemId);
                    foreach (var targetItem in items)
                    {
                        total = total + targetItem.Amount;
                    }
                    return total;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: CountItensById failed: {ex.Message}");
                    return 0;
                }
            }
        }

        public bool RemoveOrReduceItemsBySection(int itemSection, int totalAmount)
        {
            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    var targetAmount = totalAmount;
                    var targetItems = FindItemsBySectionInternal(itemSection);
                    targetItems = targetItems.OrderBy(x => x.Slot).ToList();

                    foreach (var targetItem in targetItems)
                    {
                        if (targetItem.Amount >= targetAmount)
                        {
                            targetItem.ReduceAmount(targetAmount);
                            targetAmount = 0;
                        }
                        else
                        {
                            targetAmount -= targetItem.Amount;
                            targetItem.SetAmount();
                        }

                        if (targetAmount == 0)
                            break;
                    }

                    if (targetAmount > 0)
                    {
                        RevertOperation(backup);
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItemsBySection failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool RemoveOrReduceItemsByItemId(int itemId, int totalAmount)
        {
            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    var targetAmount = totalAmount;
                    var targetItems = FindItemsByIdInternal(itemId);
                    targetItems = targetItems.OrderBy(x => x.Slot).ToList();

                    foreach (var targetItem in targetItems)
                    {
                        if (targetItem.Amount >= targetAmount)
                        {
                            targetItem.ReduceAmount(targetAmount);
                            targetAmount = 0;
                        }
                        else
                        {
                            targetAmount -= targetItem.Amount;
                            targetItem.SetAmount();
                        }

                        if (targetAmount == 0)
                            break;
                    }

                    if (targetAmount > 0)
                    {
                        RevertOperation(backup);
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItemsByItemId failed: {ex.Message}");
                    return false;
                }
            }
        }

        public List<ItemModel> FindItemsBySection(int itemSection)
        {
            lock (_itemsLock)
            {
                try
                {
                    return FindItemsBySectionInternal(itemSection);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemsBySection failed: {ex.Message}");
                    return new List<ItemModel>();
                }
            }
        }

        private List<ItemModel> FindItemsBySectionInternal(int itemSection)
        {
            return Items
                .Where(x => x.Amount > 0 && x.ItemInfo?.Section == itemSection)
                .ToList();
        }

        public ItemModel? FindItemBySection(int itemSection)
        {
            lock (_itemsLock)
            {
                try
                {
                    return Items.FirstOrDefault(x => x.Amount > 0 && x.ItemInfo?.Section == itemSection);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemBySection failed: {ex.Message}");
                    return null;
                }
            }
        }

        public ItemModel? FindItemById(int itemId, bool allowEmpty = false)
        {
            lock (_itemsLock)
            {
                try
                {
                    if (allowEmpty)
                        return Items.FirstOrDefault(x => itemId == x.ItemId);
                    else
                        return Items.FirstOrDefault(x => x.Amount > 0 && itemId == x.ItemId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemById failed: {ex.Message}");
                    return null;
                }
            }
        }

        public int FindAvailableSlot(ItemModel targetItem)
        {
            lock (_itemsLock)
            {
                try
                {
                    var slot = Items.FindIndex(x =>
                        x.ItemId == targetItem.ItemId &&
                        x.Amount + targetItem.Amount < targetItem.ItemInfo.Overlap);

                    if (slot < 0)
                        slot = GetEmptySlotInternal();

                    return slot;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindAvailableSlot failed: {ex.Message}");
                    return -1;
                }
            }
        }

        public List<ItemModel> FindItemsById(int itemId, bool allowEmpty = false)
        {
            lock (_itemsLock)
            {
                try
                {
                    return FindItemsByIdInternal(itemId, allowEmpty);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemsById failed: {ex.Message}");
                    return new List<ItemModel>();
                }
            }
        }

        private List<ItemModel> FindItemsByIdInternal(int itemId, bool allowEmpty = false)
        {
            if (allowEmpty)
                return Items.Where(x => itemId == x.ItemId).ToList();
            else
                return Items.Where(x => x.Amount > 0 && itemId == x.ItemId).ToList();
        }

        public ItemModel FindItemBySlot(int slot)
        {
            if (slot < 0) return null;

            lock (_itemsLock)
            {
                try
                {
                    return FindItemBySlotInternal(slot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemBySlot failed: {ex.Message}");
                    return null;
                }
            }
        }

        private ItemModel FindItemBySlotInternal(int slot)
        {
            if (slot < 0) return null;
            return Items.First(x => x.Slot == slot);
        }

        public ItemModel FindItemByTradeSlot(int slot)
        {
            if (slot < 0) return null;

            lock (_itemsLock)
            {
                try
                {
                    return Items.First(x => x.TradeSlot == slot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: FindItemByTradeSlot failed: {ex.Message}");
                    return null;
                }
            }
        }

        public ItemModel GiftFindItemBySlot(int slot)
        {
            if (slot < 0) return null;

            lock (_itemsLock)
            {
                try
                {
                    return Items.FirstOrDefault(x => x.Slot == slot && x.ItemId > 0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: GiftFindItemBySlot failed: {ex.Message}");
                    return null;
                }
            }
        }

        public bool UpdateGiftSlot()
        {
            lock (_itemsLock)
            {
                try
                {
                    var ItemInfo = Items.Where(x => x.ItemId > 0).ToList();

                    if (ItemInfo.Count <= 0)
                        return false;

                    var slot = -1;

                    foreach (var item in ItemInfo)
                    {
                        slot++;

                        var newItem = new ItemModel();
                        newItem.SetItemId(item.ItemId);
                        newItem.SetAmount(item.Amount);
                        newItem.SetItemInfo(item.ItemInfo);

                        RemoveItemInternal(item, (short)item.Slot);
                        AddItemInternal(newItem);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: UpdateGiftSlot failed: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns the first empty slot index or -1.
        /// </summary>
        public int GetEmptySlot
        {
            get
            {
                lock (_itemsLock)
                {
                    return GetEmptySlotInternal();
                }
            }
        }

        private int GetEmptySlotInternal()
        {
            return Items.FindIndex(x => x.ItemId == 0);
        }

        public int InsertItem(ItemModel newItem)
        {
            lock (_itemsLock)
            {
                try
                {
                    return InsertItemInternal(newItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: InsertItem failed: {ex.Message}");
                    return -1;
                }
            }
        }

        private int InsertItemInternal(ItemModel newItem)
        {
            var targetSlot = GetEmptySlotInternal();
            if (targetSlot < 0)
                return -1;

            newItem.Id = Items[targetSlot].Id;
            newItem.Slot = targetSlot;
            Items[targetSlot] = newItem;

            return targetSlot;
        }

        public bool AddBits(long bits)
        {
            lock (_itemsLock)
            {
                try
                {
                    if (Bits + bits > long.MaxValue)
                    {
                        Bits = long.MaxValue;
                        return false;
                    }
                    else
                    {
                        Bits += bits;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddBits failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool RemoveBits(long bits)
        {
            lock (_itemsLock)
            {
                try
                {
                    if (Bits >= bits)
                    {
                        Bits -= bits;
                        return true;
                    }
                    else
                    {
                        Bits = 0;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveBits failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool ClearBits()
        {
            lock (_itemsLock)
            {
                try
                {
                    Bits = 0;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: ClearBits failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool AddItems(List<ItemModel> itemsToAdd, bool isShop = false)
        {
            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    foreach (var itemToAdd in itemsToAdd)
                    {
                        itemToAdd.ItemList = null;
                        itemToAdd.ItemListId = 0;
                        if (itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                            continue;

                        FillExistentSlotsInternal(itemToAdd, isShop);
                        AddNewSlotsInternal(itemToAdd, isShop);

                        if (itemToAdd.Amount > 0)
                        {
                            RevertOperation(backup);
                            return false;
                        }
                    }

                    CheckEmptyItemsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddItems failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool AddItemGiftStorage(ItemModel newItem)
        {
            if (newItem.Amount <= 0 || newItem.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    var itemToAdd = (ItemModel)newItem.Clone();

                    while (itemToAdd.Amount > 0)
                    {
                        var targetSlot = GetEmptySlotInternal();
                        if (targetSlot == -1)
                            break;

                        var currentCount = Items.Count(x => x.ItemId != 0);
                        if (currentCount >= Size)
                            break;

                        itemToAdd.Slot = targetSlot;
                        var newClon = (ItemModel)itemToAdd.Clone();

                        if (itemToAdd.Amount > itemToAdd.ItemInfo.Overlap)
                        {
                            newClon.SetAmount(itemToAdd.ItemInfo.Overlap);
                            itemToAdd.SetAmount(itemToAdd.Amount - itemToAdd.ItemInfo.Overlap);
                        }
                        else
                        {
                            newClon.SetAmount(itemToAdd.Amount);
                            itemToAdd.SetAmount();
                        }

                        newClon.Id = Items[targetSlot].Id;
                        newClon.Slot = targetSlot;
                        Items[targetSlot] = newClon;
                    }

                    CheckEmptyItemsInternal();
                    newItem.Slot = itemToAdd.Slot;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddItemGiftStorage failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool AddItem(ItemModel newItem)
        {
            if (newItem.Amount == 0 || newItem.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    return AddItemInternal(newItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddItem failed for ItemId {newItem.ItemId}: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        private bool AddItemInternal(ItemModel newItem)
        {
            var backup = BackupOperation();

            var itemToAdd = (ItemModel)newItem.Clone();

            FillExistentSlotsInternal(itemToAdd);
            AddNewSlotsInternal(itemToAdd);

            if (itemToAdd.Amount > 0)
            {
                RevertOperation(backup);
                return false;
            }

            CheckEmptyItemsInternal();
            newItem.Slot = itemToAdd.Slot;

            return true;
        }

        public bool AddItemTrade(ItemModel newItem)
        {
            if (newItem.Amount == 0 || newItem.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    var itemToAdd = (ItemModel)newItem.Clone();

                    AddNewSlotsInternal(itemToAdd);

                    if (itemToAdd.Amount > 0)
                    {
                        RevertOperation(backup);
                        return false;
                    }

                    CheckEmptyItemsInternal();
                    newItem.Slot = itemToAdd.Slot;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddItemTrade failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool AddItemWithSlot(ItemModel itemToAdd, int slot)
        {
            if (itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    var tempItem = (ItemModel)itemToAdd.Clone();

                    var targetSlot = FindItemBySlotInternal(slot);
                    targetSlot.ItemId = tempItem.ItemId;
                    targetSlot.Amount = tempItem.Amount;
                    targetSlot.Power = tempItem.Power;
                    targetSlot.RerollLeft = tempItem.RerollLeft;
                    targetSlot.FamilyType = tempItem.FamilyType;
                    targetSlot.Duration = tempItem.Duration;
                    targetSlot.EndDate = tempItem.EndDate;
                    targetSlot.FirstExpired = tempItem.FirstExpired;
                    targetSlot.AccessoryStatus = tempItem.AccessoryStatus;
                    targetSlot.SocketStatus = tempItem.SocketStatus;
                    targetSlot.ItemInfo = tempItem.ItemInfo;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddItemWithSlot failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool SplitItem(ItemModel itemToAdd, int targetSlot)
        {
            if (itemToAdd == null || itemToAdd.Amount == 0 || itemToAdd.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    FillExistentSlotInternal(itemToAdd, targetSlot);
                    CheckEmptyItemsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: SplitItem failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        private List<ItemModel> BackupOperation()
        {
            var backup = new List<ItemModel>();
            backup.AddRange(Items);
            return backup;
        }

        private void RevertOperation(List<ItemModel> backup)
        {
            Items.Clear();
            Items.AddRange(backup);
            CheckEmptyItemsInternal();
        }

        private void AddNewSlotsInternal(ItemModel itemToAdd, bool isShop = false)
        {
            while (itemToAdd.Amount > 0)
            {
                var emptySlot = GetEmptySlotInternal();
                if (emptySlot == -1)
                    break;

                itemToAdd.Slot = emptySlot;
                var newItem = (ItemModel)itemToAdd.Clone();

                if (itemToAdd.Amount > itemToAdd.ItemInfo.Overlap && !isShop)
                {
                    itemToAdd.ReduceAmount(itemToAdd.ItemInfo.Overlap);
                    newItem.SetAmount(itemToAdd.ItemInfo.Overlap);
                }
                else
                {
                    newItem.SetAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount();
                }

                InsertItemInternal(newItem);
            }
        }

        internal void CheckExpiredItems()
        {
        }

        private void FillExistentSlotsInternal(ItemModel itemToAdd, bool isShop = false)
        {
            var targetItems = FindItemsByIdInternal(itemToAdd.ItemId);

            foreach (var targetItem in targetItems.Where(x => x.ItemInfo.Overlap > 1))
            {
                if (targetItem.Amount + itemToAdd.Amount > itemToAdd.ItemInfo.Overlap && !isShop)
                {
                    itemToAdd.ReduceAmount(itemToAdd.ItemInfo.Overlap - targetItem.Amount);
                    targetItem.SetAmount(itemToAdd.ItemInfo.Overlap);
                }
                else
                {
                    targetItem.IncreaseAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount();
                }

                itemToAdd.Slot = targetItem.Slot;
            }
        }

        private void FillExistentSlotInternal(ItemModel itemToAdd, int targetSlot)
        {
            var targetItem = FindItemBySlotInternal(targetSlot);

            if (targetItem.ItemId == itemToAdd.ItemId || targetItem.ItemId == 0)
            {
                if (targetItem.Amount + itemToAdd.Amount > itemToAdd.ItemInfo.Overlap)
                {
                    itemToAdd.IncreaseAmount(itemToAdd.ItemInfo.Overlap - targetItem.Amount);
                    targetItem.SetAmount(targetItem.ItemInfo.Overlap);
                }
                else
                {
                    targetItem.IncreaseAmount(itemToAdd.Amount);
                    itemToAdd.SetAmount();
                }

                targetItem.SetItemId(itemToAdd.ItemId);
                targetItem.SetItemInfo(itemToAdd.ItemInfo);
                targetItem.SetRemainingTime((uint)itemToAdd.ItemInfo.UsageTimeMinutes);
            }
        }

        public bool MoveItem(short originSlot, short destinationSlot)
        {
            lock (_itemsLock)
            {
                try
                {
                    var originItem = FindItemBySlotInternal(originSlot);
                    var destinationItem = FindItemBySlotInternal(destinationSlot);

                    if (originItem.ItemId == 0)
                        return false;

                    if (originItem.ItemId == destinationItem.ItemId)
                    {
                        if (originItem.Amount + destinationItem.Amount > originItem.ItemInfo.Overlap)
                        {
                            originItem.ReduceAmount(originItem.ItemInfo.Overlap - destinationItem.Amount);
                            destinationItem.SetAmount(originItem.ItemInfo.Overlap);
                        }
                        else
                        {
                            destinationItem.IncreaseAmount(originItem.Amount);
                            originItem.SetAmount();
                        }
                    }
                    else
                    {
                        if (destinationItem.ItemId == 0)
                        {
                            var tempItem = (ItemModel)originItem.Clone(destinationItem.Id);
                            tempItem.Slot = destinationItem.Slot;

                            destinationItem = tempItem;
                            originItem.SetItemId();
                        }
                        else
                        {
                            var tempItem = (ItemModel)destinationItem.Clone(originItem.Id);
                            tempItem.Slot = originItem.Slot;

                            var tempItem2 = (ItemModel)originItem.Clone(destinationItem.Id);
                            tempItem2.Slot = destinationItem.Slot;

                            destinationItem = tempItem2;
                            originItem = (ItemModel)tempItem.Clone(originItem.Id);
                        }
                    }

                    Items[originSlot] = originItem;
                    Items[destinationSlot] = destinationItem;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: MoveItem failed: {ex.Message}");
                    return false;
                }
            }
        }

        public void Clear()
        {
            lock (_itemsLock)
            {
                try
                {
                    foreach (var item in Items)
                    {
                        item.SetItemId();
                        item.SetAmount();
                        item.SetRemainingTime();
                        item.SetSellPrice(0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Clear failed: {ex.Message}");
                }
            }
        }

        public bool RemoveOrReduceItems(List<ItemModel> itemsToRemoveOrReduce)
        {
            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    foreach (var itemToRemove in itemsToRemoveOrReduce)
                    {
                        if (itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                            continue;

                        var targetItems = FindItemsByIdInternal(itemToRemove.ItemId);

                        foreach (var targetItem in targetItems)
                        {
                            if (targetItem.Amount >= itemToRemove.Amount)
                            {
                                targetItem.ReduceAmount(itemToRemove.Amount);
                                itemToRemove.SetAmount();
                                break;
                            }
                            else
                            {
                                itemToRemove.ReduceAmount(targetItem.Amount);
                                targetItem.SetAmount();
                            }
                        }

                        if (itemToRemove.Amount > 0)
                        {
                            RevertOperation(backup);
                            return false;
                        }
                    }

                    CheckEmptyItemsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItems failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool RemoveOrReduceItems(List<ItemModel> itemsToRemoveOrReduce, bool reArrangeSlots = true)
        {
            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    foreach (var itemToRemove in itemsToRemoveOrReduce)
                    {
                        if (itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                            continue;

                        var targetItems = FindItemsByIdInternal(itemToRemove.ItemId);

                        foreach (var targetItem in targetItems)
                        {
                            if (targetItem.Amount >= itemToRemove.Amount)
                            {
                                targetItem.ReduceAmount(itemToRemove.Amount);
                                itemToRemove.SetAmount();
                                break;
                            }

                            itemToRemove.ReduceAmount(targetItem.Amount);
                            targetItem.SetAmount();
                        }

                        if (itemToRemove.Amount <= 0)
                        {
                            continue;
                        }

                        RevertOperation(backup);
                        return false;
                    }

                    CheckEmptyItemsThenRearrangeSlotsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItems (with rearrange) failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool RemoveOrReduceItem(ItemModel? itemToRemove, int amount, int slot = -1)
        {
            if (itemToRemove == null || amount == 0) return false;

            try
            {
                var tempItem = (ItemModel?)itemToRemove.Clone();
                tempItem?.SetAmount(amount);

                return slot > -1 ? RemoveOrReduceItemWithSlot(tempItem, slot) : RemoveOrReduceItemWithoutSlot(tempItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: RemoveOrReduceItem failed: {ex.Message}");
                return false;
            }
        }

        public List<ItemModel> AddSlotsAll(byte amount = 1)
        {
            lock (_itemsLock)
            {
                try
                {
                    List<ItemModel> newSlots = new List<ItemModel>();
                    for (byte i = 0; i < amount; i++)
                    {
                        var maxSlot = Items.Any() ? Items.Max(x => x.Slot) : -1;
                        var newItemSlot = new ItemModel(maxSlot)
                        {
                            ItemListId = Id
                        };

                        newSlots.Add(newItemSlot);
                        Items.Add(newItemSlot);
                        Size++;
                    }

                    return newSlots;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: AddSlotsAll failed: {ex.Message}");
                    return new List<ItemModel>();
                }
            }
        }

        public bool RemoveOrReduceItemWithSlot(ItemModel? itemToRemove, int slot)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    var targetItem = FindItemBySlotInternal(slot);
                    targetItem?.ReduceAmount(itemToRemove.Amount);
                    itemToRemove.SetAmount();

                    if (itemToRemove.Amount > 0)
                    {
                        RevertOperation(backup);
                        return false;
                    }

                    CheckEmptyItemsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItemWithSlot failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool RemoveOrReduceItemWithoutSlot(ItemModel? itemToRemove)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    var backup = BackupOperation();

                    var targetItems = FindItemsByIdInternal(itemToRemove.ItemId);

                    foreach (var targetItem in targetItems)
                    {
                        if (targetItem.Amount >= itemToRemove.Amount)
                        {
                            targetItem.ReduceAmount(itemToRemove.Amount);
                            itemToRemove.SetAmount();
                            break;
                        }
                        else
                        {
                            itemToRemove.ReduceAmount(targetItem.Amount);
                            targetItem.SetAmount();
                        }
                    }

                    if (itemToRemove.Amount > 0)
                    {
                        RevertOperation(backup);
                        return false;
                    }

                    CheckEmptyItemsInternal();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveOrReduceItemWithoutSlot failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        public bool RemoveItem(ItemModel itemToRemove, short slot)
        {
            if (itemToRemove == null || itemToRemove.Amount == 0 || itemToRemove.ItemId == 0)
                return false;

            lock (_itemsLock)
            {
                try
                {
                    return RemoveItemInternal(itemToRemove, slot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: RemoveItem failed: {ex.Message}");
                    CheckEmptyItemsInternal();
                    return false;
                }
            }
        }

        private bool RemoveItemInternal(ItemModel itemToRemove, short slot)
        {
            var backup = BackupOperation();

            var targetItem = FindItemBySlotInternal(slot);

            if (targetItem == null)
                return false;

            if (targetItem.Amount >= itemToRemove.Amount)
            {
                targetItem.ReduceAmount(itemToRemove.Amount);
                itemToRemove.SetAmount();
                CheckEmptyItemsInternal();
                return true;
            }
            else
            {
                RevertOperation(backup);
                CheckEmptyItemsInternal();
                return false;
            }
        }

        public void CheckEmptyItems()
        {
            lock (_itemsLock)
            {
                try
                {
                    CheckEmptyItemsInternal();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: CheckEmptyItems failed: {ex.Message}");
                }
            }
        }

        private void CheckEmptyItemsInternal()
        {
            var itemsToReset = Items.Where(item => item.ItemId == 0 || item.Amount <= 0).ToList();

            foreach (var item in itemsToReset)
            {
                item.SetItemId();
                item.SetAmount();
                item.SetRemainingTime();
                item.SetSellPrice(0);
            }
        }

        public void CheckEmptyItemsThenRearrangeSlots()
        {
            lock (_itemsLock)
            {
                try
                {
                    CheckEmptyItemsThenRearrangeSlotsInternal();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: CheckEmptyItemsThenRearrangeSlots failed: {ex.Message}");
                }
            }
        }

        private void CheckEmptyItemsThenRearrangeSlotsInternal()
        {
            foreach (var item in Items.Where(item => item.ItemId == 0 || item.Amount <= 0))
            {
                item.SetItemId();
                item.SetAmount();
                item.SetRemainingTime();
                item.SetSellPrice(0);
            }

            int slot = 0;
            Items = Items
                .OrderBy(item => item.ItemId > 0 ? 0 : 1)
                .ThenBy(item => item.Slot)
                .Select(item =>
                {
                    item.SetSlot(slot++);
                    return item;
                })
                .ToList();
        }

        public byte[] ToArray()
        {
            byte[] buffer;

            lock (_itemsLock)
            {
                try
                {
                    using (MemoryStream m = new())
                    {
                        var sortedItems = Items.OrderBy(x => x.Slot);

                        foreach (var item in sortedItems)
                            m.Write(item.ToArray(), 0, 68);

                        buffer = m.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: ToArray failed: {ex.Message}");
                    buffer = Array.Empty<byte>();
                }
            }

            return buffer;
        }

        public byte[] GiftToArray()
        {
            byte[] buffer;

            lock (_itemsLock)
            {
                try
                {
                    using MemoryStream m = new();
                    var filteredItems = Items.Where(x => x.ItemId > 0).OrderBy(x => x.Slot);
                    var filteredItemsList = filteredItems.ToList();

                    if (filteredItemsList.Any())
                    {
                        foreach (var item in filteredItemsList)
                        {
                            m.Write(item.GiftToArray(), 0, 68);
                        }

                        buffer = m.ToArray();
                    }
                    else
                    {
                        buffer = Array.Empty<byte>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: GiftToArray failed: {ex.Message}");
                    buffer = Array.Empty<byte>();
                }
            }

            return buffer;
        }

        public byte[] NewGiftToArray()
        {
            byte[] buffer;

            lock (_itemsLock)
            {
                try
                {
                    using MemoryStream m = new();
                    var filteredItems = Items.Where(x => x.ItemId > 0).OrderBy(x => x.Slot);
                    var filteredItemsList = filteredItems.ToList();

                    if (filteredItemsList.Any())
                    {
                        foreach (var item in filteredItemsList)
                        {
                            var itemArray = item.NewGiftToArray();
                            m.Write(itemArray, 0, itemArray.Length);
                        }

                        buffer = m.ToArray();
                    }
                    else
                    {
                        buffer = Array.Empty<byte>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: NewGiftToArray failed: {ex.Message}");
                    buffer = Array.Empty<byte>();
                }
            }

            return buffer;
        }

        public string ToString()
        {
            var sb = new StringBuilder();

            lock (_itemsLock)
            {
                try
                {
                    sb.AppendLine($"Inventory{Id}");
                    foreach (var item in Items.OrderBy(x => x.Slot))
                    {
                        sb.AppendLine($"Item[{item.Slot}] - {item.ItemId}");
                        sb.AppendLine(item.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: ToString failed: {ex.Message}");
                    sb.AppendLine($"Error generating inventory string: {ex.Message}");
                }
            }

            return sb.ToString();
        }
    }
}