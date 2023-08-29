using BaCSharp;

using Popolo.ThermalLoad;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Weather;
using System.Security.Cryptography;

using System.IO.BACnet;
using Shizuku.Models;
using Popolo.Numerics;
using Popolo.ThermophysicalProperty;
using Shizuku2.BACnet;

namespace Shizuku2
{
    internal class Program
  {

    #region 定数宣言

    /// <summary>デバッグ用熱負荷計算モード</summary>
    /// <remarks>BACnet通信を切って高速で熱負荷を計算し、混合損失の有無や原単位の妥当性を確認する目的。</remarks>
    private const bool HL_TEST_MODE = false;

    /// <summary>大気圧[kPa]（海抜0m）</summary>
    private const double ATM = 101.325;

    /// <summary>加湿風量[kg/s]</summary>
    /// <remarks>
    /// 冬季の必要加湿量:0.036 kg/h/m2, 
    /// てんまい加湿器の風量:260 CMH/(kg/h)
    /// 卓上式としては能力が大きすぎるか・・・</remarks>
    private const double HMD_AFLOW = 0.036 * 260.0 * 1.2 / 3600;

    /// <summary>上下空間の噴流によらない空気循環[回/s]</summary>
    private const double UPDOWN_VENT = 1.0 / 3600d;

    /// <summary>電力の一次エネルギー換算係数[GJ/kWh]</summary>
    private const double ELC_PRIM_RATE = 0.00976;

    /// <summary>バージョン（メジャー）</summary>
    private const int V_MAJOR = 0;

    /// <summary>バージョン（マイナー）</summary>
    private const int V_MINOR = 4;

    /// <summary>バージョン（リビジョン）</summary>
    private const int V_REVISION = 0;

    /// <summary>バージョン（日付）</summary>
    private const string V_DATE = "2023.08.26";

    /// <summary>加湿開始時刻</summary>
    private const int HUMID_START = 8;

    /// <summary>加湿終了時刻</summary>
    private const int HUMID_END = 20;

    /// <summary>計算結果ファイル</summary>
    private const string RESULT_FILE = "result.szk";

    /// <summary>データ出力ディレクトリ</summary>
    private const string OUTPUT_DIR = "data";

    #endregion

    #region クラス変数

    /// <summary>初期設定</summary>
    private static readonly Dictionary<string, int> initSettings = new Dictionary<string, int>();

    /// <summary>熱負荷計算モデル</summary>
    private static BuildingThermalModel building;

    /// <summary>VRFモデル</summary>
    private static ExVRFSystem[] vrfs;

    /// <summary>換気システム</summary>
    private static VentilationSystem ventSystem;

    /// <summary>テナントリスト</summary>
    private static TenantList tenants;

    /// <summary>計算が遅れているか否か</summary>
    private static bool isDelayed = false;

    /// <summary>平均不満足率（温冷感+ドラフト）</summary>
    private static double averagedDissatisfactionRate = 0.0;

    /// <summary>温冷感による不満足率[-]</summary>
    private static double dissatisfactionRate_thermal = 0.0;

    /// <summary>ドラフトによる不満足率[-]</summary>
    private static double dissatisfactionRate_draft = 0.0;

    /// <summary>上下温度分布による不満足者率[-]</summary>
    private static double dissatisfactionRate_vTempDif = 0.0;

    /// <summary>CO2濃度による不満足者率[-]</summary>
    private static double dissatisFactionRate_CO2 = 0.0;

    /// <summary>積算エネルギー消費量[GJ]</summary>
    private static double totalEnergyConsumption = 0.0;

    /// <summary>瞬時エネルギー消費量[GJ/h]</summary>
    private static double instantaneousEnergyConsumption = 0.0;

    #region BACnet Device

    /// <summary>日時コントローラ</summary>
    private static DateTimeController dtCtrl;

    /// <summary>VRFコントローラ</summary>
    private static IBACnetController vrfCtrl;

    /// <summary>外気モニタ</summary>
    private static EnvironmentMonitor envMntr;

    /// <summary>執務者モニタ</summary>
    private static OccupantMonitor ocMntr;

    /// <summary>換気システムコントローラ</summary>
    private static VentilationController ventCtrl;

    /// <summary>VRFスケジューラ</summary>
    private static IBACnetController? vrfSchedl;

    #endregion

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      //タイトル表示
      showTitle();

      //出力ディレクトリを用意
      if (!Directory.Exists(OUTPUT_DIR)) Directory.CreateDirectory(OUTPUT_DIR);

