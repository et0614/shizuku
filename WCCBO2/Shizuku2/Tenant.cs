using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Popolo.BuildingOccupant;
using Popolo.Numerics;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;
using Shizuku2;

namespace Shizuku.Models
{
  /// <summary>テナント</summary>
  [Serializable]
  public class Tenant : ImmutableTenant
  {

    #region インスタンス変数・プロパティ

    /// <summary>テナント名称を取得する</summary>
    public string Name { get; private set; }

    /// <summary>テナントが入居している建物を取得する</summary>
    public ImmutableBuildingThermalModel Building { private set; get; }

    /// <summary>入居ゾーンリストを取得する</summary>
    public ImmutableZone[] Zones { private set; get; }

    /// <summary>フリーアドレス方式か否か</summary>
    public bool IsNonTerritorialOffice { private set; get; }

    /// <summary>執務者リストを取得する</summary>
    public ImmutableOccupant[] Occupants { get { return occupants; } }

    /// <summary>入居ゾーン別情報</summary>
    private OfficeTenant[] znTenants;

    /// <summary>執務者リスト</summary>
    private Occupant[] occupants;

    /// <summary>ゾーン別の執務者リスト</summary>
    private Occupant[][] znOccupants;

    /// <summary>入居ゾーンの乾球温度リスト</summary>
    private double[] dbTemps;

    /// <summary>入居ゾーンの相対湿度リスト</summary>
    private double[] relHumids;

    /// <summary>入居ゾーンの平均放射温度リスト</summary>
    private double[] mrTemps;

    /// <summary>オフィス不在の真偽</summary>
    private bool nobodyStay = true;

    /// <summary>テナント用VRF</summary>
    private ExVRFSystem vrf;

