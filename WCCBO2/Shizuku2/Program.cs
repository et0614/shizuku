using BaCSharp;

using Popolo.ThermalLoad;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Weather;
using System.Data.Common;
using System.Security.Principal;

namespace Shizuku2
{
  internal class Program
  {

    #region クラス変数

    /// <summary>初期設定</summary>
    private static readonly Dictionary<string, int> initSettings = new Dictionary<string, int>();

    /// <summary>熱負荷計算モデル</summary>
    private static readonly BuildingThermalModel building = makeBuildingModel();

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

          //内部発熱や換気量の更新
          //***

          //熱環境更新
          building.UpdateHeatTransferWithinCapacityLimit();

          //機器やセンサの検出値を取得
          vrfCtrl.ReadMeasuredValues();
          dtCtrl.ReadMeasuredValues();
        }
      }
    }

    private static void setVRFInletAir()
    {
      for (int i = 0; i < 6; i++)
        vrfs[0].VRFSystem.SetIndoorUnitInletAirState
          (i, building.MultiRoom[0].Zones[i].Temperature, building.MultiRoom[0].Zones[i].HumidityRatio);
      for (int i = 0; i < 6; i++)
        vrfs[1].VRFSystem.SetIndoorUnitInletAirState
          (i, building.MultiRoom[0].Zones[i + 6].Temperature, building.MultiRoom[0].Zones[i + 6].HumidityRatio);
      for (int i = 0; i < 6; i++)
        vrfs[2].VRFSystem.SetIndoorUnitInletAirState
          (i, building.MultiRoom[1].Zones[i].Temperature, building.MultiRoom[1].Zones[i].HumidityRatio);
      for (int i = 0; i < 8; i++)
        vrfs[3].VRFSystem.SetIndoorUnitInletAirState
          (i, building.MultiRoom[1].Zones[i + 6].Temperature, building.MultiRoom[1].Zones[i + 6].HumidityRatio);
    }

    private static void setVRFOutletAir()
    {
      for (int i = 0; i < 6; i++)
        building.SetSupplyAir(0, i,
          vrfs[0].VRFSystem.IndoorUnits[i].OutletAirTemperature,
          vrfs[0].VRFSystem.IndoorUnits[i].OutletAirHumidityRatio,
          vrfs[0].VRFSystem.IndoorUnits[i].AirFlowRate);
      for (int i = 0; i < 6; i++)
        building.SetSupplyAir(0, i + 6,
          vrfs[1].VRFSystem.IndoorUnits[i].OutletAirTemperature,
          vrfs[1].VRFSystem.IndoorUnits[i].OutletAirHumidityRatio,
          vrfs[1].VRFSystem.IndoorUnits[i].AirFlowRate);
      for (int i = 0; i < 6; i++)
        building.SetSupplyAir(1, i,
          vrfs[2].VRFSystem.IndoorUnits[i].OutletAirTemperature,
          vrfs[2].VRFSystem.IndoorUnits[i].OutletAirHumidityRatio,
          vrfs[2].VRFSystem.IndoorUnits[i].AirFlowRate);
      for (int i = 0; i < 8; i++)
        building.SetSupplyAir(1, i + 6,
          vrfs[3].VRFSystem.IndoorUnits[i].OutletAirTemperature,
          vrfs[3].VRFSystem.IndoorUnits[i].OutletAirHumidityRatio,
          vrfs[3].VRFSystem.IndoorUnits[i].AirFlowRate);
    }

    #endregion

    #region 補助関数

    /// <summary>タイトル表示</summary>
    private static void showTitle()
    {
      Console.WriteLine("\r\n");
      Console.WriteLine("#########################################################################");
      Console.WriteLine("#                                                                       #");
      Console.WriteLine("#                  Shizuku2  verstion 0.1.0 (2023.02.10)                #");
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
            if (initSettings.ContainsKey(st[0])) initSettings[st[0]] = int.Parse(st[1]);
            else initSettings.Add(st[0], int.Parse(st[1]));
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

      return new ExVRFSystem[] 
      {
        new ExVRFSystem(vrfs[0]),
        new ExVRFSystem(vrfs[1]),
        new ExVRFSystem(vrfs[2]),
        new ExVRFSystem(vrfs[3])
      };
    }

    #endregion

    #region 建物モデルの作成

    private static BuildingThermalModel makeBuildingModel()
    {
      //傾斜面の作成(四方位)//////////////
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

      //壁構成を作成////////////////////////
      WallLayer[] exWL = new WallLayer[6];  //外壁一般部分
      exWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      exWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);
      exWL[4] = new AirGapLayer("非密閉中空層", false, 0.05);
      exWL[5] = new WallLayer("石膏ボード", 0.22, 830, 0.008);

      WallLayer[] exbmWL = new WallLayer[4];  //外壁梁部分
      exbmWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exbmWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exbmWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.750);
      exbmWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);

      WallLayer[] exRF = new WallLayer[9];  //外壁屋根
      exRF[0] = new WallLayer("コンクリート", 1.6, 2000, 0.060);
      exRF[1] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.050);
      exRF[2] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.015);
      exRF[3] = new WallLayer("アスファルト類", 0.110, 920, 0.005);
      exRF[4] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.015);
      exRF[5] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      exRF[6] = new AirGapLayer("非密閉中空層", false, 0.05);
      exRF[7] = new WallLayer("石膏ボード", 0.220, 830, 0.010);
      exRF[8] = new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015);

      WallLayer[] flWL = new WallLayer[6];  //床・天井
      flWL[0] = new WallLayer("ビニル系床材", 0.190, 2000, 0.003);
      flWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      flWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      flWL[3] = new AirGapLayer("非密閉中空層", false, 0.05);
      flWL[4] = new WallLayer("石膏ボード", 0.220, 830, 0.009);
      flWL[5] = new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015);

      WallLayer[] inWL = new WallLayer[3];  //内壁
      inWL[0] = new WallLayer("石膏ボード", 0.220, 830, 0.012);
      inWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      inWL[2] = new WallLayer("石膏ボード", 0.220, 830, 0.012);

      WallLayer[] inSWL = new WallLayer[1];  //内壁_テナント間仕切用仮想壁
      inSWL[0] = new WallLayer("仮想壁", 10000, 1, 0.01);

      //ゾーンを作成/////////////////////////
      Zone[] znSs = new Zone[12];
      Zone[] znNs = new Zone[14];
      Zone[] znCs = new Zone[2];
      double[] SZN_AREAS = new double[] { 26, 26, 26, 65, 65, 65, 20, 20, 26, 50, 50, 65 };
      double[] NZN_AREAS = new double[] { 26, 26, 26, 65, 65, 65, 20, 20, 26, 26, 50, 50, 65, 65 };
      for (int i = 0; i < znSs.Length; i++)
        znSs[i] = new Zone("S" + i, SZN_AREAS[i] * 2.7 * 1.2, SZN_AREAS[i]);
      for (int i = 0; i < znNs.Length; i++)
        znNs[i] = new Zone("N" + i, NZN_AREAS[i] * 2.7 * 1.2, NZN_AREAS[i]);
      znCs[0] = new Zone("WC", 90 * 2.4 * 1.2, 90);
      znCs[1] = new Zone("IL", 160 * 2.4 * 1.2, 160);

      //壁体の作成***************************************************************************************
      Wall[] walls = new Wall[81];
      const double WAL_HEIGHT = 0.7;
      const double WIN_HEIGHT = 2.0;
      const double CEL_HEIGHT = 1.5;
      //外壁（南）
      walls[0] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[1] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[2] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[3] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[4] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[5] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[6] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[7] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[8] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[9] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[10] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[11] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      //外壁（南西）
      walls[12] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[13] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[14] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[15] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //外壁（北）
      walls[16] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[17] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[18] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[19] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[20] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[21] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[22] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[23] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[24] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[25] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[26] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[27] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[28] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[29] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      //外壁（北西）
      walls[30] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[31] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[32] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[33] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //外壁（北東）
      walls[34] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[35] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[36] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[37] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //内壁*********************************
      //南側内壁
      walls[38] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[39] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[40] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[41] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[42] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[43] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[44] = new Wall(4.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL); //WC側1
      walls[45] = new Wall(10.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL); //WC側2
      //北側内壁
      walls[46] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[47] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[48] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[49] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[50] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[51] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      walls[52] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT + CEL_HEIGHT), inWL);
      //南側床
      walls[53] = new Wall(26, flWL);
      walls[54] = new Wall(26, flWL);
      walls[55] = new Wall(26, flWL);
      walls[56] = new Wall(65, flWL);
      walls[57] = new Wall(65, flWL);
      walls[58] = new Wall(65, flWL);
      walls[59] = new Wall(20, flWL);
      walls[60] = new Wall(20, flWL);
      walls[61] = new Wall(26, flWL);
      walls[62] = new Wall(50, flWL);
      walls[63] = new Wall(50, flWL);
      walls[64] = new Wall(65, flWL);
      //北側床
      walls[65] = new Wall(26, flWL);
      walls[66] = new Wall(26, flWL);
      walls[67] = new Wall(26, flWL);
      walls[68] = new Wall(65, flWL);
      walls[69] = new Wall(65, flWL);
      walls[70] = new Wall(65, flWL);
      walls[71] = new Wall(20, flWL);
      walls[72] = new Wall(20, flWL);
      walls[73] = new Wall(26, flWL);
      walls[74] = new Wall(26, flWL);
      walls[75] = new Wall(50, flWL);
      walls[76] = new Wall(50, flWL);
      walls[77] = new Wall(65, flWL);
      walls[78] = new Wall(65, flWL);
      //共用部床
      walls[79] = new Wall(90, flWL);
      walls[80] = new Wall(160, flWL);

      //壁の初期化
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].RadiativeCoefficientF = walls[i].RadiativeCoefficientB = 5;
        string nm = walls[i].Layers[0].Name;
        if (nm == "コンクリート" || nm == "タイル") walls[i].ConvectiveCoefficientF = 18;
        else walls[i].ConvectiveCoefficientF = 4;
        walls[i].ConvectiveCoefficientB = 4;
        walls[i].Initialize(20);
      }

      //窓を作成***************************************************************************************
      double[] TAU_WIN, RHO_WIN;
      TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
      RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]     
      Window[] winSs = new Window[8];
      Window[] winNs = new Window[11];
      Window[] winCs = new Window[0];
      //南
      winSs[0] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[1] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[2] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[3] = new Window(5.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[4] = new Window(5.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[5] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incS);
      winSs[6] = new Window(4.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incW);
      winSs[7] = new Window(10.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incW);
      //北
      winNs[0] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[1] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[2] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[3] = new Window(5.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[4] = new Window(5.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[5] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[6] = new Window(6.5 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incN);
      winNs[7] = new Window(4.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incW);
      winNs[8] = new Window(10.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incW);
      winNs[9] = new Window(4.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incE);
      winNs[10] = new Window(10.0 * WIN_HEIGHT, TAU_WIN, RHO_WIN, incE);

      //初期化
      void initWin(Window[] wins)
      {
        for (int i = 0; i < wins.Length; i++)
        {
          VenetianBlind blind = new VenetianBlind(25, 22.5, 0, 0, 0.80, 0.90);
          blind.SlatAngle = 0;
          wins[i].SetShadingDevice(0, blind);
          wins[i].ConvectiveCoefficientF = 18;
          wins[i].ConvectiveCoefficientB = 4;
          wins[i].LongWaveEmissivityF = wins[i].LongWaveEmissivityB = 0.9;
        }
      }
      initWin(winSs);
      initWin(winNs);

      //多数室の作成************************************************************************************
      MultiRooms[] mRm = new MultiRooms[3];
      //北側事務室
      mRm[0] = new MultiRooms(1, znSs, walls, winSs);
      for (int i = 0; i < znSs.Length; i++) mRm[0].AddZone(0, i);

      //南側事務室
      mRm[1] = new MultiRooms(1, znNs, walls, winNs);
      for (int i = 0; i < znNs.Length; i++) mRm[1].AddZone(0, i);

      //共用部
      mRm[2] = new MultiRooms(1, znCs, walls, winCs);
      mRm[2].AddZone(0, 0); //廊下・EVホール
      mRm[2].AddZone(0, 1); //便所

      //外壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(0, 0, false); mRm[0].SetOutsideWall(0, true, incS);
      mRm[0].AddWall(0, 1, false); mRm[0].SetOutsideWall(1, true, incS);
      mRm[0].AddWall(1, 2, false); mRm[0].SetOutsideWall(2, true, incS);
      mRm[0].AddWall(1, 3, false); mRm[0].SetOutsideWall(3, true, incS);
      mRm[0].AddWall(2, 4, false); mRm[0].SetOutsideWall(4, true, incS);
      mRm[0].AddWall(2, 5, false); mRm[0].SetOutsideWall(5, true, incS);
      mRm[0].AddWall(6, 6, false); mRm[0].SetOutsideWall(6, true, incS);
      mRm[0].AddWall(6, 7, false); mRm[0].SetOutsideWall(7, true, incS);
      mRm[0].AddWall(7, 8, false); mRm[0].SetOutsideWall(8, true, incS);
      mRm[0].AddWall(7, 9, false); mRm[0].SetOutsideWall(9, true, incS);
      mRm[0].AddWall(8, 10, false); mRm[0].SetOutsideWall(10, true, incS);
      mRm[0].AddWall(8, 11, false); mRm[0].SetOutsideWall(11, true, incS);
      mRm[0].AddWall(0, 12, false); mRm[0].SetOutsideWall(12, true, incS);
      mRm[0].AddWall(0, 13, false); mRm[0].SetOutsideWall(13, true, incS);
      //北西側
      mRm[0].AddWall(3, 14, false); mRm[0].SetOutsideWall(14, true, incE);
      mRm[0].AddWall(3, 15, false); mRm[0].SetOutsideWall(15, true, incE);
      //北側
      mRm[1].AddWall(0, 16, false); mRm[1].SetOutsideWall(16, true, incN);
      mRm[1].AddWall(0, 17, false); mRm[1].SetOutsideWall(17, true, incN);
      mRm[1].AddWall(1, 18, false); mRm[1].SetOutsideWall(18, true, incN);
      mRm[1].AddWall(1, 19, false); mRm[1].SetOutsideWall(19, true, incN);
      mRm[1].AddWall(2, 20, false); mRm[1].SetOutsideWall(20, true, incN);
      mRm[1].AddWall(2, 21, false); mRm[1].SetOutsideWall(21, true, incN);
      mRm[1].AddWall(6, 22, false); mRm[1].SetOutsideWall(22, true, incN);
      mRm[1].AddWall(6, 23, false); mRm[1].SetOutsideWall(23, true, incN);
      mRm[1].AddWall(7, 24, false); mRm[1].SetOutsideWall(24, true, incN);
      mRm[1].AddWall(7, 25, false); mRm[1].SetOutsideWall(25, true, incN);
      mRm[1].AddWall(8, 26, false); mRm[1].SetOutsideWall(26, true, incN);
      mRm[1].AddWall(8, 27, false); mRm[1].SetOutsideWall(27, true, incN);
      mRm[1].AddWall(9, 28, false); mRm[1].SetOutsideWall(28, true, incN);
      mRm[1].AddWall(9, 29, false); mRm[1].SetOutsideWall(29, true, incN);
      //北西
      mRm[1].AddWall(0, 30, false); mRm[1].SetOutsideWall(30, true, incW);
      mRm[1].AddWall(0, 31, false); mRm[1].SetOutsideWall(31, true, incW);
      mRm[1].AddWall(3, 32, false); mRm[1].SetOutsideWall(32, true, incW);
      mRm[1].AddWall(3, 33, false); mRm[1].SetOutsideWall(33, true, incW);
      //北東
      mRm[1].AddWall(9, 34, false); mRm[1].SetOutsideWall(34, true, incE);
      mRm[1].AddWall(9, 35, false); mRm[1].SetOutsideWall(35, true, incE);
      mRm[1].AddWall(13, 36, false); mRm[1].SetOutsideWall(36, true, incE);
      mRm[1].AddWall(13, 37, false); mRm[1].SetOutsideWall(37, true, incE);

      //共用部
      //一旦、無し
      //mRm[2].AddWall(0, 28, false); mRm[2].SetOutsideWall(28, true, incS);
      //mRm[2].AddWall(0, 29, false); mRm[2].SetOutsideWall(29, true, incE);
      //mRm[2].AddWall(1, 30, false); mRm[2].SetOutsideWall(30, true, incE);

      //内壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(4, 38, true); mRm[0].UseAdjacentSpaceFactor(38, false, 0.7); //機械室
      mRm[0].AddWall(5, 39, true); mRm[2].AddWall(1, 39, false);
      mRm[0].AddWall(6, 40, true); mRm[2].AddWall(1, 40, false);
      mRm[0].AddWall(9, 41, true); mRm[2].AddWall(1, 41, false);
      mRm[0].AddWall(10, 42, true); mRm[2].AddWall(1, 42, false);
      mRm[0].AddWall(11, 43, true); mRm[2].AddWall(1, 43, false);
      mRm[0].AddWall(8, 44, true); mRm[2].AddWall(0, 44, false);
      mRm[0].AddWall(11, 45, true); mRm[2].AddWall(0, 45, false);
      //北側
      mRm[1].AddWall(4, 46, true); mRm[2].AddWall(1, 46, false);
      mRm[1].AddWall(5, 47, true); mRm[2].AddWall(1, 47, false);
      mRm[1].AddWall(6, 48, true); mRm[2].AddWall(1, 48, false);
      mRm[1].AddWall(10, 49, true); mRm[2].AddWall(1, 49, false);
      mRm[1].AddWall(11, 50, true); mRm[2].AddWall(1, 50, false);
      mRm[1].AddWall(12, 51, true); mRm[2].AddWall(1, 51, false);
      mRm[1].AddWall(13, 52, true); mRm[2].AddWall(1, 52, false);
      //床
      for (int i = 0; i < 12; i++)
      {
        mRm[0].AddWall(i, 53 + i, true);
        mRm[0].AddWall(i, 53 + i, false);
      }
      for (int i = 0; i < 14; i++)
      {
        mRm[1].AddWall(i, 65 + i, true);
        mRm[1].AddWall(i, 65 + i, false);
      }
      mRm[2].AddWall(0, 79, true); mRm[2].AddWall(0, 79, false);
      mRm[2].AddWall(1, 80, true); mRm[2].AddWall(1, 80, false);

      //窓を登録***************************************************************************************
      mRm[0].AddWindow(0, 0);
      mRm[0].AddWindow(1, 1);
      mRm[0].AddWindow(2, 2);
      mRm[0].AddWindow(6, 3);
      mRm[0].AddWindow(7, 4);
      mRm[0].AddWindow(8, 5);
      mRm[0].AddWindow(0, 6);
      mRm[0].AddWindow(3, 7);
      mRm[1].AddWindow(0, 0);
      mRm[1].AddWindow(1, 1);
      mRm[1].AddWindow(2, 2);
      mRm[1].AddWindow(6, 3);
      mRm[1].AddWindow(7, 4);
      mRm[1].AddWindow(8, 5);
      mRm[1].AddWindow(9, 6);
      mRm[1].AddWindow(0, 7);
      mRm[1].AddWindow(3, 8);
      mRm[1].AddWindow(9, 9);
      mRm[1].AddWindow(13, 10);

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      mRm[0].SetSWDistributionRateToFloor(0, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(1, 53 + 1, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(2, 53 + 2, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(3, 53 + 6, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(4, 53 + 7, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(5, 53 + 8, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(6, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(7, 53 + 3, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(0, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(1, 65 + 1, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(2, 65 + 2, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(3, 65 + 6, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(4, 65 + 7, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(5, 65 + 8, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(6, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(7, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(8, 65 + 3, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(9, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(10, 65 + 13, true, SW_RATE_TO_FLOOR);

      //隙間風と熱容量設定*************************************************************************
      void initZone(Zone[] zns)
      {
        for (int i = 0; i < zns.Length; i++)
        {
          zns[i].HeatCapacity = zns[i].AirMass * 1006 * 10;
          zns[i].VentilationRate = 0.05e-3 * zns[i].GetWindowSurface(); //窓面積法:6m/sで中程度の気密性サッシ
          zns[i].InitializeAirState(22, 0.0105);
        }
      }
      initZone(znNs);
      initZone(znSs);
      initZone(znCs);

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(mRm);

      //ゾーン間換気の設定
      const double cvRate = 150d * 1.2 / 3600d;
      bModel.SetCrossVentilation(0, 0, 0, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 2, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 6, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 6, 0, 7, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 7, 0, 8, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 3, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 4, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 5, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 6, 0, 9, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 7, 0, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 8, 0, 11, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 3, 0, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 4, 0, 5, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 5, 0, 6, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 6, 0, 9, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 9, 0, 10, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 10, 0, 11, 10.0 * cvRate);

      bModel.SetCrossVentilation(1, 0, 1, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 2, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 2, 1, 6, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 6, 1, 7, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 7, 1, 8, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 8, 1, 9, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 3, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 4, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 2, 1, 5, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 6, 1, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 7, 1, 11, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 8, 1, 12, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 9, 1, 13, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 3, 1, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 4, 1, 5, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 5, 1, 6, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 6, 1, 10, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 10, 1, 11, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 11, 1, 12, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 12, 1, 13, 10.0 * cvRate);

      return bModel;
    }

    #endregion

  }
}