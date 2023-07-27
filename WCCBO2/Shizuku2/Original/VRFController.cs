using BaCSharp;
using Popolo.HVAC.MultiplePackagedHeatPump;
using System;
using System.IO.BACnet;

namespace Shizuku2.Original
{
  /// <summary>オリジナルVRFコントローラ</summary>
  public class VRFController : IBACnetController
  {
    //BACnet Object IDは以下のルールで付与
    //1000*室外機番号 + 100*室内機番号 + Member Number
    //Ex. VRF 3-2 Setpoint_Setting(5)  ->  1000*3+100*2+5=3205
    //ただし蒸発・凝縮温度などシステム全体に関わる項目は室内機番号=0とする
    //Ex. VRF 3-2 CondensingTemperatureSetpoint_Status(19)  ->  1000*3+100*0+19=3019

    #region 定数宣言

    const uint DEVICE_ID = 2;

    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "WCCBO Original VRF controller";

    const string DEVICE_DESCRIPTION = "WCCBO Original VRF controller";

    const int SIGNAL_UPDATE_SPAN = 60;

    #endregion

    #region 列挙型

    private enum MemberNumber
    {
      OnOff_Setting = 1,
      OnOff_Status = 2,
      OperationMode_Setting = 3,
      OperationMode_Status = 4,
      Setpoint_Setting = 5,
      Setpoint_Status= 6,
      MeasuredRoomTemperature = 7,
      FanSpeed_Setting = 8,
      FanSpeed_Status = 9,
      AirflowDirection_Setting = 10,
      AirflowDirection_Status = 11,
      RemoteControllerPermittion_Setpoint_Setting = 12,
      RemoteControllerPermittion_Setpoint_Status = 13,
      ForcedRefrigerantTemperature_Setting = 14,
      ForcedRefrigerantTemperature_Status = 15,
      EvaporatingTemperatureSetpoint_Setting = 16,
      EvaporatingTemperatureSetpoint_Status = 17,
      CondensingTemperatureSetpoint_Setting = 18,
      CondensingTemperatureSetpoint_Status = 19
    }

    #endregion

    #region インスタンス変数・プロパティ

    private BACnetCommunicator communicator;

    /// <summary>室内機の台数を取得する</summary>
    public int NumberOfIndoorUnits
    { get { return vrfUnitIndices.Length; } }

