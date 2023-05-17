using BaCSharp;
using Popolo.HVAC.MultiplePackagedHeatPump;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Shizuku2
{
  internal class DateTimeController : IBACnetController
  {

    #region 定数宣言

    const uint DEVICE_ID = 1;

    const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "Date and time controller";

    const string DEVICE_DESCRIPTION = "Date and time controller";

    #endregion

    #region インスタンス変数・プロパティ

    private BACnetCommunicator communicator;

    /// <summary>加速度</summary>
    private uint accRate = 60;

    /// <summary>タイムステップ[sec]</summary>
    private double timeStep = 1;

    /// <summary>基準となる現実の日時</summary>
    private DateTime baseRealDTime;

    /// <summary>基準となるシミュレーション上の日時</summary>
    private DateTime baseSimulationDTime;

    /// <summary>加速度を設定・取得する</summary>
    public uint AccerarationRate
    {
      get { return accRate; }
      private set
      {
        if(accRate != value && 1 <= value)
        {
          accRate = value;
          baseRealDTime = DateTime.Now;
          baseSimulationDTime = CurrentDateTime;
        }
      }
    }

    /// <summary>現在の日時を取得する</summary>
    public DateTime CurrentDateTime { get; set; }

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
      CurrentDateTime = baseSimulationDTime = currentDTime;
      baseRealDTime = DateTime.Now;
      this.accRate = 1 <= accRate ? accRate : 1;

      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      //日時
      BacnetDateTime dTime = new BacnetDateTime(0, "Current date and time", "Current date and time");
      dObject.AddBacnetObject(dTime);

      //加速度
      dObject.AddBacnetObject(new AnalogOutput<uint>
        (0,
        "Acceraration rate",
        "This object is used to set the acceleration rate to run the emulator.", AccerarationRate, BacnetUnitsId.UNITS_NO_UNITS));

      return dObject;
    }

    #endregion

    /// <summary>加速度を考慮しながら計算時刻を進める</summary>
    /// <returns>計算を進めるべきであればTrue</returns>
    public bool TryProceed()
    {
      //本来のシミュレーション計算時刻
      DateTime essDTime = baseSimulationDTime.AddSeconds
        ((DateTime.Now - baseRealDTime).TotalSeconds * AccerarationRate);

      if (CurrentDateTime.AddSeconds(TimeStep) <= essDTime)
      {
        CurrentDateTime = CurrentDateTime.AddSeconds(TimeStep);
        return true;
      }
      else return false;
    }

    #region IBACnetController実装

    public void ApplyManipulatedVariables()
    {
      BacnetObjectId boID;

      //加速度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0);
      AccerarationRate = ((AnalogOutput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
    }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues()
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
