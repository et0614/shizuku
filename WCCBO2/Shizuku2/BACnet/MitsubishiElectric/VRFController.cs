using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2.BACnet.MitsubishiElectric
{
    /// <summary>三菱電機用VRFコントローラ</summary>
    public class VRFController : IBACnetController
    {

        #region 定数宣言

        const uint DEVICE_ID = 2;

        public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

        const string DEVICE_NAME = "Mitsubishi Electric VRF controller";

        const string DEVICE_DESCRIPTION = "Mitsubishi Electric VRF controller";

        const int SIGNAL_UPDATE_SPAN = 60;

        #endregion

        #region インスタンス変数・プロパティ

        private BACnetCommunicator communicator;

        /// <summary>室内機の台数を取得する</summary>
        public int NumberOfIndoorUnits
        { get { return vrfUnitIndices.Length; } }

        private readonly VRFUnitIndex[] vrfUnitIndices;

        private readonly ExVRFSystem[] vrfSystems;

        private DateTime nextSignalApply = new DateTime(1980, 1, 1, 0, 0, 0);
        private DateTime nextSignalRead = new DateTime(1980, 1, 1, 0, 0, 0);

        #endregion

        #region コンストラクタ

        public VRFController(ExVRFSystem[] vrfs)
        {
            vrfSystems = vrfs;

            List<VRFUnitIndex> vrfInd = new List<VRFUnitIndex>();
            for (int i = 0; i < vrfs.Length; i++)
                for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
                    vrfInd.Add(new VRFUnitIndex(i, j));
            vrfUnitIndices = vrfInd.ToArray();

            //AE-200Jが扱える台数は50台まで
            if (50 <= NumberOfIndoorUnits)
                throw new Exception("Invalid indoor unit number");

            communicator = new BACnetCommunicator
              (makeDeviceObject(), EXCLUSIVE_PORT);
        }

        /// <summary>BACnet Deviceを作成する</summary>
        private DeviceObject makeDeviceObject()
        {
            DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

            for (int grpNum = 0; grpNum < NumberOfIndoorUnits; grpNum++) //室内機番号=グループ番号とする(1グループ1台)
            {
                dObject.AddBacnetObject(new BinaryOutput
                  (10000 + grpNum * 100 + 1,
                  "On Off Setup_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to start (On)/stop (Off) the indoor unit.", false));

                dObject.AddBacnetObject(new BinaryInput
                  (10000 + grpNum * 100 + 2,
                  "On Off State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the indoor unit’s On/Off status.", false));

                dObject.AddBacnetObject(new BinaryInput
                  (10000 + grpNum * 100 + 3,
                  "Alarm Signal_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the indoor unit’s normal/malfunction status.", false));

                dObject.AddBacnetObject(new MultiStateInput
                  (10000 + grpNum * 100 + 4,
                  "Error Code_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the error code of an indoor unit.", 9, 1, false)); //1正常,2その他の異常,3冷媒系異常,4水系異常,5空気系異常,6電気系異常,7センサー異常,8通信異常,9システム異常

                dObject.AddBacnetObject(new MultiStateOutput
                  (10000 + grpNum * 100 + 5,
                  "Operational Mode Setup_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set an indoor unit’s operation mode.", 3, 5)); //1冷房,2暖房,3送風,4自動,5ドライ

                dObject.AddBacnetObject(new MultiStateInput
                  (10000 + grpNum * 100 + 6,
                  "Operational Mode State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor an indoor unit’s operation mode.", 5, 3, false)); //1冷房,2暖房,3送風,4自動,5ドライ

                dObject.AddBacnetObject(new MultiStateOutput
                  (10000 + grpNum * 100 + 7,
                  "Fan Speed Setup_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set an indoor unit’s fan speed.", 2, 4)); //1弱,2強,3中2,4中1,5自動

                dObject.AddBacnetObject(new MultiStateInput
                  (10000 + grpNum * 100 + 8,
                  "Fan Speed Status_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the indoor unit’s fan speed.", 4, 2, false)); //1弱,2強,3中2,4中1,5自動

                dObject.AddBacnetObject(new AnalogInput<double>
                  (10000 + grpNum * 100 + 9,
                  "Room Temp_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the room temperature detected by the indoor unit return air sensor, remote sensor, or remote controller sensor.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

                dObject.AddBacnetObject(new AnalogValue<double>
                  (10000 + grpNum * 100 + 10,
                  "Set Temp_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the indoor unit’s setpoint.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false)
                { m_PROP_HIGH_LIMIT = 32, m_PROP_LOW_LIMIT = 16 });

                dObject.AddBacnetObject(new BinaryInput
                  (10000 + grpNum * 100 + 11,
                  "Filter Sign_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the indoor unit’s filter sign status.", false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 12,
                  "Filter Sign Reset_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to reset the indoor unit’s filter sign signal.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 13,
                  "Prohibition On Off_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to permit or prohibit the On/Off operation from the remote controller used to start/stop the indoor unit.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 14,
                  "Prohibition Mode_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to permit or prohibit the remote controller from changing the indoor unit’s operation mode.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 15,
                  "Prohibition Filter Sign Reset_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to permit or prohibit the filter sign reset.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 16,
                  "Prohibition Set Temperature_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to permit or prohibit the remote controller to set the indoor unit setpoint.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 17,
                  "Prohibition Fan Speed_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to permit or prohibit the remote controller to set the fan speed.", false, false));

                dObject.AddBacnetObject(new BinaryInput
                  (10000 + grpNum * 100 + 20,
                  "M=NET Communication State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the communication status between the Interface for use in BACnet and the indoor units.", false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + grpNum * 100 + 21,
                  "System Forced Off (Individual)_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to stop individual indoor units connected to the network port and permits/prohibits the On/Off operation from the connected remote controller.", false, false));

                dObject.AddBacnetObject(new BinaryValue
                  (10000 + 9900 + 21,
                  "System Forced Off (Collective)_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to stop all the indoor units connected to the network port and permits/prohibits the On/Off operation from the connected remote controller.", false, false));

                dObject.AddBacnetObject(new MultiStateOutput
                  (10000 + grpNum * 100 + 22,
                  "Air Direction Setup_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to change the indoor unit’s airflow direction.", 1, 5)); //1水平,2下向き60%,3下向き80%,4下向き100%,5スイング

                dObject.AddBacnetObject(new MultiStateInput
                  (10000 + grpNum * 100 + 23,
                  "AirDirection State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor the indoor unit’s airflow direction setting.", 5, 1, false)); //1水平,2下向き60%,3下向き80%,4下向き100%,5スイング

                dObject.AddBacnetObject(new AnalogValue<double>
                  (10000 + grpNum * 100 + 24,
                  "Set Temp (Cool)_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the indoor unit’s setpoint (Cool).", 26, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false));

                dObject.AddBacnetObject(new AnalogValue<double>
                  (10000 + grpNum * 100 + 25,
                  "Set Temp (Heat)_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the indoor unit’s setpoint (Heat).", 22, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false));

                dObject.AddBacnetObject(new AnalogValue<double>
                  (10000 + grpNum * 100 + 26,
                  "Set Temp (Auto)_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the indoor unit’s setpoint (Auto).", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false));

                //Set High (Low) Limit Setback Tempは未実装。北米仕様にはあり。
                //
                //

                dObject.AddBacnetObject(new MultiStateOutput
                  (10000 + grpNum * 100 + 35,
                  "Ventilation Mode Setup_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the Energy Recovery Ventilator’s Ventilation Mode.", 1, 3)); //1熱交換,2普通,3自動

                dObject.AddBacnetObject(new MultiStateInput
                  (10000 + grpNum * 100 + 36,
                  "Ventilation Mode State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to set the Energy Recovery Ventilator’s Ventilation Mode.", 3, 1, false)); //1熱交換,2普通,3自動

                //仕様では按分機能だが室内機ファンのみ計上する
                dObject.AddBacnetObject(new Accumulator<double>
                  (10000 + grpNum * 100 + 39,
                  "ElecTotalPower_" + vrfUnitIndices[grpNum].ToString(),
                  "No description.", 0, BacnetUnitsId.UNITS_KILOWATT_HOURS));

                dObject.AddBacnetObject(new BinaryInput
                  (10000 + grpNum * 100 + 47,
                  "Thermo On Off State_" + vrfUnitIndices[grpNum].ToString(),
                  "This object is used to monitor if the indoor unit is actively cooling or heating.", false));

                //トレンドログは未実装。
                //
                //
            }

            return dObject;
        }

        #endregion

        #region IBACnetController実装

        /// <summary>制御値を機器やセンサに反映する</summary>
        public void ApplyManipulatedVariables(DateTime dTime)
        {
            if (dTime < nextSignalApply) return;
            nextSignalApply = dTime.AddSeconds(SIGNAL_UPDATE_SPAN);

            lock (communicator.BACnetDevice)
            {
                int grpNum = 0;
                for (int i = 0; i < vrfSystems.Length; i++)
                {
                    ExVRFSystem vrf = vrfSystems[i];
                    bool isSystemOn = false;
                    for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
                    {
                        BacnetObjectId boID;

                        //On/off******************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(10000 + grpNum * 100 + 1));
                        bool isIUonSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(10000 + grpNum * 100 + 2));
                        bool isIUonStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        if (isIUonSet != isIUonStt) //設定!=状態の場合には更新処理
                            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = isIUonSet ? 1u : 0u;
                        //1台でも室内機が動いていれば室外機はOn
                        isSystemOn |= isIUonSet;

                        //運転モード****************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 5));
                        uint modeSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(10000 + grpNum * 100 + 6));
                        uint modeStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        if (modeSet != modeStt) //設定!=状態の場合には更新処理
                        {
                            ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = modeSet;
                            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(10000 + grpNum * 100 + 47));
                            //送風以外の場合にはサーモOn
                            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE =
                              modeSet != 3 ? 1u : 0u;
                        }

                        vrf.IndoorUnitModes[j] =
                          !isIUonSet ? ExVRFSystem.Mode.ShutOff :
                          modeSet == 1 ? ExVRFSystem.Mode.Cooling :
                          modeSet == 2 ? ExVRFSystem.Mode.Heating :
                          modeSet == 3 ? ExVRFSystem.Mode.ThermoOff :
                          modeSet == 4 ? ExVRFSystem.Mode.Auto : ExVRFSystem.Mode.Dry;

                        //室内温度設定***************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + 10));
                        double tSp = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE; //この値、いつ使う？
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + 24));
                        double tSpCool = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + 25));
                        double tSpHeat = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        //通常の温度設定と冷暖モード別の設定値との使い分けが不明
                        vrf.SetSetpoint(tSpCool, j, true);
                        vrf.SetSetpoint(tSpHeat, j, false);

                        //フィルタ信号リセット********
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(10000 + grpNum * 100 + 12));
                        bool restFilter = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        if (restFilter)
                        {
                            //リセット処理
                            //***未実装***

                            //信号を戻す
                            ((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0;
                        }

                        //リモコン手元操作許可禁止*****
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(10000 + grpNum * 100 + 13));
                        bool rmtPmtOnOff = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(10000 + grpNum * 100 + 14));
                        bool rmtPmtMode = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(10000 + grpNum * 100 + 16));
                        bool rmtPmtSP = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(10000 + grpNum * 100 + 17));
                        bool rmtPmtFSpeed = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
                        vrf.PermitSPControl[j] = rmtPmtSP;
                        //***未実装***

                        //ファン風量*****************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 7));
                        uint fanSpdSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(10000 + grpNum * 100 + 8));
                        uint fanSpdStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        if (fanSpdSet != fanSpdStt)
                            ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = fanSpdSet;

                        double fRate =
                          fanSpdSet == 1 ? 0.3 : //弱
                          fanSpdSet == 2 ? 1.0 : //強
                          fanSpdSet == 3 ? 0.5 : 0.7; //中2, 中1（係数は適当）
                        vrf.VRFSystem.SetIndoorUnitAirFlowRate(j, vrf.VRFSystem.IndoorUnits[j].NominalAirFlowRate * fRate);

                        //風向***********************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 22));
                        uint afDirSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(10000 + grpNum * 100 + 23));
                        uint afDirStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        if (afDirSet != afDirStt) //設定!=状態の場合には更新処理
                            ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = afDirSet;
                        double ptge =
                          afDirStt == 1 ? 0.0 : //水平
                          afDirStt == 2 ? 0.6 : //下向き60%
                          afDirStt == 3 ? 0.8 : 1.0; //下向き80%, 100%
                        vrf.Direction[j] = Math.PI / 180d * ptge * 90.0;

                        //換気モード*****************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 35));
                        uint vModeSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(10000 + grpNum * 100 + 36));
                        uint vModeStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
                        if (vModeSet != vModeStt) //設定!=状態の場合には更新処理
                            ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vModeSet;
                        //***未実装***

                        grpNum++;
                    }
                }
            }
        }

        /// <summary>機器やセンサの検出値を取得する</summary>
        public void ReadMeasuredValues(DateTime dTime)
        {
            if (dTime < nextSignalRead) return;
            nextSignalRead = dTime.AddSeconds(SIGNAL_UPDATE_SPAN);

            lock (communicator.BACnetDevice)
            {
                int grpNum = 0;
                for (int i = 0; i < vrfSystems.Length; i++)
                {
                    ExVRFSystem vrf = vrfSystems[i];
                    for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
                    {
                        BacnetObjectId boID;

                        //警報***********************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(10000 + grpNum * 100 + 3));
                        ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //未実装

                        //故障***********************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(10000 + grpNum * 100 + 4));
                        ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 1u; //未実装

                        //フィルタサイン***************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(10000 + grpNum * 100 + 11));
                        ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //未実装

                        //吸い込み室温****************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(10000 + grpNum * 100 + 9));
                        ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vrf.VRFSystem.IndoorUnits[j].InletAirTemperature;

                        //電力消費（仕様では按分機能だが室内機ファンのみ計上する）********************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ACCUMULATOR, (uint)(10000 + grpNum * 100 + 39));
                        ((Accumulator<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE += vrf.VRFSystem.IndoorUnits[j].FanElectricity;

                        //通信状況********************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(10000 + grpNum * 100 + 20));
                        ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //1でBACnet通信エラー

                        //室内温度設定***************
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + 24));
                        ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vrf.GetSetpoint(j, true);
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + 25));
                        ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vrf.GetSetpoint(j, false);

                        grpNum++;
                    }
                }
            }
        }

        /// <summary>BACnetControllerのサービスを開始する</summary>
        public void StartService()
        {
            communicator.StartService();
        }

        /// <summary>BACnetControllerのリソースを解放する</summary>
        public void EndService()
        {
            communicator.EndService();
        }


        #endregion

        #region 構造体定義

        /// <summary>室外機と室内機の番号を保持する</summary>
        private struct VRFUnitIndex
        {
            public string ToString()
            {
                return (OUnitIndex + 1).ToString() + "-" + (IUnitIndex + 1).ToString();
            }

            public int OUnitIndex { get; private set; }

            public int IUnitIndex { get; private set; }

            public VRFUnitIndex(int oUnitIndex, int iUnitIndex)
            {
                OUnitIndex = oUnitIndex;
                IUnitIndex = iUnitIndex;
            }
        }

        #endregion

    }
}