      //初期設定ファイル読み込み
      if (!loadInitFile())
      {
        Console.WriteLine("Failed to load \"setting.ini\"");
        return;
      }

      //建物モデルを作成
      building = BuildingMaker.Make();
      vrfs = makeVRFSystem(building);
      ventSystem = new VentilationSystem(building);

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
      tenants.OutputOccupantsInfo("occupants.csv");

      //日時コントローラを用意して助走計算
      Console.Write("Start precalculation...");
      DateTime dt =
        initSettings["period"] == 0 ? new DateTime(1999, 7, 21, 0, 0, 0) : //夏季
        new DateTime(1999, 2, 10, 0, 0, 0); //冬季
      //  new DateTime(1999, 4, 28, 0, 0, 0); //中間期//未対応
      dtCtrl = new DateTimeController(dt, 0); //加速度0で待機
      dtCtrl.TimeStep = building.TimeStep = initSettings["timestep"];
      //初期化・周期定常化処理
      preRun(dt, wetLoader, sun);
      Console.WriteLine("Done." + Environment.NewLine);

      //VRFコントローラ用意
      switch (initSettings["controller"])
      {
        case 0:
          vrfCtrl = new BACnet.Original.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new BACnet.Original.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        case 1:
          vrfCtrl = new BACnet.Daikin.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new BACnet.Daikin.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        case 2:
          vrfCtrl = new BACnet.MitsubishiElectric.VRFController(vrfs);
          if (initSettings["scheduller"] == 1) vrfSchedl = new BACnet.MitsubishiElectric.VRFScheduller(vrfs, dtCtrl.AccelerationRate, dtCtrl.CurrentDateTime);
          break;
        default:
          throw new Exception("VRF controller number not supported.");
      }

      //その他のBACnet Device      
      envMntr = new EnvironmentMonitor(building, vrfs); //外気モニタ
      ocMntr = new OccupantMonitor(tenants); //執務者モニタ
      ventCtrl = new VentilationController(ventSystem); //換気システムコントローラ
      DummyDevice dummyDv = new DummyDevice(); //ダミーデバイス

      //BACnet Device起動
      dtCtrl.StartService();
      vrfCtrl.StartService();
      envMntr.StartService();
      ocMntr.StartService();
      ventCtrl.StartService();
      dummyDv.StartService();

      //BACnet controllerの登録を待つ
      Console.WriteLine("Waiting for BACnet controller registration.");
      Console.WriteLine("Press \"Enter\" key to continue.");
      //Defaultコントローラ開始
      vrfSchedl?.StartService();
      int key;
      while ((key = Console.Read()) != -1)
      {
        if ((char)key == (char)ConsoleKey.Enter) break;
      }
      //while (Console.ReadKey().Key != ConsoleKey.Enter);

