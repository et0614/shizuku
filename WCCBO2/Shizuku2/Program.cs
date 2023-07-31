using BaCSharp;

using Popolo.ThermalLoad;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Weather;
using System.Security.Cryptography;

using System.IO.BACnet;
using System.Collections.Generic;
using Shizuku.Models;
using Popolo.Numerics;
using PacketDotNet.Tcp;
using Popolo.HVAC.HeatExchanger;

namespace Shizuku2
{
  internal class Program
  {

    #region 定数宣言

    /// <summary>漏気量[回/h]</summary>
    private const double LEAK_RATE = 0.2;

    /// <summary>電力の一次エネルギー換算係数[GJ/kWh]</summary>
    private const double ELC_PRIM_RATE = 0.00976;

    /// <summary>バージョン（メジャー）</summary>
    private const int V_MAJOR = 0;

    /// <summary>バージョン（マイナー）</summary>
    private const int V_MINOR = 2;

    /// <summary>バージョン（リビジョン）</summary>
    private const int V_REVISION = 0;

    /// <summary>バージョン（日付）</summary>
    private const string V_DATE = "2023.06.04";

    #endregion

    #region クラス変数

    /// <summary>初期設定</summary>
    private static readonly Dictionary<string, int> initSettings = new Dictionary<string, int>();

    /// <summary>熱負荷計算モデル</summary>
    private static BuildingThermalModel building;

    /// <summary>VRFモデル</summary>
    private static ExVRFSystem[] vrfs;

    /// <summary>テナントリスト</summary>
    private static TenantList tenants;

    /// <summary>日時コントローラ</summary>
    private static DateTimeController dtCtrl;

    /// <summary>VRFコントローラ</summary>
    private static IBACnetController vrfCtrl;

    /// <summary>VRFスケジューラ</summary>
    private static IVRFScheduller? vrfSchedl;

    /// <summary>エネルギー消費量[MJ]</summary>
    private static double energyConsumption = 0.0;

    /// <summary>平均不満足率[-]</summary>
    private static double averageDissatisfactionRate = 0.0;

    /// <summary>計算が遅れているか否か</summary>
    private static bool isDelayed = false;

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      //タイトル表示
      showTitle();

      //初期設定ファイル読み込み
      if (!loadInitFile())
      {
        Console.WriteLine("Failed to load \"setting.ini\"");
        return;
      }

      //建物モデルを作成
      building = BuildingMaker.Make();
      vrfs = makeVRFSystem(building);

      //気象データを生成
      WeatherLoader wetLoader = new WeatherLoader((uint)initSettings["rseed3"],
        initSettings["weather"] == 1 ? RandomWeather.Location.Sapporo :
        initSettings["weather"] == 2 ? RandomWeather.Location.Sendai :
        initSettings["weather"] == 3 ? RandomWeather.Location.Tokyo :
        initSettings["weather"] == 4 ? RandomWeather.Location.Osaka :
        initSettings["weather"] == 5 ? RandomWeather.Location.Fukuoka :
        RandomWeather.Location.Naha);
      Sun sun =
        initSettings["weather"] == 1 ? new Sun(43.0621, 141.3544, 135) :
        initSettings["weather"] == 2 ? new Sun(38.2682, 140.8693, 135) :
        initSettings["weather"] == 3 ? new Sun(35.6894, 139.6917, 135) :
        initSettings["weather"] == 4 ? new Sun(34.6937, 135.5021, 135) :
        initSettings["weather"] == 5 ? new Sun(33.5903, 130.4017, 135) :
        new Sun(26.2123, 127.6791, 135);

      //テナントを生成//生成と行動で乱数シードを分ける
      tenants = new TenantList((uint)initSettings["rseed1"], building, vrfs);
      tenants.ResetRandomSeed((uint)initSettings["rseed2"]);
      //tenants.OutputOccupantsInfo("occupants.csv");

