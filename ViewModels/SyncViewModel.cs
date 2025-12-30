using EasyFlips.Messages;
using EasyFlips.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class SyncViewModel : ObservableObject
    {
        private readonly SyncService _syncService;
        private readonly IMessenger _messenger;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteSyncCommand))]
        private bool _canSync;

        [ObservableProperty] private SyncService.SyncPlan _plan;

        public IAsyncRelayCommand CheckSyncCommand { get; }
        public IAsyncRelayCommand ExecuteSyncCommand { get; }
        public IRelayCommand<Window> CloseCommand { get; }

        public SyncViewModel(SyncService syncService, IMessenger messenger)
        {
            _syncService = syncService;
            _messenger = messenger;
            StatusMessage = "Waiting...";

            CheckSyncCommand = new AsyncRelayCommand(CheckSync);
            ExecuteSyncCommand = new AsyncRelayCommand(ExecuteSync, () => CanSync);

            CloseCommand = new RelayCommand<Window>((window) => window?.Close());

            CheckSyncCommand.ExecuteAsync(null);
        }

        private async Task CheckSync()
        {
            IsBusy = true;
            StatusMessage = "Checking data...";
            CanSync = false;
            Plan = null;

            try
            {
                var plan = await _syncService.PlanSyncAsync();
                Plan = plan;

                if (plan.IsEmpty)
                    StatusMessage = "Data is synced! Nothing new.";
                else
                {
                    int total = plan.ToUpload.Count + plan.ToDownload.Count +
                                plan.ToUpdateCloud.Count + plan.ToUpdateLocal.Count;
                    StatusMessage = $"Found {total} changes to sync.";
                    CanSync = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error checking: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSync()
        {
            if (Plan == null || Plan.IsEmpty) return;

            IsBusy = true;
            CanSync = false;
            StatusMessage = "Syncing data...";

            try
            {
                await _syncService.ExecuteSyncAsync(Plan);
                StatusMessage = "Sync completed successfully!";

                _messenger.Send(new SyncCompletedMessage());

                Plan = new SyncService.SyncPlan();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync error: {ex.Message}";
                CanSync = true;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}