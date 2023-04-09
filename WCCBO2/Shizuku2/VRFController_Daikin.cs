using BaCSharp;
using Popolo.HVAC.MultiplePackagedHeatPump;
using System.IO.BACnet;

namespace Shizuku2
{
  /// <summary>ダイキン用VRFコントローラ</summary>
  public class VRFController_Daikin : IBACnetController
  {

    #region 定数宣言

    const uint DEVICE_ID = 2;

    const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "VRF controller";

    const string DEVICE_DESCRIPTION = "VRF controller";

    #endregion

    #region 列挙型

    private enum ObjectNumber
    {
      AnalogInput = 0 * 4194304,
      AnalogOutput = 1 * 4194304,
      AnalogValue = 2 * 4194304,
      BinaryInput = 3 * 4194304,
      BinaryOutput = 4 * 4194304,
      BinaryValue = 5 * 4194304,
      MultiStateInput = 13 * 4194304,
      MultiStateOutput = 14 * 4194304,
      Accumulator = 23 * 4194304
    }

    private enum MemberNumber
    {
      OnOff_Setting = 1,
      OnOff_Status = 2,
      Alarm = 3,
      MalfunctionCode = 4,
      OperationMode_Setting = 5,
      OperationMode_Status = 6,
      FanSpeed_Setting = 7,
      FanSpeed_Status = 8,
      MeasuredRoomTemperature = 9,
      Setpoint = 10,
      FilterSignSignal = 11,
      FilterSignSignalReset = 12,
      RemoteControllerPermittion_OnOff = 13,
      RemoteControllerPermittion_OperationMode = 14,
      RemoteControllerPermittion_Setpoint = 16,
      CentralizedControl = 17,
      AccumulatedGas = 18,
      AccumulatedPower = 19,
      CommunicationStatus = 20,
      ForcedSystemStop = 21,
      AirflowDirection_Setting = 22,
      AirflowDirection_Status = 23,
      ForcedThermoOff_Setting = 24,
      ForcedThermoOff_Status = 25,
      EnergySaving_Setting = 26,
      EnergySaving_Status = 27,
      ThermoOn_Status = 28,
      Compressor_Status = 29,
      IndoorFan_Status = 30,
      Heater_Status = 31,
      VentilationMode_Setting = 32,
      VentilationMode_Status = 33,
      VentilationAmount_Setting = 34,
      VentilationAmount_Status = 35
    }

    #endregion

    #region インスタンス変数・プロパティ

    private BACnetCommunicator communicator;

    /// <summary>室内機の台数を取得する</summary>
    public int NumberOfIndoorUnits
    { get { return vrfUnitIndices.Length; } }

    private readonly VRFUnitIndex[] vrfUnitIndices;

    private readonly ExVRFSystem[] vrfSystems;

    #endregion

    #region コンストラクタ

    public VRFController_Daikin(ExVRFSystem[] vrfs)
    {
      vrfSystems = vrfs;

      List< VRFUnitIndex > vrfInd = new List< VRFUnitIndex >();
      for (int i = 0; i < vrfs.Length; i++)
        for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
          vrfInd.Add(new VRFUnitIndex(i, j));
      vrfUnitIndices = vrfInd.ToArray();

      //DMS502B71が扱える台数は256台まで
      if (256 <= NumberOfIndoorUnits)
        throw new Exception("Invalid indoor unit number");

      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      for(int iuNum = 0; iuNum < NumberOfIndoorUnits;iuNum++)
      {
        dObject.AddBacnetObject(new BinaryOutput
          (getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.OnOff_Setting),
          "StartStopCommand_" + iuNum.ToString("000"),
          "This object is used to start (On)/stop (Off) the indoor unit.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.OnOff_Status),
          "StartStopStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s On/Off status.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Alarm),
          "Alarm_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s normal/malfunction status.", false));

        dObject.AddBacnetObject(new MultiStateInput
          (getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.MalfunctionCode),
          "MalfunctionCode_" + iuNum.ToString("000"),
          "This object is used to monitor the malfunction code of an indoor unit in malfunction status.", 512, 1, false));

        dObject.AddBacnetObject(new MultiStateOutput
          (getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.OperationMode_Setting),
          "AirConModeCommand_" + iuNum.ToString("000"),
          "This object is used to set an indoor unit’s operation mode.", 3, 5));

