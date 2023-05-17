using BaCSharp;

using Popolo.ThermalLoad;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Weather;
using System.Security.Cryptography;

using System.Collections.Generic;
using Shizuku.Models;
using Popolo.Numerics;

namespace Shizuku2
{
  internal class Program
  {

    #region 定数宣言

    /// <summary>漏気量[回/h]</summary>
    private const double LEAK_RATE = 0.2;

    /// <summary>電力の一次エネルギー換算係数[MJ/kWh]</summary>
    private const double ELC_PRIM_RATE = 9.76;

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

    /// <summary>エネルギー消費量[MJ]</summary>
    private static double energyConsumption = 0.0;

    /// <summary>平均不満足率[-]</summary>
    private static double averageDissatisfactionRate = 0.0;
    
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
      vrfs = makeVRFSystem(building.CurrentDateTime);

      //テナントを生成
      tenants = new TenantList((uint)initSettings["seed"], building);

      //VRFコントローラ選択
      switch (initSettings["controller"])
      {
        case 1:
          vrfCtrl = new Daikin.VRFController(vrfs);
          break;
        default:
          throw new Exception("VRF controller number not supported.");
      }

      //コントローラ開始
      DateTime dt =
        initSettings["period"] == 0 ? new DateTime(1999, 7, 21, 0, 0, 0) : //夏季
        initSettings["period"] == 1 ? new DateTime(1999, 2, 10, 0, 0, 0) : //冬季
        new DateTime(1999, 4, 28, 0, 0, 0); //中間期
      dtCtrl = new DateTimeController(dt, (uint)initSettings["accerarationRate"]);
      dtCtrl.TimeStep = initSettings["timestep"];
      dtCtrl.StartService();
      vrfCtrl.StartService();
      building.TimeStep = dtCtrl.TimeStep;

      //DEBUG
      //while (true) ;
      Daikin.VRFScheduller scc = new Daikin.VRFScheduller(vrfs);
      scc.StartScheduling();
      //DEBUG

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
              "  " + averageDissatisfactionRate.ToString("F4")
              );
            Thread.Sleep(1000);
          }
        });

        //メイン処理
        run();
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

        Console.Write(e.ToString());
        Console.WriteLine("Emulation aborted. Press any key to exit.");
        Console.ReadLine();
      }
    }

    /// <summary>期間計算を実行する</summary>
    /// <param name="eConsumption">エネルギー消費[MJ]</param>
    /// <param name="aveDissatisfactionRate">平均不満足率[-]</param>
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
      Sun sun = 
        initSettings["weather"] == 1 ? new Sun(43.0621, 141.3544, 135) :
        initSettings["weather"] == 2 ? new Sun(38.2682, 140.8693, 135) :
        initSettings["weather"] == 3 ? new Sun(35.6894, 139.6917, 135) :
        initSettings["weather"] == 4 ? new Sun(34.6937, 135.5021, 135) :
        initSettings["weather"] == 5 ? new Sun(33.5903, 130.4017, 135) :
        new Sun(26.2123, 127.6791, 135);

      //初期化・周期定常化処理
      preRun(dtCtrl.CurrentDateTime.AddDays(-1), sun, wetLoader);

      DateTime endDTime = dtCtrl.CurrentDateTime.AddDays(7);
      uint ttlOcNum = 0;
      //加速度を考慮して計算を進める
      while (true)
      {
        while (dtCtrl.TryProceed())
        {
          //1週間で計算終了
          if (endDTime < dtCtrl.CurrentDateTime) break;

          //コントローラの制御値を機器やセンサに反映
          vrfCtrl.ApplyManipulatedVariables();
          dtCtrl.ApplyManipulatedVariables();

          //気象データを建物モデルに反映
          sun.Update(dtCtrl.CurrentDateTime);
          wetLoader.GetWeather(dtCtrl.CurrentDateTime, out double dbt, out double hmd, ref sun);
          building.UpdateOutdoorCondition(dtCtrl.CurrentDateTime, sun, dbt, 0.001 * hmd, 0);

          //テナントを更新（内部発熱もここで更新される）
          tenants.Update(dtCtrl.CurrentDateTime, dtCtrl.TimeStep);

          //VRF更新
          setVRFInletAir();
          for (int i = 0; i < vrfs.Length; i++)
          {
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
          vrfCtrl.ReadMeasuredValues();
          dtCtrl.ReadMeasuredValues();

          //成績を集計
          getScore(ref ttlOcNum, ref averageDissatisfactionRate, out energyConsumption);
        }

        //1週間で計算終了
        if (endDTime < dtCtrl.CurrentDateTime) break;
      }
    }

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
      building.TimeStep = tStep;
    }

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
      //デマンドレスポンス検討が無いのであれば、テナントの消費電力は評価対象外の方が良いかもしれない
      /*for (int i=0;i<tList.Tenants.Length;i++)
        eConsumption +=
            (tList.Tenants[i].ElectricityMeter_Light.IntegratedValue +
            tList.Tenants[i].ElectricityMeter_Plug.IntegratedValue)* ELC_PRIM_RATE;*/
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
      byte[] message = new byte[16];
      Array.Copy(BitConverter.GetBytes(eConsumption), 0, message, 0, 8);
      Array.Copy(BitConverter.GetBytes(aveDissatisfiedRate), 0, message, 8, 8);
      byte[] cipherText = new byte[16];
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

    static ExVRFSystem[] makeVRFSystem(DateTime now)
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
        new ExVRFSystem(now, vrfs[0]),
        new ExVRFSystem(now, vrfs[1]),
        new ExVRFSystem(now, vrfs[2]),
        new ExVRFSystem(now, vrfs[3])
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