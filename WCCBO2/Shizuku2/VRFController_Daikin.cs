using BaCSharp;
using Popolo.HVAC.MultiplePackagedHeatPump;
using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  /// <summary>ダイキン用VRFコントローラ</summary>
  public class VRFController_Daikin : IBACnetController
  {

    #region 定数宣言

    const uint DEVICE_ID = 99;

    const int EXCLUSIVE_PORT = 0xBAC0 + 1;

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
      CentralizedController = 17,
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

    private readonly VRFSystem[] vrfSystems;

  #endregion

    #region コンストラクタ

    public VRFController_Daikin(VRFSystem[] vrfs)
    {
      vrfSystems = vrfs;

      List< VRFUnitIndex > vrfInd = new List< VRFUnitIndex >();
      for (int i = 0; i < vrfs.Length; i++)
        for (int j = 0; j < vrfs[i].IndoorUnitNumber; j++)
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
          (getInstanceNumber(ObjectNumber.BinaryValue, iuNum, MemberNumber.CentralizedController),
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

    private int getInstanceNumber(ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; //DBACSではこの番号で管理しているようだが。。。
      return iUnitNumber * 256 + (int)memNumber;
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables()
    {

      lock (communicator.BACnetDevice)
      {
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          VRFSystem vrf = vrfSystems[vrfUnitIndices[i].OUnitIndex];
          bool isSystemOn = false;
          for (int j = 0; j < vrf.IndoorUnitNumber; j++)
          {
            BacnetObjectId boID;

            //On/off
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.OnOff_Setting));
            bool isIUon = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);

            //運転モード
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.OperationMode_Setting));
            uint mode = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            VRFUnit.Mode md;
            if (mode == 1) md = VRFUnit.Mode.Cooling;
            else if (mode == 2) md = VRFUnit.Mode.Heating;
            else md = VRFUnit.Mode.ThermoOff; //AutoとDryは一旦無視
            vrf.SetIndoorUnitMode(j, isIUon ? md : VRFUnit.Mode.ShutOff);

            //ファン風量
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.FanSpeed_Setting));
            uint fan = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            double fRate;
            if (fan == 1) fRate = 0.3;
            else if (fan == 2) fRate = 1.0;
            else fRate = 0.7; //Low, High, Middleの係数は適当
            vrf.SetIndoorUnitAirFlowRate(j, vrf.IndoorUnits[j].NominalAirFlowRate * fRate);

            //1台でも室内機が動いていれば室外機はOn
            isSystemOn |= isIUon;

            iuNum++;
          }
        }
      }
    }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues()
    {
      //
    }

    public void StartService()
    {
      communicator.StartService();
    }

    #endregion

    #region 構造体定義

    /// <summary>室外機と室内機の番号を保持する</summary>
    private struct VRFUnitIndex
    {
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
