using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using Popolo.BuildingOccupant;
using Popolo.Numerics;
using Popolo.ThermalLoad;
using Popolo.HumanBody;
using System.Reflection;
using PacketDotNet.Tcp;
using System.Net.NetworkInformation;

namespace Shizuku.Models
{
  /// <summary>建物滞在者</summary>
  /// <remarks>Jared Langevin et.al, Simulating the human-building interaction: Development and validation of an agent-based model of office occupant behaviors</remarks>
  [Serializable]
  public class Occupant : ImmutableOccupant
  {

    #region 定数宣言

    /// <summary>最大着衣量[clo]</summary>
    private const double MAX_CLO = 1.30;

    /// <summary>最小着衣量[clo]</summary>
    private const double MIN_CLO = 0.3;

    /// <summary>温冷感計算の代謝量はオフィス作業（1.1）に固定</summary>
    private const double MET = 1.1;

    /// <summary>温冷感計算の風速は0.1 m/sに固定</summary>
    private const double VELOCITY = 0.1;

    /// <summary>移動更新時間間隔[sec]</summary>
    private const int MOVE_UPDATE_SPAN = 30;

    /// <summary>熱的快適性の更新時間間隔[sec]</summary>
    private const int COMFORT_UPDATE_SPAN = 60;

    /// <summary>調整行動の更新間隔[sec]</summary>
    private const int COMFORT_ADJUST_SPAN = 60 * 15;

    /// <summary>コントローラの操作許可時のPMVボーナス</summary>
    private const double PMV_BONUS = 0.2;

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
    public uint Age { get { return (uint)Worker.Age; } }

    /// <summary>身長[m]を取得する</summary>
    public double Height { get; private set; }

    /// <summary>体重[kg]を取得する</summary>
    public double Weight { get; private set; }

    /// <summary>執務者行動モデルを取得する</summary>
    public OfficeTenant.ImmutableWorker Worker { get; }

    /// <summary>Langevinによる温冷感モデル</summary>
    public OccupantModel_Langevin OCModel { get; private set; }

    /// <summary>豪華ゲストか否か</summary>
    public bool IsSpecialCharacter { get; private set; } = false;

    /// <summary>着衣量[clo]を取得する</summary>
    public double CloValue
    {
      get
      {
        return mornClo + 
          (RollUpSleeves ? -0.08 : 0) + 
          (WearSweater ? 0.3 : 0);
      }
    }

    /// <summary>袖をまくりあげているか否か</summary>
    public bool RollUpSleeves { get; private set; } = false;

    /// <summary>セータを着ているか否か</summary>
    public bool WearSweater { get; private set; } = false;

    /// <summary>温度設定値を上げようとしているか否か</summary>
    public bool TryToRaiseTemperatureSP { get; private set; } = false;

    /// <summary>温度設定値を下げようとしているか否か</summary>
    public bool TryToLowerTemperatureSP { get; private set; } = false;

    /// <summary>自席のゾーンを取得する</summary>
    public ImmutableZone DeskZone { private set; get; }

    /// <summary>テナントを取得する</summary>
    public ImmutableTenant Tenant { private set; get; }

    /// <summary>推移確率行列</summary>
    private double[,] transProbs;

    /// <summary>現在滞在しているゾーンを取得する</summary>
    public ImmutableZone CurrentZone { private set; get; }

    /// <summary>一様乱数生成器</summary>
    private readonly MersenneTwister myRnd;

    /// <summary>正規乱数生成器</summary>
    private readonly NormalRandom myNRnd;

    /// <summary>前呼び出し時に滞在していたか否か</summary>
    private bool stayInOffice_lst = false;

    /// <summary>最終の移動計算日時</summary>
    private DateTime lastMove = new DateTime(3000, 1, 1);

    /// <summary>最終の人体モデル更新日時</summary>
    private DateTime lastOCcalc = new DateTime(3000, 1, 1);

    /// <summary>最終の環境調整行動日時</summary>
    private DateTime lastAdj = new DateTime(3000, 1, 1);