      //日時コントローラを用意して助走計算
      Console.Write("Start precalculation...");
      DateTime dt =
        initSettings["period"] == 0 ? new DateTime(1999, 7, 21, 0, 0, 0) : //夏季
        initSettings["period"] == 1 ? new DateTime(1999, 2, 10, 0, 0, 0) : //冬季
        new DateTime(1999, 4, 28, 0, 0, 0); //中間期
      dtCtrl = new DateTimeController(dt, 0); //加速度0で待機
      dtCtrl.TimeStep = building.TimeStep = initSettings["timestep"];
      //初期化・周期定常化処理
      preRun(dtCtrl.CurrentDateTime, sun, wetLoader);
      Console.WriteLine("Done." + Environment.NewLine);

      //VRFコントローラ用意
      switch (initSettings["controller"])
      {
        case 0:
          vrfCtrl = new Original.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new Original.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        case 1:
          vrfCtrl = new Daikin.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new Daikin.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        case 2:
          vrfCtrl = new MitsubishiElectric.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new MitsubishiElectric.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        default:
          throw new Exception("VRF controller number not supported.");
      }

      //VRF controller開始
      dtCtrl.StartService();
      vrfCtrl.StartService();

      //BACnet controllerの登録を待つ
      Console.WriteLine("Waiting for BACnet controller registration.");
      Console.WriteLine("Press \"Enter\" key to continue.");
      //Defaultコントローラ開始
      vrfSchedl?.StartService();
      vrfSchedl?.Synchronize();
      while (Console.ReadKey().Key != ConsoleKey.Enter) ;

