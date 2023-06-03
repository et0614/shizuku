using BaCSharp;
using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shizuku2;
using System.IO.BACnet.Base;

namespace Shizuku2.Daikin
{
  public class VRFScheduller : IVRFScheduller
  {

    #region 定数宣言

    const uint TARGET_DEVICE_ID = 2; //DaikinController

    const uint THIS_DEVICE_ID = 3;

    public const int TARGET_EXCLUSIVE_PORT = 0xBAC0 + (int)TARGET_DEVICE_ID;

    public const int THIS_EXCLUSIVE_PORT = 0xBAC0 + (int)THIS_DEVICE_ID;

    const string DEVICE_NAME = "VRF scheduller";

    const string DEVICE_DESCRIPTION = "VRF scheduller";

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

    private DateTimeAccelerator dtAccl;

    /// <summary>現在の日時を取得する</summary>
    public DateTime CurrentDateTime 
    { get { return dtAccl.AcceleratedDateTime; } }

    /// <summary>加速度を取得する</summary>
    public uint AccelerationRate 
    { get { return dtAccl.AccelerationRate; } }

    private BACnetCommunicator communicator;

    /// <summary>室内機の台数を取得する</summary>
    public int NumberOfIndoorUnits { get; private set; }

    /// <summary>スケジュールを有効にする</summary>
    public bool EnableScheduling { get; set; } = false;

    private readonly ExVRFSystem[] vrfSystems;

    BacnetAddress targetBACAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + TARGET_EXCLUSIVE_PORT.ToString());

    #endregion

    #region コンストラクタ

    public VRFScheduller(ExVRFSystem[] vrfs, uint accRate, DateTime now)
    {
      vrfSystems = vrfs;
      dtAccl = new DateTimeAccelerator(accRate, now);

      NumberOfIndoorUnits = 0;
      for (int i = 0; i < vrfs.Length; i++)
        NumberOfIndoorUnits += vrfs[i].VRFSystem.IndoorUnitNumber;

      //DMS502B71が扱える台数は256台まで
      if (256 <= NumberOfIndoorUnits)
        throw new Exception("Invalid indoor unit number");

      DeviceObject dObject = new DeviceObject(THIS_DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);
      communicator = new BACnetCommunicator(dObject, THIS_EXCLUSIVE_PORT);

      try
      {
        //別スレッドでスケジュール設定
        Task.Run(() =>
        {
          DateTime lastDt = dtAccl.AcceleratedDateTime;
          while (true)
          {
            //空調開始時
            if (!isHVACTime(lastDt) && isHVACTime(dtAccl.AcceleratedDateTime))
            {
              BacnetObjectId boID;
              List<BacnetValue> values;
              for (int iuNum = 0; iuNum < NumberOfIndoorUnits; iuNum++)
              {
                //On/Off
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
                  (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.OnOff_Setting));
                values = new List<BacnetValue>
                {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE)
                };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //Mode
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
                  (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuNum, MemberNumber.OperationMode_Setting));
                values = new List<BacnetValue>
                {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 2u) //1:冷房, 2:暖房, 3:換気
                };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //SP
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
                  (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.Setpoint));
                values = new List<BacnetValue>
                {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, 22d)
                };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
              }
            }
            //空調停止時
            else if (isHVACTime(lastDt) && !isHVACTime(dtAccl.AcceleratedDateTime))
            {
              BacnetObjectId boID;
              List<BacnetValue> values;
              for (int iuNum = 0; iuNum < NumberOfIndoorUnits; iuNum++)
              {
                //On/Off
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
                (uint)getInstanceNumber(ObjectNumber.BinaryOutput, iuNum, MemberNumber.OnOff_Setting));
                values = new List<BacnetValue>
                {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE)
                };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
              }
            }

            lastDt = dtAccl.AcceleratedDateTime;
            Thread.Sleep(100);
          }
        });
      }
      catch (Exception e)
      {

      }
    }

    private bool isWeekday(DateTime dTime)
    {
      return dTime.DayOfWeek != DayOfWeek.Saturday && dTime.DayOfWeek != DayOfWeek.Sunday;
    }

    private bool isHVACTime(DateTime dTime)
    {
      return isWeekday(dTime) && (7 <= dTime.Hour && dTime.Hour <= 19);
    }

    private int getInstanceNumber
      (ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //DBACSではこの番号で管理しているようだが、これでは桁が大きすぎる。
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; 
      return iUnitNumber * 256 + (int)memNumber;
    }

    #endregion

    /// <summary>COV通告を受けた場合の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="invokeId"></param>
    /// <param name="subscriberProcessIdentifier"></param>
    /// <param name="initiatingDeviceIdentifier"></param>
    /// <param name="monitoredObjectIdentifier"></param>
    /// <param name="timeRemaining"></param>
    /// <param name="needConfirm"></param>
    /// <param name="values"></param>
    /// <param name="maxSegments"></param>
    private void Client_OnCOVNotification
      (BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //加速度が変化した場合
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == DateTimeController.EXCLUSIVE_PORT && 
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT && 
        monitoredObjectIdentifier.instance == 0)
      {
        //この処理は汚いが・・・
        foreach (BacnetPropertyValue value in values)
        {
          if(value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            dtAccl.AccelerationRate = (uint)value.value[0].Value;
            break;
          }
        }

        //現在の日時を更新
        updateCurrentDateTime();
      }
    }

    #region 日時同期用の補助関数

    /// <summary>日時を同期させる</summary>
    public void Synchronize()
    {
      updateAccelerationRate();
      updateCurrentDateTime();
    }

    /// <summary>加速度を更新する</summary>
    private void updateAccelerationRate()
    {
      BacnetAddress adr = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString());
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0);
      if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
        dtAccl.AccelerationRate = (uint)val[0].Value;
    }

    /// <summary>日時を更新する</summary>
    private void updateCurrentDateTime()
    {
      BacnetAddress adr = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString());
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, 0);
      if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        DateTime dt1 = (DateTime)val[0].Value;
        DateTime dt2 = (DateTime)val[1].Value;
        dtAccl.AcceleratedDateTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);
      }
    }

    #endregion

    #region IBACnetController実装

    /// <summary>制御値を機器やセンサに反映する</summary>
    public void ApplyManipulatedVariables(DateTime dTime)
    {

    }

    /// <summary>機器やセンサの検出値を取得する</summary>
    public void ReadMeasuredValues(DateTime dTime)
    {
      
    }

    /// <summary>BACnetControllerのサービスを開始する</summary>
    public void StartService()
    {
      communicator.StartService();

      //COV通告登録処理
      communicator.Client.OnCOVNotification += Client_OnCOVNotification;
      BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString());
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0);
      communicator.Client.SubscribeCOVRequest(bacAddress, boID, 0, false, false, 3600);
    }

    /// <summary>BACnetControllerのリソースを解放する</summary>
    public void EndService()
    {
      communicator.EndService();
    }

    #endregion

  }
}