    /// <summary>滞在しているゾーンを取得する(0:外出, 1～:ゾーン番号+1)</summary>
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
    { get { return 1 + Array.IndexOf(Tenant.Zones, DeskZone); } }

    /// <summary>自席にいるか否か</summary>
    public bool StayAtDesk { get { return DeskZone == CurrentZone; } }
    
    /// <summary>朝の着衣量[Clo]</summary>
    private double mornClo = 1.0;

    /// <summary>夏季の中立申告値</summary>
    private double NeutralSensationInSummer = 0;

    /// <summary>冬季の中立申告値</summary>
    private double NeutralSensationInWinter = 0;

    /// <summary>コントローラを操作可能と考えているか否か</summary>
    public bool ThinkControllable { get; set; } = false;

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
    public Occupant
      (uint seed, OfficeTenant.ImmutableWorker worker, ImmutableTenant tenant, ImmutableZone deskZone)
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
      myNRnd = new NormalRandom(myRnd);
      int indx;
      if (worker.IsMale) indx = 0;
      else indx = 5;
      if (worker.Age < 30) indx += 0;
      else if (worker.Age < 40) indx += 1;
      else if (worker.Age < 50) indx += 2;
      else if (worker.Age < 60) indx += 3;
      else indx += 4;
      Height = Math.Round(myNRnd.NextDouble() * SD_HEIGHT[indx] + AVE_HEIGHT[indx], 1);
      Weight = Math.Round(myNRnd.NextDouble() * SD_WEIGHT[indx] + AVE_WEIGHT[indx], 1);

      //温冷感モデル作成
      OCModel = new OccupantModel_Langevin(myRnd.Next(), true);
      NeutralSensationInSummer = 0.5 * (OCModel.HighAcceptableSensationInSummer + OCModel.LowAcceptableSensationInSummer);
      NeutralSensationInWinter = 0.5 * (OCModel.HighAcceptableSensationInWinter + OCModel.LowAcceptableSensationInWinter);

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
    public void Move(DateTime dTime)
    {
      //初回の処理
      if (dTime < lastMove)
        lastMove = dTime;

      //入館時
      if (!stayInOffice_lst && Worker.StayInOffice)
      {
        stayInOffice_lst = true;

        //フリーアドレスの場合には毎入館時に自席を更新
        if (Tenant.IsNonTerritorialOffice)
        {
          double sum = 0;
          foreach (ImmutableZone zn in Tenant.Zones) sum += zn.FloorArea;
          double rnd = myRnd.NextDouble();
          for (int i = 0; i < Tenant.Zones.Length; i++)
          {
            double delta = Tenant.Zones[i].FloorArea / sum;
            if (rnd < delta)
            {
              ResetDesk(Tenant.Zones[i]);
              break;
            }
            else rnd -= delta;
          }
        }

        CurrentZone = DeskZone;
        lastMove = dTime;
      }

      //退館時
      if (stayInOffice_lst && !Worker.StayInOffice)
      {
        stayInOffice_lst = false;

        CurrentZone = null;
        lastMove = dTime;
      }

      //入館済：ゾーン間移動のみ
      if(CurrentZone != null)
      {
        while (lastMove.AddSeconds(MOVE_UPDATE_SPAN) <= dTime)
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

          lastMove = lastMove.AddSeconds(MOVE_UPDATE_SPAN);
        }
      }
    }

