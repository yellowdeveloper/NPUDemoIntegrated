using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NPUDemoIntegrated.ViewModels
{
    class HBLVViewModel : BaseViewModel
    {
        private readonly IRSerialService _serialService;
        public IRConfig irConfig { get; }
        public SerialConfig serialConfig { get; }

        public override ICommand ConnectCommand { get; }
        public override ICommand DisconnectCommand { get; }

        public override string title => "Doksan NPU Real-Time Vision AI Demonstration";
        public override string subTitle => "Real-time camera input and on-device hemoglobin level inference";
        public HBLVViewModel(SerialConfig _serialConfig, IRConfig _irConfig, IRSerialService service)
        {
            irConfig = _irConfig;
            serialConfig = _serialConfig;
            _serialService = service;
        }

        public override void DeactivateModule(EModuleType targetModule)
        {

        }


        public override void ActivateModule()
        {

        }

        public override void Dispose()
        {

        }
    }
}