    /// <summary>各系統の室内機の台数</summary>
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

      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      for (int iuNum = 0; iuNum < NumberOfIndoorUnits; iuNum++)
      {
        int bBase = 1000 * (1 + vrfUnitIndices[iuNum].OUnitIndex) + 100 * (1 + vrfUnitIndices[iuNum].IUnitIndex);

        dObject.AddBacnetObject(new BinaryOutput
          ((int)(bBase + MemberNumber.OnOff_Setting),
          "OnOffCommand_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to start (On)/stop (Off) the indoor unit.", false));

        dObject.AddBacnetObject(new BinaryInput
          ((int)(bBase + MemberNumber.OnOff_Status),
          "OnOffStatus_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor the indoor unit's On/Off status.", false));

        dObject.AddBacnetObject(new MultiStateOutput
          ((int)(bBase + MemberNumber.OperationMode_Setting),
          "ModeCommand_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to set an indoor unit's operation mode. 1:cool, 2:heat, 3:fan", 3, 3));

        dObject.AddBacnetObject(new MultiStateInput
          ((int)(bBase + MemberNumber.OperationMode_Status),
          "ModeStatus_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor an indoor unit's operation mode. 1:cool, 2:heat, 3:fan", 3, 3, false));

        dObject.AddBacnetObject(new AnalogValue<double>
          ((int)(bBase + MemberNumber.Setpoint_Setting),
          "TempSPSetting_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to set the indoor unit's setpoint.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false)
        { m_PROP_HIGH_LIMIT = 32, m_PROP_LOW_LIMIT = 16 });

        dObject.AddBacnetObject(new AnalogValue<double>
         ((int)(bBase + MemberNumber.Setpoint_Status),
         "TempSPStatus_" + vrfUnitIndices[iuNum].ToString(),
         "This object is used to monitor the indoor unit's setpoint.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false)
        { m_PROP_HIGH_LIMIT = 32, m_PROP_LOW_LIMIT = 16 });

        dObject.AddBacnetObject(new AnalogInput<double>
          ((int)(bBase + MemberNumber.MeasuredRoomTemperature),
          "RoomTemp_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor the room temperature detected by the indoor unit return air sensor.", 24, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

        dObject.AddBacnetObject(new MultiStateOutput
          ((int)(bBase + MemberNumber.FanSpeed_Setting),
          "AirFlowRateCommand_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to set an indoor unit's fan speed. 1:Low, 2:Middle, 3:High", 2, 3));

        dObject.AddBacnetObject(new MultiStateInput
          ((int)(bBase + MemberNumber.FanSpeed_Status),
          "AirFlowRateStatus_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor the indoor unit's fan speed. 1:Low, 2:Middle, 3:High", 3, 2, false));

        dObject.AddBacnetObject(new MultiStateOutput
          ((int)(bBase + MemberNumber.AirflowDirection_Setting),
          "AirDirectionCommand_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to change the indoor unit's airflow direction. 1:Horizontal, 2:22.5deg, 3:45deg, 4:67.5deg, 5:Vertical", 5, 5));

        dObject.AddBacnetObject(new MultiStateInput
          ((int)(bBase + MemberNumber.AirflowDirection_Status),
          "AirDirectionStatus_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor the indoor unit's airflow direction setting. 1:Horizontal, 2:22.5deg, 3:45deg, 4:67.5deg, 5:Vertical", 5, 5, false));

        dObject.AddBacnetObject(new BinaryValue
          ((int)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Setting),
          "RemoteControlStart_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to permit or prohibit the On/Off operation from the remote controller.", false, false));

        dObject.AddBacnetObject(new BinaryInput
          ((int)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Status),
          "RemoteControlStart_" + vrfUnitIndices[iuNum].ToString(),
          "This object is used to monitor status of permit or prohibit the On/Off operation from the remote controller.", false));
      }

      for (int ouNum = 0; ouNum < vrfSystems.Length; ouNum++)
      {
        int bBase = 1000 * (1 + ouNum);

        dObject.AddBacnetObject(new BinaryValue
          ((int)(bBase + MemberNumber.ForcedRefrigerantTemperature_Setting),
          "RefrigerantTempCtrlSetting",
          "This object is used to change the forced evaporating/condensing control of VRF system.", false, false));

        dObject.AddBacnetObject(new BinaryInput
          ((int)(bBase + MemberNumber.ForcedRefrigerantTemperature_Status),
          "RefrigerantTempCtrlStatus",
          "This object is used to monitor the forced evaporating/condensing control of VRF system..", false));

        dObject.AddBacnetObject(new AnalogValue<double>
          ((int)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Setting),
          "EvpTempSetting",
          "This object is used to set the evaporating temperature of VRF system.", 10, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false)
        { m_PROP_HIGH_LIMIT = 15, m_PROP_LOW_LIMIT = 2 });

        dObject.AddBacnetObject(new AnalogInput<double>
          ((int)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status),
          "EvpTempStatus",
          "This object is used to monitor the evaporating temperature of VRF system.", 10, BacnetUnitsId.UNITS_DEGREES_CELSIUS)
        { m_PROP_HIGH_LIMIT = 15, m_PROP_LOW_LIMIT = 2 });

        dObject.AddBacnetObject(new AnalogValue<double>
          ((int)(bBase + MemberNumber.CondensingTemperatureSetpoint_Setting),
          "CndTempSetting",
          "This object is used to set the condensing temperature of VRF system.", 45, BacnetUnitsId.UNITS_DEGREES_CELSIUS, false)
        { m_PROP_HIGH_LIMIT = 50, m_PROP_LOW_LIMIT = 35 }); //これ、適当

        dObject.AddBacnetObject(new AnalogInput<double>
          ((int)(bBase + MemberNumber.CondensingTemperatureSetpoint_Status),
          "CndTempStatus",
          "This object is used to monitor the condensing temperature of VRF system.", 45, BacnetUnitsId.UNITS_DEGREES_CELSIUS)
        { m_PROP_HIGH_LIMIT = 50, m_PROP_LOW_LIMIT = 35 });
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
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          int bBase;
          BacnetObjectId boID;
          ExVRFSystem vrf = vrfSystems[i];
          bool isSystemOn = false;
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            bBase = 1000 * (i + 1) + 100 * (j + 1);
            
            //On/off******************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.OnOff_Setting));
            bool isIUonSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.OnOff_Status));
            bool isIUonStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if (isIUonSet != isIUonStt) //設定!=状態の場合には更新処理
              ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = isIUonSet ? 1u : 0u;
            //1台でも室内機が動いていれば室外機はOn
            isSystemOn |= isIUonSet;