    /// <summary>温冷感を更新して調整行動を取る</summary>
    /// <param name="dTime"></param>
    public void UpdateComfort(DateTime dTime)
    {
      //初回の処理
      if (dTime < lastOCcalc)
        lastOCcalc = lastAdj = dTime;

      //人体熱収支モデルを更新して不満を計算
      if (lastOCcalc.AddSeconds(COMFORT_UPDATE_SPAN) <= dTime)
      {
        lastOCcalc = dTime; //最終の更新日時を保存

        //出社中のみ計算する
        if ((DeskZone.MultiRoom.CurrentDateTime < Worker.ArriveTime) ||
          (Worker.LeaveTime < DeskZone.MultiRoom.CurrentDateTime)) return;

        //熱環境情報を取得
        Tenant.GetZoneInfo
          (CurrentZone, out double dbt, out double rhmd, out double mrt);

        //温冷感モデルを更新（コントローラの操作可能性に応じてPMVボーナスを付与）
        double pmv = ThermalComfort.GetPMV(dbt, mrt, rhmd, VELOCITY, CloValue, MET, 0); //環境のPMV
        if (ThinkControllable)
        {
          double sNeutral = OCModel.IsSummer ? NeutralSensationInSummer : NeutralSensationInWinter;
          if (pmv < sNeutral) pmv += Math.Min(PMV_BONUS, sNeutral - pmv);
          else pmv -= Math.Min(PMV_BONUS, pmv - sNeutral);
        }
        OCModel.Update(pmv);
        
        //調整行動を取る
        TryToRaiseTemperatureSP = TryToLowerTemperatureSP = false;
        if (lastAdj.AddSeconds(COMFORT_ADJUST_SPAN) <= dTime)
        {
          if (OCModel.UncomfortablyCold)
          {
            if (RollUpSleeves) RollUpSleeves = false;
            else if (!WearSweater) WearSweater = true;
            else if(StayAtDesk) TryToRaiseTemperatureSP = true;
            lastAdj = dTime;
          }
          else if (OCModel.UncomfortablyWarm)
          {
            if (!RollUpSleeves) RollUpSleeves = true;
            else if (WearSweater) WearSweater = false;
            else if (StayAtDesk) TryToLowerTemperatureSP = true;
            lastAdj = dTime;
          }
        }
      }
    }


    /// <summary>基準着衣量[clo]を更新する</summary>
    public void UpdateDailyCloValue()
    {
      OCModel.IsSummer = isSummer();
      int wPref = OCModel.IsSummer ?
        (0 < OCModel.HighAcceptableSensationInSummer + OCModel.LowAcceptableSensationInSummer ? 1 : 0) :
        (0 < OCModel.HighAcceptableSensationInWinter + OCModel.LowAcceptableSensationInWinter ? 1 : 0);
      double lgMClo = -0.91 - 0.01 * DeskZone.MultiRoom.OutdoorTemperature + 0.14 * wPref + 0.71 * mornClo + myNRnd.NextDouble() * 0.24;
      mornClo = Math.Round(Math.Max(MIN_CLO, Math.Min(MAX_CLO, Math.Exp(lgMClo))), 3);
      WearSweater = false;
      RollUpSleeves = false;
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

    /// <summary>夏か否かを取得する</summary>
    /// <returns></returns>
    private bool isSummer()
    {
      return 5 <= DeskZone.MultiRoom.CurrentDateTime.Month && DeskZone.MultiRoom.CurrentDateTime.Month <= 10;
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

    /// <summary>執務者行動モデルを取得する</summary>
    OfficeTenant.ImmutableWorker Worker { get; }

    /// <summary>豪華ゲストか否か</summary>
    bool IsSpecialCharacter { get; }

    /// <summary>着衣量[clo]を取得する</summary>
    double CloValue { get; }

    /// <summary>袖をまくりあげているか否か</summary>
    bool RollUpSleeves { get; }

    /// <summary>セータを着ているか否か</summary>
    bool WearSweater { get; }

    /// <summary>自席のゾーンを取得する</summary>
    ImmutableZone DeskZone {  get; }

    /// <summary>テナントを取得する</summary>
    ImmutableTenant Tenant { get; }

    /// <summary>現在滞在しているゾーンを取得する</summary>
    ImmutableZone CurrentZone { get; }

    /// <summary>滞在しているゾーンを取得する(0:外出,1～:ゾーン番号+1)</summary>
    int StayZoneNumber { get; }

    /// <summary>自席ゾーンを取得する</summary>
    int DeskZoneNumber { get; }
  }

  #endregion

}
