using BaCSharp;

using Popolo.ThermalLoad;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Weather;
using System.Data.Common;
using System.Security.Principal;

using System.Collections.Generic;
using System.Net.Http.Headers;
using Shizuku.Models;

namespace Shizuku2
{
  internal class Program
  {

    #region 定数宣言

    /// <summary>漏気量[回/h]</summary>
    private const double LEAK_RATE = 0.2;

    #endregion

    #region クラス変数

    /// <summary>初期設定</summary>
    private static readonly Dictionary<string, int> initSettings = new Dictionary<string, int>();

    /// <summary>熱負荷計算モデル</summary>
    private static BuildingThermalModel building;

    /// <summary>VRFモデル</summary>
    private static readonly ExVRFSystem[] vrfs = makeVRFSystem();

    /// <summary>日時コントローラ</summary>
    private static DateTimeController dtCtrl;

    /// <summary>VRFコントローラ</summary>
    private static IBACnetController vrfCtrl;

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

      //VRFコントローラ選択
      switch (initSettings["controller"])
      {
        case 1:
          vrfCtrl = new VRFController_Daikin(vrfs);
          break;
        default:
          throw new Exception("VRF controller number not supported.");
      }

      //コントローラ開始
      dtCtrl = new DateTimeController(new DateTime(1999, 1, 1, 0, 0, 0), (uint)initSettings["accerarationRate"]);
      dtCtrl.TimeStep = initSettings["timestep"];
      dtCtrl.StartService();
      vrfCtrl.StartService();
      building.TimeStep = dtCtrl.TimeStep;

      try
      {
        //別スレッドで経過を表示
        Task.Run(() =>
        {
          while (true)
          {
            Console.WriteLine(dtCtrl.CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss"));
            Thread.Sleep(1000);
          }
        });

        //メイン処理
        run();
      }
      catch (Exception e)
      {
        using (StreamWriter sWriter = new StreamWriter("error.log"))
        {
          sWriter.Write(e.ToString());
        }
        Console.Write(e.ToString());
        Console.ReadLine();
      }
    }

    private static void run()
    {
      //気象データ読み込みクラス
      WeatherLoader wetLoader = new WeatherLoader((uint)initSettings["seed"],
        initSettings["weather"] == 1 ? RandomWeather.Location.Sapporo :
        initSettings["weather"] == 2 ? RandomWeather.Location.Sendai :
        initSettings["weather"] == 3 ? RandomWeather.Location.Tokyo :
        initSettings["weather"] == 4 ? RandomWeather.Location.Osaka :
        initSettings["weather"] == 5 ? RandomWeather.Location.Fukuoka :
        RandomWeather.Location.Naha);
      Sun sun = new Sun(Sun.City.Tokyo);

      while (true)
      {
        //加速度を考慮して計算を進める
        while (dtCtrl.TryProceed())
        {
          //コントローラの制御値を機器やセンサに反映
          vrfCtrl.ApplyManipulatedVariables();
          dtCtrl.ApplyManipulatedVariables();

          //気象データを建物モデルに反映
          sun.Update(dtCtrl.CurrentDateTime);
          wetLoader.GetWeather(dtCtrl.CurrentDateTime, out double dbt, out double hmd, ref sun);
          building.UpdateOutdoorCondition(dtCtrl.CurrentDateTime, sun, dbt, hmd, 0);

          //VRF更新
          setVRFInletAir();
          for (int i = 0; i < vrfs.Length; i++)
          {
            vrfs[i].UpdateControl();
            vrfs[i].VRFSystem.UpdateState(false);
          }
          setVRFOutletAir();

          //換気量を更新
          setVentilationRate();

          //内部発熱を更新
          //***未実装***

          //熱環境更新
          building.UpdateHeatTransferWithinCapacityLimit();

          //機器やセンサの検出値を取得
          vrfCtrl.ReadMeasuredValues();
          dtCtrl.ReadMeasuredValues();
        }
      }
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

      double vRate = (ventilate ? 5 : LEAK_RATE * 2.7) / 3600d; //機械換気：5CMH/m2、漏気：天井高2.7m
      for (int i = 0; i < 12; i++)
        building.SetVentilationRate(0, i, building.MultiRoom[0].Zones[i].FloorArea * vRate);
      for (int i = 0; i < 14; i++)
        building.SetVentilationRate(1, i, building.MultiRoom[1].Zones[i].FloorArea * vRate);
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
      Console.WriteLine("#                  Shizuku2  verstion 0.1.1 (2023.05.06)                #");
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

    #endregion

    #region VRFシステムモデルの作成

    static ExVRFSystem[] makeVRFSystem()
    {
      VRFSystem[] vrfs = new VRFSystem[]
      {
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C56_0, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C45_0, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C56_0, 0, false),
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

      return new ExVRFSystem[] 
      {
        new ExVRFSystem(vrfs[0]),
        new ExVRFSystem(vrfs[1]),
        new ExVRFSystem(vrfs[2]),
        new ExVRFSystem(vrfs[3])
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