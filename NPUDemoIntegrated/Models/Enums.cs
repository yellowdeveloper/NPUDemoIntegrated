using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.Models
{
    enum EImageSize { S384, S320 }
    enum EImageMode { RESIZE, PAD }
    enum EConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        SendingImage,
        WaitingForInference,
        ProceesingBuffer
    }
    enum EModuleType
    {
        OBJ,
        IR,
        HBLV,
        RAIDAR
    }
}
