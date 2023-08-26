using Popolo.HVAC.HeatExchanger;
using Popolo.ThermalLoad;
using System.Security.Cryptography;

namespace Shizuku2
{
  /// <summary>換気システム</summary>
  internal class VentilationSystem
  {

    #region 定数宣言

    /// <summary>外気CO2濃度[ppm]</summary>
    private const double OACO2LEVEL = 400;

    #endregion

    #region 列挙型定義

    /// <summary>ファン風量</summary>
    public enum FanSpeed
    {
      /// <summary>停止</summary>
      Off = 0,
      /// <summary>弱</summary>
      Low,
      /// <summary>中</summary>
      Middle,
      /// <summary>強</summary>
      High
    }

    #endregion

    #region インスタンス変数・プロパティの定義

    /// <summary>全熱交換器を取得する</summary>
    public ImmutableAirToAirFlatPlateHeatExchanger[][] HeatExchangers { get { return oHexes; } }

    /// <summary>全熱交換器リスト</summary>
    private readonly AirToAirFlatPlateHeatExchanger[][] oHexes = new AirToAirFlatPlateHeatExchanger[][]
    {
      new AirToAirFlatPlateHeatExchanger[]{ makeHex(true), makeHex(true), makeHex(true), makeHex(false), makeHex(false) },
      new AirToAirFlatPlateHeatExchanger[]{ makeHex(false),makeHex(false),makeHex(false),makeHex(false) },
      new AirToAirFlatPlateHeatExchanger[]{ makeHex(true), makeHex(true), makeHex(true), makeHex(false), makeHex(false) },
      new AirToAirFlatPlateHeatExchanger[]{ makeHex(false),makeHex(false),makeHex(false),makeHex(false) }
    };

    /// <summary>ファン風量リスト</summary>
    private FanSpeed[][] fanSpeeds = new FanSpeed[][]
    {
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High }
    };

    /// <summary>バイパスコントロール有効か否かのリスト</summary>
    private bool[][] bypassCtrl = new bool[][] 
    {
      new bool[]{ false,false,false,false,false },
      new bool[]{ false,false,false,false},
      new bool[]{ false,false,false,false,false },
      new bool[]{ false,false,false,false }
    };

    /// <summary>南テナントのCO2濃度[ppmを取得する]</summary>
    public double CO2Level_SouthTenant { get; private set; } = 400;

    /// <summary>北テナントのCO2濃度[ppmを取得する]</summary>
    public double CO2Level_NorthTenant { get; private set; } = 400;

    /// <summary>南側テナント容積[m3]</summary>
    private readonly double roomVolumeSouth;

    /// <summary>北側テナント容積[m3]</summary>
    private readonly double roomVolumeNorth;

    /// <summary>南側テナントのファン消費電力[kW]を取得する</summary>
    public double FanElectricity_SouthTenant { get; private set; } = 0.0;

    /// <summary>北側テナントのファン消費電力[kW]を取得する</summary>
    public double FanElectricity_NorthTenant { get; private set; } = 0.0;

    /// <summary>CO2濃度による不満足者率[-]を取得する</summary>
    public double DissatisifactionRateFromCO2Level { get; private set; } = 0.0;

    #endregion

    #region コンストラクタ

    public VentilationSystem(BuildingThermalModel bModel)
    {
      for (int i = 0; i < 18; i++) roomVolumeSouth += bModel.MultiRoom[0].Zones[i].AirMass;
      for (int i = 0; i < 18; i++) roomVolumeNorth += bModel.MultiRoom[1].Zones[i].AirMass;
      //kgをm3に変換
      roomVolumeSouth /= 1.2;
      roomVolumeNorth /= 1.2;
    } 

    #endregion

    #region インスタンスメソッド

