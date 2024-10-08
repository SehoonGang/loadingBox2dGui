﻿using CoPick.Setting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loadingBox2dGui.views
{
    public interface IMainForm
    {
        #region Properties
        OperationMode ProgramMode { get; }
        string PlcInfo { set; }
        int CarType { get; set; }
        Image LhImage { set; }
        Image RhImage { set; }
        #endregion

        #region Event Handlers
        event EventHandler<ChangeModeEventArgs> ChangeModeRequeted;
        event EventHandler ConnectCameraRequested;
        event EventHandler ShowSettingManagerRequested;
        event EventHandler CalculateRequested;
        event EventHandler UpdateRequested;
        event EventHandler GetReferenceDataPathRequested;
        event EventHandler GetHandEyeCalibrationFilePathRequested;
        event EventHandler ScanPointRequsted;
        #endregion
    }
}

#region EventArgs
public class ChangeModeEventArgs
{
    public OperationMode Mode { get; private set; }
    public bool HasFreePassTicket { get; set; }
    public ChangeModeEventArgs(OperationMode programMode, bool hasFreePassTicket = false)
    {
        Mode = programMode;
        HasFreePassTicket = hasFreePassTicket;
    }
}

#endregion
