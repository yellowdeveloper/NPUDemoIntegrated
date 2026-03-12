using FTD2XX_NET;
using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using NPUDemoIntegrated.Utils;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    class MainViewModel: Notifier
    {
        public OBJViewModel OBJVM { get; }
        public IRViewModel IRVM { get; }
        public HBLVViewModel HBLVVM { get; }

        private object _currentContext;
        private EModuleType _currentModule;
        private string _currentViewName;

        public double windowHeight;
        public double windowWidth;
        public ResizeMode resizeMode;
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
            SharedStatus sharedStatus = new SharedStatus();

            // Eject Config Object
            var serialConfig = GlobalConfigManager.Instance.serialConfig;
            var objConfig = GlobalConfigManager.Instance.objConfig;
            var irConfig = GlobalConfigManager.Instance.irConfig;

            // Inject Config to Service
            var objService = new OBJSerialService(serialConfig, objConfig, sp, ftdi, sharedStatus);
            var irService = new IRSerialService(serialConfig, irConfig, sp, ftdi, sharedStatus);

            // Inject Service & Config to ViewModel
            OBJVM = new OBJViewModel(serialConfig, objConfig, objService);
            IRVM  = new IRViewModel(serialConfig, irConfig, irService);
            HBLVVM = new HBLVViewModel(serialConfig, irConfig, irService);

            // --- Init ViewModel Status ---
            currentContext = OBJVM;
            currentModule = EModuleType.OBJ;
            currentViewName = currentModule.ToString();

            OBJVM.ActivateModule();

            // --- Set Command ---
            SwitchViewCommand = new RelayCommand(param =>
            {
                string targetView = param.ToString();

                Enum.TryParse(targetView, out EModuleType targetModule);

                if (currentModule == targetModule) return;

                if (currentContext is BaseViewModel oldVM)
                {
                    GlobalLogManager.Instance.ConsoleLog($"Change from {oldVM} to {targetModule}");
                    oldVM.DeactivateModule(targetModule);
                    GlobalLogManager.Instance.ConsoleLog($"Deactivate {currentViewName}");
                }

                if (targetView == "OBJ")
                {
                    if (sharedStatus.connectionState == EConnectionState.Disconnected)
                    {
                        OBJVM.ButtonCommand = OBJVM.ConnectCommand;
                    }
                    else
                    {
                        OBJVM.ButtonCommand = OBJVM.DisconnectCommand;
                    }

                    currentContext = OBJVM;
                    currentModule = EModuleType.OBJ;

                    windowHeight = OBJVM.windowHeight;
                    windowWidth = OBJVM.windowWidth;
                    resizeMode = OBJVM.resizeMode;

                    currentViewName = "OBJ";
                    OBJVM.ActivateModule();
                }
                else if (targetView == "IR")
                {
                    if (sharedStatus.connectionState == EConnectionState.Disconnected)
                    {
                        IRVM.ButtonCommand = IRVM.ConnectCommand;
                        IRVM.modColor = "White";
                    }
                    else
                    {
                        IRVM.ButtonCommand = IRVM.DisconnectCommand;
                        IRVM.modColor = "White";
                    }

                    currentContext = IRVM;
                    currentModule = EModuleType.IR;

                    windowHeight = IRVM.windowHeight;
                    windowWidth = IRVM.windowWidth;
                    resizeMode = IRVM.resizeMode;

                    currentViewName = "IR";
                    IRVM.ActivateModule();
                }
                else if (targetView == "HBLV")
                {
                    if (sharedStatus.connectionState == EConnectionState.Disconnected)
                    {
                        HBLVVM.ButtonCommand = HBLVVM.ConnectCommand;
                    }
                    else
                    {
                        HBLVVM.ButtonCommand = HBLVVM.DisconnectCommand;
                    }

                    currentContext = HBLVVM;
                    currentModule = EModuleType.HBLV;

                    windowHeight = HBLVVM.windowHeight;
                    windowWidth = HBLVVM.windowWidth;
                    resizeMode = HBLVVM.resizeMode;

                    currentViewName = "HBLV";
                    HBLVVM.ActivateModule();
                }
            });
        }
    }
}
