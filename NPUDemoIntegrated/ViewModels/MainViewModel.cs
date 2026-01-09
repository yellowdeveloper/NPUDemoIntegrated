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
        private ModuleType _currentModule;
        private string _currentViewName;
        public object currentContext
        {
            get { return _currentContext; }
            set { _currentContext = value; OnPropertyChanged(); }
        }

        public ModuleType currentModule
        {
            get => _currentModule;
            set { _currentModule = value; OnPropertyChanged(); }
        }

        public string currentViewName
        {
            get => _currentViewName;
            set { _currentViewName = value; OnPropertyChanged(); }
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
            currentModule = ModuleType.OBJ;
            currentViewName = currentModule.ToString();

            // --- Set Command ---
            SwitchViewCommand = new RelayCommand(param =>
            {
                string targetView = param.ToString();

                Enum.TryParse(targetView, out ModuleType targetModule);

                if (currentModule == targetModule) return;

                if (currentContext is BaseViewModel oldVM)
                {
                    oldVM.DeactivateModule(targetModule);
                    GlobalLogManager.Instance.ConsoleLog($"Deactivate {currentViewName}");
                }

                if (targetView == "OBJ")
                {
                    currentContext = OBJVM;
                    currentModule = ModuleType.OBJ;
                    currentViewName = "OBJ";
                }
                else if (targetView == "IR")
                {
                    currentContext = IRVM;
                    currentModule = ModuleType.IR;
                    currentViewName = "IR";
                }
            });
        }
    }
}