            //運転モード****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.OperationMode_Setting));
            uint modeSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.OperationMode_Status));
            uint modeStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (modeSet != modeStt) //設定!=状態の場合には更新処理
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = modeSet;

            vrf.IndoorUnitModes[j] =
              !isIUonSet ? ExVRFSystem.Mode.ShutOff :
              modeSet == 1 ? ExVRFSystem.Mode.Cooling :
              modeSet == 2 ? ExVRFSystem.Mode.Heating : ExVRFSystem.Mode.ThermoOff;

            //室内温度設定***************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.Setpoint_Setting));
            double tSpSet = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.Setpoint_Status));
            double tSpStt = ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (tSpSet != tSpStt) //設定!=状態の場合には更新処理
            {
              vrf.SetSetpoint(tSpSet, j, true);
              vrf.SetSetpoint(tSpSet, j, false);
            }

            //ファン風量*****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.FanSpeed_Setting));
            uint fanSpdSet = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.FanSpeed_Status));
            uint fanSpdStt = ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (fanSpdSet != fanSpdStt)
              ((MultiStateInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = fanSpdSet;

            double fRate =
              fanSpdSet == 1 ? 0.3 :
              fanSpdSet == 2 ? 1.0 : 0.7; //Low, High, Middleの係数は適当
            vrf.VRFSystem.SetIndoorUnitAirFlowRate(j, vrf.VRFSystem.IndoorUnits[j].NominalAirFlowRate * fRate);

            //風向***********************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.AirflowDirection_Setting));
            uint afDirSet = ((AnalogValue<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.AirflowDirection_Status));
            uint afDirStt = ((AnalogInput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            if (afDirSet != afDirStt) //設定!=状態の場合には更新処理
              ((AnalogInput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = afDirSet;
            vrf.Direction[j] = (Math.PI / 180d) * (1 + afDirSet) * 22.5;

            //リモコン手元操作許可禁止*****
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Setting));
            bool rmtPmtSPSet = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Status));
            bool rmtPmtSPStt = BACnetCommunicator.ConvertToBool(((BinaryValue)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
            if(rmtPmtSPSet != rmtPmtSPStt)
              vrf.PermitSPControl[j] = rmtPmtSPSet;

            iuNum++;
          }

          bBase = 1000 * (i + 1);

          //蒸発温度・凝縮温度強制設定***
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Setting));
          bool fcRefSet = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Status));
          bool fcRefStt = BACnetCommunicator.ConvertToBool(((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
          if (fcRefSet != fcRefStt) //設定!=状態の場合には更新処理
            ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = fcRefSet ? 1u : 0u;

          //蒸発温度設定***************
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Setting));
          double tEvpSet = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status));
          double tEvpStt = ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
          if (tEvpSet != tEvpStt) //設定!=状態の場合には更新処理
            ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = tEvpSet;

          //凝縮温度設定***************
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Setting));
          double tCndSet = ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Status));
          double tCndStt = ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
          if (tCndSet != tCndStt) //設定!=状態の場合には更新処理
            ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = tCndSet;

          //蒸発・凝縮温度反映
          vrfSystems[i].VRFSystem.MaxEvaporatingTemperature
            = vrfSystems[i].VRFSystem.MinEvaporatingTemperature
            = fcRefSet ? tEvpSet : VRFSystem.NOMINAL_EVPORATING_TEMPERATURE;

          vrfSystems[i].VRFSystem.MaxCondensingTemperature
            = vrfSystems[i].VRFSystem.MinCondensingTemperature
            = fcRefSet ? tCndSet : VRFSystem.NOMINAL_CONDENSING_TEMPERATURE;
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
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          ExVRFSystem vrf = vrfSystems[vrfUnitIndices[i].OUnitIndex];
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            int bBase = 1000 * (i + 1) + 100 * (j + 1);
            BacnetObjectId boID;

            //室内温度設定***************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.Setpoint_Setting));
            ((AnalogValue<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE =
              vrf.VRFSystem.CurrentMode == Popolo.HVAC.MultiplePackagedHeatPump.VRFSystem.Mode.Heating ? vrf.GetSetpoint(j, false) : vrf.GetSetpoint(j, true);

            //吸い込み室温****************
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.MeasuredRoomTemperature));
            ((AnalogInput<double>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = vrf.VRFSystem.IndoorUnits[j].InletAirTemperature;

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
