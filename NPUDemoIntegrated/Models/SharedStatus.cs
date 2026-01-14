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
        private static EConnectionState _connectionState;

        public EConnectionState connectionState
        {
            get => _connectionState;
            set { _connectionState = value; OnPropertyChanged(); }
        }
    }
}