    /// <summary>正規乱数製造機</summary>
    private NormalRandom nRnd;

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="building">入居建物モデル</param>
    /// <param name="name">入居テナント名称</param>
    /// <param name="isNonTerritorialOffice">フリーアドレス方式か否か</param>
    /// <param name="zones">入居ゾーンリスト</param>
    /// <param name="cInd">業種</param>
    /// <param name="dOfWeek">休日の曜日</param>
    /// <param name="seed">乱数シード</param>
    public Tenant(
      ImmutableBuildingThermalModel building, string name, bool isNonTerritorialOffice, 
      ImmutableZone[] zones, OfficeTenant.CategoryOfIndustry cInd, OfficeTenant.DaysOfWeek dOfWeek, ExVRFSystem vrf, uint seed)
    {
      this.Building = building;
      this.Name = name;
      IsNonTerritorialOffice = isNonTerritorialOffice;
      this.Zones = zones;
      this.vrf = vrf;

      MersenneTwister uRnd = new MersenneTwister(seed);
      nRnd = new NormalRandom(uRnd.Next());

      //ゾーン別のテナント情報を初期化
      znTenants = new OfficeTenant[zones.Length];
      for (int i = 0; i < znTenants.Length; i++)
      {
        znTenants[i] = new OfficeTenant(cInd, zones[i].FloorArea, dOfWeek, uRnd.Next(), 9, 0, 18, 0, 12, 0, 13, 0); //営業9:00-18:00、昼休み12:00-13:00
        znTenants[i].ClearSpecialHolidays();  //祝祭日無し
      }

      //温湿度配列初期化
      dbTemps = new double[zones.Length];
      relHumids = new double[zones.Length];
      mrTemps = new double[zones.Length];

      //執務者リストを作成する
      List<Occupant> ocs = new List<Occupant>();
      znOccupants = new Occupant[zones.Length][];
      for (int i = 0; i < znTenants.Length; i++)
      {
        znOccupants[i] = new Occupant[znTenants[i].OfficeWorkerNumber];
        for (int j = 0; j < znTenants[i].OfficeWorkerNumber; j++)
        {
          Occupant oc = new Occupant(uRnd.Next(), znTenants[i].OfficeWorkers[j], this, zones[i]);
          ocs.Add(oc);
          znOccupants[i][j] = oc;
        }
      }
      occupants = ocs.ToArray();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>1日の執務スケジュールを更新する</summary>
    /// <param name="dTime">日にち</param>
    public void UpdateDailySchedule(DateTime dTime)
    {
      foreach (OfficeTenant tnt in znTenants)
        tnt.UpdateDailySchedule(dTime);
    }

    /// <summary>基準着衣量[clo]を更新する</summary>
    public void UpdateDailyCloValues()
    {
      for (int i = 0; i < occupants.Length; i++)
        occupants[i].UpdateDailyCloValue();
    }

    /// <summary>執務者の在不在情報、滞在ゾーン、温冷感を更新する</summary>
    public void UpdateOccupants(DateTime dTime)
    {
      //在不在情報
      nobodyStay = true;
      foreach (OfficeTenant tnt in znTenants)
      {
        tnt.UpdateStatus(dTime);
        if (tnt.StayWorkerNumber != 0) nobodyStay = false;
      }

      //ゾーン間移動と温冷感の更新
      foreach (Occupant oc in occupants)
      {
        oc.Move(dTime);
        oc.UpdateComfort(dTime);
      }

      //コントローラ操作の判定
      for (int i = 0; i < Zones.Length; i++)
      {
        bool controllable = vrf.ControlPermited(Zones[i]);
        int upDown = 0;
        for (int j = 0; j < znOccupants[i].Length; j++)
        {
          //制御を試みた執務者には制御可能性を通達
          if(znOccupants[i][j].TryToRaiseTemperatureSP || znOccupants[i][j].TryToLowerTemperatureSP) 
            znOccupants[i][j].ThinkControllable = controllable;

          //制御方向を積算
          if (znOccupants[i][j].Worker.StayInOffice)
          {
            if (znOccupants[i][j].TryToRaiseTemperatureSP) upDown++;
            else if (znOccupants[i][j].TryToLowerTemperatureSP) upDown--;
          }
        }
        //制御の向きは多数決
        int delta = Math.Sign(upDown);
        vrf.SetSetpoint(vrf.GetSetpoint(i, true) + delta, i, true); //冷却
        vrf.SetSetpoint(vrf.GetSetpoint(i, false) + delta, i, false); //加熱
      }
    }

    /// <summary>ゾーン情報を更新する</summary>
    public void UpdateZoneInfo()
    {
      for (int i = 0; i < Zones.Length; i++)
      {
        dbTemps[i] = Zones[i].Temperature;
        relHumids[i] = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
          (Zones[i].Temperature, Zones[i].HumidityRatio, 101.325);
        mrTemps[i] = Zones[i].GetMeanSurfaceTemperature();
      }
    }

    /// <summary>ゾーンの情報を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="drybulbTemperature">乾球温度[C]</param>
    /// <param name="relativeHumidity">相対湿度[%]</param>
    /// <param name="meanRadiantTemperature">平均放射温度[C]</param>
    public void GetZoneInfo
      (ImmutableZone zone, out double drybulbTemperature, out double relativeHumidity,
      out double meanRadiantTemperature)
    {
      if (Zones.Contains(zone))
      {
        int indx = Array.IndexOf(Zones, zone);
        drybulbTemperature = dbTemps[indx];
        relativeHumidity = relHumids[indx];
        meanRadiantTemperature = mrTemps[indx];
      }
      else drybulbTemperature = relativeHumidity = meanRadiantTemperature = 0;
    }

    /// <summary>豪華ゲスト達を登場させる</summary>
    /// <param name="uRnd">一様乱数生成器</param>
    internal void introduceSpecialCharacter(MersenneTwister uRnd, bool isMale, string firstName, string lastName)
    {
      for (int i = 0; i < 10; i++)
      {
        int ocN = (int)Math.Floor(occupants.Length * uRnd.NextDouble());
        Occupant ocp = occupants[ocN];
        if (ocp.IsMale == isMale && !ocp.IsSpecialCharacter)
        {
          ocp.makeSpecialCharacter(firstName, lastName);
          break;
        }
      }
    }

    /// <summary>ゾーンの負荷情報を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="number">ゾーンに滞在する人間の数</returns>
    /// <param name="sensibleHeat">ゾーンの顕熱負荷[W](人体・コンセント・照明)</param>
    /// <param name="latentHeat">ゾーンの潜熱負荷[W](人体)</param>
    public void UpdateHeatLoadInfo(ImmutableZone zone, out uint number, out double sensibleHeat, out double latentHeat)
    {
      sensibleHeat = latentHeat = number = 0;
      if (Zones.Contains(zone))
      {
        //人体負荷
        foreach (Occupant oc in occupants)
          if (zone.Equals(oc.CurrentZone))
            number++;

        //人体負荷（潜顕比は石野「人体Two-node modelの簡易化と応用に関する研究」より回帰」
        double znT = Math.Max(22, Math.Min(28, zone.Temperature));
        sensibleHeat += number * (-5.3 * znT + 211.3);
        latentHeat += number * (-5.4 * znT - 91.5);

        //コンセント負荷（金融業と通信業は70W、その他は40W）
        double pcLoad = 40;
        if (znTenants[0].Industry == OfficeTenant.CategoryOfIndustry.InformationAndCommunications ||
          znTenants[0].Industry == OfficeTenant.CategoryOfIndustry.FinanceOrInsurance) pcLoad = 70;

        //コンセント負荷 + 待機電力（3W/m2+ゆらぎ: 野部:オフィスにおける内部発熱負荷要素に関する実態調査）
        sensibleHeat += number * pcLoad + zone.FloorArea * Math.Min(30, Math.Max(0, (3 + 5 * nRnd.NextDouble_Standard())));

        //照明負荷//営業時間内か誰かが残っている場合には点灯
        if (znTenants[0].IsBuisinessHours(zone.MultiRoom.CurrentDateTime) || !nobodyStay)
          sensibleHeat += 7.1 * zone.FloorArea; //7.1W/m2
      }
    }

    /// <summary>乱数シードを再設定する</summary>
    /// <param name="seed">乱数シード</param>
    public void ResetRandomSeed(uint seed)
    {
      MersenneTwister rnd = new MersenneTwister(seed);
      foreach (OfficeTenant tnt in znTenants)
        tnt.ResetRandomSeed(rnd.Next());
    }

    /// <summary>あるゾーンの執務者一覧を取得する</summary>
    /// <param name="zone">あるゾーン</param>
    /// <returns>執務者一覧</returns>
    public ImmutableOccupant[] GetOccupants(ImmutableZone zone)
    {
      return znOccupants[Array.IndexOf(Zones, zone)];
    }

    #endregion

  }

