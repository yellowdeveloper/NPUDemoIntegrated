using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models.IRModule
{
    class IRSerialService
    {
        private readonly IRConfig _config;
        public IRSerialService(IRConfig config)
        {
            _config = config;
        }
    }
}
