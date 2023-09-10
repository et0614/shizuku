using Popolo.Numerics;
using Popolo.BuildingOccupant;
using Popolo.ThermalLoad;
using System.Reflection;
using Shizuku2;

namespace Shizuku.Models
{
  /// <summary>オフィステナントリスト</summary>
  [Serializable]
  public class TenantList : ImmutableTenantList
  {

    #region インスタンス変数・プロパティ

    /// <summary>1タイムステップ前の時刻</summary>
    private int lastDTimeHour = 23;

    /// <summary>内部発熱の最終更新日時</summary>
    private DateTime lastHLcalc = new DateTime(3000, 1, 1);
    
    /// <summary>テナントリストを取得する</summary>
    public ImmutableTenant[] Tenants { get { return tenants; } }

    /// <summary>テナント一覧</summary>
    private Tenant[] tenants;

    /// <summary>ゾーン別滞在者数リスト</summary>
    private Dictionary<ImmutableZone, uint> workerNumbers = new Dictionary<ImmutableZone, uint>();

    /// <summary>ゾーン別人体顕熱負荷リスト</summary>
    private Dictionary<ImmutableZone, double> sHeatLoads = new Dictionary<ImmutableZone, double>();

    /// <summary>ゾーン別人体潜熱負荷リスト</summary>
    private Dictionary<ImmutableZone, double> lHeatLoads = new Dictionary<ImmutableZone, double>();

