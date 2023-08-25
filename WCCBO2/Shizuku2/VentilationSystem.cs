using Popolo.HVAC.HeatExchanger;
using Popolo.ThermalLoad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Shizuku2
{
  /// <summary>換気システム</summary>
  internal class VentilationSystem
  {

    #region 列挙型定義

    /// <summary>ファン風量</summary>
    public enum FanSpeed
    {
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
      new AirToAirFlatPlateHeatExchanger[]{ makeHex(false),makeHex(false),makeHex(false),makeHex(false) },
    };

    /// <summary>ファン風量リスト</summary>
    private FanSpeed[][] fanSpeeds = new FanSpeed[][]
    {
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
      new FanSpeed[]{ FanSpeed.High,FanSpeed.High,FanSpeed.High,FanSpeed.High },
    };

    /// <summary>バイパスコントロール有効か否かのリスト</summary>
    private bool[][] bypassCtrl = new bool[][] 
    {
      new bool[]{ false,false,false,false,false },
      new bool[]{ false,false,false,false},
      new bool[]{ false,false,false,false,false },
      new bool[]{ false,false,false,false }
    };

    /// <summary>北側テナントの換気量[m3/s]を取得する</summary>
    public double VentilationVolume_NorthTenant { get; private set; } = 0;

    /// <summary>南側テナントの換気量[m3/s]を取得する</summary>
    public double VentilationVolume_SouthTenant { get; private set; } = 0;

    #endregion


    #region コンストラクタ

    public VentilationSystem() { } 

    #endregion

    #region インスタンスメソッド

    public void UpdateVentilation(BuildingThermalModel bModel)
    {
      VentilationVolume_NorthTenant = VentilationVolume_SouthTenant = 0;

      for (int oIdx = 0; oIdx < 4; oIdx++)
      {
        for (int iIdx = 0; iIdx < 4; iIdx++)
        {
          getZoneIndex(oIdx, iIdx, out int rmIdx, out int znIdx);
          double af = getAirFlow(oIdx, iIdx);
          oHexes[oIdx][iIdx].UpdateState(
            af, bypassCtrl[oIdx][iIdx] ? 0.0 : af,
            bModel.OutdoorTemperature, bModel.OutdoorHumidityRatio,
            bModel.MultiRoom[rmIdx].Zones[znIdx].Temperature, 
            bModel.MultiRoom[rmIdx].Zones[znIdx].HumidityRatio);

          //換気量を積算
          if (oIdx < 2) VentilationVolume_SouthTenant += af;
          else VentilationVolume_NorthTenant += af;
        }
      }
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

    /// <summary>定格風量[CMH]を取得する</summary>
    /// <param name="is150Type">150CMHタイプか否か（Falseで250CMHタイプ）</param>
    /// <returns>定格風量[CMH]</returns>
    private static double getNominalFlow(bool is150Type)
    {
      //三菱業務用ロスナイLGH-N15RXW2相当(75W)
      if (is150Type)
        return 150;

      //三菱業務用ロスナイLGH-N25RXW2相当(100W)
      else
        return 250;
    }

    private static void getZoneIndex(int oUnitIndex, int iUnitIndex, out int mrmIndex, out int znIndex)
    {
      mrmIndex = (2 <= oUnitIndex) ? 1 : 0;
      oUnitIndex -= 2 * mrmIndex;
      znIndex = oUnitIndex * 5 + iUnitIndex;
    }

    #endregion

    private double getAirFlow(int oUnitIndex, int iUnitIndex)
    {
      double rate =
        fanSpeeds[oUnitIndex][iUnitIndex] == FanSpeed.Low ? 0.3 :
        fanSpeeds[oUnitIndex][iUnitIndex] == FanSpeed.Middle ? 0.7 : 1.0;
      bool is150Type = (oUnitIndex == 0 && iUnitIndex < 4) || (oUnitIndex == 2 && iUnitIndex < 4);
      return rate * getNominalFlow(is150Type);
    }

  }
}
