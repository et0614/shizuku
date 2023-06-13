using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2
{
  internal class DateTimeController : IBACnetController
  {

    #region 定数宣言

    const uint DEVICE_ID = 1;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "Date and time controller";

    const string DEVICE_DESCRIPTION = "Date and time controller";

    #endregion

    #region インスタンス変数・プロパティ

    private BACnetCommunicator communicator;

    public DateTimeAccelerator dtAccelerator;

    /// <summary>タイムステップ[sec]</summary>
    private double timeStep = 1;

    /// <summary>加速度を取得する</summary>
    public uint AccelerationRate
    {
      get { return dtAccelerator.AccelerationRate; }
      private set
      { dtAccelerator.AccelerationRate = value; }
    }

    /// <summary>現在の日時</summary>
    private DateTime cDTime;

    /// <summary>現在の日時を取得する</summary>
    public DateTime CurrentDateTime
    {
      get { return cDTime; }
      set { cDTime = dtAccelerator.AcceleratedDateTime = value; }
    }

    /// <summary>タイムステップ[sec]を設定・取得する</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set { timeStep = Math.Max(1, Math.Min(3600, value)); }
    }

    #endregion

    #region コンストラクタ

    public DateTimeController(DateTime currentDTime, uint accRate)
    {
      dtAccelerator = new DateTimeAccelerator(accRate, currentDTime);
      CurrentDateTime = currentDTime;

      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      //シミュレーション内の現在日時
      BacnetDateTime dTime1 = new BacnetDateTime(0, "Current date and time on the simulation", "Current date and time on the simulation. This value might been accelerated.");
      dTime1.m_PresentValue = CurrentDateTime;
      dObject.AddBacnetObject(dTime1);

      //加速度
      dObject.AddBacnetObject(new AnalogOutput<uint>
        (0,
        "Acceraration rate",
        "This object is used to set the acceleration rate to run the emulator.", AccelerationRate, BacnetUnitsId.UNITS_NO_UNITS));

      //加速の基準となる現実の日時
      BacnetDateTime dTime2 = new BacnetDateTime(1, "Base real date and time", "Real world date and time starting to accelerate.");
      dTime2.m_PresentValue = dtAccelerator.BaseRealDateTime;
      dObject.AddBacnetObject(dTime2);

      //加速の基準となるシミュレーション内の日時
      BacnetDateTime dTime3 = new BacnetDateTime(2, "Base date and time in the simulation", "Date and time on the simulation when the acceleration started");
      dTime3.m_PresentValue = dtAccelerator.BaseAcceleratedDateTime;
      dObject.AddBacnetObject(dTime3);

      return dObject;
    }

    #endregion

    /// <summary>加速度を考慮しながら計算時刻を進める</summary>
    /// <returns>計算を進めるべきであればTrue</returns>
    public bool TryProceed(out bool isDelayed)
    {
      isDelayed = false;
      DateTime dt = dtAccelerator.AcceleratedDateTime;
      if (cDTime.AddSeconds(TimeStep) <= dt)
      {
        isDelayed = (cDTime.AddSeconds(2 * TimeStep) <= dt);
        cDTime = cDTime.AddSeconds(TimeStep);
        return true;
      }
      else return false;
    }

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      BacnetObjectId boID;

      //加速度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0);
      AccelerationRate = ((AnalogOutput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
    }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      BacnetObjectId boID;

      //現在の日時
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, 0);
      ((BacnetDateTime)communicator.BACnetDevice.FindBacnetObject(boID)).m_PresentValue = CurrentDateTime;
    }

    public void StartService()
    {
      communicator.StartService();
    }

    #endregion

  }
}
