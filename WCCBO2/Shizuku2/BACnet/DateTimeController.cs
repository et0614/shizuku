using BaCSharp;
using System.IO.BACnet;
using System.IO.BACnet.Storage;
using System.Reflection;

namespace Shizuku2.BACnet
{
  internal class DateTimeController : IBACnetController
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 1;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private DateTimeAccelerator dtAccelerator;

    /// <summary>タイムステップ[sec]</summary>
    private double timeStep = 1;

    /// <summary>加速度を取得する</summary>
    public int AccelerationRate
    {
      set
      {
        dtAccelerator.AccelerationRate = value;
        Communicator.Storage.WriteProperty(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.Acceleration),
          BacnetPropertyIds.PROP_PRESENT_VALUE,
          new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)value)
        );
      }
      get { return dtAccelerator.AccelerationRate; }
    }

    /// <summary>現在の日時</summary>
    private DateTime cDTime;

    /// <summary>現在の日時を取得する</summary>
    public DateTime CurrentDateTime
    {
      get { return cDTime; }
    }

    /// <summary>タイムステップ[sec]を設定・取得する</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set { timeStep = Math.Max(1, Math.Min(3600, value)); }
    }

    #endregion

    #region 列挙型

    public enum MemberNumber
    {
      CurrentDateTimeInSimulation = 1,
      Acceleration = 2,
      BaseRealDateTime = 3,
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    #region コンストラクタ

    public DateTimeController(DateTime currentDTime, int accRate, string localEndpointIP)
    {
      dtAccelerator = new DateTimeAccelerator(accRate, currentDTime);
      cDTime = currentDTime;

      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.DateTimeControllerStorage.xml"))
        );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.CurrentDateTimeInSimulation), 
        BacnetPropertyIds.PROP_PRESENT_VALUE, 
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, CurrentDateTime)
        );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.Acceleration),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)AccelerationRate)
        );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.BaseRealDateTime),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, dtAccelerator.BaseRealDateTime)
        );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.BaseAcceleratedDateTime),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, dtAccelerator.BaseAcceleratedDateTime)
        );

      Communicator = new BACnetCommunicator(strg, EXCLUSIVE_PORT, localEndpointIP);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>加速度を考慮しながら計算時刻を進める</summary>
    /// <returns>計算を進めるべきであればTrue</returns>
    public bool TryProceed(out bool isDelayed)
    {
      isDelayed = false;
      DateTime dt = dtAccelerator.AcceleratedDateTime;
      if (cDTime.AddSeconds(TimeStep) <= dt)
      {
        isDelayed = cDTime.AddSeconds(2 * TimeStep) <= dt;
        cDTime = cDTime.AddSeconds(TimeStep);
        return true;
      }
      else return false;
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      //加速度
      AccelerationRate = (int)(float)Communicator.Storage.ReadPresentValue(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.Acceleration));
    }


    public void ReadMeasuredValues(DateTime dTime)
    {
      //現在の日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.CurrentDateTimeInSimulation),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, CurrentDateTime)
        );

      //加速が開始された現実の日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.BaseRealDateTime),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, dtAccelerator.BaseRealDateTime)
        );

      //加速された日時における加速開始日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.BaseAcceleratedDateTime),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, dtAccelerator.BaseAcceleratedDateTime)
        );
    }

    public void StartService()
    {
      Communicator.StartService();
    }
    public void EndService()
    {
      Communicator.EndService();
    }

    #endregion

  }
}
