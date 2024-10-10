using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoPick;
using CoPick.Plc;
using loadingBox2dGui.models;

namespace loadingBox2dGui.Tk1MelsecCommunicator
{
    [PlcModel(PlcModel.MELSEC)]
    public class Tk1MelsecCommunicator : PlcCommunicatorForLoadingBox, IDisposable
    {
        private MelsecPLCTransferer _melsecPlc = new MelsecPLCTransferer();

        private bool _heartbeatSignal = false;

        private int _logicalStationNumber;
        private string _heartbeatDeviceName;
        private PlcDataType _heartbeatDeviceType;
        private PlcDbInfo _heartbeatDbInfo;

        private bool _disposedValue;

        private bool _visionUpdateSignal = false;
        private bool _visionStartSignal = false;
        private bool _visionResetSignal = false;
        private bool _visionEndSignal = false;
        private int _visionGlassSectionSignal = 0;
        private int _beforeSection = 0;

        private bool _isConnected;
        private bool _isConnecting;

        private int[] _writeBuf = new int[1];

        public override bool IsConnecting
        {
            get => _isConnecting;
            set => _isConnecting = value;
        }

        public override bool IsConnected
        {
            get => _isConnected;
            set => _isConnected = value;
        }


        public override event EventHandler<VisionUpdateEventArgs> CarTypeUpdate;
        public override event EventHandler VisionStart;
        public override event EventHandler VisionReset;
        public override event EventHandler VisionEnd;
        public override event EventHandler PlcSent;
        public override event EventHandler PlcReceived;
        public override event EventHandler PlcConnected;
        public override event EventHandler PlcDisconnected;
        public override event EventHandler PlcError;

        //public Tk1MelsecCommunicator(Dictionary<PlcAttribute, string> config) : base(1)
        //{
        //    try
        //    {
        //        _logicalStationNumber = int.Parse(config[PlcAttribute.LOGICAL_STATION]);
        //        _heartbeatDeviceName = config[PlcAttribute.HeartbeatDeviceName];
        //        _heartbeatDeviceType = config[PlcAttribute.HeartbeatDeviceType].ToEnum<PlcDataType>();
        //        _heartbeatDbInfo = new PlcDbInfo(int.Parse(config[PlcAttribute.HeartbeatPos]),
        //                                         int.Parse(config[PlcAttribute.HeartbeatBit]));

        //        _melsecPlc.PlcError += (s, e) => Disconnect();
        //        LoadPlcSignalDictForSealer();
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Models.Lang.MSGPlc.ConstructingPlcCommunicatorFailedDueToError : {ex.Message}");
        //    }
        //}

        public Tk1MelsecCommunicator(Dictionary<PlcAttribute, string> config): base(250)
        {
            try
            {
                _logicalStationNumber = 1;
                _heartbeatDeviceName = "D";
                _heartbeatDeviceType = PlcDataType.WORD;
                _heartbeatDbInfo = new PlcDbInfo(5500, 0);

                _melsecPlc.PlcError += (s, e) => Disconnect();
                LoadPlcSignalDictForSealer();
            }
            catch (Exception ex)
            {
                Logger.Error($"Models.Lang.MSGPlc.ConstructingPlcCommunicatorFailedDueToError : {ex.Message}");
            }
        }

