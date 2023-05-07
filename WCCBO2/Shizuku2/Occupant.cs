using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using Popolo.BuildingOccupant;
using Popolo.Numerics;
using Popolo.ThermalLoad;
using Popolo.HumanBody;
using Popolo.ThermophysicalProperty;
using System.Reflection;

namespace Shizuku.Models
{
  /// <summary>建物滞在者</summary>
  [Serializable]
  public class Occupant : ImmutableOccupant
  {

    #region 定数宣言

    /// <summary>日中に着脱可能な着衣量[clo]</summary>
    private const double JACKET_CLO = 0.10;

    #endregion

    #region static変数

    /// <summary>名前(姓)リスト</summary>
    private static string[] lastNames;

    /// <summary>男性の名前(名)リスト</summary>
    private static string[] firstNames_M;

    /// <summary>女性の名前(名)リスト</summary>
    private static string[] firstNames_F;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>名前(姓)を取得する</summary>
    public string LastName { get; private set; }

    /// <summary>名前(名)を取得する</summary>
    public string FirstName { get; private set; }

    /// <summary>男性か否かを取得する</summary>
    public bool IsMale { get { return Worker.IsMale; } }

    /// <summary>年齢を取得する</summary>
    public uint Age { get { return tnModel.Age; } }

    /// <summary>身長[m]を取得する</summary>
    public double Height { get { return tnModel.Height; } }

    /// <summary>体重[kg]を取得する</summary>
    public double Weight { get { return tnModel.Weight; } }

    /// <summary>執務者行動モデルを取得する</summary>
    public OfficeTenant.ImmutableWorker Worker { get; }

    /// <summary>TwoNodeモデルを取得する</summary>
    public ImmutableTwoNodeModel TNModel { get { return tnModel; } }

    /// <summary>Langevinによる温冷感モデル</summary>
    public OccupantModel_Langevin OCModel { get; private set; }

    /// <summary>豪華ゲストか否か</summary>
    public bool IsSpecialCharacter { get; private set; } = false;

    /// <summary>基準着衣量[clo]</summary>
    private double rCloVal = 0.7;

    /// <summary>着衣量[clo]を取得する</summary>
    public double CloValue
    {
      get
      {
        if (UseJacket) return rCloVal + JACKET_CLO;
        else return rCloVal;
      }
    }

    /// <summary>上着を着ているか否か</summary>
    public bool UseJacket { get; private set; } = true;

    /// <summary>最大着衣量[clo]</summary>
    private double maxCloVal = 1.00 - JACKET_CLO;

    /// <summary>最小着衣量[clo]</summary>
    private double minCloVal = 0.5;

    /// <summary>温冷感申告値[-]の積算値</summary>
    private double tsSum = 0;

    /// <summary>自席のゾーンを取得する</summary>
    public ImmutableZone DeskZone { private set; get; }

    /// <summary>テナントを取得する</summary>
    public ImmutableTenant Tenant { private set; get; }

    /// <summary>推移確率行列</summary>
    private double[,] transProbs;

    /// <summary>現在滞在しているゾーンを取得する</summary>
    public ImmutableZone CurrentZone { private set; get; }

    /// <summary>乱数生成器</summary>
    private MersenneTwister myRnd;

    /// <summary>帰社時か否か(EVホール滞在に関係)</summary>
    private bool comingIn;

    /// <summary>外出時か否か(EVホール滞在に関係)</summary>
    private bool goingOut;

    /// <summary>前呼び出し時に滞在していたか否か</summary>
    private bool stayInOffice_lst = false;

    /// <summary>入退館時の基準時刻</summary>
    private DateTime crtTime;

    /// <summary>最終の移動計算日時</summary>
    private DateTime lastMove = new DateTime(3000, 1, 1);

    /// <summary>最終の人体モデル更新日時</summary>
    private DateTime lastOCcalc;

    /// <summary>TwoNodeモデル</summary>
    private TwoNodeModel tnModel;