    /// <summary>ゾーンリスト</summary>
    private ImmutableZone[] zones;

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="seed">乱数シード</param>
    public TenantList(uint seed, ImmutableBuildingThermalModel building, ExVRFSystem[] vrfs)
    {
      MersenneTwister uRnd = new MersenneTwister(seed);
      OfficeTenant.DaysOfWeek dw = OfficeTenant.DaysOfWeek.Saturday | OfficeTenant.DaysOfWeek.Sunday;

      //リストの初期化
      List<ImmutableZone> znLst = new List<ImmutableZone>();
      for (int i = 0; i < building.MultiRoom.Length; i++)
      {
        for (int j = 0; j < building.MultiRoom[i].ZoneNumber; j++)
        {
          workerNumbers.Add(building.MultiRoom[i].Zones[j], 0);
          sHeatLoads.Add(building.MultiRoom[i].Zones[j], 0);
          lHeatLoads.Add(building.MultiRoom[i].Zones[j], 0);
          znLst.Add(building.MultiRoom[i].Zones[j]);
        }
      }
      zones = znLst.ToArray();

      tenants = new Tenant[2];
      ImmutableZone[] zns;
      //南側
      zns = building.MultiRoom[0].Zones;
      tenants[0] = new Tenant(building, "South tenant", false, 
        new ImmutableZone[] { zns[0], zns[1], zns[2], zns[3], zns[4], zns[5], zns[6], zns[7], zns[8] },
        OfficeTenant.CategoryOfIndustry.Manufacturing, dw, new ExVRFSystem[] { vrfs[0], vrfs[1] }, uRnd.Next());

      //北側
      zns = building.MultiRoom[1].Zones;
      tenants[1] = new Tenant(building, "North tenant", false,
        new ImmutableZone[] { zns[0], zns[1], zns[2], zns[3], zns[4], zns[5], zns[6], zns[7], zns[8] },
        OfficeTenant.CategoryOfIndustry.InformationAndCommunications, dw, new ExVRFSystem[] { vrfs[2], vrfs[3] }, uRnd.Next());

      //豪華ゲストを登場させる
      introduceSpecialCharacters(uRnd);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>モデルを更新する</summary>
    public void Update(DateTime cTime)
    {
      //日付変化時に執務者行動を更新
      if (lastDTimeHour != cTime.Hour && cTime.Hour == 0)
        foreach (Tenant tnt in tenants)
          tnt.UpdateDailySchedule(cTime);
      //AM6時に着衣量を更新
      if (lastDTimeHour != cTime.Hour && cTime.Hour == 6)
        foreach (Tenant tnt in tenants)
          tnt.UpdateDailyCloValues();
      lastDTimeHour = cTime.Hour;

      //在不在情報・内部発熱を更新
      foreach (Tenant tnt in tenants)
      {
        tnt.UpdateZoneInfo();
        tnt.UpdateOccupants(cTime);
      }

      //負荷情報を更新（60secに1回）
      //初回の処理
      if (cTime < lastHLcalc) lastHLcalc = cTime;
      if (lastHLcalc.AddSeconds(60) <= cTime)
      {
        //内部発熱を更新（消費電力含む）
        lastHLcalc = cTime;
        foreach (ImmutableZone zn in zones)
        {
          //上部空間または天井裏でなければ
          if (!(zn.Name.Contains("_Up") || zn.Name.Contains("_Attic")))
          {
            uint num, numSum;
            double sload, lload, sloadSum, lloadSum;
            sloadSum = lloadSum = numSum = 0;
            foreach (Tenant tnt in tenants)
            {
              tnt.UpdateHeatLoadInfo(zn, out num, out sload, out lload);
              numSum += num;
              sloadSum += sload;
              lloadSum += lload;
            }
            workerNumbers[zn] = numSum;
            sHeatLoads[zn] = sloadSum;
            lHeatLoads[zn] = lloadSum;
          }
        }
      }
    }

    /// <summary>豪華ゲスト達を登場させる</summary>
    /// <param name="uRnd">一様乱数生成器</param>
    internal void introduceSpecialCharacters(MersenneTwister uRnd)
    {
      List<bool> isM = new List<bool>();
      List<string> lNm = new List<string>();
      List<string> fNm = new List<string>();

      var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.SpecialCharacters.txt");
      using (StreamReader sReader = new StreamReader(stream))
      {        
        string bf;
        while ((bf = sReader.ReadLine()) != null)
        {
          string[] bf2 = bf.Split(',');
          lNm.Add(bf2[0]);
          fNm.Add(bf2[1]);
          isM.Add(bool.Parse(bf2[2]));
        }
      }

      for (int i = 0; i < isM.Count; i++)
      {
        int tntN = (int)Math.Floor(tenants.Length * uRnd.NextDouble());
        tenants[tntN].introduceSpecialCharacter(uRnd, isM[i], fNm[i], lNm[i]);
      }
    }

    /// <summary>不満情報を取得する</summary>
    /// <param name="stayNumber">建物内執務者数</param>
    /// <param name="aveDissatisfaction_thermal">平均不満足率</param>
    public void GetDissatisfiedInfo(
      ImmutableBuildingThermalModel bModel, ExVRFSystem[] vrfs, out uint stayNumber, out double aveDissatisfaction_thermal, out double aveDissatisfaction_draft, out double aveDissatisfaction_vTempDif)
    {
      stayNumber = tenants[0].StayWorkerNumber + tenants[1].StayWorkerNumber;
      aveDissatisfaction_thermal = 0;
      aveDissatisfaction_draft = 0;
      aveDissatisfaction_vTempDif = 0;

      //執務者0なら計算しない
      if (stayNumber == 0) return;

      //温冷感による不満
      for (int i = 0; i < tenants.Length; i++)
        foreach (Occupant oc in tenants[i].Occupants)
          if (oc.Worker.StayInOffice)
            aveDissatisfaction_thermal += oc.OCModel.UncomfortableProbability;

      //上下温度分布による不満
      for (int i = 0; i < 2; i++)
      {
        for (int j = 0; j < 9; j++)
        {
          double tmpL = bModel.MultiRoom[i].Zones[j].Temperature;
          double tmpU = bModel.MultiRoom[i].Zones[j + 9].Temperature;
          double dT = Math.Max(0, tmpU - tmpL) / 1.35 * 1.0; //上下空間の距離をもとに足下と頭の高さの温度差を推定
          double pd = Math.Max(0, Math.Min(1, 1.0 / (1 + Math.Exp(5.76 - 0.856 * dT))));
          aveDissatisfaction_vTempDif += pd * tenants[i].GetStayWorkerNumber(bModel.MultiRoom[i].Zones[j]);
        }        
      }

      //ドラフトによる不満
      for (int i = 0; i < tenants.Length; i++)
      {
        for (int j = 0; j < tenants[i].Zones.Length; j++)
        {
          int occCold = 0;
          int ocStay = 0;
          ImmutableOccupant[] ocs = tenants[i].GetOccupants(tenants[i].Zones[j]);
          foreach (Occupant oc in ocs)
          {
            if (oc.Worker.StayInOffice)
            {
              ocStay++;
              if (
                oc.OCModel.Vote == Popolo.HumanBody.OccupantModel_Langevin.ASHRAE_Vote.SlightlyCool ||
                oc.OCModel.Vote == Popolo.HumanBody.OccupantModel_Langevin.ASHRAE_Vote.Cool ||
                oc.OCModel.Vote == Popolo.HumanBody.OccupantModel_Langevin.ASHRAE_Vote.Cold ||
                oc.OCModel.Vote == Popolo.HumanBody.OccupantModel_Langevin.ASHRAE_Vote.Neutral)
                occCold++; //中立から寒い側申告の人数を数える
            }
          }
          int oUnt;
          int iUnt = j < 5 ? j : j - 5;
          if (i == 0) oUnt = j < 5 ? 0 : 1;
          else oUnt = j < 5 ? 2 : 3;
          aveDissatisfaction_draft += ocStay * (occCold / (double)ocs.Length) * vrfs[oUnt].DissatisfiedRateByJet[iUnt];
        }
      }

      aveDissatisfaction_thermal /= stayNumber;
      aveDissatisfaction_draft /= stayNumber;
      aveDissatisfaction_vTempDif /= stayNumber;
    }

    /// <summary>ゾーンに滞在する人数を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンに滞在する人数</returns>
    public uint GetOccupantNumber(ImmutableZone zone)
    { return workerNumbers[zone]; }

    /// <summary>ゾーンの顕熱負荷[W]を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンの顕熱負荷[W]</returns>
    public double GetSensibleHeat(ImmutableZone zone)
    { return sHeatLoads[zone]; }

    /// <summary>ゾーンの潜熱負荷[W]を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンの潜熱負荷[W]</returns>
    public double GetLatentHeat(ImmutableZone zone)
    { return lHeatLoads[zone]; }

    /// <summary>乱数シードを再設定する</summary>
    /// <param name="seed">乱数シード</param>
    public void ResetRandomSeed(uint seed)
    {
      MersenneTwister rnd = new MersenneTwister(seed);
      foreach (Tenant tnt in tenants)
        tnt.ResetRandomSeed(rnd.Next());
    }

    public void OutputOccupantsInfo(string filePath)
    {
      using (StreamWriter sWriter = new StreamWriter(filePath))
      {
        sWriter.WriteLine("Tenant,Zone,First name,Last name,Age,Height,Weight,M/F,Job");
        for (int i = 0; i < tenants.Length; i++)
        {
          for (int j = 0; j < tenants[i].Zones.Length; j++)
          {
            ImmutableOccupant[] occs = tenants[i].GetOccupants(tenants[i].Zones[j]);
            for (int k = 0; k < occs.Length; k++)
            {
              ImmutableOccupant oc = occs[k];
              sWriter.WriteLine(
                tenants[i].Name + "," +
                tenants[i].Zones[j].Name + "," +
                oc.FirstName + "," +
                oc.LastName + "," +
                oc.Age + "," +
                oc.Height + "," +
                oc.Weight + "," +
                (oc.IsMale ? "M" : "F") + "," +
                oc.Worker.Job.ToString());
            }
          }
        }
      }
    }

    #endregion

  }

  #region 読み取り専用インターフェース

  /// <summary>読み取り専用オフィステナントリスト</summary>
  public interface ImmutableTenantList
  {

    /// <summary>テナントリストを取得する</summary>
    ImmutableTenant[] Tenants { get; }

    /// <summary>ゾーンに滞在する人数を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンに滞在する人数</returns>
    uint GetOccupantNumber(ImmutableZone zone);

    /// <summary>ゾーンの顕熱負荷[W]を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンの顕熱負荷[W]</returns>
    double GetSensibleHeat(ImmutableZone zone);

    /// <summary>ゾーンの潜熱負荷[W]を取得する</summary>
    /// <param name="zone">ゾーン</param>
    /// <returns>ゾーンの潜熱負荷[W]</returns>
    double GetLatentHeat(ImmutableZone zone);

  }

  #endregion

}