      //コントローラが接続されたら加速開始:BACnetで送信してCOV eventを発生させる
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeController.MemberNumber.AccerarationRate);
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, initSettings["accelerationRate"]));
      dtCtrl.Communicator.Client.WritePropertyRequest(
        new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString()), 
        boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

      bool finished = false;
      try
      {
        //別スレッドで経過を表示
        Task.Run(() =>
        {
          while (!finished)
          {
            Console.WriteLine(
              dtCtrl.CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss") +
              "  " + energyConsumption.ToString("F4") +
              "  " + averageDissatisfactionRate.ToString("F4") +
              "  " + (isDelayed ? "DELAYED" : "")
              );
            Thread.Sleep(1000);
          }
        });

        //メイン処理
        run(wetLoader, sun);
        finished = true;
        //結果書き出し
        saveScore("result.szk", energyConsumption, averageDissatisfactionRate);

        Console.WriteLine("Emulation finished. Press any key to exit.");
        Console.ReadLine();
      }
      catch (Exception e)
      {
        finished = true;
        using (StreamWriter sWriter = new StreamWriter("error.log"))
        {
          sWriter.Write(e.ToString());
        }

        Console.WriteLine(e.ToString());
        Console.WriteLine("Emulation aborted. Press any key to exit.");
        Console.ReadLine();
      }
    }

    /// <summary>期間計算を実行する</summary>
    private static void run(WeatherLoader wetLoader, Sun sun)
    {
      DateTime endDTime = dtCtrl.CurrentDateTime.AddDays(7);
      DateTime nextOutput = dtCtrl.CurrentDateTime;
      uint ttlOcNum = 0;
      if (!Directory.Exists("data")) Directory.CreateDirectory("data");
      using (StreamWriter swGen = new StreamWriter("data" + Path.DirectorySeparatorChar + "general.csv"))
      using (StreamWriter swZone = new StreamWriter("data" + Path.DirectorySeparatorChar + "zone.csv"))
      using (StreamWriter swVRF = new StreamWriter("data" + Path.DirectorySeparatorChar + "vrf.csv"))
      using (StreamWriter swOcc = new StreamWriter("data" + Path.DirectorySeparatorChar + "occupant.csv"))
      {
        //タイトル行書き出し
        outputStatus(swGen, swZone, swVRF, swOcc, true);

        //加速度を考慮して計算を進める
        while (true)
        {
          //最低でも0.1秒ごとに計算実施判定
          Thread.Sleep(100);
          dtCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime); //加速度を監視

          while (dtCtrl.TryProceed(out isDelayed))
          {
            //1週間で計算終了
            if (endDTime < dtCtrl.CurrentDateTime) break;

            //コントローラの制御値を機器やセンサに反映
            vrfCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime);
            dtCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime);

            //気象データを建物モデルに反映
            sun.Update(dtCtrl.CurrentDateTime);
            wetLoader.GetWeather(dtCtrl.CurrentDateTime, out double dbt, out double hmd, ref sun);
            building.UpdateOutdoorCondition(dtCtrl.CurrentDateTime, sun, dbt, 0.001 * hmd, 0);

            //テナントを更新（内部発熱もここで更新される）
            tenants.Update(dtCtrl.CurrentDateTime);

            //VRF更新
            setVRFInletAir();
            for (int i = 0; i < vrfs.Length; i++)
            {
              //外気条件
              vrfs[i].VRFSystem.OutdoorAirDrybulbTemperature = dbt;
              vrfs[i].VRFSystem.OutdoorAirHumidityRatio = 0.001 * hmd;
              //制御と状態の更新
              vrfs[i].UpdateControl(building.CurrentDateTime);
              vrfs[i].VRFSystem.UpdateState(false);
            }
            setVRFOutletAir();

            //換気量を更新
            setVentilationRate();

            //熱環境更新
            building.ForecastHeatTransfer();
            building.ForecastWaterTransfer();
            building.FixState();

            //機器やセンサの検出値を取得
            vrfCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime);
            dtCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime);

            //成績を集計
            getScore(ref ttlOcNum, ref averageDissatisfactionRate, out energyConsumption);

            //書き出し
            if (nextOutput <= building.CurrentDateTime)
            {
              outputStatus(swGen, swZone, swVRF, swOcc, false);
              nextOutput = building.CurrentDateTime.AddSeconds(initSettings["outputSpan"]);
            }
          }

          //1週間で計算終了
          if (endDTime < dtCtrl.CurrentDateTime) break;
        }
      }
    }

    /// <summary>助走計算する</summary>
    /// <param name="dTime">日時</param>
    /// <param name="sun">太陽</param>
    /// <param name="wetLoader">気象データ</param>
    private static void preRun(DateTime dTime, Sun sun, WeatherLoader wetLoader)
    {
      double tStep = building.TimeStep;
      building.TimeStep = 3600;
      for(int i = 0; i < 10; i++)
      {
        DateTime dt = dTime;
        for (int j = 0; j < 24; j++)
        {
          //気象データを建物モデルに反映
          sun.Update(dt);
          wetLoader.GetWeather(dt, out double dbt, out double hmd, ref sun);
          building.UpdateOutdoorCondition(dt, sun, dbt, 0.001 * hmd, 0);
          //換気量を更新
          setVentilationRate();

          //熱環境更新
          building.ForecastHeatTransfer();
          building.ForecastWaterTransfer();
          building.FixState();

          dt = dt.AddHours(1);
        }
      }

      //気象データと時刻を初期化
      sun.Update(dTime);
      wetLoader.GetWeather(dTime, out double dbt2, out double hmd2, ref sun);
      building.UpdateOutdoorCondition(dTime, sun, dbt2, 0.001 * hmd2, 0);

      building.TimeStep = tStep;
    }

    /// <summary>スコアを計算する</summary>
    /// <param name="totalOccupants">延執務者数[人]</param>
    /// <param name="aveDisrate">平均不満足者率[-]</param>
    /// <param name="eConsumption">エネルギー消費量[GJ]</param>
    private static void getScore( 
      ref uint totalOccupants, ref double aveDisrate, out double eConsumption) 
    {
      tenants.GetDissatisfiedInfo(out uint noc, out double dis);
      uint tNum = noc + totalOccupants;
      aveDisrate = (tNum == 0) ? 0 : (aveDisrate * totalOccupants + dis * noc) / tNum;
      totalOccupants = tNum;

      eConsumption = 0;
      for(int i=0;i<vrfs.Length;i++)
        eConsumption += vrfs[i].ElectricityMeters.IntegratedValue * ELC_PRIM_RATE;
    }

    private static void outputStatus(
      StreamWriter swGen, StreamWriter swZone, StreamWriter swVRF, StreamWriter swOcc, bool isTitleLine)
    {
      //タイトル行
      if (isTitleLine)
      {
        //一般の情報
        swGen.Write("date,time");
        swGen.WriteLine(",Outdoor drybulb temperature[C],Outdoor humidity ratio[g/kg],Global horizontal radiation [W/m2]");

        //ゾーンの情報
        swZone.Write("date,time");
        for (int i = 0; i < building.MultiRoom.Length; i++)
        {
          int znNum = building.MultiRoom[i].ZoneNumber / 2; //上部下部空間それぞれ書き出す
          for (int j = 0; j < znNum; j++)
          {
            swZone.Write(
              "," + building.MultiRoom[i].Zones[j].Name + " drybulb temperature(High) [CDB]" +
              "," + building.MultiRoom[i].Zones[j + znNum].Name + " drybulb temperature(Low) [CDB]" +
              "," + building.MultiRoom[i].Zones[j].Name + " absolute humidity(High) [g/kg]" +
              "," + building.MultiRoom[i].Zones[j + znNum].Name + " absolute humidity(Low) [g/kg]"
              );
          }
        }
        swZone.WriteLine();

        //VRFの情報
        swVRF.Write("date,time");
        for (int i = 0; i < vrfs.Length; i++)
        {
          int oHex = i + 1;
          swVRF.Write(",VRF" + oHex + " electricity [kW]");
          for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
          {
            string name = ",VRF" + oHex + "-" + (j + 1);
            swVRF.Write(
              name + " Mode" +
              name + " Return temperature [C]" +
              name + " Return humidity [g/kg]" +
              name + " Supply temperature [C]" +
              name + " Supply humidity [g/kg]" +
              name + " Airflow rate [kg/s]" +
              name + " Setpoint temperature (cooling) [C]" +
              name + " Setpoint temperature (heating) [C]"
              );
          }
        }
        swVRF.WriteLine();

        //執務者情報
        swOcc.Write("date,time");
        //着衣量
        for (int i = 0; i < tenants.Tenants.Length; i++)
          for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
            swOcc.Write("," + tenants.Tenants[i].Occupants[j].FirstName + " " + tenants.Tenants[i].Occupants[j].LastName + " Clo value [clo]");
        //温冷感申告値
        for (int i = 0; i < tenants.Tenants.Length; i++)
          for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
            swOcc.Write("," + tenants.Tenants[i].Occupants[j].FirstName + " " + tenants.Tenants[i].Occupants[j].LastName + " Thermal sensation [-]");
        //上昇要求
        for (int i = 0; i < tenants.Tenants.Length; i++)
          for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
            swOcc.Write("," + tenants.Tenants[i].Occupants[j].FirstName + " " + tenants.Tenants[i].Occupants[j].LastName + " Raise request [-]");
        //下降要求
        for (int i = 0; i < tenants.Tenants.Length; i++)
          for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
            swOcc.Write("," + tenants.Tenants[i].Occupants[j].FirstName + " " + tenants.Tenants[i].Occupants[j].LastName + " Lower request [-]");
        swOcc.WriteLine();
      }

      //ここから実際の値
      string dtHeader = building.CurrentDateTime.ToString("MM/dd") + "," + building.CurrentDateTime.ToString("HH:mm:ss");

      //一般の情報
      swGen.Write(dtHeader);
      swGen.WriteLine(
        "," + building.OutdoorTemperature.ToString("F1") + 
        "," + (1000 * building.OutdoorHumidityRatio).ToString("F1") + 
        "," + building.Sun.GlobalHorizontalRadiation.ToString("F1"));

      //ゾーンの情報
      swZone.Write(dtHeader);
      for (int i = 0; i < building.MultiRoom.Length; i++)
      {
        int znNum = building.MultiRoom[i].ZoneNumber / 2; //上部下部空間それぞれ書き出す
        for (int j = 0; j < znNum; j++)
        {
          swZone.Write(
            "," + building.MultiRoom[i].Zones[j].Temperature.ToString("F1") +
            "," + building.MultiRoom[i].Zones[j + znNum].Temperature.ToString("F1") +
            "," + (1000 * building.MultiRoom[i].Zones[j].HumidityRatio).ToString("F1") +
            "," + (1000 * building.MultiRoom[i].Zones[j + znNum].HumidityRatio).ToString("F1")
            );
        }
      }
      swZone.WriteLine();

      //VRFの情報
      swVRF.Write(dtHeader);
      for (int i = 0; i < vrfs.Length; i++)
      {
        swVRF.Write("," + vrfs[i].ElectricityMeters.InstantaneousValue.ToString("F2"));
        for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
        {
          swVRF.Write(
            "," + vrfs[i].VRFSystem.IndoorUnits[j].CurrentMode.ToString() + 
            "," + vrfs[i].VRFSystem.IndoorUnits[j].InletAirTemperature.ToString("F1") +
            "," + (1000 * vrfs[i].VRFSystem.IndoorUnits[j].InletAirHumidityRatio).ToString("F1") +
            "," + vrfs[i].VRFSystem.IndoorUnits[j].OutletAirTemperature.ToString("F1") +
            "," + (1000 * vrfs[i].VRFSystem.IndoorUnits[j].OutletAirHumidityRatio).ToString("F1") +
            "," + vrfs[i].VRFSystem.IndoorUnits[j].AirFlowRate.ToString("F3") +
            "," + vrfs[i].GetSetpoint(j, true).ToString("F0") +
            "," + vrfs[i].GetSetpoint(j, false).ToString("F0")
            );
        }
      }
      swVRF.WriteLine();

      //執務者情報
      swOcc.Write(dtHeader);
      //着衣量
      for (int i = 0; i < tenants.Tenants.Length; i++)
        for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
          swOcc.Write("," + (tenants.Tenants[i].Occupants[j].Worker.StayInOffice ? 
            tenants.Tenants[i].Occupants[j].CloValue.ToString("F3") : ""));
      //温冷感申告値
      for (int i = 0; i < tenants.Tenants.Length; i++)
        for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
          swOcc.Write("," + (tenants.Tenants[i].Occupants[j].Worker.StayInOffice ?
            ((int)tenants.Tenants[i].Occupants[j].OCModel.Vote).ToString("F0") : ""));
      //上昇要求
      for (int i = 0; i < tenants.Tenants.Length; i++)
        for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
          swOcc.Write("," + (tenants.Tenants[i].Occupants[j].Worker.StayInOffice ?
            (tenants.Tenants[i].Occupants[j].TryToRaiseTemperatureSP ? "1" : "0" ) : ""));
      //下降要求
      for (int i = 0; i < tenants.Tenants.Length; i++)
        for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
          swOcc.Write("," + (tenants.Tenants[i].Occupants[j].Worker.StayInOffice ?
            (tenants.Tenants[i].Occupants[j].TryToLowerTemperatureSP ? "1" : "0") : ""));
      swOcc.WriteLine();
    }

    #endregion

    #region 換気設定

    private static void setVentilationRate()
    {
      //機械換気の真偽
      bool ventilate = !(
        dtCtrl.CurrentDateTime.DayOfWeek == DayOfWeek.Saturday |
        dtCtrl.CurrentDateTime.DayOfWeek == DayOfWeek.Sunday |
        dtCtrl.CurrentDateTime.Hour < 8 |
        20 < dtCtrl.CurrentDateTime.Hour);

      double vRateDwn = (ventilate ? 5 : LEAK_RATE * 1.7) / 3600d; //機械換気：5CMH/m2、漏気：天井高1.7m
      double vRateUp = (ventilate ? 5 : LEAK_RATE * 1.0) / 3600d; //機械換気：5CMH/m2、漏気：天井高1.0m
      for (int i = 0; i < 12; i++)
      {
        building.SetVentilationRate(0, i, building.MultiRoom[0].Zones[i].FloorArea * vRateDwn);
        building.SetVentilationRate(0, i + 12, building.MultiRoom[0].Zones[i + 12].FloorArea * vRateUp);
      }
      for (int i = 0; i < 14; i++)
      {
        building.SetVentilationRate(1, i, building.MultiRoom[1].Zones[i].FloorArea * vRateDwn);
        building.SetVentilationRate(1, i + 14, building.MultiRoom[1].Zones[i + 14].FloorArea * vRateDwn);
      }
    }

    #endregion

    #region VRFの制御

    /// <summary>下部空間と上部空間の風量比を計算して還空気状態を求める</summary>
    private static void setVRFInletAir()
    {
      for (int i = 0; i < 6; i++)
      {
        ImmutableZone znL = building.MultiRoom[0].Zones[i];
        ImmutableZone znU = building.MultiRoom[0].Zones[i + 12];
        vrfs[0].UpdateBlowRate(i, znL.Temperature, znU.Temperature);
        vrfs[0].VRFSystem.SetIndoorUnitInletAirState
          (i,
          znL.Temperature * vrfs[0].LowZoneBlowRate[i] + znU.Temperature * (1.0 - vrfs[0].LowZoneBlowRate[i]),
          znL.HumidityRatio * vrfs[0].LowZoneBlowRate[i] + znU.HumidityRatio * (1.0 - vrfs[0].LowZoneBlowRate[i])
          );
      }
      for (int i = 0; i < 6; i++)
      {
        ImmutableZone znL = building.MultiRoom[0].Zones[i + 6];
        ImmutableZone znU = building.MultiRoom[0].Zones[i + 18];
        vrfs[1].UpdateBlowRate(i, znL.Temperature, znU.Temperature);
        vrfs[1].VRFSystem.SetIndoorUnitInletAirState
          (i,
          znL.Temperature * vrfs[1].LowZoneBlowRate[i] + znU.Temperature * (1.0 - vrfs[1].LowZoneBlowRate[i]),
          znL.HumidityRatio * vrfs[1].LowZoneBlowRate[i] + znU.HumidityRatio * (1.0 - vrfs[1].LowZoneBlowRate[i])
          );
      }
      for (int i = 0; i < 6; i++)
      {
        ImmutableZone znL = building.MultiRoom[1].Zones[i];
        ImmutableZone znU = building.MultiRoom[1].Zones[i + 14];
        vrfs[2].UpdateBlowRate(i, znL.Temperature, znU.Temperature);
        vrfs[2].VRFSystem.SetIndoorUnitInletAirState
          (i,
          znL.Temperature * vrfs[2].LowZoneBlowRate[i] + znU.Temperature * (1.0 - vrfs[2].LowZoneBlowRate[i]),
          znL.HumidityRatio * vrfs[2].LowZoneBlowRate[i] + znU.HumidityRatio * (1.0 - vrfs[2].LowZoneBlowRate[i])
          );

        vrfs[2].VRFSystem.SetIndoorUnitInletAirState
          (i, building.MultiRoom[1].Zones[i].Temperature, building.MultiRoom[1].Zones[i].HumidityRatio);
      }
      for (int i = 0; i < 8; i++)
      {
        ImmutableZone znL = building.MultiRoom[1].Zones[i + 6];
        ImmutableZone znU = building.MultiRoom[1].Zones[i + 20];
        vrfs[3].UpdateBlowRate(i, znL.Temperature, znU.Temperature);
        vrfs[3].VRFSystem.SetIndoorUnitInletAirState
          (i,
          znL.Temperature * vrfs[3].LowZoneBlowRate[i] + znU.Temperature * (1.0 - vrfs[3].LowZoneBlowRate[i]),
          znL.HumidityRatio * vrfs[3].LowZoneBlowRate[i] + znU.HumidityRatio * (1.0 - vrfs[3].LowZoneBlowRate[i])
          );
      }
    }

    /// <summary>下部空間と上部空間へ給気する</summary>
    private static void setVRFOutletAir()
    {
      for (int i = 0; i < 6; i++)
      {
        ImmutableVRFUnit unt = vrfs[0].VRFSystem.IndoorUnits[i];
        building.SetSupplyAir(0, i, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * vrfs[0].LowZoneBlowRate[i]);
        building.SetSupplyAir(0, i + 12, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * (1.0 - vrfs[0].LowZoneBlowRate[i]));
      }
      for (int i = 0; i < 6; i++)
      {
        ImmutableVRFUnit unt = vrfs[1].VRFSystem.IndoorUnits[i];
        building.SetSupplyAir(0, i + 6, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * vrfs[1].LowZoneBlowRate[i]);
        building.SetSupplyAir(0, i + 18, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * (1.0 - vrfs[1].LowZoneBlowRate[i]));
      }
      for (int i = 0; i < 6; i++)
      {
        ImmutableVRFUnit unt = vrfs[2].VRFSystem.IndoorUnits[i];
        building.SetSupplyAir(1, i, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * vrfs[2].LowZoneBlowRate[i]);
        building.SetSupplyAir(1, i + 14, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * (1.0 - vrfs[2].LowZoneBlowRate[i]));
      }
      for (int i = 0; i < 8; i++)
      {
        ImmutableVRFUnit unt = vrfs[3].VRFSystem.IndoorUnits[i];
        building.SetSupplyAir(1, i + 6, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * vrfs[3].LowZoneBlowRate[i]);
        building.SetSupplyAir(1, i + 20, unt.OutletAirTemperature, unt.OutletAirHumidityRatio, unt.AirFlowRate * (1.0 - vrfs[3].LowZoneBlowRate[i]));
      }
    }

    #endregion

    #region 補助関数

    /// <summary>タイトル表示</summary>
    private static void showTitle()
    {
      Console.WriteLine("\r\n");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#                  Shizuku2  verstion " + V_MAJOR + "." + V_MINOR + "." + V_REVISION + " (" + V_DATE + ")                #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#     Thermal Emvironmental System Emulator to participate WCCBO2       #");
      Console.WriteLine("#  (The Second World Championship in Cybernetic Building Optimization)  #");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("\r\n");
    }

    private static bool loadInitFile()
    {
      //初期設定ファイル読み込み
      string sFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini";
      if (File.Exists(sFile))
      {
        using (StreamReader sReader = new StreamReader(sFile))
        {
          string line;
          while ((line = sReader.ReadLine()) != null && !line.StartsWith("#"))
          {
            line = line.Remove(line.IndexOf(';'));
            string[] st = line.Split('=');
            if (initSettings.ContainsKey(st[0])) 
              initSettings[st[0]] = int.Parse(st[1]);
            else 
              initSettings.Add(st[0], int.Parse(st[1]));
          }
        }
        return true;
      }
      else return false;
    }

    private static void saveScore
      (string fileName, double eConsumption, double aveDissatisfiedRate)
    {
      //32byteの秘密鍵を生成（固定）
      MersenneTwister rnd1 = new MersenneTwister(19800614);
      byte[] key = new byte[32];
      for (int i = 0; i < key.Length; i++)
        key[i] = (byte)Math.Ceiling(rnd1.NextDouble() * 256);

      //12byteのランダムなナンスを生成
      MersenneTwister rnd2 = new MersenneTwister((uint)DateTime.Now.Millisecond);
      byte[] nonce = new byte[12];
      for (int i = 0; i < nonce.Length; i++)
        nonce[i] = (byte)Math.Ceiling(rnd2.NextDouble() * 256);

      //ChaCha20Poly1305用インスタンスの生成
      ChaCha20Poly1305 cha2 = new ChaCha20Poly1305(key);

      //暗号化
      byte[] message = new byte[8 + 8 + 4 + 4 + 4 + 4];
      Array.Copy(BitConverter.GetBytes(eConsumption), 0, message, 0, 8); //エネルギー消費
      Array.Copy(BitConverter.GetBytes(aveDissatisfiedRate), 0, message, 8, 8); //平均不満足者率
      Array.Copy(BitConverter.GetBytes(initSettings["userid"]), 0, message, 16, 4); //ユーザーID
      Array.Copy(BitConverter.GetBytes(V_MAJOR), 0, message, 20, 4); //バージョン（メジャー）
      Array.Copy(BitConverter.GetBytes(V_MINOR), 0, message, 24, 4); //バージョン（マイナー）
      Array.Copy(BitConverter.GetBytes(V_REVISION), 0, message, 28, 4); //バージョン（リビジョン）

      byte[] cipherText = new byte[message.Length];
      byte[] tag = new byte[16];
      cha2.Encrypt(nonce, message, cipherText, tag);
      List<byte> oBytes = new List<byte>();
      for (int i = 0; i < nonce.Length; i++) oBytes.Add(nonce[i]);
      for (int i = 0; i < tag.Length; i++) oBytes.Add(tag[i]);
      for (int i = 0; i < cipherText.Length; i++) oBytes.Add(cipherText[i]);

      if (File.Exists(fileName)) File.Delete(fileName);
      using (FileStream fWriter = new FileStream("result.szk", FileMode.Create))
      {
        fWriter.Write(oBytes.ToArray());
      }
    }

    #endregion

    #region VRFシステムモデルの作成

    static ExVRFSystem[] makeVRFSystem(ImmutableBuildingThermalModel building)
    {
      VRFSystem[] vrfs = new VRFSystem[]
      {
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C56_0, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C45_0, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C61_5, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C61_5, 0, false)
      };

      vrfs[0].AddIndoorUnit(new VRFUnit[]
      {

        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C11_2),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0)
      });

      vrfs[1].AddIndoorUnit(new VRFUnit[]
      {

        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0)
      });

      vrfs[2].AddIndoorUnit(new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C11_2),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0)
      });

      vrfs[3].AddIndoorUnit(new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C9_0),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C11_2)
      });

      //冷媒温度設定
      for (int i = 0; i < 4; i++)
      {
        vrfs[i].MinEvaporatingTemperature = 5;
        vrfs[i].MaxEvaporatingTemperature = 20;
        vrfs[i].MinCondensingTemperature = 30;
        vrfs[i].MaxCondensingTemperature = 50;
        vrfs[i].TargetEvaporatingTemperature = vrfs[i].MinEvaporatingTemperature;
        vrfs[i].TargetCondensingTemperature = vrfs[i].MaxCondensingTemperature;

        //冷暖房モード
        vrfs[i].CurrentMode = (initSettings["period"] == 0) ? VRFSystem.Mode.Heating : VRFSystem.Mode.Cooling;
        for (int j = 0; j < vrfs[i].IndoorUnitNumber; j++)
          vrfs[i].SetIndoorUnitMode((initSettings["period"] == 0) ? VRFUnit.Mode.Heating : VRFUnit.Mode.Cooling);
      }

      //空調対象のゾーンリストを作成
      ImmutableZone[] znS = building.MultiRoom[0].Zones;
      ImmutableZone[] znN = building.MultiRoom[1].Zones;
      return new ExVRFSystem[] 
      {
        new ExVRFSystem(building.CurrentDateTime, vrfs[0], new ImmutableZone[] { znS[0], znS[1], znS[2], znS[3], znS[4], znS[5] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[1], new ImmutableZone[] { znS[6], znS[7], znS[8], znS[9], znS[10], znS[11] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[2], new ImmutableZone[] { znN[0], znN[1], znN[2], znN[3], znN[4], znN[5] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[3], new ImmutableZone[] { znN[6], znN[7], znN[8], znN[9], znN[10], znN[11], znN[12], znN[13] })
      };
    }

    #endregion

    #region Debug用

    private static void testBuildingModel()
    {
      BuildingThermalModel bm = BuildingMaker.Make();
      bm.TimeStep = 3600;
      Sun sun = new Sun(Sun.City.Tokyo);
      bm.UpdateOutdoorCondition(
        new DateTime(1999, 1, 1, 0, 0, 0),
        sun,
        10, 0.02, 0);
      //bm.SetSupplyAir(0, 2, 30, 0.01, 1);
      while (true)
      {
        bm.UpdateHeatTransferWithinCapacityLimit();
        for (int i = 0; i < bm.MultiRoom[0].ZoneNumber; i++)
          Console.Write("," + bm.MultiRoom[0].Zones[i].Temperature.ToString("F2"));
        for (int i = 0; i < bm.MultiRoom[1].ZoneNumber; i++)
          Console.Write("," + bm.MultiRoom[1].Zones[i].Temperature.ToString("F2"));
        Console.WriteLine();
      }
    }

    #endregion

  }
}