      //コントローラが接続されたら加速開始:BACnetで送信してCOV eventを発生させる
      dtCtrl.AccelerationRate = initSettings["accelerationRate"];
      dtCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime); //基準現在時刻を更新
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeController.MemberNumber.AccerarationRate);
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, dtCtrl.AccelerationRate));
      dtCtrl.Communicator.Client.WritePropertyRequest(
        new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString()),
        boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

      bool finished = false;
      try
      {
        //別スレッドで経過を表示
        Task.Run(() =>
        {
          Console.WriteLine();
          Console.WriteLine("Start emulation.");

          while (!finished)
          {
            Console.WriteLine(
              dtCtrl.CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss") +
              "  " + totalEnergyConsumption.ToString("F4") + " (" + instantaneousEnergyConsumption.ToString("F4") + ")" +
              "  " + averagedDissatisfactionRate.ToString("F4") + " (" +
              dissatisfactionRate_thermal.ToString("F4") + " , " +
              dissatisfactionRate_draft.ToString("F4") + " , " +
              dissatisfactionRate_vTempDif.ToString("F4") + " , " +
              ventSystem.DissatisifactionRateFromCO2Level.ToString("F4") +
              ")" +
              "  " + (isDelayed ? "DELAYED" : "")
              );
            Thread.Sleep(1000);
          }
        });

        //メイン処理
        run(wetLoader, sun);
        finished = true;
        //結果書き出し
        saveScore();

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
        Console.WriteLine("Emulation aborted. The errors were written out to \"error.log\".");
        Console.WriteLine("Press any key to exit.");
        Console.ReadLine();
      }
    }

    /// <summary>期間計算を実行する</summary>
    /// <param name="wetLoader">気象データ</param>
    /// <param name="sun">太陽</param>
    private static void run(WeatherLoader wetLoader, Sun sun)
    {
      DateTime endDTime = dtCtrl.CurrentDateTime.AddDays(7);
      DateTime nextOutput = dtCtrl.CurrentDateTime;
      uint ttlOcNum = 0;
      using (StreamWriter swGen = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "general.csv"))
      using (StreamWriter swZone = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "zone.csv"))
      using (StreamWriter swVRF = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "vrf.csv"))
      using (StreamWriter swVent = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "vent.csv"))
      using (StreamWriter swOcc = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "occupant.csv"))
      {
        //タイトル行書き出し
        outputStatus(swGen, swZone, swVRF, swVent, swOcc, true);

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
            if (!HL_TEST_MODE)
            {
              vrfCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime);
              dtCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime);
              ventCtrl.ApplyManipulatedVariables(dtCtrl.CurrentDateTime);
            }

            //気象データを建物モデルに反映
            sun.Update(dtCtrl.CurrentDateTime);
            wetLoader.GetWeather(dtCtrl.CurrentDateTime, out double dbt, out double hmd, out double nocRad, ref sun);
            building.UpdateOutdoorCondition(dtCtrl.CurrentDateTime, sun, dbt, 0.001 * hmd, nocRad);

            //テナントを更新（内部発熱もここで更新される）
            tenants.Update(dtCtrl.CurrentDateTime);
            updateHeatGain();
            //換気・CO2濃度を更新
            ventSystem.UpdateVentilation(building, tenants.Tenants[0].StayWorkerNumber, tenants.Tenants[1].StayWorkerNumber);

            if (HL_TEST_MODE)
            {
              setDebugBoundary();
            }
            else
            {
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
              updateSupplyAir();
            }

            //熱環境更新
            if (HL_TEST_MODE) building.UpdateHeatTransferWithinCapacityLimit();
            else
            {
              building.ForecastHeatTransfer();
              building.ForecastWaterTransfer();
              building.FixState();
            }

            if (!HL_TEST_MODE)
            {
              //機器やセンサの検出値を取得
              vrfCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime);
              dtCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime);
              envMntr.ReadMeasuredValues(dtCtrl.CurrentDateTime);
              ventCtrl.ReadMeasuredValues(dtCtrl.CurrentDateTime);
              ocMntr.ReadMeasuredValues(dtCtrl.CurrentDateTime);
            }

            //成績を集計
            updateScore(ref ttlOcNum);

            //書き出し
            if (nextOutput <= building.CurrentDateTime)
            {
              outputStatus(swGen, swZone, swVRF, swVent, swOcc, false);
              nextOutput = building.CurrentDateTime.AddSeconds(initSettings["outputSpan"]);
            }
          }

          //1週間で計算終了
          if (endDTime < dtCtrl.CurrentDateTime) break;
        }
      }
    }

    /// <summary>助走計算する</summary>
    /// <param name="wetLoader">気象データ</param>
    /// <param name="sun">太陽</param>
    private static void preRun(DateTime dTime, WeatherLoader wetLoader, Sun sun)
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
          wetLoader.GetWeather(dt, out double dbt, out double hmd, out double nocRad, ref sun);
          building.UpdateOutdoorCondition(dt, sun, dbt, 0.001 * hmd, nocRad);

          //熱環境更新
          building.ForecastHeatTransfer();
          building.ForecastWaterTransfer();
          building.FixState();

          dt = dt.AddHours(1);
        }
      }

      //気象データと時刻を初期化
      sun.Update(dTime);
      wetLoader.GetWeather(dTime, out double dbt2, out double hmd2, out double nocRad2, ref sun);
      building.UpdateOutdoorCondition(dTime, sun, dbt2, 0.001 * hmd2, nocRad2);

      building.TimeStep = tStep;
    }

    /// <summary>スコアを計算する</summary>
    /// <param name="totalOccupants">延執務者数[人]</param>
    private static void updateScore(ref uint totalOccupants) 
    {
      //不満足者率を更新
      tenants.GetDissatisfiedInfo(building, vrfs, out uint noc, 
        out dissatisfactionRate_thermal, out dissatisfactionRate_draft, out dissatisfactionRate_vTempDif);
      uint tNum = noc + totalOccupants;
      if (tNum != 0)
      {
        double disRate = 1 - 
          (1 - dissatisfactionRate_thermal) * 
          (1 - dissatisfactionRate_draft) * 
          (1 - dissatisfactionRate_vTempDif) * 
          (1 - ventSystem.DissatisifactionRateFromCO2Level);
        averagedDissatisfactionRate = (averagedDissatisfactionRate * totalOccupants + disRate * noc) / tNum;
      }
      totalOccupants = tNum;

      //エネルギー消費関連を更新
      instantaneousEnergyConsumption = ventSystem.FanElectricity_SouthTenant + ventSystem.FanElectricity_SouthTenant;
      for (int i = 0; i < vrfs.Length; i++)
        instantaneousEnergyConsumption += vrfs[i].Electricity;
      instantaneousEnergyConsumption *= ELC_PRIM_RATE;
      totalEnergyConsumption += instantaneousEnergyConsumption * (building.TimeStep / 3600d);
    }

    /// <summary>内部発熱を反映する</summary>
    private static void updateHeatGain()
    {
      for (int i = 0; i < building.MultiRoom.Length; i++) {
        for (int j = 0; j < building.MultiRoom[i].ZoneNumber; j++)
        {
          ImmutableZone zn = building.MultiRoom[i].Zones[j];
          double sh = tenants.GetSensibleHeat(zn);
          double lh = tenants.GetLatentHeat(zn);
          building.SetBaseHeatGain(i, j, 0.6 * sh, 0.4 * sh, 0.001 * lh / 2500d);
        }
      }
    }

    /// <summary>デバッグ用高速熱負荷計算用の境界条件を設定する</summary>
    private static void setDebugBoundary()
    {
      bool isSummer = 5 <= building.CurrentDateTime.Month && building.CurrentDateTime.Month <= 10;
      for (int i = 0; i < 2; i++)
      {
        for (int j = 0; j < 18; j++)
        {
          if (building.CurrentDateTime.Hour < 8 || 20 < building.CurrentDateTime.Hour)
          {
            building.ControlHeatSupply(i, j, 0);
            building.ControlWaterSupply(i, j, 0);
          }
          else
          {
            building.ControlDrybulbTemperature(i, j, isSummer ? 26 : 22);
            building.ControlHumidityRatio(i, j, isSummer ? 0.0105 : 0.0065);
          }
        }
      }
    }

    #endregion

    #region 書き出し処理

    private static void outputStatus(
  StreamWriter swGen, StreamWriter swZone, StreamWriter swVRF, StreamWriter swVent, StreamWriter swOcc, bool isTitleLine)
    {
      //タイトル行
      if (isTitleLine)
      {
        //一般の情報
        swGen.Write("date,time");
        swGen.WriteLine(
          ",Outdoor drybulb temperature[C],Outdoor humidity ratio[g/kg],Global horizontal radiation [W/m2]" +
          ",Energy consumption (Total) [GJ],Energy consumption (Current) [GJ/h]" +
          ",Averaged dissatisfaction[-],Dissatisfaction rate (Thermal comfort)[-],Dissatisfaction rate (Draft)[-],Dissatisfaction rate (Vertical temp. dif)[-],Dissatisfaction rate (CO2 level)[-]");

        //ゾーンの情報
        swZone.Write("date,time");
        for (int i = 0; i < building.MultiRoom.Length; i++)
        {
          int znNum = building.MultiRoom[i].ZoneNumber / 2; //上部下部空間それぞれ書き出す
          for (int j = 0; j < znNum; j++)
          {
            if (HL_TEST_MODE)
            {
              swZone.Write(
                "," + building.MultiRoom[i].Zones[j].Name + " sensible heat load [W]" +
                "," + building.MultiRoom[i].Zones[j].Name + " latent heat load [W]"
                );
            }
            else
            {
              swZone.Write(
                "," + building.MultiRoom[i].Zones[j].Name + " drybulb temperature [CDB]" +
                "," + building.MultiRoom[i].Zones[j + znNum].Name + " drybulb temperature [CDB]" +
                "," + building.MultiRoom[i].Zones[j].Name + " absolute humidity [g/kg]" +
                "," + building.MultiRoom[i].Zones[j + znNum].Name + " absolute humidity [g/kg]"
                );
            }
          }
          if (!HL_TEST_MODE)
          {
            //天井裏
            swZone.Write(
              "," + building.MultiRoom[i].Zones[building.MultiRoom[i].ZoneNumber - 1].Name + " drybulb temperature [CDB]" +
              "," + building.MultiRoom[i].Zones[building.MultiRoom[i].ZoneNumber - 1].Name + " absolute humidity [g/kg]"
                );
          }
        }
        swZone.WriteLine();

        //VRFの情報
        swVRF.Write("date,time");
        for (int i = 0; i < vrfs.Length; i++)
        {
          int oHex = i + 1;
          swVRF.Write(",VRF" + oHex + " electricity [kW],VRF" + oHex + " heat load[kW]");
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
              name + " Setpoint temperature (heating) [C]" +
              name + " Low blow rate[-]"
              );
          }
        }
        swVRF.WriteLine();

        //Ventilation Systemの情報
        swVent.WriteLine("date,time,CO2 level (South)[ppm],CO2 level (North)[ppm],Fan electricity (South)[kW],Fan electricity (North)[kW]");

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
        "," + building.Sun.GlobalHorizontalRadiation.ToString("F1") +
        "," + totalEnergyConsumption.ToString("F5") +
        "," + instantaneousEnergyConsumption.ToString("F5") +
        "," + averagedDissatisfactionRate.ToString("F4") +
        "," + dissatisfactionRate_thermal.ToString("F4") +
        "," + dissatisfactionRate_draft.ToString("F4") +
        "," + dissatisfactionRate_vTempDif.ToString("F4") +
        "," + dissatisFactionRate_CO2.ToString("F4"));

      //ゾーンの情報
      swZone.Write(dtHeader);
      for (int i = 0; i < building.MultiRoom.Length; i++)
      {
        int znNum = building.MultiRoom[i].ZoneNumber / 2; //上部下部空間それぞれ書き出す
        for (int j = 0; j < znNum; j++)
        {
          if (HL_TEST_MODE)
          {
            swZone.Write(
              "," + (building.MultiRoom[i].Zones[j].HeatSupply + building.MultiRoom[i].Zones[j + znNum].HeatSupply).ToString("F1") +
              "," + (2500000 * (building.MultiRoom[i].Zones[j].WaterSupply + building.MultiRoom[i].Zones[j + znNum].WaterSupply)).ToString("F2")
              );
          }
          else
          {
            swZone.Write(
              "," + building.MultiRoom[i].Zones[j].Temperature.ToString("F1") +
              "," + building.MultiRoom[i].Zones[j + znNum].Temperature.ToString("F1") +
              "," + (1000 * building.MultiRoom[i].Zones[j].HumidityRatio).ToString("F2") +
              "," + (1000 * building.MultiRoom[i].Zones[j + znNum].HumidityRatio).ToString("F2")
              );
          }
        }
        if (!HL_TEST_MODE)
        {
          //天井裏
          swZone.Write(
            "," + building.MultiRoom[i].Zones[building.MultiRoom[i].ZoneNumber - 1].Temperature.ToString("F1") +
            "," + (1000 * building.MultiRoom[i].Zones[building.MultiRoom[i].ZoneNumber - 1].HumidityRatio).ToString("F2")
            );
        }
      }
      swZone.WriteLine();

      //VRFの情報
      swVRF.Write(dtHeader);
      for (int i = 0; i < vrfs.Length; i++)
      {
        double hl = 0;
        for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
          hl += vrfs[i].VRFSystem.IndoorUnits[j].HeatTransfer;
        swVRF.Write("," + vrfs[i].Electricity.ToString("F2") + "," + hl.ToString("F2"));
        for (int j = 0; j < vrfs[i].VRFSystem.IndoorUnitNumber; j++)
        {
          swVRF.Write(
            "," + vrfs[i].VRFSystem.IndoorUnits[j].CurrentMode.ToString() +
            "," + vrfs[i].VRFSystem.IndoorUnits[j].InletAirTemperature.ToString("F1") +
            "," + (1000 * vrfs[i].VRFSystem.IndoorUnits[j].InletAirHumidityRatio).ToString("F2") +
            "," + vrfs[i].VRFSystem.IndoorUnits[j].OutletAirTemperature.ToString("F1") +
            "," + (1000 * vrfs[i].VRFSystem.IndoorUnits[j].OutletAirHumidityRatio).ToString("F2") +
            "," + vrfs[i].VRFSystem.IndoorUnits[j].AirFlowRate.ToString("F3") +
            "," + vrfs[i].GetSetpoint(j, true).ToString("F0") +
            "," + vrfs[i].GetSetpoint(j, false).ToString("F0") +
            "," + vrfs[i].LowZoneBlowRate[j].ToString("F3")
            );
        }
      }
      swVRF.WriteLine();

      //Ventilation Systemの情報
      swVent.Write(dtHeader);
      swVent.WriteLine(
        "," + ventSystem.CO2Level_SouthTenant.ToString("F0") +
        "," + ventSystem.CO2Level_NorthTenant.ToString("F0") +
        "," + ventSystem.FanElectricity_SouthTenant.ToString("F2") +
        "," + ventSystem.FanElectricity_NorthTenant.ToString("F2")
        );

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
            (tenants.Tenants[i].Occupants[j].TryToRaiseTemperatureSP ? "1" : "0") : ""));
      //下降要求
      for (int i = 0; i < tenants.Tenants.Length; i++)
        for (int j = 0; j < tenants.Tenants[i].Occupants.Length; j++)
          swOcc.Write("," + (tenants.Tenants[i].Occupants[j].Worker.StayInOffice ?
            (tenants.Tenants[i].Occupants[j].TryToLowerTemperatureSP ? "1" : "0") : ""));
      swOcc.WriteLine();
    }

    #endregion

    #region 加湿設定

    /// <summary>加湿する時間帯か否かを取得する</summary>
    /// <returns>加湿する時間帯か否か</returns>
    private static bool isHumidifyTime()
    {
      return initSettings["period"] == 1 && !(
        dtCtrl.CurrentDateTime.DayOfWeek == DayOfWeek.Saturday |
        dtCtrl.CurrentDateTime.DayOfWeek == DayOfWeek.Sunday |
        dtCtrl.CurrentDateTime.Hour < HUMID_START |
        HUMID_END <= dtCtrl.CurrentDateTime.Hour);
    }

    #endregion

    #region VRFの制御

    /// <summary>室内機の吸込空気を設定する</summary>
    private static void setVRFInletAir()
    {
      for (int i = 0; i < 5; i++)
      {
        ImmutableZone znU = building.MultiRoom[0].Zones[i + 9];
        vrfs[0].VRFSystem.SetIndoorUnitInletAirState(i, 
          Math.Max(5, Math.Min(35, znU.Temperature)), 
          Math.Max(0, Math.Min(0.025, znU.HumidityRatio))
          );
      }
      for (int i = 0; i < 4; i++)
      {
        ImmutableZone znU = building.MultiRoom[0].Zones[i + 14];
        vrfs[1].VRFSystem.SetIndoorUnitInletAirState(i,
          Math.Max(5, Math.Min(35, znU.Temperature)),
          Math.Max(0, Math.Min(0.025, znU.HumidityRatio))
          );
      }
      for (int i = 0; i < 5; i++)
      {
        ImmutableZone znU = building.MultiRoom[1].Zones[i + 9];
        vrfs[2].VRFSystem.SetIndoorUnitInletAirState(i,
          Math.Max(5, Math.Min(35, znU.Temperature)),
          Math.Max(0, Math.Min(0.025, znU.HumidityRatio))
          );
      }
      for (int i = 0; i < 4; i++)
      {
        ImmutableZone znU = building.MultiRoom[1].Zones[i + 14];
        vrfs[3].VRFSystem.SetIndoorUnitInletAirState(i,
          Math.Max(5, Math.Min(35, znU.Temperature)),
          Math.Max(0, Math.Min(0.025, znU.HumidityRatio))
          );
      }
    }

    /// <summary>下部空間と上部空間へ給気する（一括）</summary>
    private static void updateSupplyAir()
    {
      for(int k=0;k< 2; k++)
      {
        int ofst = k * 2;
        for (int i = 0; i < 5; i++)
          setVRFOutletAir(vrfs[ofst], i, k, i, i + 9,
            ventSystem.HeatExchangers[ofst][i].SupplyAirOutletDrybulbTemperature,
            ventSystem.HeatExchangers[ofst][i].SupplyAirOutletHumidityRatio,
            ventSystem.HeatExchangers[ofst][i].SupplyAirFlowVolume / 3600d * 1.2);
        for (int i = 0; i < 4; i++)
          setVRFOutletAir(vrfs[ofst + 1], i, k, i + 5, i + 14,
            ventSystem.HeatExchangers[ofst + 1][i].SupplyAirOutletDrybulbTemperature,
            ventSystem.HeatExchangers[ofst + 1][i].SupplyAirOutletHumidityRatio,
            ventSystem.HeatExchangers[ofst + 1][i].SupplyAirFlowVolume / 3600d * 1.2);
      }
    }

    /// <summary>下部空間と上部空間へ給気する（室内機別）</summary>
    /// <param name="vrf">VRF</param>
    /// <param name="untIndex">室内機番号</param>
    /// <param name="mrIndex">多数室番号</param>
    /// <param name="lwZnIndex">下部ゾーンの番号</param>
    /// <param name="upZnIndex">上部ゾーンの番号</param>
    /// <param name="ventDB">換気給気温度[C]</param>
    /// <param name="ventHmd">換気給気湿度[kg/kg]</param>
    /// <param name="ventFlow">換気風量[kg/s]</param>
    private static void setVRFOutletAir(
      ExVRFSystem vrf, int untIndex, int mrIndex, int lwZnIndex, int upZnIndex,
      double ventDB, double ventHmd, double ventFlow)
    {
      //換気量を高さ比で配分
      const double LOW_RATE = BuildingMaker.L_ZONE_HEIGHT / (BuildingMaker.U_ZONE_HEIGHT + BuildingMaker.L_ZONE_HEIGHT);
      double ventLow = ventFlow * LOW_RATE;
      double ventHigh = ventFlow - ventLow;

      //給気風量比を計算
      ImmutableZone znL = building.MultiRoom[mrIndex].Zones[lwZnIndex];
      ImmutableZone znU = building.MultiRoom[mrIndex].Zones[upZnIndex];
      vrf.UpdateBlowRate(untIndex, znL.Temperature, znU.Temperature);

      ImmutableVRFUnit unt = vrf.VRFSystem.IndoorUnits[untIndex];
      double lowerBlow = unt.AirFlowRate * vrf.LowZoneBlowRate[untIndex];
      double upperBlow = unt.AirFlowRate * (1.0 - vrf.LowZoneBlowRate[untIndex]);

      //換気と混ぜて上部空間に給気
      double upTmp = unt.OutletAirTemperature;
      double upHmd = unt.OutletAirHumidityRatio;
      blendAir(ref upTmp, ref upHmd, upperBlow, ventDB, ventHmd, ventHigh);
      building.SetSupplyAir(mrIndex, upZnIndex, upTmp, upHmd, upperBlow + ventHigh);

      //冬季は加湿運転判断
      double saTmp = unt.OutletAirTemperature;
      double saHmd = unt.OutletAirHumidityRatio;
      double saFlow = lowerBlow;
      if (isHumidifyTime())
      {
        double rhmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (znL.Temperature, znL.HumidityRatio, ATM);
        //40%を下回ったら加湿
        if (rhmd < 40)
        {
          double hmdAFlow = HMD_AFLOW * znL.FloorArea;
          humidify(znL.Temperature, znL.HumidityRatio, out double hmdATmp, out double hmdAHmd);
          blendAir(ref saTmp, ref saHmd, lowerBlow, hmdATmp, hmdAHmd, hmdAFlow);
          saFlow += hmdAFlow; //加湿給気分の風量を加算
        }
      }
      //換気と混ぜて下部空間に給気
      blendAir(ref saTmp, ref saHmd, saFlow, ventDB, ventHmd, ventLow);
      saFlow += ventLow; //換気給気分の風量を加算
      building.SetSupplyAir(mrIndex, lwZnIndex, saTmp, saHmd, saFlow);

      //下部空間に吹き込まれた風量分は下部から上部へ移動する
      double udVent = 1.2 * building.MultiRoom[mrIndex].Zones[lwZnIndex].FloorArea * 
        (BuildingMaker.U_ZONE_HEIGHT + BuildingMaker.L_ZONE_HEIGHT) * UPDOWN_VENT;
      building.SetCrossVentilation(mrIndex, lwZnIndex, upZnIndex, Math.Max(saFlow, udVent)); //これ、混ざる処理なので厳密には不正確。
    }

    /// <summary>空気を混合する</summary>
    /// <param name="tmp1">空気1の温度</param>
    /// <param name="hmd1">空気1の湿度</param>
    /// <param name="aflow1">空気1の風量</param>
    /// <param name="tmp2">空気2の温度</param>
    /// <param name="hmd2">空気2の湿度</param>
    /// <param name="aflow2">空気2の風量</param>
    private static void blendAir(
      ref double tmp1, ref double hmd1, double aflow1,
      double tmp2, double hmd2, double aflow2)
    {
      double sum = aflow1 + aflow2;
      if (sum == 0) return;
      double rate1 = aflow1 / sum;
      double rate2 = 1 - rate1;
      tmp1 = rate1 * tmp1 + rate2 * tmp2;
      hmd1 = rate1 * hmd1 + rate2 * hmd2;
    }

    /// <summary>水滴下で加湿する</summary>
    /// <param name="inletTemp">入口乾球温度[C]</param>
    /// <param name="inletHumid">入口絶対湿度[kg/kg]</param>
    /// <param name="outletTemp">出口乾球温度[C]</param>
    /// <param name="outletHumid">出口絶対湿度[kg/kg]</param>
    private static void humidify(double inletTemp, double inletHumid, out double outletTemp, out double outletHumid)
    {
      const double MAX_HMD = 95;
      double rhmd = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(inletTemp, inletHumid, ATM);

      if (MAX_HMD <= rhmd)
      {
        outletTemp = inletTemp;
        outletHumid = inletHumid;
      }
      else
      {
        double wb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(inletTemp, inletHumid, ATM);
        outletTemp = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity(wb, MAX_HMD, ATM);
        outletHumid = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(outletTemp, MAX_HMD, ATM);
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

    /// <summary>初期設定ファイルを読み込む</summary>
    /// <returns>読み込み成功の真偽</returns>
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
        if (HL_TEST_MODE) initSettings["accelerationRate"] = 1000000;
        return true;
      }
      else return false;
    }

    /// <summary>スコアを暗号化して保存する</summary>
    private static void saveScore()
    {
      //テキストデータの書き出し********************************
      using (StreamWriter sWriter = new StreamWriter(OUTPUT_DIR + Path.DirectorySeparatorChar + "result.txt"))
      {
        sWriter.WriteLine("Period:" + (initSettings["period"] == 0 ? "Summer" : "Winter"));
        sWriter.WriteLine("Energy consumption[GJ]:" + totalEnergyConsumption);
        sWriter.WriteLine("Average dissatisfied rate[-]:" + averagedDissatisfactionRate);
        sWriter.WriteLine("User ID:" + initSettings["userid"]);
        sWriter.WriteLine("Version:" + V_MAJOR + "." + V_MINOR + "." + V_REVISION);
      }

      //暗号化ファイルの書き出し********************************
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
      byte[][] data = new byte[][]
      {
        BitConverter.GetBytes(initSettings["period"]), //季節
        BitConverter.GetBytes(totalEnergyConsumption), //エネルギー消費
        BitConverter.GetBytes(averagedDissatisfactionRate), //平均不満足者率
        BitConverter.GetBytes(initSettings["userid"]), //ユーザーID
        BitConverter.GetBytes(V_MAJOR), //メジャーバージョン
        BitConverter.GetBytes(V_MINOR), //マイナーバージョン
        BitConverter.GetBytes(V_REVISION) //リビジョン
      };
      int tBytes = 0;
      for (int i = 0; i < data.Length; i++) tBytes += data[i].Length;
      byte[] message = new byte[tBytes];
      tBytes = 0;
      for (int i = 0; i < data.Length; i++)
      {
        Array.Copy(data[i], 0, message, tBytes, data[i].Length);
        tBytes += data[i].Length;
      }

      byte[] cipherText = new byte[message.Length];
      byte[] tag = new byte[16];
      cha2.Encrypt(nonce, message, cipherText, tag);
      List<byte> oBytes = new List<byte>();
      for (int i = 0; i < nonce.Length; i++) oBytes.Add(nonce[i]);
      for (int i = 0; i < tag.Length; i++) oBytes.Add(tag[i]);
      for (int i = 0; i < cipherText.Length; i++) oBytes.Add(cipherText[i]);

      string path = OUTPUT_DIR + Path.DirectorySeparatorChar + RESULT_FILE;
      if (File.Exists(path)) File.Delete(path);
      using (FileStream fWriter = new FileStream(path, FileMode.Create))
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
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C40_0, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C22_4, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C33_5, 0, false),
        VRFInitializer.MakeOutdoorUnit(VRFInitializer.OutdoorUnitModel.Daikin_VRVX, VRFInitializer.CoolingCapacity.C22_4, 0, false)
      };

      vrfs[0].AddIndoorUnit(new VRFUnit[]
      {

        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1)
      });

      vrfs[1].AddIndoorUnit(new VRFUnit[]
      {

        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6)
      });

      vrfs[2].AddIndoorUnit(new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C7_1)
      });

      vrfs[3].AddIndoorUnit(new VRFUnit[]
      {
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6),
        VRFInitializer.MakeIndoorUnit_Daikin(VRFInitializer.IndoorUnitType.CeilingRoundFlow_S, VRFInitializer.CoolingCapacity.C5_6)
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
        new ExVRFSystem(building.CurrentDateTime, vrfs[0], new ImmutableZone[] { znS[0], znS[1], znS[2], znS[3], znS[4] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[1], new ImmutableZone[] { znS[5], znS[6], znS[7], znS[8] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[2], new ImmutableZone[] { znN[0], znN[1], znN[2], znN[3], znN[4] }),
        new ExVRFSystem(building.CurrentDateTime, vrfs[3], new ImmutableZone[] { znN[5], znN[6], znN[7], znN[8] })
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