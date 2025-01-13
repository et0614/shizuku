using BaCSharp;
using System.IO.BACnet;
using System.IO.BACnet.Storage;
using System.Reflection;
using static Popolo.ThermophysicalProperty.Refrigerant;

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

    /// <summary>計算遅延中か否か</summary>
    private bool isDelayed = false;

    /// <summary>タイムステップ[sec]</summary>
    private double timeStep = 1;

    /// <summary>加速度を取得する</summary>
    public int AccelerationRate
    {      
      get { return dtAccelerator.AccelerationRate; }
    }

    /// <summary>現在の日時</summary>
    private DateTime cDTime;

    /// <summary>現在の日時を取得する</summary>
    /// <remarks>この値は遅延している可能性がある。</remarks>
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

    /// <summary>シミュレーションを終了する日時を取得する</summary>
    public DateTime TerminateDateTime { get; private set; }

    /// <summary>計算遅延中か否か</summary>
    public bool IsDelayed
    {
      get { return IsFinished ? false : isDelayed; }
    }

    /// <summary>計算が終了済か否か</summary>
    public bool IsFinished { get; set; }

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

    #region コンストラクタ

    public DateTimeController(DateTime currentDTime, DateTime terminateDateTime, int accRate, string localEndpointIP)
    {
      dtAccelerator = new DateTimeAccelerator(accRate, currentDTime);
      cDTime = currentDTime;
      TerminateDateTime = terminateDateTime;

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
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.AccelerationRate),
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

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.EndDateTime),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, TerminateDateTime)
      );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsDelayed),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, 0u)
        );

      strg.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsFinished),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, 0u)
        );

      strg.ChangeOfValue += Strg_ChangeOfValue;
      Communicator = new BACnetCommunicator(strg, EXCLUSIVE_PORT, localEndpointIP);
    }

    private void Strg_ChangeOfValue(DeviceStorage sender, BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, IList<BacnetValue> value)
    {
      //加速度変化時には関連情報もまとめて更新
      if (
        objectId.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        objectId.instance == (uint)MemberNumber.AccelerationRate &&
        propertyId == BacnetPropertyIds.PROP_PRESENT_VALUE)
      {
        dtAccelerator.AccelerationRate = (int)(float)value[0].Value;

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
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>加速度を考慮しながら計算時刻を進める</summary>
    /// <returns>計算を進めるべきであればTrue</returns>
    public bool TryProceed()
    {
      DateTime dt = dtAccelerator.AcceleratedDateTime;
      if (cDTime.AddSeconds(TimeStep) <= dt)
      {
        isDelayed = cDTime.AddSeconds(2 * TimeStep) <= dt;
        cDTime = cDTime.AddSeconds(TimeStep);
        return true;
      }
      else isDelayed = false;
      return false;
    }

    /// <summary>計算開始日を初期化する</summary>
    /// <param name="dateTime">計算開始日</param>
    public void ResetDateTime(DateTime dateTime)
    {
      dtAccelerator.InitDateTime(dtAccelerator.AccelerationRate, DateTime.Now, dateTime);

      //加速度
      Communicator.Storage.WriteProperty(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.AccelerationRate),
          BacnetPropertyIds.PROP_PRESENT_VALUE,
          new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)dtAccelerator.AccelerationRate)
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

    /// <summary>加速度[-]を設定する</summary>
    /// <param name="accelerationRate">加速度[-]</param>
    /// <remarks>基準日時などの関連情報も含めて即座にBACnet Deviceの値が書き換えられる</remarks>
    public void SetAccelerationRate(int accelerationRate)
    {
      //最初に加速度を書き換えることで、関連情報の値を変える
      dtAccelerator.AccelerationRate = accelerationRate;

      //この値を書き換えるとCOVによって関連情報もBACnet Deviceに反映される
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (int)MemberNumber.AccelerationRate),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)accelerationRate));
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      //現在のシミュレーション内の日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (int)MemberNumber.CurrentDateTimeInSimulation),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, CurrentDateTime)
        );

      //計算遅延
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsDelayed),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, IsDelayed ? 1u : 0u)
        );

      //計算終了
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsFinished),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, IsFinished ? 1u : 0u)
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