    public void UpdateVentilation(
      BuildingThermalModel bModel, uint southTenantOccupants, uint northTenantOccupants)
    {
      double vVolS = BuildingMaker.LEAK_RATE * roomVolumeSouth; 
      double vVolN = BuildingMaker.LEAK_RATE * roomVolumeNorth;
      FanElectricity_SouthTenant = FanElectricity_NorthTenant = 0.0;

      //全熱交換器出口空気状態を計算
      for (int oIdx = 0; oIdx < oHexes.Length; oIdx++)
      {
        for (int iIdx = 0; iIdx < oHexes[oIdx].Length; iIdx++)
        {
          getZoneIndex(oIdx, iIdx, out int rmIdx, out int znIdx);
          getHexState(oIdx, iIdx, out double af, out double elc);
          oHexes[oIdx][iIdx].UpdateState(
            af, bypassCtrl[oIdx][iIdx] ? 0.0 : af,
            bModel.OutdoorTemperature, bModel.OutdoorHumidityRatio,
            bModel.MultiRoom[rmIdx].Zones[znIdx + 9].Temperature,  //上部ゾーンから吸い込む
            bModel.MultiRoom[rmIdx].Zones[znIdx + 9].HumidityRatio);

          //換気量と消費電力を積算
          if (rmIdx == 0)
          {
            vVolS += af;
            FanElectricity_SouthTenant += elc;
          }
          else
          {
            vVolN += af;
            FanElectricity_NorthTenant += elc;
          }
        }
      }
      FanElectricity_SouthTenant *= 0.001; //W -> kW変換
      FanElectricity_NorthTenant *= 0.001;

      //CO2濃度を更新
      CO2Level_SouthTenant = 
        updateCO2Level(CO2Level_SouthTenant, vVolS / 3600d, OACO2LEVEL, roomVolumeSouth,
        getCO2Generation(southTenantOccupants), bModel.TimeStep);
      CO2Level_NorthTenant = 
        updateCO2Level(CO2Level_NorthTenant, vVolN / 3600d, OACO2LEVEL, roomVolumeNorth,
        getCO2Generation(northTenantOccupants), bModel.TimeStep);

      //不満足者率[-]を更新する
      uint ocNum = southTenantOccupants + northTenantOccupants;
      if (ocNum == 0) DissatisifactionRateFromCO2Level = 0;
      else
      {
        double disS = getDissatisfideRate(CO2Level_SouthTenant);
        double disN = getDissatisfideRate(CO2Level_NorthTenant);
        DissatisifactionRateFromCO2Level = (disS * southTenantOccupants + disN * northTenantOccupants) / ocNum;
      }
    }

    private void getHexState
      (int oUnitIndex, int iUnitIndex, out double aFlow, out double elec)
    {
      double rate =
        fanSpeeds[oUnitIndex][iUnitIndex] == FanSpeed.Off ? 0.0 :
        fanSpeeds[oUnitIndex][iUnitIndex] == FanSpeed.Low ? 0.3 :
        fanSpeeds[oUnitIndex][iUnitIndex] == FanSpeed.Middle ? 0.7 : 1.0;
      bool is150Type = (oUnitIndex == 0 && iUnitIndex < 3) || (oUnitIndex == 2 && iUnitIndex < 3);

      aFlow = rate * (is150Type ? 150 : 250);
      elec = rate * rate * (is150Type ? 75 : 100);
    }

    /// <summary>全熱交換器のファン風量を取得する(VRF番号と同じ)</summary>
    /// <param name="oUnitIndex">VRF室外機番号</param>
    /// <param name="iUnitIndex">VRF室内機番号</param>
    /// <param name="fanSpeed">全熱交換器のファン風量</param>
    public void SetFanSpeed(uint oUnitIndex, uint iUnitIndex, FanSpeed fanSpeed)
    {
      fanSpeeds[oUnitIndex][iUnitIndex] = fanSpeed;
    }

    /// <summary>全熱交換器のファン風量を設定する(VRF番号と同じ)</summary>
    /// <param name="oUnitIndex">VRF室外機番号</param>
    /// <param name="iUnitIndex">VRF室内機番号</param>
    /// <returns>全熱交換器のファン風量</returns>
    public FanSpeed GetFanSpeed(uint oUnitIndex, uint iUnitIndex)
    {
      return fanSpeeds[oUnitIndex][iUnitIndex];
    }

    /// <summary>バイパス制御を有効にする(VRF番号と同じ)</summary>
    /// <param name="oUnitIndex">VRF室外機番号</param>
    /// <param name="iUnitIndex">VRF室内機番号</param>
    public void EnableBypassControl(uint oUnitIndex, uint iUnitIndex)
    {
      bypassCtrl[oUnitIndex][iUnitIndex] = true;
    }

