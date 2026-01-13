using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using System.IO.Ports;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    class MainViewModel: Notifier
    {
        public OBJViewModel OBJVM { get; }
        public IRViewModel IRVM { get; }



        private object _currentContext;
        private EModuleType _currentModule;
        private string _currentViewName;
        public object currentContext
        {
            get { return _currentContext; }
            set { _currentContext = value; OnPropertyChanged(); }
        }

        public EModuleType currentModule
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
            SerialPort sp = new SerialPort();
            FTDI ftdi = new FTDI();


            // Eject Config Object
            var serialConfig = GlobalConfigManager.Instance.serialConfig;
            var objConfig = GlobalConfigManager.Instance.objConfig;
            var irConfig = GlobalConfigManager.Instance.irConfig;

            // Inject Config to Service
            var objService = new OBJSerialService(serialConfig, objConfig, sp, ftdi);
            var irService = new IRSerialService(serialConfig, irConfig, sp, ftdi);

            // Inject Service & Config to ViewModel
            OBJVM = new OBJViewModel(serialConfig, objConfig, objService);
            IRVM = new IRViewModel(serialConfig, irConfig, irService);

            // --- Init ViewModel Status ---
            currentContext = OBJVM;
            currentModule = EModuleType.OBJ;
            currentViewName = currentModule.ToString();

            // --- Set Command ---
            SwitchViewCommand = new RelayCommand(param =>
            {
                string targetView = param.ToString();

                Enum.TryParse(targetView, out EModuleType targetModule);

                if (currentModule == targetModule) return;

                if (currentContext is BaseViewModel oldVM)
                {
                    oldVM.DeactivateModule(targetModule);
                    GlobalLogManager.Instance.ConsoleLog($"Deactivate {currentViewName}");
                }

                if (targetView == "OBJ")
                {
                    currentContext = OBJVM;
                    currentModule = EModuleType.OBJ;
                    currentViewName = "OBJ";
                }
                else if (targetView == "IR")
                {
                    currentContext = IRVM;
                    currentModule = EModuleType.IR;
                    currentViewName = "IR";
                }
            });
        }
    }
}
