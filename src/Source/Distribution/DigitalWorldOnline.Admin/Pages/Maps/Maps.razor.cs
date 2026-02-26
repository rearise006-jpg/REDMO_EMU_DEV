using AutoMapper;
using DigitalWorldOnline.Admin.Shared;
using DigitalWorldOnline.Application.Admin.Commands;
using DigitalWorldOnline.Application.Admin.Queries;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Enums.Admin;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.ViewModel.Maps;
using MediatR;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Admin.Pages.Maps
{
    public partial class Maps
    {
        private MudTextField<string> _filterParameter;
        private MudTable<MapViewModel> _table;

        private async Task<TableData<MapViewModel>> ServerReload(TableState state)
        {
            var users = await Sender.Send(
                new GetMapsQuery(
                    state.Page,
                    state.PageSize,
                    state.SortLabel,
                    (SortDirectionEnum)state.SortDirection.GetHashCode(),
                    _filterParameter?.Value
                )
            );

            var pageData = Mapper.Map<IEnumerable<MapViewModel>>(users.Registers);

            return new TableData<MapViewModel>() { TotalItems = users.TotalRegisters, Items = pageData };
        }

        private void ViewSpawnPoints(long id)
        {
            Nav.NavigateTo($"/maps/spawnpoints/{id}");
        }

        private void ViewMobs(long id)
        {
            Nav.NavigateTo($"/maps/mobs/{id}");
        }

        private void ViewRaids(long id)
        {
            Nav.NavigateTo($"/maps/raids/{id}");
        }

        // 🆕 YENİ FONKSİYON: Map Düzenlemesi (Edit Sayfasına Git)
        private void EditMap(long id)
        {
            Logger.Information($"Opening map edit page for map id: {id}");
            Nav.NavigateTo($"/maps/{id}/edit");
        }

        // 🆕 YENİ FONKSİYON: Harita Durumunu Hızlıca Değiştir
        // 🆕 YENİ FONKSİYON: Harita Durumunu Hızlıca Değiştir
        // 🆕 YENİ FONKSİYON: Harita Durumunu Hızlıca Değiştir
        private async Task ToggleMapStatus(long mapId, bool currentStatus)
        {
            try
            {
                Logger.Information($"Toggle map status for MapId: {mapId}, Current: {currentStatus}");

                // ✅ FIX: DialogParameters (non-generic)
                var parameters = new DialogParameters();
                parameters.Add("Title", currentStatus ? "Close Map?" : "Open Map?");
                parameters.Add("Message",
                    currentStatus
                        ? "Are you sure you want to CLOSE this map? Players will not be able to enter."
                        : "Are you sure you want to OPEN this map? Players will be able to enter.");
                parameters.Add("ButtonText", "Confirm");
                parameters.Add("Color", currentStatus ? Color.Error : Color.Success);

                // ✅ FIX: DialogOptions - DisableBackdropClick false
                var options = new DialogOptions()
                {
                    CloseButton = true,
                    MaxWidth = MaxWidth.ExtraSmall,
                    DisableBackdropClick = false,
                    NoHeader = false
                };

                var dialog = DialogService.Show<ConfirmDialog>("Confirm", parameters, options);

                // ✅ FIX: Result null check
                var result = await dialog.Result;

                // ✅ FIX: Dialog disposed iken erişim yapma
                if (result == null)
                {
                    Logger.Warning($"Dialog result is null for MapId: {mapId}");
                    return;
                }

                if (result.Cancelled)
                {
                    Logger.Information($"User cancelled map status change for MapId: {mapId}");
                    return;
                }

                Logger.Information($"User confirmed map status change for MapId: {mapId}");

                try
                {
                    // Harita config'ini al
                    // ✅ DOĞRU - Database ID ile sorgu yapın:
            var mapConfigDTO = await Sender.Send(new GameMapConfigByIdQuery(mapId));

                    if (mapConfigDTO != null)
                    {
                        Logger.Information($"Map config retrieved for MapId: {mapId}, Current MapIsOpen: {mapConfigDTO.MapIsOpen}");

                        // Durumu tersine çevir
                        mapConfigDTO.MapIsOpen = !currentStatus;

                        Logger.Information($"Sending update command for MapId: {mapId}, NewStatus: {mapConfigDTO.MapIsOpen}");

                        // Güncelleme komutunu gönder
                        await Sender.Send(new UpdateMapConfigCommand(mapConfigDTO));

                        Logger.Information($"Map {mapId} status changed to {(!currentStatus ? "OPEN" : "CLOSED")}");

                        Toast.Add(
                            $"✓ Map is now {(!currentStatus ? "OPEN" : "CLOSED")}!",
                            (!currentStatus ? Severity.Success : Severity.Warning)
                        );

                        // Tabloyu yenile
                        if (_table != null)
                        {
                            await _table.ReloadServerData();
                            Logger.Information($"Table reloaded after map status change");
                        }
                    }
                    else
                    {
                        Toast.Add("❌ Map not found in database!", Severity.Error);
                        Logger.Warning($"Map configuration not found for MapId: {mapId}");
                    }
                }
                catch (Exception innerEx)
                {
                    Logger.Error($"Inner error in ToggleMapStatus: {innerEx.Message}\n{innerEx.StackTrace}");
                    Toast.Add($"❌ Error updating map: {innerEx.Message}", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error toggling map status: {ex.Message}\n{ex.StackTrace}");
                Toast.Add($"❌ Unexpected error: {ex.Message}", Severity.Error);
            }
        }

        private async Task Reset(long id)
        {
            try
            {
                var parameters = new DialogParameters();
                parameters.Add("Title", "Reset Map Configuration");
                parameters.Add("Message", "All the related config for this map gonna be reseted. Do you want to proceed? This process cannot be undone.");
                parameters.Add("ButtonText", "Reset");
                parameters.Add("Color", Color.Error);

                var options = new DialogOptions()
                {
                    CloseButton = true,
                    MaxWidth = MaxWidth.ExtraSmall,
                    DisableBackdropClick = false,
                    NoHeader = false
                };

                var dialog = DialogService.Show<ConfirmDialog>("Confirm", parameters, options);

                // ✅ FIX: Result null check
                var result = await dialog.Result;

                if (result == null)
                {
                    Logger.Warning($"Dialog result is null for Reset");
                    return;
                }

                if (!result.Cancelled)
                    Nav.NavigateTo($"/maps/reset/{id}");
                else
                    await Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Reset: {ex.Message}");
                Toast.Add("Error resetting map", Severity.Error);
            }
        }

        private async Task Filter(string value)
        {
            try
            {
                Logger.Information($"Filtering maps with value: {value}");
                await Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Filter: {ex.Message}");
            }
        }

        // ✅ FIX: Async Task - Clear
        private async Task Clear()
        {
            try
            {
                if (_filterParameter != null)
                {
                    _filterParameter.Clear();
                    Logger.Information("Filter cleared");
                }
                await Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Clear: {ex.Message}");
                Toast.Add("Error clearing filter", Severity.Error);
            }
        }

        // ✅ FIX: Async Task - Refresh
        private async Task Refresh()
        {
            try
            {
                if (_table != null)
                {
                    Logger.Information("Refreshing maps table");
                    await _table.ReloadServerData();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Refresh: {ex.Message}");
                Toast.Add("Error refreshing table", Severity.Error);
            }
        }
    }
}