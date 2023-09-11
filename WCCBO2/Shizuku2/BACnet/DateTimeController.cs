using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2.BACnet
{
  internal class DateTimeController : IBACnetController
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 1;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "Date and time controller";

    const string DEVICE_DESCRIPTION = "Date and time controller";

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
        BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccerarationRate);
        ((AnalogOutput<int>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = value;
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
      AccerarationRate = 2,
      BaseRealDateTime = 3,
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    #region コンストラクタ

    public DateTimeController(DateTime currentDTime, int accRate)
    {
      dtAccelerator = new DateTimeAccelerator(accRate, currentDTime);
      cDTime = currentDTime;

      Communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      //シミュレーション内の現在日時（タイムステップで離散化された値）
      BacnetDateTime dTime1 = new BacnetDateTime(
        (int)MemberNumber.CurrentDateTimeInSimulation,
        "Current date and time on the simulation",
        "Current date and time on the simulation. This value might been accelerated.");
      dTime1.m_PresentValue = CurrentDateTime;
      dObject.AddBacnetObject(dTime1);

      //加速度
      dObject.AddBacnetObject(new AnalogOutput<int>
        ((int)MemberNumber.AccerarationRate,
        "Acceraration rate",
        "This object is used to set the acceleration rate to run the emulator.", AccelerationRate, BacnetUnitsId.UNITS_NO_UNITS)
      { m_PROP_LOW_LIMIT = 0 });

      //加速の基準となる現実の日時
      BacnetDateTime dTime2 = new BacnetDateTime(
        (int)MemberNumber.BaseRealDateTime,
        "Base real date and time",
        "Real world date and time starting to accelerate.");
      dTime2.m_PresentValue = dtAccelerator.BaseRealDateTime;
      dObject.AddBacnetObject(dTime2);

      //加速の基準となるシミュレーション内の日時
      BacnetDateTime dTime3 = new BacnetDateTime(
        (int)MemberNumber.BaseAcceleratedDateTime,
        "Base date and time in the simulation",
        "Date and time on the simulation when the acceleration started");
      dTime3.m_PresentValue = dtAccelerator.BaseAcceleratedDateTime;
      dObject.AddBacnetObject(dTime3);

      return dObject;
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
    public void OutputBACnetObjectInfo
      (out uint[] instances, out string[] types, out string[] names, out string[] descriptions, out string[] values)
    {
      List<string> tLst = new List<string>();
      List<uint> iLst = new List<uint>();
      List<string> nLst = new List<string>();
      List<string> dLst = new List<string>();
      List<string> vLst = new List<string>();
      foreach (BaCSharpObject bObj in Communicator.BACnetDevice.ObjectsList)
      {
        tLst.Add(bObj.PROP_OBJECT_IDENTIFIER.type.ToString().Substring(7));
        iLst.Add(bObj.PROP_OBJECT_IDENTIFIER.instance);
        nLst.Add(bObj.PROP_OBJECT_NAME);
        dLst.Add(bObj.PROP_DESCRIPTION);
        IList<BacnetValue> bVal = bObj.FindPropValue("PROP_PRESENT_VALUE");
        if (bVal != null) vLst.Add(bVal[0].Value.ToString());
        else vLst.Add(null);
      }
      types = tLst.ToArray();
      instances = iLst.ToArray();
      names = nLst.ToArray();
      descriptions = dLst.ToArray();
      values = vLst.ToArray();
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      BacnetObjectId boID;

      //加速度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccerarationRate);
      AccelerationRate = ((AnalogOutput<int>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
    }

    public void EndService()
    {
      Communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      BacnetObjectId boID;

      //現在の日時
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.CurrentDateTimeInSimulation);
      ((BacnetDateTime)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PresentValue = CurrentDateTime;

      //加速が開始された現実の日時
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseRealDateTime);
      ((BacnetDateTime)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PresentValue = dtAccelerator.BaseRealDateTime;

      //加速された日時における加速開始日時
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseAcceleratedDateTime);
      ((BacnetDateTime)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PresentValue = dtAccelerator.BaseAcceleratedDateTime;
    }

    public void StartService()
    {
      Communicator.StartService();
    }

    #endregion

  }
}
