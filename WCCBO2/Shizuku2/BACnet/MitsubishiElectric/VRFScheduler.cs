﻿using BaCSharp;
using System.IO.BACnet;
using System.IO.BACnet.Base;

namespace Shizuku2.BACnet.MitsubishiElectric
{
  public class VRFScheduler : IBACnetController
  {

    #region 定数宣言

    const uint TARGET_DEVICE_ID = 2; //MitsubishiElectric.Controller

    const uint THIS_DEVICE_ID = 3;

    public const int TARGET_EXCLUSIVE_PORT = 0xBAC0 + (int)TARGET_DEVICE_ID;

    public const int THIS_EXCLUSIVE_PORT = 0xBAC0 + (int)THIS_DEVICE_ID;

    const string DEVICE_NAME = "Mitsubishi Electric VRF scheduler";

    const string DEVICE_DESCRIPTION = "Mitsubishi Electric VRF scheduler";

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    private enum MemberNumber
    {
      /// <summary>現在のシミュレーション上の日時</summary>
      CurrentDateTimeInSimulation = 1,
      /// <summary>加速度</summary>
      AccelerationRate = 2,
      /// <summary>現実時間の基準日時</summary>
      BaseRealDateTime = 3,
      /// <summary>シミュレーション上の基準日時</summary>
      BaseAcceleratedDateTime = 4,
      /// <summary>シミュレーション上の終了日時</summary>
      EndDateTime = 5,
      /// <summary>計算遅延中か否か</summary>
      IsDelayed = 6,
      /// <summary>計算完了済か否か</summary>
      IsFinished = 7
    }

    #endregion

    #region インスタンス変数・プロパティ

    private DateTimeAccelerator dtAccl;

    /// <summary>現在の日時を取得する</summary>
    public DateTime CurrentDateTime
    { get { return dtAccl.AcceleratedDateTime; } }

    /// <summary>加速度を取得する</summary>
    public int AccelerationRate
    { get { return dtAccl.AccelerationRate; } }

    private BACnetCommunicator2 communicator;

    /// <summary>室内機の台数を取得する</summary>
    public int NumberOfIndoorUnits { get; private set; }

    /// <summary>スケジュールを有効にする</summary>
    public bool EnableScheduling { get; set; } = false;

    BacnetAddress targetBACAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + TARGET_EXCLUSIVE_PORT.ToString());

    #endregion

    #region コンストラクタ

    public VRFScheduler(ExVRFSystem[] vrfs, int accRate, DateTime now)
    {
      dtAccl = new DateTimeAccelerator(accRate, now);

      NumberOfIndoorUnits = 0;
      for (int i = 0; i < vrfs.Length; i++)
        NumberOfIndoorUnits += vrfs[i].VRFSystem.IndoorUnitNumber;

      //AE-200Jが扱える台数は50台まで
      if (50 <= NumberOfIndoorUnits)
        throw new Exception("Invalid indoor unit number");

      DeviceObject dObject = new DeviceObject(THIS_DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);
      communicator = new BACnetCommunicator2(dObject, THIS_EXCLUSIVE_PORT);

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
              for (int grpNum = 0; grpNum < NumberOfIndoorUnits; grpNum++)
              {
                //On/Off
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(10000 + grpNum * 100 + 1));
                values = new List<BacnetValue>
                        {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE)
                        };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //Mode
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 5));
                bool isCooling = 5 <= dtAccl.AcceleratedDateTime.Month && dtAccl.AcceleratedDateTime.Month <= 10;
                values = new List<BacnetValue>
                        {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, isCooling ? 1u : 2u) //1:冷房, 2:暖房, 3:換気
                        };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //SP
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(10000 + grpNum * 100 + (isCooling ? 24 : 25)));
                values = new List<BacnetValue>
                        {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, isCooling ? 26f : 25f)
                        };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //角度
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 22));
                values = new List<BacnetValue>
                        {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 4u) //1水平,2下向き60%,3下向き80%,4下向き100%,5スイング
                        };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                //風量
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(10000 + grpNum * 100 + 7));
                values = new List<BacnetValue>
                        {
                  new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 1u) //1弱,2強,3中2,4中1,5自動
                        };
                communicator.Client.WritePropertyRequest(targetBACAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
              }
            }
            //空調停止時
            else if (isHVACTime(lastDt) && !isHVACTime(dtAccl.AcceleratedDateTime))
            {
              BacnetObjectId boID;
              List<BacnetValue> values;
              for (int grpNum = 0; grpNum < NumberOfIndoorUnits; grpNum++)
              {
                //On/Off
                boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(10000 + grpNum * 100 + 1));
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
      return isWeekday(dTime) && 7 <= dTime.Hour && dTime.Hour <= 19;
    }

    #endregion

    #region 日時同期用の補助関数

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
      (BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier,
      BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier,
      uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //加速度が変化した場合      
      ushort port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == DateTimeController.EXCLUSIVE_PORT &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        monitoredObjectIdentifier.instance == (uint)MemberNumber.AccelerationRate)
      {
        //この処理は汚いが・・・
        foreach (BacnetPropertyValue value in values)
        {
          if (value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            int acc = (int)((float)value.value[0].Value);

            BacnetObjectId boID;
            //基準日時（加速時間）
            //adr = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_PORT.ToString());
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseAcceleratedDateTime);
            if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val1))
            {
              DateTime dt1 = (DateTime)val1[0].Value;
              DateTime dt2 = (DateTime)val1[1].Value;
              DateTime bAccDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

              //基準日時（現実時間）
              boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseRealDateTime);
              if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val2))
              {
                dt1 = (DateTime)val2[0].Value;
                dt2 = (DateTime)val2[1].Value;
                DateTime bRealDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                //初期化
                dtAccl.InitDateTime(acc, bRealDTime, bAccDTime);
              }
            }

            break;
          }
        }
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
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccelerationRate);
      communicator.Client.SubscribeCOVRequest(bacAddress, boID, (uint)MemberNumber.AccelerationRate, false, false, 3600);

      //Who is送信
      communicator.Client.WhoIs();
    }

    /// <summary>BACnetControllerのリソースを解放する</summary>
    public void EndService()
    {
      communicator.EndService();
    }

    #endregion

  }
}