  #region 読み取り専用テナント

  /// <summary>読み取り専用のテナント</summary>
  public interface ImmutableTenant
  {
    /// <summary>テナント名称を取得する</summary>
    string Name { get; }

    /// <summary>テナントが入居している建物を取得する</summary>
    ImmutableBuildingThermalModel Building { get; }

    /// <summary>入居ゾーンリストを取得する</summary>
    ImmutableZone[] Zones { get; }

    /// <summary>フリーアドレス方式か否か</summary>
    bool IsNonTerritorialOffice { get; }

    /// <summary>執務者リストを取得する</summary>
    ImmutableOccupant[] Occupants { get; }

    /// <summary>ゾーンの情報を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="drybulbTemperature">乾球温度[C]</param>
    /// <param name="relativeHumidity">相対湿度[%]</param>
    /// <param name="meanRadiantTemperature">平均放射温度[C]</param>
    void GetZoneInfo(ImmutableZone zone, out double drybulbTemperature, out double relativeHumidity, out double meanRadiantTemperature);

    /// <summary>あるゾーンの執務者一覧を取得する</summary>
    /// <param name="zone">あるゾーン</param>
    /// <returns>執務者一覧</returns>
    ImmutableOccupant[] GetOccupants(ImmutableZone zone);
  }

  #endregion

}
