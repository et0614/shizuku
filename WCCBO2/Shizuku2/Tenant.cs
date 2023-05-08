using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Popolo.BuildingOccupant;
using Popolo.Numerics;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;

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
    public ImmutableOccupant[] Occupants { get { return occs; } }

    /// <summary>入居ゾーン別情報</summary>
    private OfficeTenant[] znTenants;

    /// <summary>執務者リスト</summary>
    private Occupant[] occs;

    /// <summary>入居ゾーンの乾球温度、相対湿度、平均放射温度、CO2濃度、直達日照リスト</summary>
    private double[] dbTemps, relHumids, mrTemps, co2Lvls, dirIllm;

    /// <summary>EVホールの温湿度および放射温度</summary>
    private double dbTempEV1F, relHumidEV1F, mrTempEV1F, dbTempEVTF, relHumidEVTF, mrTempEVTF;

    /// <summary>オフィス不在の真偽</summary>
    private bool nobodyStay = true;

    private double ePlug, eLight;

    /// <summary>コンセント積算電力量計</summary>
    private Accumulator eAcmPlug = new Accumulator(3600, 2, 1);

    /// <summary>照明積算電力量計</summary>
    private Accumulator eAcmLight = new Accumulator(3600, 2, 1);

    /// <summary>正規乱数製造機</summary>
    private NormalRandom nRnd;

    /// <summary>コンセント電力量計を取得する</summary>
    public ImmutableAccumulator ElectricityMeter_Plug { get { return eAcmPlug; } }

    /// <summary>照明電力量計を取得する</summary>
    public ImmutableAccumulator ElectricityMeter_Light { get { return eAcmLight; } }

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
      ImmutableZone[] zones, OfficeTenant.CategoryOfIndustry cInd, OfficeTenant.DaysOfWeek dOfWeek, uint seed)
    {
      this.Building = building;
      this.Name = name;
      IsNonTerritorialOffice = isNonTerritorialOffice;
      this.Zones = zones;

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
      co2Lvls = new double[zones.Length];
      dirIllm = new double[zones.Length];

      //執務者リストを作成する
      List<Occupant> ocs = new List<Occupant>();
      for (int i = 0; i < znTenants.Length; i++)
      {
        for (int j = 0; j < znTenants[i].OfficeWorkerNumber; j++)
          ocs.Add(new Occupant(uRnd.Next(), znTenants[i].OfficeWorkers[j], this, zones[i]));
      }
      occs = ocs.ToArray();
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
      for (int i = 0; i < occs.Length; i++)
        occs[i].UpdateDailyCloValue();
    }

    /// <summary>執務者の在不在情報と滞在ゾーンを更新する</summary>
    public void MoveOccupants(DateTime dTime)
    {
      //在不在情報
      nobodyStay = true;
      foreach (OfficeTenant tnt in znTenants)
      {
        tnt.UpdateStatus(dTime);
        if (tnt.StayWorkerNumber != 0) nobodyStay = false;
      }

      //執務者のゾーン間移動
      foreach (Occupant oc in occs) oc.UpdateStatus(dTime);
    }

    /// <summary>消費電力を更新する</summary>
    /// <param name="tStep">計算時間間隔[sec]</param>
    public void UpdateElectricity(double tStep)
    {
      eAcmPlug.Update(tStep, 0.001 * ePlug);
      eAcmLight.Update(tStep, 0.001 * eLight);
      ePlug = eLight = 0;
    }

    /// <summary>ゾーン情報を更新する</summary>
    /// <param name="zone">ゾーン</param>
    public void UpdateZoneInfo(ImmutableZone zone, double co2Lvl)
    {
      int indx = Array.IndexOf(Zones, zone);
      dbTemps[indx] = zone.Temperature;
      relHumids[indx] = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (zone.Temperature, zone.HumidityRatio, 101.325);
      mrTemps[indx] = zone.GetMeanSurfaceTemperature();
      co2Lvls[indx] = co2Lvl;
    }

    /// <summary>ゾーンの情報を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="drybulbTemperature">乾球温度[C]</param>
    /// <param name="relativeHumidity">相対湿度[%]</param>
    /// <param name="meanRadiantTemperature">平均放射温度[C]</param>
    /// <param name="co2Lvl">CO2濃度[m3/m3]</param>
    /// <param name="directIlluminance">直達光の床面面積入射比率[-]</param>
    public void GetZoneInfo
      (ImmutableZone zone, out double drybulbTemperature, out double relativeHumidity,
      out double meanRadiantTemperature, out double co2Lvl, out double directIlluminance)
    {
      directIlluminance = 0;
      if (Zones.Contains(zone))
      {
        int indx = Array.IndexOf(Zones, zone);
        drybulbTemperature = dbTemps[indx];
        relativeHumidity = relHumids[indx];
        meanRadiantTemperature = mrTemps[indx];
        co2Lvl = co2Lvls[indx];
        directIlluminance = dirIllm[indx];
      }
      else drybulbTemperature = relativeHumidity = meanRadiantTemperature = co2Lvl = 0;
    }

    /// <summary>豪華ゲスト達を登場させる</summary>
    /// <param name="uRnd">一様乱数生成器</param>
    internal void introduceSpecialCharacter(MersenneTwister uRnd, bool isMale, string firstName, string lastName)
    {
      for (int i = 0; i < 10; i++)
      {
        int ocN = (int)Math.Floor(occs.Length * uRnd.NextDouble());
        Occupant ocp = occs[ocN];
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
        foreach (Occupant oc in occs)
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
        double ep = number * pcLoad + zone.FloorArea * Math.Min(30, Math.Max(0, (3 + 5 * nRnd.NextDouble_Standard())));
        sensibleHeat += ep;
        ePlug += ep;

        //照明負荷//営業時間内か誰かが残っている場合には点灯
        int indx = Array.IndexOf(Zones, zone);
        dirIllm[indx] = 0;
        if (znTenants[0].IsBuisinessHours(zone.MultiRoom.CurrentDateTime) || !nobodyStay)
        {
          //自然採光による作業面照度を計算
          double difE, dirE;
          calcIlluminance(zone, out difE, out dirE);
          dirIllm[indx] = dirE;

          //500lxを基準に消費電力軽減。LEDで130lm/W,保守率0.9,照明率0.6
          //(7.1W/m2=500/(130*0.6*0.9))
          //直接光の照度は眩しいだけなので無視
          double uE = 7.1 * Math.Max(0, 500 - difE) / 500d;
          double wt = zone.FloorArea * uE;
          sensibleHeat += wt;
          eLight += wt;
        }
      }
    }

    /// <summary>乱数シードを再設定する</summary>
    /// <param name="seed">乱数シード</param>
    public void ResetRandomSeed(uint seed)
    {
      foreach (OfficeTenant tnt in znTenants)
        tnt.ResetRandomSeed(seed);
    }

    #endregion

    #region 照度関連の処理

    /// <summary>ゾーン作業面の照度[lx]を計算する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="diffIlluminance">拡散光による作業面照度[lx]</param>
    /// <param name="directIlluminanceRate">直達光の床面入射比率[-]</param>
    private void calcIlluminance(ImmutableZone zone, out double diffIlluminance, out double directIlluminanceRate)
    {
      const double RHO_F = 0.7; //天井の反射率[-]
      const double RHO_C = 0.2; //床の反射率[-]

      Popolo.Weather.ImmutableSun sun = zone.MultiRoom.Sun;

      ImmutableWindow[] wins = zone.GetWindows();
      if (wins.Length == 0)
      {
        diffIlluminance = directIlluminanceRate = 0;
        return;
      }

      directIlluminanceRate = 0;
      double eU = 0;
      double eL = 0;
      //double winArea = 0;
      for (int i = 0; i < wins.Length; i++)
      {
        //winArea += wins[i].Area;
        double dirE = wins[i].OutsideIncline.GetDirectSolarIlluminance(sun) * wins[i].DirectSolarIncidentTransmittance * wins[i].Area;
        double difE = wins[i].OutsideIncline.GetDiffuseSolarIlluminance(sun, zone.MultiRoom.Albedo) * wins[i].DiffuseSolarIncidentTransmittance * wins[i].Area;
        double diffTU, diffTL, dirTU, dirTL, dirDir;
        IShadingDevice shDv = wins[i].GetShadingDevice(0);
        if (shDv is VenetianBlind)
        {
          VenetianBlind blind = (VenetianBlind)wins[i].GetShadingDevice(0);
          blind.ComputeOpticalProperties(out diffTU, out diffTL, out dirTU, out dirTL, out dirDir);
          double sum = dirTU + dirTL + dirDir;
          if (0 < sum)
          {
            //directIlluminanceRate += dirE * dirDir / sum;
            if (0 < dirDir && 0 < dirE) directIlluminanceRate += wins[i].Area / Math.Tan(sun.Altitude);
            eU += dirE * dirTU / sum;
            //eL += dirE * dirTL / sum; //間接光のみを照度向上に寄与するとみなす場合
            eL += dirE * (dirTL + dirDir) / sum;  //直射光も照度向上に寄与するとみなす場合
          }
          sum = diffTU + diffTL;
          if (0 < sum)
          {
            eU += difE * diffTU / sum;
            eL += difE * diffTL / sum;
          }          
        }
        else
        {
          //directIlluminanceRate += dirE;
          if (0 < dirE) directIlluminanceRate += wins[i].Area / Math.Tan(sun.Altitude);
          eU += difE * 0.5;
          //eL += difE * 0.5;   //間接光のみを照度向上に寄与するとみなす場合
          eL += dirE + difE * 0.5;   //直射光も照度向上に寄与するとみなす場合
        }
      }
      directIlluminanceRate = Math.Min(directIlluminanceRate / zone.FloorArea, 1d);

      //作業面切断公式（松浦邦男）
      diffIlluminance = ((RHO_F * eL + eU) * RHO_C) / (zone.FloorArea * (1.0 - RHO_C * RHO_F));
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

    /// <summary>コンセント電力量計を取得する</summary>
    ImmutableAccumulator ElectricityMeter_Plug { get; }

    /// <summary>照明電力量計を取得する</summary>
    ImmutableAccumulator ElectricityMeter_Light { get; }

    /// <summary>ゾーンの情報を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <param name="drybulbTemperature">乾球温度[C]</param>
    /// <param name="relativeHumidity">相対湿度[%]</param>
    /// <param name="meanRadiantTemperature">平均放射温度[C]</param>
    /// <param name="co2Lvl">CO2濃度[m3/m3]</param>
    void GetZoneInfo(ImmutableZone zone, out double drybulbTemperature, out double relativeHumidity, out double meanRadiantTemperature, out double co2Lvl, out double directIlluminance);

  }

  #endregion

}
