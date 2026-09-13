using Content.Client.UserInterface.Controls;
using Content.Client.VendingMachines.UI;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using System.Linq;
using Robust.Client.GameObjects;
using Content.Shared._NF.Bank.Components; // Frontier
using Content.Shared.Containers.ItemSlots; // Frontier
using Content.Shared.Stacks; // Frontier

namespace Content.Client.VendingMachines
{
    public sealed class VendingMachineBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private VendingMachineMenu? _menu;

        [ViewVariables]
        private List<VendingMachineInventoryEntry> _cachedInventory = new();

        // Frontier: market price modifier & balance
        private UserInterfaceSystem _uiSystem = default!;
        private ItemSlotsSystem _itemSlots = default!;

        [ViewVariables]
        private float _mod = 1f;
        [ViewVariables]
        private int _balance = 0;
        [ViewVariables]
        private int _cashSlotBalance = 0;
        // End Frontier
        [ViewVariables]
        private bool _requiresCash; // mono

        public VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            // Frontier: state, market modifier, balance status
            _uiSystem = EntMan.System<UserInterfaceSystem>();
            _itemSlots = EntMan.System<ItemSlotsSystem>();

            if (EntMan.TryGetComponent<MarketModifierComponent>(Owner, out var market))
                _mod = market.Mod;
            // End Frontier

            _menu = this.CreateWindowCenteredLeft<VendingMachineMenu>();
            // Frontier: no exceptions
            if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta))
                _menu.Title = meta.EntityName;
            else
                _menu.Title = Loc.GetString("vending-machine-nf-fallback-title");
            // End Frontier: no exceptions
            _menu.OnItemSelected += OnItemSelected;
            Refresh();
        }

        public void Refresh()
        {
            // LuaM start - the engine registers a BUI as open a frame before calling Open(), so a vending state
            // arriving in between used to call Refresh() with _uiSystem still null. That exception made the client
            // drop its game state and request a full one from the server, which is the lag spike players felt.
            // Open() refreshes on its own, and a missing component (client resetting to a full state) refreshes
            // again once the state is applied.
            if (_menu == null || !EntMan.HasComponent<VendingMachineComponent>(Owner))
                return;
            // LuaM end
            // Вкратце, исправлен древний баг торгоматов из-за которых при открытии окна
            // и покупки одновременно с другим игроком или с резким закрытием торгомата
            // вылезает данная ошибка. Исправлена добавлением проверки перед Refresh()

            var system = EntMan.System<VendingMachineSystem>();
            _cachedInventory = system.GetAllInventory(Owner);

            // Frontier: state, market modifier, balance status
            var uiUsers = _uiSystem.GetActors(Owner, UiKey);
            foreach (var uiUser in uiUsers)
            {
                if (EntMan.TryGetComponent<BankAccountComponent>(uiUser, out var bank))
                    _balance = bank.Balance;
            }
            int? cashSlotValue = null;
            if (EntMan.TryGetComponent<VendingMachineComponent>(Owner, out var vendingMachine))
            {
                _cashSlotBalance = vendingMachine.CashSlotBalance;
                _requiresCash = vendingMachine.RequiresCash; // mono
                if (vendingMachine.CashSlotName != null)
                    cashSlotValue = _cashSlotBalance;
            }
            else
            {
                _cashSlotBalance = 0;
            }
            // End Frontier

            _menu?.Populate(_cachedInventory, _mod, _balance, cashSlotValue, _requiresCash); // Frontier: add _balance, mono: add _requiresCash
        }

        private void OnItemSelected(GUIBoundKeyEventArgs args, ListData data)
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            if (data is not VendorItemsListData { ItemIndex: var itemIndex })
                return;

            if (_cachedInventory.Count == 0)
                return;

            var selectedItem = _cachedInventory.ElementAtOrDefault(itemIndex);

            if (selectedItem == null)
                return;

            SendMessage(new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_menu == null)
                return;

            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnClose -= Close;
            _menu.Dispose();
        }
    }
}