    /// <summary>バイパス制御を無効にする(VRF番号と同じ)</summary>
    /// <param name="oUnitIndex">VRF室外機番号</param>
    /// <param name="iUnitIndex">VRF室内機番号</param>
    public void DisableBypassControl(uint oUnitIndex, uint iUnitIndex)
    {
      bypassCtrl[oUnitIndex][iUnitIndex] = false;
    }

    /// <summary>バイパス制御は有効か否かを取得する(VRF番号と同じ)</summary>
    /// <param name="oUnitIndex">VRF室外機番号</param>
    /// <param name="iUnitIndex">VRF室内機番号</param>
    /// <returns>バイパス制御は有効か否か</returns>
    public bool IsBypassControlEnabled(uint oUnitIndex, uint iUnitIndex)
    {
      return bypassCtrl[oUnitIndex][iUnitIndex];
    }

    #endregion

    #region CO2関連

    /// <summary>timeStep後のCO2濃度[ppm]を計算する</summary>
    /// <param name="co2lvl">0時点のCO2濃度[ppm]</param>
    /// <param name="ventFLow">換気量[m3/s]</param>
    /// <param name="ventCO2">外気CO2濃度[ppm]</param>
    /// <param name="volume">室容積[m3]</param>
    /// <param name="genCO2">CO2発生量[m3/sec *10^6]</param>
    /// <param name="timeStep">タイムステップ[sec]</param>
    /// <returns>timeStep後のCO2濃度[ppm]</returns>
    private static double updateCO2Level
      (double co2lvl, double ventFLow, double ventCO2, double volume, double genCO2, double timeStep)
    {
      //換気無
      if (ventFLow == 0) 
        return co2lvl + genCO2 / volume * timeStep;

      //換気有
      double ex = Math.Exp(-ventFLow / volume * timeStep);
      return ventCO2 + (co2lvl - ventCO2) * ex + genCO2 / ventFLow * (1 - ex);
    }

    /// <summary>執務者からのCO2発生量[m3/sec *10^6]を計算する</summary>
    /// <param name="occupantNumber">執務者数</param>
    /// <returns>執務者からのCO2発生量[m3/sec *10^6]</returns>
    private static double getCO2Generation(uint occupantNumber)
    {
      return 1e6 * 0.02 * occupantNumber / 3600d;
    }

    /// <summary>CO2濃度[ppm]にもとづく不満足者率[-]を計算する</summary>
    /// <param name="co2Level">CO2濃度[ppm]</param>
    /// <returns>CO2濃度[ppm]にもとづく不満足者率[-]</returns>
    /// <remarks>
    /// 900ppmで0.5%、1000ppmで99.5%程度
    /// </remarks>
    private static double getDissatisfideRate(double co2Level)
    {
      return 1d / (1d + Math.Exp((950d - co2Level) / 10d));
    }

    #endregion

    #region staticメソッド

    /// <summary>全熱交換器モデルを作る</summary>
    /// <param name="is150Type">150CMHタイプか否か（Falseで250CMHタイプ）</param>
    /// <returns>全熱交換器モデル</returns>
    private static AirToAirFlatPlateHeatExchanger makeHex(bool is150Type)
    {
      //三菱業務用ロスナイLGH-N15RXW2相当(75W)
      if (is150Type)
        return new AirToAirFlatPlateHeatExchanger(
          150, 150, 0.74, 0.645,
          AirToAirFlatPlateHeatExchanger.AirFlow.CrossFlow,
          AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2017_Cooling);

      //三菱業務用ロスナイLGH-N25RXW2相当(100W)
      else
        return new AirToAirFlatPlateHeatExchanger(
          250, 250, 0.70, 0.630,
          AirToAirFlatPlateHeatExchanger.AirFlow.CrossFlow,
          AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2017_Cooling);
    }

    private static void getZoneIndex(int oUnitIndex, int iUnitIndex, out int mrmIndex, out int znIndex)
    {
      mrmIndex = (2 <= oUnitIndex) ? 1 : 0;
      oUnitIndex -= 2 * mrmIndex;
      znIndex = oUnitIndex * 5 + iUnitIndex;
    }

    #endregion

  }
}
