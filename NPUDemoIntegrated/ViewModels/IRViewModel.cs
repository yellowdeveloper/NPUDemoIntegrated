using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    class IRViewModel : BaseViewModel
    {
        public IRConfig irConfig { get; }
        private readonly IRSerialService _serialService;

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        public IRViewModel(IRConfig config, IRSerialService service)
        {
            _serialService = service;
            irConfig = config;

            ConnectCommand = new RelayCommand(param => {
                if (_serialService.Connect() == 1) ButtonCommand = DisconnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
                //Debug.Write("\nConnect button clicked");
            });

            DisconnectCommand = new RelayCommand(param => {
                Task.Run(() => _serialService.Disconnect());
                ButtonCommand = ConnectCommand;
                if (is_menu_open) is_menu_open = !is_menu_open;
                //Debug.Write("\nDisconnect button clicked");
            });

            ButtonCommand = ConnectCommand;
        }
        public override void Dispose()
        {
            
        }

    }
}