    /// <summary>温冷感申告値を取得する</summary>
    public double ThermalSensation { private set; get; }

    /// <summary>不満か否かを取得する</summary>
    public bool Dissatisfied { private set; get; } = false;

    /// <summary>滞在しているゾーンを取得する(0:外出, 1～:ゾーン番号+3)</summary>
    public int StayZoneNumber
    {
      get
      {
        if (CurrentZone == null) return 0;
        else return 1 + Array.IndexOf(Tenant.Zones, CurrentZone);
      }
    }

    /// <summary>自席ゾーンを取得する</summary>
    public int DeskZoneNumber
    { get { return 3 + Array.IndexOf(Tenant.Zones, DeskZone); } }
    
    /// <summary>光環境にもとづく不満発生率を設定・取得する</summary>
    public double PPD_Lighting { get; private set; }
    
    /// <summary>空気質環境にもとづく不満発生率を設定・取得する</summary>
    public double PPD_AirState { get; private set; }

    /// <summary>熱的中立時の平均皮膚温度[C]を取得する</summary>
    public double NeutralSkinTemperature { get; private set; }

    /// <summary>満足・不満の閾値を取得する</summary>
    public double ThermalThreshold { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>staticコンストラクタ</summary>
    static Occupant()
    {
      var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.Names.txt");
      using (StreamReader srNames = new StreamReader(stream))
      {
        lastNames = srNames.ReadLine().Split(',');
        firstNames_M = srNames.ReadLine().Split(',');
        firstNames_F = srNames.ReadLine().Split(',');
      }
    }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="worker">執務者モデル</param>
    /// <param name="tenant">テナント</param>
    /// <param name="deskZone">自席のゾーン</param>
    public Occupant(uint seed, OfficeTenant.ImmutableWorker worker, ImmutableTenant tenant, ImmutableZone deskZone)
    {
      transProbs = new double[tenant.Zones.Length, tenant.Zones.Length];
      this.Worker = worker;
      this.Tenant = tenant;
      this.myRnd = new MersenneTwister(seed);

      double[] AVE_HEIGHT = new double[] { 171.90, 172.04, 171.49, 170.31, 167.39, 158.56, 158.82, 158.67, 157.17, 154.38 };  //平均身長
      double[] SD_HEIGHT = new double[] { 5.64, 5.64, 5.65, 5.50, 5.35, 5.29, 5.10, 5.19, 5.04, 4.89 }; //身長標準偏差
      double[] AVE_WEIGHT = new double[] { 66.34, 68.19, 69.38, 68.14, 65.21, 50.60, 51.35, 52.71, 53.23, 52.24 };  //平均体重
      double[] SD_WEIGHT = new double[] { 9.23, 9.24, 9.45, 8.84, 8.01, 5.78, 6.02, 6.21, 6.58, 6.94 }; //体重標準偏差

      //身長体重初期化
      NormalRandom nRnd = new NormalRandom(myRnd);
      int indx;
      if (worker.IsMale) indx = 0;
      else indx = 5;
      if (worker.Age < 30) indx += 0;
      else if (worker.Age < 40) indx += 1;
      else if (worker.Age < 50) indx += 2;
      else if (worker.Age < 60) indx += 3;
      else indx += 4;
      double height = Math.Round(nRnd.NextDouble() * SD_HEIGHT[indx] + AVE_HEIGHT[indx], 1);
      double weight = Math.Round(nRnd.NextDouble() * SD_WEIGHT[indx] + AVE_WEIGHT[indx], 1);

      //温冷感の特性
      NeutralSkinTemperature = 33.883 + nRnd.NextDouble() * 0.436;
      LogNormalRandom lRnd = new LogNormalRandom(myRnd.Next(), 1.014, 0.325);
      ThermalThreshold = lRnd.NextDouble();

      //TwoNodeモデル作成
      int ageByas = (int)(10 * myRnd.NextDouble() - 5);
      tnModel = new TwoNodeModel((uint)(worker.Age + ageByas), Worker.IsMale, 0.01 * height, weight);

      //温冷感モデル作成
      OCModel = new OccupantModel_Langevin(myRnd.Next(), true);

      //名前初期化
      if (IsMale)
      {
        int rnNum = (int)((firstNames_M.Length - 1) * myRnd.NextDouble());
        FirstName = firstNames_M[rnNum];
      }
      else
      {
        int rnNum = (int)((firstNames_F.Length - 1) * myRnd.NextDouble());
        FirstName = firstNames_F[rnNum];
      }
      LastName = lastNames[(int)((lastNames.Length - 1) * myRnd.NextDouble())];

      //自席のゾーンをリセットする
      ResetDesk(deskZone);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>自席のゾーンをリセットする</summary>
    /// <param name="deskZone">自席のゾーン</param>
    public void ResetDesk(ImmutableZone deskZone)
    {
      const double P_MM = 0.97;  //自席→自席の推移確率
      const double P_CC = 0.85;  //訪問先→訪問先の推移確率
      const double P_CM = 0.10;  //訪問先→自席の推移確率

      this.DeskZone = deskZone;

      //推移確率行列更新処理
      int deskIndex = Array.IndexOf(Tenant.Zones, DeskZone);
      for (int i = 0; i < Tenant.Zones.Length; i++)
      {
        if (i == deskIndex)
        {
          transProbs[i, i] = P_MM;

          double areaSum = 0;
          for (int j = 0; j < Tenant.Zones.Length; j++)
            if (j != deskIndex) areaSum += Tenant.Zones[j].FloorArea;
          for (int j = 0; j < Tenant.Zones.Length; j++)
            if (j != deskIndex) transProbs[i, j] = (1 - P_MM) * Tenant.Zones[j].FloorArea / areaSum;
        }
        else
        {
          transProbs[i, deskIndex] = P_CM;
          transProbs[i, i] = P_CC;

          double areaSum = 0;
          for (int j = 0; j < Tenant.Zones.Length; j++)
            if (j != deskIndex && i != j) areaSum += Tenant.Zones[j].FloorArea;
          for (int j = 0; j < Tenant.Zones.Length; j++)
            if (j != deskIndex && i != j) transProbs[i, j] = (1 - (P_CC + P_CM)) * Tenant.Zones[j].FloorArea / areaSum;
        }
      }
    }

    /// <summary>滞在情報を更新する</summary>
    /// <param name="dTime">現在の日時</param>
    /// <param name="tStep">計算時間間隔</param>
    public void UpdateStatus(DateTime dTime, double tStep)
    {
      //初回の処理
      if (dTime < lastMove)
        lastMove = lastOCcalc = dTime;

      //人体熱収支モデルを更新して不満を計算//60secに一度
      if (lastOCcalc.AddSeconds(60) <= dTime)
      {
        double stp = (dTime - lastOCcalc).TotalSeconds;
        updateComfort(stp);
        lastOCcalc = dTime;
      }

      //入館確認
      if (!stayInOffice_lst && Worker.StayInOffice)
      {
        stayInOffice_lst = true;
        crtTime = dTime;
        comingIn = true;
      }
      //退館確認
      else if (stayInOffice_lst && !Worker.StayInOffice)
      {
        stayInOffice_lst = false;
        crtTime = dTime;
        goingOut = true;
      }

      //入館移動中
      if (comingIn)
      {
        //フリーアドレスの場合には毎入館時に自席を更新
        if (Tenant.IsNonTerritorialOffice)
        {
          double sum = 0;
          foreach (ImmutableZone zn in Tenant.Zones) sum += zn.AirMass;
          double rnd = myRnd.NextDouble();
          for (int i = 0; i < Tenant.Zones.Length; i++)
          {
            double delta = Tenant.Zones[i].AirMass / sum;
            if (rnd < delta)
            {
              ResetDesk(Tenant.Zones[i]);
              break;
            }
            else rnd -= delta;
          }
        }

        CurrentZone = DeskZone;
        comingIn = false;
        lastMove = dTime;
      }
      //退館移動中
      else if (goingOut)
      {
        CurrentZone = null;
        goingOut = false;
        lastMove = dTime;
      }
      //入館済：ゾーン間移動のみ
      else if(CurrentZone != null)
      {
        while (lastMove.AddSeconds(30) <= dTime)
        {
          int curZnIndex = Array.IndexOf(Tenant.Zones, CurrentZone);
          double rnd = myRnd.NextDouble();

          for (int i = 0; i < Tenant.Zones.Length; i++)
          {
            if (rnd < transProbs[curZnIndex, i])
            {
              CurrentZone = Tenant.Zones[i];
              break;
            }
            rnd -= transProbs[curZnIndex, i];
          }

          lastMove = lastMove.AddSeconds(30);
        }
      }
    }

    /// <summary>快・不快モデルを更新する</summary>
    /// <param name="timeStep">計算時間間隔</param>
    private void updateComfort(double timeStep)
    {
      //時間が経過していなければ無視
      if (timeStep <= 0) return;

      //出社中のみ計算する
      if ((DeskZone.MultiRoom.CurrentDateTime < Worker.ArriveTime) || 
        (Worker.LeaveTime < DeskZone.MultiRoom.CurrentDateTime)) return;

      //熱環境情報を取得
      Tenant.GetZoneInfo
        (CurrentZone, out double dbt, out double rhmd, out double mrt, out double co2lvl, out double dirIll);

      //温冷感モデルを更新
      double pmv = ThermalComfort.GetPMV(dbt, mrt, rhmd, 0.1, CloValue, 1.1, 0);
      OCModel.Update(pmv);

      //空気質にもとづく不満発生率
      PPD_AirState = 1d / (1 + Math.Exp((0.0012 - co2lvl) / 0.000065));  //CO2濃度900ppmで不満1%、1500ppmで不満99%となるようにロジスティック関数で表現

      //窓面光束発散度にもとづく不満発生率
      PPD_Lighting = dirIll;  //直達日射が床面に入射する面積比率で不満発生

      //温熱感・CO2濃度・光束発散度の最大を閾値として不満を判定
      Dissatisfied = myRnd.NextDouble() < 
          Math.Max(OCModel.UncomfortableProbability, Math.Max(PPD_Lighting, PPD_AirState));

      if (CurrentZone != null) tsSum += (OCModel.UncomfortablyWarmProbability - OCModel.UncomfortablyColdProbability);
    }

    /// <summary>基準着衣量[clo]を更新する</summary>
    public void UpdateDailyCloValue()
    {
      //前出社日が暑かった場合
      if (0 < tsSum)
        rCloVal = Math.Max(minCloVal, rCloVal - 0.05);
      //前出社日が寒かった場合
      else
        rCloVal = Math.Min(maxCloVal, rCloVal + 0.05);
      //温冷感申告値をリセット
      tsSum = 0;
    }

    /// <summary>豪華ゲストに変更する</summary>
    /// <param name="firstName">名前(名)</param>
    /// <param name="lastName">名前(姓)</param>
    internal void makeSpecialCharacter(string firstName, string lastName)
    {
      this.FirstName = firstName;
      this.LastName = lastName;

      IsSpecialCharacter = true;
    }

    /// <summary>温冷感申告値を計算する</summary>
    /// <param name="skinTemperature_n">標準化された皮膚温度[C]</param>
    /// <param name="diffSkinTemperature">皮膚温度微分値[K/sec]</param>
    /// <returns>温冷感申告値</returns>
    /// <remarks>高田暁:非定常状態における全身温冷感予測に関する研究,H24年度 AIJ近畿支部研究発表会</remarks>
    private double getThermalSensation(double skinTemperature_n, double diffSkinTemperature)
    {
      return 11.018 -12.511 * (0.5 + Math.Atan((skinTemperature_n - 2.430) / (-2.791)) / Math.PI) 
        -3.969 * (0.5 + Math.Atan((diffSkinTemperature + 0.035) / (-0.203)) / Math.PI);
    }

    /// <summary>定常状態における温冷感申告値を計算する</summary>
    /// <param name="dbTemp">乾球温度[C]</param>
    /// <param name="relHumid">相対湿度[%]</param>
    /// <param name="mrt">平均放射温度[C]</param>
    /// <param name="cloValue">着衣量[Clo]</param>
    /// <returns>定常状態における温冷感申告値</returns>
    public double GetSteadyStateThermalSensation
      (double dbTemp, double relHumid, double mrt, double cloValue)
    {
      //定常状態まで進める
      double lstTsk = -50;
      while (0.001 < Math.Abs(tnModel.SkinTemperature - lstTsk))
      {
        lstTsk = tnModel.SkinTemperature;
        tnModel.UpdateState(600, dbTemp, mrt, relHumid, 0.1, cloValue, 1.1, 0, 101.325);
      }

      //return lstTsk;
      return getThermalSensation(lstTsk - NeutralSkinTemperature, 0);
    }

    #endregion

  }

  #region 読み取り専用Occupantクラス
  
  /// <summary>読み取り専用のオフィス滞在者</summary>
  public interface ImmutableOccupant
  {
    /// <summary>名前(姓)を取得する</summary>
    string LastName { get; }

    /// <summary>名前(名)を取得する</summary>
    string FirstName { get; }

    /// <summary>男性か否かを取得する</summary>
    bool IsMale { get; }

    /// <summary>年齢を取得する</summary>
    uint Age { get; }

    /// <summary>身長[m]を取得する</summary>
    double Height { get; }

    /// <summary>体重[kg]を取得する</summary>
    double Weight { get; }

    /// <summary>温冷感申告値[-]を取得する</summary>
    double ThermalSensation { get; }

    /// <summary>不満か否かを取得する</summary>
    bool Dissatisfied { get; }

    /// <summary>執務者行動モデルを取得する</summary>
    OfficeTenant.ImmutableWorker Worker { get; }

    /// <summary>TwoNodeモデルを取得する</summary>
    ImmutableTwoNodeModel TNModel { get; }

    /// <summary>豪華ゲストか否か</summary>
    bool IsSpecialCharacter { get; }

    /// <summary>着衣量[clo]を取得する</summary>
    double CloValue { get; }

    /// <summary>上着を着ているか否か</summary>
    bool UseJacket { get; }

    /// <summary>自席のゾーンを取得する</summary>
    ImmutableZone DeskZone {  get; }

    /// <summary>テナントを取得する</summary>
    ImmutableTenant Tenant { get; }

    /// <summary>現在滞在しているゾーンを取得する</summary>
    ImmutableZone CurrentZone { get; }

    /// <summary>滞在しているゾーンを取得する(0:外出, 1:エントランス, 2:ホール, 3～:ゾーン番号+3)</summary>
    int StayZoneNumber { get; }

    /// <summary>自席ゾーンを取得する</summary>
    int DeskZoneNumber { get; }

    /// <summary>光環境にもとづく不満発生率を取得する</summary>
    double PPD_Lighting { get; }

    /// <summary>空気質環境にもとづく不満発生率を取得する</summary>
    double PPD_AirState { get; }

    /// <summary>熱的中立時の平均皮膚温度[C]を取得する</summary>
    double NeutralSkinTemperature { get; }

    /// <summary>満足・不満の閾値を取得する</summary>
    double ThermalThreshold { get; }

    /// <summary>定常状態における温冷感申告値を計算する</summary>
    /// <param name="dbTemp">乾球温度[C]</param>
    /// <param name="relHumid">相対湿度[%]</param>
    /// <param name="mrt">平均放射温度[C]</param>
    /// <param name="cloValue">着衣料[Clo]</param>
    /// <returns>定常状態における温冷感申告値</returns>
    /// <remarks>状態が変わってしまうので、Immutableでは無い!!</remarks>
    double GetSteadyStateThermalSensation
      (double dbTemp, double relHumid, double mrt, double cloValue);

  }

  #endregion

}
