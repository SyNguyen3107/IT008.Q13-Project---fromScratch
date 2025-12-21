using EasyFlips.Interfaces;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyFlips.ViewModels
{
    internal class HostGameViewModel : BaseGameViewModel
    {
        public HostGameViewModel(IAuthService authService, SupabaseService supabaseService, INavigationService navigationService, AudioService audioService) : base(authService, supabaseService, navigationService, audioService)
        {
            throw new NotImplementedException();
        }
        protected override Task SubscribeToRealtimeChannel()
        {
            throw new NotImplementedException();
        }
    }
}
