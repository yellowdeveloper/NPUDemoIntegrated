using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models
{
    class SharedStatus :Notifier
    {
        private EConnectionState _connectionState = EConnectionState.Disconnected;

        public EConnectionState connectionState
        {
            get => _connectionState;
            set { _connectionState = value; OnPropertyChanged(); }
        }
    }
}