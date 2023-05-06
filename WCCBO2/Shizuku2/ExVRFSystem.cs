using Popolo.HVAC.MultiplePackagedHeatPump;
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

    #endregion

    #region コンストラクタ

    public ExVRFSystem(VRFSystem vrfSystem)
    {
      VRFSystem = vrfSystem;

      IndoorUnitModes = new Mode[VRFSystem.IndoorUnitNumber];
      SetPoints_C = new double[VRFSystem.IndoorUnitNumber];
      SetPoints_H = new double[VRFSystem.IndoorUnitNumber];

      for (int i = 0; i < IndoorUnitModes.Length; i++)
      {
        IndoorUnitModes[i] = Mode.ShutOff;
        SetPoints_C[i] = 25;
        SetPoints_H[i] = 20;
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

  }
}
