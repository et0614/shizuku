using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.ThermalLoad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  /// <summary>BACnet通信のために拡張したVRFSystem</summary>
  public class ExVRFSystem
  {

    #region 列挙型定義

    public enum Mode
    {
      ThermoOff,
      Cooling,
      Heating,
      Auto,
      Dry,
      ShutOff
    }

    #endregion

    #region プロパティ・インスタンス変数

    public VRFSystem VRFSystem { get; private set; }

    public Mode[] IndoorUnitModes { get; private set; }

    public double[] SetPoints_C { get; private set; }

    public double[] SetPoints_H { get; private set; }

    /// <summary>吹き出し風向[radian]を設定・取得する</summary>
    /// <remarks>水平が0 radian、下向きがプラス</remarks>
    public double[] Direction { get; private set; }

    /// <summary>下部ゾーンへ送る風量比[-]を取得する</summary>
    public double[] LowZoneBlowRate { get; private set; }

    #endregion

    #region コンストラクタ

    public ExVRFSystem(VRFSystem vrfSystem)
    {
      VRFSystem = vrfSystem;

      IndoorUnitModes = new Mode[VRFSystem.IndoorUnitNumber];
      SetPoints_C = new double[VRFSystem.IndoorUnitNumber];
      SetPoints_H = new double[VRFSystem.IndoorUnitNumber];
      Direction = new double[VRFSystem.IndoorUnitNumber];
      LowZoneBlowRate = new double[VRFSystem.IndoorUnitNumber];

      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        IndoorUnitModes[i] = Mode.ShutOff;
        SetPoints_C[i] = 25;
        SetPoints_H[i] = 20;
        Direction[i] = 0;
        LowZoneBlowRate[i] = 0;
      }
    }

    #endregion

    /// <summary>受信した制御信号にもとづいてVRFの制御を更新する</summary>
    public void UpdateControl()
    {
      //運転モードを確認
      VRFSystem.Mode mode = VRFSystem.Mode.ShutOff;
      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        if (IndoorUnitModes[i] == Mode.Cooling || IndoorUnitModes[i] == Mode.Dry) mode = VRFSystem.Mode.Cooling;
        else if (IndoorUnitModes[i] == Mode.Heating) mode = VRFSystem.Mode.Heating;
      }
      VRFSystem.CurrentMode = mode;

      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        if(mode == VRFSystem.Mode.ShutOff)
          VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ShutOff);
        else if (mode == VRFSystem.Mode.Cooling && SetPoints_C[i] < VRFSystem.IndoorUnits[i].InletAirTemperature)
          VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
        else if (mode == VRFSystem.Mode.Heating && VRFSystem.IndoorUnits[i].InletAirTemperature < SetPoints_H[i])
          VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
        else VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ThermoOff);
      }
    }

    /// <summary>下部空間へ吹き出す流量を更新する</summary>
    /// <param name="iuntIndex">室内機番号</param>
    /// <param name="lowerZoneTemperature">下部空間の温度[C]</param>
    /// <param name="upperZoneTemperature">上部空間の温度[C]</param>
    public void UpdateBlowRate
      (int iuntIndex, double lowerZoneTemperature, double upperZoneTemperature)
    {
      //最大風速[m/s]
      const double MAX_VELOCITY = 3.0;

      ImmutableVRFUnit unt = VRFSystem.IndoorUnits[iuntIndex];
      double dTdY = Math.Max(0, upperZoneTemperature - lowerZoneTemperature) / 1.35;
      double ambT = upperZoneTemperature + dTdY * 0.5;
      PrimaryFlow.CalcBlowDown(
        unt.OutletAirTemperature, 
        ambT,
        MAX_VELOCITY * (unt.AirFlowRate / unt.NominalAirFlowRate), dTdY, Direction[iuntIndex],
        out double hRate, out _, out _);
      LowZoneBlowRate[iuntIndex] = hRate;
    }
  }
}