        dObject.AddBacnetObject(new MultiStateInput
          (getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.OperationMode_Status),
          "AirConModeStatus_" + iuNum.ToString("000"),
          "This object is used to monitor an indoor unit’s operation mode.", 3, 5, false));

        dObject.AddBacnetObject(new MultiStateOutput
          (getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.FanSpeed_Setting),
          "AirFlowRateCommand_" + iuNum.ToString("000"),
          "This object is used to set an indoor unit’s fan speed.", 2, 4));

        dObject.AddBacnetObject(new MultiStateInput
          (getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.FanSpeed_Status),
          "AirFlowRateStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s fan speed.", 2, 4, false));

        dObject.AddBacnetObject(new AnalogInput<double>
          (getInstanceNumber(ObjectNumber.AnalogInput, iuNum, MemberNumber.MeasuredRoomTemperature),
          "RoomTemp_" + iuNum.ToString("000"),
          "This object is used to monitor the room temperature detected by the indoor unit return air sensor, remote sensor, or remote controller sensor.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

        dObject.AddBacnetObject(new AnalogValue<double>
          (getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.Setpoint),
          "TempAdjest_" + iuNum.ToString("000"),
          "This object is used to set the indoor unit’s setpoint.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.FilterSignSignal),
          "FilterSign_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s filter sign status.", false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.FilterSignSignalReset),
          "FilterSignReset_" + iuNum.ToString("000"),
          "This object is used to reset the indoor unit’s filter sign signal.", false, false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_OnOff),
          "RemoteControlStart_" + iuNum.ToString("000"),
          "This object is used to permit or prohibit the On/Off operation from the remote controller used to start/stop the indoor unit.", false, false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_OperationMode),
          "RemoteContorlAirConModeSet_" + iuNum.ToString("000"),
          "This object is used to permit or prohibit the remote controller from changing the indoor unit’s operation mode.", false, false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_Setpoint),
          "RemoteControlTempAdjust_" + iuNum.ToString("000"),
          "This object is used to permit or prohibit the remote controller to set the indoor unit setpoint.", false, false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.CentralizedControl),
          "CL_Rejection_X" + iuNum,
          "This object is used to disable or enable control by the Daikin Centralized Controllers which includes the Intelligent Touch Controller used on each DIII-Net system (up to 4 DIII-Net system can be connected to the Interface for use in BACnet).", false, false));

        dObject.AddBacnetObject(new Accumulator<double>
          (getInstanceNumber(ObjectNumber.Accumulator, iuNum, MemberNumber.AccumulatedGas),
          "GasTotalPower_" + iuNum.ToString("000"),
          "No description.", 0, BacnetUnitsId.UNITS_CUBIC_METERS));

        dObject.AddBacnetObject(new Accumulator<double>
          (getInstanceNumber(ObjectNumber.Accumulator, iuNum, MemberNumber.AccumulatedPower),
          "ElecTotalPower_" + iuNum.ToString("000"),
          "No description.", 0, BacnetUnitsId.UNITS_KILOWATT_HOURS));
        
        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.CommunicationStatus),
          "CommunicationStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the communication status between the Interface for use in BACnet and the indoor units.", false));

        dObject.AddBacnetObject(new BinaryValue
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.ForcedSystemStop),
          "SystemForcedOff_" + iuNum.ToString("000"),
          "This object is used to stop all the indoor units connected to the specified DIII network port and permits/prohibits the On/Off operation from the connected remote controller.", false, false));

        dObject.AddBacnetObject(new AnalogValue<double>
          (getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.AirflowDirection_Setting),
          "AirDirectionCommand_" + iuNum.ToString("000"),
          "This object is used to change the indoor unit’s airflow direction.", 0, BacnetUnitsId.UNITS_NO_UNITS, false));

        dObject.AddBacnetObject(new AnalogInput<double>
          (getInstanceNumber(ObjectNumber.AnalogInput, iuNum, MemberNumber.AirflowDirection_Status),
          "AirDirectionStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s airflow direction setting.", 0, BacnetUnitsId.UNITS_NO_UNITS));

        dObject.AddBacnetObject(new BinaryOutput
          (getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.ForcedThermoOff_Setting),
          "ForcedThermoOFFCommand_" + iuNum.ToString("000"),
          "This object is used to force the indoor unit to operate without actively cooling or heating.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.ForcedThermoOff_Status),
          "ForcedThermoOFFStatus_" + iuNum.ToString("000"),
          "This object is used to monitor whether or not the indoor unit is forced to operate without actively cooling or heating.", false));

        dObject.AddBacnetObject(new BinaryOutput
          (getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.EnergySaving_Setting),
          "EnergyEfficiencyCommand_" + iuNum.ToString("000"),
          "This object is used to instruct the indoor unit to operate at a temperature offset of 3.6 0F (20C) from the setpoint for saving energy. The actual setpoint is not changed.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.EnergySaving_Status),
          "EnergyEfficiencyStatus_" + iuNum.ToString("000"),
          "This object is used to monitor whether or not the indoor unit is operating at a temperature offset of 3.6 0F (20C) from the setpoint for saving energy.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.ThermoOn_Status),
          "ThermoStatus_" + iuNum.ToString("000"),
          "This object is used to monitor if the indoor unit is actively cooling or heating.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Compressor_Status),
          "CompressorStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the compressor status of the outdoor unit connected to the indoor unit.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.IndoorFan_Status),
          "IndoorFanStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the indoor unit’s fan status.", false));

        dObject.AddBacnetObject(new BinaryInput
          (getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Heater_Status),
          "HeaterStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the heater status commanded by the indoor unit logic.", false));

        dObject.AddBacnetObject(new MultiStateOutput
          (getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.VentilationMode_Setting),
          "VentilationModeCommand_" + iuNum.ToString("000"),
          "This object is used to set the Energy Recovery Ventilator’s Ventilation Mode.", 2, 3));

        dObject.AddBacnetObject(new MultiStateInput
          (getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.VentilationMode_Status),
          "VentilationModeStatus_" + iuNum.ToString("000"),
          "This object is used to set the Energy Recovery Ventilator’s Ventilation Mode.", 2, 3, false));

        dObject.AddBacnetObject(new MultiStateOutput
          (getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.VentilationAmount_Setting),
          "VentilationAmountCommand_" + iuNum.ToString("000"),
          "This object is used to set the Energy Recovery Ventilator’s Ventilation Amount.", 2, 6));

        dObject.AddBacnetObject(new MultiStateInput
          (getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.VentilationAmount_Status),
          "VentilationAmountStatus_" + iuNum.ToString("000"),
          "This object is used to monitor the Energy Recovery Ventilator’s Ventilation Amount.", 2, 6, false));
      }

      return dObject;
    }

    private int getInstanceNumber
      (ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //DBACSではこの番号で管理しているようだが、これでは桁が大きすぎる。
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; 
      return iUnitNumber * 256 + (int)memNumber;
    }

    #endregion

    #region IBACnetController実装

    /// <summary>制御値を機器やセンサに反映する</summary>
    public void ApplyManipulatedVariables()
    {

      lock (communicator.BACnetDevice)
      {
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          ExVRFSystem vrf = vrfSystems[vrfUnitIndices[i].OUnitIndex];
          bool isSystemOn = false;
          VRFSystem.Mode pMode = VRFSystem.Mode.ThermoOff;
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            BacnetObjectId boID;

            //On/off******************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.OnOff_Setting));
            bool isIUonSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.OnOff_Status));
            bool isIUonStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if (isIUonSet != isIUonStt) //設定!=状態の場合には更新処理
              ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = isIUonSet ? 1u : 0u;
            //1台でも室内機が動いていれば室外機はOn
            isSystemOn |= isIUonSet;

            //運転モード****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
              (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.OperationMode_Setting));
            uint modeSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
              (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.OperationMode_Status));
            uint modeStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (modeSet != modeStt) //設定!=状態の場合には更新処理
            {
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = modeSet;
              boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
                (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.ThermoOn_Status));
              ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 
                (modeSet == 1 || modeSet == 2) ? 1u : 0u;
            }

            ExVRFSystem.Mode md;
            if (modeSet == 1) md = ExVRFSystem.Mode.Cooling;
            else if (modeSet == 2) md = ExVRFSystem.Mode.Heating;
            else md = ExVRFSystem.Mode.ThermoOff; //AutoとDryは一旦無視
            vrf.IndoorUnitModes[j] = isIUonSet ? md : ExVRFSystem.Mode.ShutOff;
            //室外機は最後の稼働室内機のモードに依存（修正必要）
            if (md == ExVRFSystem.Mode.Cooling) pMode = VRFSystem.Mode.Cooling;
            else if (md == ExVRFSystem.Mode.Heating) pMode = VRFSystem.Mode.Heating;

            //室内温度設定***************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.Setpoint));
            double tSp = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            //ダイキンの設定温度は冷暖で5度の偏差を持つ
            vrf.SetPoints_C[j] = tSp;
            vrf.SetPoints_H[j] = tSp - 5;

            //フィルタ信号リセット********
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.FilterSignSignalReset));
            bool restFilter = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if (restFilter)
            {
              //リセット処理
              //***未実装***

              //信号を戻す
              ((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0;
            }

            //リモコン手元操作許可禁止*****
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_OnOff));
            bool rmtPmtOnOff = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_OperationMode));
            bool rmtPmtMode = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.RemoteControllerPermittion_Setpoint));
            bool rmtPmtSP = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            //***未実装***

            //中央制御*******************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.CentralizedControl));
            bool cntCtrl = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            //***未実装***

            //ファン風量*****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.FanSpeed_Setting));
            uint fanSpdSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.FanSpeed_Status));
            uint fanSpdStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if(fanSpdSet != fanSpdStt)
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = fanSpdSet;

            double fRate =
              fanSpdSet == 1 ? 0.3 :
              fanSpdSet == 2 ? 1.0 : 0.7; //Low, High, Middleの係数は適当
            vrf.VRFSystem.SetIndoorUnitAirFlowRate(j, vrf.VRFSystem.IndoorUnits[j].NominalAirFlowRate * fRate);

            //風向***********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.AirflowDirection_Setting));
            double afDirSet = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)getInstanceNumber(ObjectNumber.AnalogInput, iuNum, MemberNumber.AirflowDirection_Status));
            double afDirStt = ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (afDirSet != afDirStt) //設定!=状態の場合には更新処理
              ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = afDirSet;
            //***未実装***

            //強制サーモオフ*************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.ForcedThermoOff_Setting));
            bool fceTOffSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.ForcedThermoOff_Status));
            bool fceTOffStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if(fceTOffSet != fceTOffStt) //設定!=状態の場合には更新処理
              ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = fceTOffStt ? 1u : 0u;
            //***未実装***

            //省エネ指令*****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.EnergySaving_Setting));
            bool engySavingSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.EnergySaving_Status));
            bool engySavingStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if (engySavingSet != engySavingStt) //設定!=状態の場合には更新処理
              ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = engySavingSet ? 1u : 0u;
            //***未実装***

            //換気モード*****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.VentilationMode_Setting));
            uint vModeSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.VentilationMode_Status));
            uint vModeStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (vModeSet != vModeStt) //設定!=状態の場合には更新処理
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vModeSet;
            //***未実装***

            //換気量********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.VentilationAmount_Setting));
            uint vAmountSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.VentilationAmount_Status));
            uint vAmountStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (vAmountSet != vAmountStt) //設定!=状態の場合には更新処理
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vAmountSet;
            //***未実装***

            //強制停止
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.ForcedSystemStop));
            uint fceStop = ((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            //***未実装***

            iuNum++;
          }
          if (isSystemOn) vrf.VRFSystem.CurrentMode = pMode;
          else vrf.VRFSystem.CurrentMode = VRFSystem.Mode.ShutOff;
        }
      }
    }

    /// <summary>機器やセンサの検出値を取得する</summary>
    public void ReadMeasuredValues()
    {
      lock (communicator.BACnetDevice)
      {
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          ExVRFSystem vrf = vrfSystems[vrfUnitIndices[i].OUnitIndex];
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            BacnetObjectId boID;

            //警報***********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Alarm));
            //未実装
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u;

            //故障***********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
              (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.MalfunctionCode));
            //未実装
            ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 1u;

            //フィルタサイン***************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, 
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.FilterSignSignal));
            //未実装
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u;

            //吸い込み室温****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT,
              (uint)getInstanceNumber(ObjectNumber.AnalogInput, iuNum, MemberNumber.MeasuredRoomTemperature));
            ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vrf.VRFSystem.IndoorUnits[j].InletAirTemperature;

            //ガス消費（EHPのため0固定）****
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ACCUMULATOR,
              (uint)getInstanceNumber(ObjectNumber.Accumulator, iuNum, MemberNumber.AccumulatedGas));
            ((Accumulator<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0;

            //電力消費********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ACCUMULATOR,
              (uint)getInstanceNumber(ObjectNumber.Accumulator, iuNum, MemberNumber.AccumulatedPower));
            ((Accumulator<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE += vrf.VRFSystem.IndoorUnits[j].FanElectricity;

            //通信状況********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.CommunicationStatus));
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //1でBACnet通信エラー

            //圧縮機、ファン、ヒータ異常*****
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Compressor_Status));
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //1でエラー
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.IndoorFan_Status));
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //1でエラー
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
              (uint)getInstanceNumber(ObjectNumber.BinaryInput, iuNum, MemberNumber.Heater_Status));
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = 0u; //1でエラー

            iuNum++;
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
