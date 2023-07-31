using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.Numerics;
using Popolo.ThermalLoad;
using Shizuku.Models;
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

    #region 定数

    /// <summary>制御が変更できる時間間隔[sec]</summary>
    private const int CONTROL_INTERVAL = 300;

    /// <summary>温度制御のゼロエナジーバンド</summary>
    private const double ZERO_ENERGY_BAND = 0.5;

    #endregion

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

    /// <summary>温度設定値[C]</summary>
    private double[] spC, spH;

    public VRFSystem VRFSystem { get; private set; }

    public Mode[] IndoorUnitModes { get; private set; }

    /// <summary>温度設定値の操作を許可するか否か</summary>
    public bool[] PermitSPControl { get; private set; }

    /// <summary>吹き出し風向[radian]を設定・取得する</summary>
    /// <remarks>水平が0 radian、下向きがプラス</remarks>
    public double[] Direction { get; private set; }

    /// <summary>下部ゾーンへ送る風量比[-]を取得する</summary>
    public double[] LowZoneBlowRate { get; private set; }

    /// <summary>電力量計</summary>
    private Accumulator　eMeters = new Accumulator(3600, 2, 1);

    /// <summary>電力量計を取得する</summary>
    public ImmutableAccumulator ElectricityMeters { get { return eMeters; } }

    /// <summary>噴流による不満足者率[-]を取得する</summary>
    public double DissatisfiedRateByJet { get; private set; }

    /// <summary>前回の更新日時</summary>
    private DateTime lastUpdate;

    /// <summary>次の制御可能時点</summary>
    private DateTime[] nextControllerbleTime;

    /// <summary>給気対象のゾーンリスト（下部）</summary>
    private ImmutableZone[] lowerZones;

    #endregion

    #region コンストラクタ

    /// <summary></summary>
    /// <param name="now"></param>
    /// <param name="vrfSystem"></param>
    /// <param name="lowerZones">給気対象のゾーン（下部）</param>
    public ExVRFSystem(DateTime now, VRFSystem vrfSystem, ImmutableZone[] lowerZones)
    {
      lastUpdate = now;
      VRFSystem = vrfSystem;
      this.lowerZones = lowerZones;

      int iuNum = VRFSystem.IndoorUnitNumber;
      IndoorUnitModes = new Mode[iuNum];
      spC = new double[iuNum];
      spH = new double[iuNum];
      Direction = new double[iuNum];
      LowZoneBlowRate = new double[iuNum];
      nextControllerbleTime = new DateTime[iuNum];
      PermitSPControl = new bool[iuNum];

      for (int i = 0; i < iuNum; i++)
      {
        IndoorUnitModes[i] = Mode.ShutOff;
        spC[i] = 25;
        spH[i] = 20;
        Direction[i] = 0;
        LowZoneBlowRate[i] = 0;
        nextControllerbleTime[i] = now;
      }
    }

    #endregion

    /// <summary>受信した制御信号にもとづいてVRFの制御を更新する</summary>
    public void UpdateControl(DateTime now)
    {
      //室外機の運転モードを確認：若い番号の室内機を優先
      VRFSystem.CurrentMode = VRFSystem.Mode.ShutOff;
      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        if (IndoorUnitModes[i] == Mode.Cooling || IndoorUnitModes[i] == Mode.Dry)
        {
          VRFSystem.CurrentMode = VRFSystem.Mode.Cooling;
          break;
        }
        else if (IndoorUnitModes[i] == Mode.Heating)
        {
          VRFSystem.CurrentMode = VRFSystem.Mode.Heating;
          break;
        }
      }

      //運転モードを設定
      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        if (nextControllerbleTime[i] <= now)
        {
          ImmutableVRFUnit iUnit = VRFSystem.IndoorUnits[i];

          //室外機運転モードに応じて
          switch (VRFSystem.CurrentMode)
          {
            //停止
            case VRFSystem.Mode.ShutOff:
              if (iUnit.CurrentMode != VRFUnit.Mode.ShutOff)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ShutOff);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              break;

            //サーモオフ
            case VRFSystem.Mode.ThermoOff:
              if (iUnit.CurrentMode != VRFUnit.Mode.ThermoOff)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ThermoOff);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              break;

            //冷却
            case VRFSystem.Mode.Cooling:
              if (spC[i] + 0.5 * ZERO_ENERGY_BAND < VRFSystem.IndoorUnits[i].InletAirTemperature &&
                iUnit.CurrentMode != VRFUnit.Mode.Cooling)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              else if (VRFSystem.IndoorUnits[i].InletAirTemperature < spC[i] - 0.5 * ZERO_ENERGY_BAND &&
                iUnit.CurrentMode == VRFUnit.Mode.Cooling)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ThermoOff);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              break;

            //加熱
            case VRFSystem.Mode.Heating:
              if (VRFSystem.IndoorUnits[i].InletAirTemperature < spH[i] - 0.5 * ZERO_ENERGY_BAND &&
                iUnit.CurrentMode != VRFUnit.Mode.Heating)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              else if (spH[i] + 0.5 * ZERO_ENERGY_BAND < VRFSystem.IndoorUnits[i].InletAirTemperature &&
                iUnit.CurrentMode == VRFUnit.Mode.Heating)
              {
                VRFSystem.SetIndoorUnitMode(i, VRFUnit.Mode.ThermoOff);
                nextControllerbleTime[i] = now.AddSeconds(CONTROL_INTERVAL);
              }
              break;

            default:
              break;
          }
        }
      }

      //消費電力を更新
      eMeters.Update((now - lastUpdate).TotalSeconds,
        VRFSystem.CompressorElectricity +
        VRFSystem.OutdoorUnitFanElectricity + 
        VRFSystem.IndoorUnitFanElectricity
        );
      lastUpdate = now;
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

      //室内機特定
      ImmutableVRFUnit unt = VRFSystem.IndoorUnits[iuntIndex];

      //停止時
      if (unt.CurrentMode == VRFUnit.Mode.ShutOff)
      {
        DissatisfiedRateByJet = 0.0;
        LowZoneBlowRate[iuntIndex] = 0.0;
        return;
      }

      double dTdY = Math.Max(0, upperZoneTemperature - lowerZoneTemperature) / 1.35;
      double ambT = upperZoneTemperature + dTdY * 0.5;
      PrimaryFlow.CalcBlowDown(
        unt.OutletAirTemperature,
        ambT,
        MAX_VELOCITY * (unt.AirFlowRate / unt.NominalAirFlowRate), dTdY, Direction[iuntIndex],
        out double hRate, out double velAtNeck, out double tempAtNeck, out double jetLengthAtNeck);
      DissatisfiedRateByJet = PrimaryFlow.GetDraftRate(tempAtNeck, velAtNeck, ambT, jetLengthAtNeck);
      LowZoneBlowRate[iuntIndex] = hRate;
    }

    /// <summary>コントローラの操作の許可を設定する</summary>
    /// <param name="zone">対象ゾーン</param>
    /// <param name="permit">許可する場合はtrue</param>
    public void PermitControl(ImmutableZone zone, bool permit)
    {
      int indx = Array.IndexOf(lowerZones, zone);
      PermitSPControl[indx] = permit;
    }

    /// <summary>コントローラの操作の許可状態を取得する</summary>
    /// <param name="zone">対象ゾーン</param>
    /// <returns>許可されているか否か</returns>
    public bool ControlPermited(ImmutableZone zone)
    {
      int indx = Array.IndexOf(lowerZones, zone);
      return PermitSPControl[indx];
    }

    /// <summary>温度設定値[C]を取得する</summary>
    /// <param name="iUnitIndex">室内機番号</param>
    /// <param name="isCoolingMode">冷却モードか否か</param>
    /// <returns>温度設定値[C]</returns>
    public double GetSetpoint(int iUnitIndex, bool isCoolingMode)
    {
      return isCoolingMode ? spC[iUnitIndex] : spH[iUnitIndex];
    }

    /// <summary>温度設定値[C]を設定する</summary>
    /// <param name="setpoint">温度設定値[C]</param>
    /// <param name="iUnitIndex">室内機番号</param>
    /// <param name="isCoolingMode">冷却モードか否か</param>
    public void SetSetpoint(double setpoint, int iUnitIndex, bool isCoolingMode)
    {
      if (isCoolingMode) spC[iUnitIndex] = Math.Max(16, Math.Min(32, setpoint));
      else spH[iUnitIndex] = Math.Max(16, Math.Min(32, setpoint));
    }

  }
}
