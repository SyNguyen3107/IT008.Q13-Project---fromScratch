using EasyFlips.Interfaces;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyFlips.ViewModels
{
    internal class MemberGameViewModel : BaseGameViewModel
    {
        public MemberGameViewModel(IAuthService authService, SupabaseService supabaseService, INavigationService navigationService, AudioService audioService) : base(authService, supabaseService, navigationService, audioService)
        {
        }

        protected override Task SubscribeToRealtimeChannel()
        {
            throw new NotImplementedException();
        }
    }
}