        private void LoadPlcSignalDictForSealer()
        {
            PlcMonitorInfos = new List<PlcMonitorInfo<PlcSignalForLoadingBox>>
            {
                // [0]
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5000", 1, PlcDataType.WORD, PlcDataType.BIT)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.VISION_UPDATE] = new PlcDbInfo(5000,3),
                        [PlcSignalForLoadingBox.VISION_START] = new PlcDbInfo(5000,0),
                        [PlcSignalForLoadingBox.VISION_END] = new PlcDbInfo (5000,1),
                        [PlcSignalForLoadingBox.VISION_RESET] = new PlcDbInfo(5000,2),
                        [PlcSignalForLoadingBox.VISION_PASS] = new PlcDbInfo(5000,15)
                    }
                },
                // [1]
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5001", 1, PlcDataType.WORD, PlcDataType.WORD)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.CAR_TYPE | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5001, -1),
                    }
                },
                // [2]
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5002", 2, PlcDataType.WORD, PlcDataType.ASCII)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.CAR_SEQ1 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5002, -1),
                        [PlcSignalForLoadingBox.CAR_SEQ2 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5003, -1),
                    }
                },
                // [3]
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5100", 1, PlcDataType.WORD, PlcDataType.BIT)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.VISION_OK] = new PlcDbInfo(5100, 0),
                        [PlcSignalForLoadingBox.VISION_NG] = new PlcDbInfo(5100, 1),
                        [PlcSignalForLoadingBox.P1_COMPLETED] = new PlcDbInfo(5100, 10),
                    }
                },
                // [4]
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5500", 1, PlcDataType.WORD, PlcDataType.BIT)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.VISION_LIVE_BIT] = new PlcDbInfo(5500, 0),
                    }
                },

                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5004", 4, PlcDataType.WORD, PlcDataType.ASCII)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.BODY_NO1 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5004, -1),
                        [PlcSignalForLoadingBox.BODY_NO2 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5005, -1),
                        [PlcSignalForLoadingBox.BODY_NO3 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5006, -1),
                        [PlcSignalForLoadingBox.BODY_NO4 | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5007, -1),
                    }
                },
                new MelsecMonitorDeviceInfo<PlcSignalForLoadingBox>("D", "5101", 4, PlcDataType.DWORD, PlcDataType.DWORD)
                {
                    SignalDict = new ConcurrentDictionary<PlcSignalForLoadingBox, PlcDbInfo>()
                    {
                        [PlcSignalForLoadingBox.SHIFT_X | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5101, -1),
                        [PlcSignalForLoadingBox.SHIFT_Y | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5103, -1),
                        [PlcSignalForLoadingBox.SHIFT_Z | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5105, -1),
                        [PlcSignalForLoadingBox.SHIFT_RX | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5107, -1),
                        [PlcSignalForLoadingBox.SHIFT_RY | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5109, -1),
                        [PlcSignalForLoadingBox.SHIFT_RZ | PlcSignalForLoadingBox.VALUE] = new PlcDbInfo(5111, -1),
                    }
                },
            };
        }

        public override async Task<int> SendLocalizerStatusAsync(PlcSignalForLoadingBox status, bool val, int nMaxTrials, int delay)
        {
            try
            {
                int res = 0;
                for (var i = 0; i < nMaxTrials && PlcMonitorInfos[3].SignalDict[status].IsOn != val; i++)
                {
                    res = SetBit("D", PlcDataType.WORD, PlcMonitorInfos[3].SignalDict[status], val);
                    await Task.Delay(delay);
                }
                return res;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error : Models.Lang.MSGPlc.SendingSignalToPlcFailed : {status}" + ex.Message);
                return 1;
            }
        }

        protected int SetBit(string device, PlcDataType deviceType, PlcDbInfo dbInfo, bool isOn)
        {
            if (deviceType == PlcDataType.BIT)
            {
                return _melsecPlc.SetBitForBitDevice(device, $"{dbInfo.Pos + dbInfo.Bit:X}", isOn);
            }
            else
            {
                return _melsecPlc.SetBitForWordDevice(device, dbInfo.Pos.ToString(), dbInfo.Bit, isOn);
            }
        }

        public override void Connect(CancellationToken? cancelToken = null)
        {
            if (!_melsecPlc.Init())
            {
                Console.WriteLine("Models.Lang.MSGPlc.MelsecPlcErrorOccured");
                Logger.Error("Models.Lang.MSGPlc.MelsecPlcErrorOccured");
                IsConnected = false;
                return;
            }

            var ret = _melsecPlc.Open(_logicalStationNumber);
            if (ret != 0 && (uint)ret != 0xF0000003)
            {
                IsConnected = false;
                return;
            }

            IsConnected = true;
            StartMonitoringPlc();
            PlcConnected?.Invoke(this, null);
        }


        public override void Disconnect()
        {
            PauseMonitoringPlc();
            Logger.Info("Models.Lang.MSGPlc.DisconnectingPlc");
            var ret = _melsecPlc.Close();
            if (ret != 0)
            {
                _melsecPlc.Dispose();
                _melsecPlc = new MelsecPLCTransferer();
                _melsecPlc.PlcError += (s, e) => Disconnect();
            }

            Logger.Info("Models.Lang.MSGPlc.DisconnectingPlcSucceed");
            IsConnected = false;
            PlcDisconnected?.Invoke(this, null);
        }


        public override void StartMonitoringPlc()
        {
            if (IsConnected)
            {
                _plcMonitorStartEvent.Set();
            }
        }

        public override void StopMonitoringPlc()
        {
            _plcMonitorStopEvent.Set();
            _plcMonitorStartEvent.Set();
        }

        public override void PauseMonitoringPlc()
        {
            _plcMonitorStartEvent.Reset();
        }

        public override void SendHeartbeat()
        {
            int ret;
            _heartbeatSignal = !_heartbeatSignal;
            ret = SetBit(_heartbeatDeviceName, _heartbeatDeviceType, _heartbeatDbInfo, _heartbeatSignal);

            if (ret == 0)
            {
                Task.Run(() =>
                {
                    PlcSent?.Invoke(this, null);
                });
            }
        }

        public override Task SendHeartbeatAsync()
        {
            return Task.Run(() => SendHeartbeat());
        }

        public override void MonitorPlc()
        {
            // MakeDeviceList
            List<string> deviceList = new List<string>();
            int numberOfData = 0;
            foreach (MelsecMonitorDeviceInfo<PlcSignalForLoadingBox> monitorInfo in PlcMonitorInfos)
            {
                if (monitorInfo.DataParseType == PlcDataType.FLOAT)
                {
                    // TODO
                }
                else
                {
                    foreach (var dbInfo in monitorInfo.SignalDict.Values)
                    {
                        if (monitorInfo.DeviceType == PlcDataType.BIT)
                        {
                            // TODO
                        }
                        else if (monitorInfo.DeviceType == PlcDataType.WORD)
                        {
                            ++numberOfData;
                            string deviceName = $"{monitorInfo.DeviceName}{dbInfo.Pos}";

                            deviceList.Add(deviceName);
                        }
                    }
                }
            }

            (var ret, var resData) = _melsecPlc.ReadRandom(deviceList);
            if (ret != 0)
            {
                return;
            }

            int idx = 0;
            foreach (MelsecMonitorDeviceInfo<PlcSignalForLoadingBox> monitorInfo in PlcMonitorInfos)
            {
                if (monitorInfo.DataParseType == PlcDataType.FLOAT)
                {
                    // TODO
                }
                else
                {
                    foreach (var dbInfo in monitorInfo.SignalDict.Values)
                    {
                        if (monitorInfo.DeviceType == PlcDataType.BIT)
                        {
                            // TODO
                        }
                        else if (monitorInfo.DeviceType == PlcDataType.WORD)
                        {
                        }

                        if (monitorInfo.DataParseType == PlcDataType.BIT)
                        {
                            var desired = 1 << dbInfo.Bit;
                            dbInfo.IsOn = (resData[idx] & desired) == desired;
                        }
                        else if (monitorInfo.DataParseType == PlcDataType.WORD)
                        {
                            dbInfo.Int32Value = resData[idx];
                        }
                        else if (monitorInfo.DataParseType == PlcDataType.ASCII || monitorInfo.DataParseType == PlcDataType.TWISTED_ASCII)
                        {
                            dbInfo.Int32Value = resData[idx];
                        }
                        ++idx;
                    }
                }
            }
            Task.Run(() =>
            {
                PlcReceived?.Invoke(this, EventArgs.Empty);
            });
            return;
        }

        public override void RaiseEventIfItNeeds()
        {
            VisionPass = PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_PASS].IsOn;

            if (!_visionUpdateSignal && PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_UPDATE].IsOn)
            {
                OnCarTypeUpdateEvent();
            }
            _visionUpdateSignal = PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_UPDATE].IsOn;

            if (!_visionStartSignal && PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_START].IsOn)
            {
                OnVisionStartEvent();
            }
            _visionStartSignal = PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_START].IsOn;

            if (!_visionEndSignal && PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_END].IsOn)
            {
                OnVisionEndEvent();
            }
            _visionEndSignal = PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_END].IsOn;

            if (!_visionResetSignal && PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_RESET].IsOn)
            {
                OnVisionResetEvent();
            }
            _visionResetSignal = PlcMonitorInfos[0].SignalDict[PlcSignalForLoadingBox.VISION_RESET].IsOn;
        }

        private void OnVisionEndEvent()
        {
            VisionEnd?.Invoke(this, EventArgs.Empty);
        }

        private void OnVisionResetEvent()
        {
            VisionReset?.Invoke(this, EventArgs.Empty);
        }

        private void OnVisionStartEvent()
        {
            VisionStart?.Invoke(this, EventArgs.Empty);
        }

        private bool OnCarTypeUpdateEvent()
        {
            if (_visionUpdateSignal)
            {
                return false;
            }

            int oldCarType = CarType;
            string oldCarSeq = CarSeq;
            string oldBodyNumber = BodyNumber;

            CarType = PlcMonitorInfos[1].SignalDict[PlcSignalForLoadingBox.CAR_TYPE | PlcSignalForLoadingBox.VALUE].Int32Value;

            string carSeq = PlcMonitorInfos[2].SignalDict[PlcSignalForLoadingBox.CAR_SEQ1 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII)
                + PlcMonitorInfos[2].SignalDict[PlcSignalForLoadingBox.CAR_SEQ2 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII);
            //string carSeq = PlcMonitorInfos[1].SignalDict[PlcSignalForLoadingBox.CAR_SEQ | PlcSignalForLoadingBox.VALUE].GetText(PlcMonitorInfos[1].DataParseType);
            CarSeq = carSeq;

            string bodyNumber = PlcMonitorInfos[5].SignalDict[PlcSignalForLoadingBox.BODY_NO1 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII)
                + PlcMonitorInfos[5].SignalDict[PlcSignalForLoadingBox.BODY_NO2 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII)
                + PlcMonitorInfos[5].SignalDict[PlcSignalForLoadingBox.BODY_NO3 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII)
                + PlcMonitorInfos[5].SignalDict[PlcSignalForLoadingBox.BODY_NO4 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII);
            //+ PlcMonitorInfos[5].SignalDict[PlcSignalForLoadingBox.BODY_NUM_5 | PlcSignalForLoadingBox.VALUE].GetText(PlcDataType.ASCII);
            BodyNumber = bodyNumber;

            CarTypeUpdate?.Invoke(this, new VisionUpdateEventArgs(CarType, CarSeq, BodyNumber));
            return oldCarSeq != CarSeq || oldCarType != CarType || oldBodyNumber != BodyNumber;

            //CarTypeUpdate?.Invoke(this, new VisionUpdateEventArgs(CarType, CarSeq));
            //return oldCarSeq != CarSeq || oldCarType != CarType;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: 관리형 상태(관리형 개체)를 삭제합니다.
                }

                // TODO: 비관리형 리소스(비관리형 개체)를 해제하고 종료자를 재정의합니다.
                // TODO: 큰 필드를 null로 설정합니다.
                _disposedValue = true;

                base.Dispose(disposing);
            }
        }
    }
}
