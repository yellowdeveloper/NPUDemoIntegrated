using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    class MainViewModel: Notifier
    {
        public OBJViewModel OBJVM { get; }
        public IRViewModel IRVM { get; }

        private object _currentContext;
        public object currentContext
        {
            get { return _currentContext; }
            set { _currentContext = value; OnPropertyChanged(); }
        }

        public ICommand SwitchViewCommand { get; }

        public MainViewModel()
        {
            // Eject Config Object
            var objConfig = GlobalConfigManager.Instance.objConfig;
            var irConfig = GlobalConfigManager.Instance.irConfig;

            // Inject Config to Service
            var objService = new OBJSerialService(objConfig);
            var irService = new IRSerialService(irConfig);

            // Inject Service & Config to ViewModel
            OBJVM = new OBJViewModel(objConfig, objService);
            IRVM = new IRViewModel(irConfig, irService);

            // --- Init ViewModel Status ---
            currentContext = OBJVM;

            // --- Set Command ---
            SwitchViewCommand = new RelayCommand(param =>
            {
                if (param.ToString() == "OBJ") currentContext = OBJVM;
                else if (param.ToString() == "IR") currentContext = IRVM;
            });
        }
    }
}
