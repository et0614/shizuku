using Popolo.ThermalLoad;
using Popolo.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  public static class BuildingMaker
  {

    #region 定数宣言

    /// <summary>換気量[CMH/m2]</summary>
    /// <remarks>25CMH/人,0.2人/m2のため</remarks>
    public const double VENT_RATE = 5.0;

    /// <summary>漏気量[回/h]</summary>
    public const double LEAK_RATE = 0.2;

    /// <summary>下部空間の高さ[m]</summary>
    public const double L_ZONE_HEIGHT = 1.7;

    /// <summary>上部空間の高さ[m]</summary>
    public const double U_ZONE_HEIGHT = 1.0;

    /// <summary>家具の熱容量（空気の熱容量に対する倍数）[-]</summary>
    public const double HCAP_FURNITURE = 10;

    #endregion

    public static BuildingThermalModel Make()
    {
      //傾斜面の作成(四方位)//////////////
      Incline incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      Incline incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      Incline incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      Incline incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

      //壁構成を作成////////////////////////
      WallLayer[] exWL = new WallLayer[6];  //外壁一般部分
      exWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);
      exWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);
      exWL[4] = new AirGapLayer("非密閉中空層", false, 0.05);
      exWL[5] = new WallLayer("石膏ボード", 0.22, 830, 0.008);

      WallLayer[] exbmWL = new WallLayer[4];  //外壁梁部分
      exbmWL[0] = new WallLayer("タイル", 1.3, 2000, 0.010);
      exbmWL[1] = new WallLayer("セメント・モルタル", 1.5, 1600, 0.025);
      exbmWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.750);
      exbmWL[3] = new WallLayer("押出ポリスチレンフォーム1種", 0.040, 33, 0.025);

      WallLayer[] flrWL = new WallLayer[3];  //床
      flrWL[0] = new WallLayer("ビニル系床材", 0.190, 2000, 0.003);
      flrWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      flrWL[2] = new WallLayer("コンクリート", 1.6, 2000, 0.150);

      WallLayer[] clWL = new WallLayer[2];  //天井
      clWL[0] = new WallLayer("石膏ボード", 0.220, 830, 0.009);
      clWL[1] = new WallLayer("ロックウール化粧吸音板", 0.064, 290, 0.015);

      WallLayer[] inWL = new WallLayer[3];  //内壁
      inWL[0] = new WallLayer("石膏ボード", 0.220, 830, 0.012);
      inWL[1] = new AirGapLayer("非密閉中空層", false, 0.05);
      inWL[2] = new WallLayer("石膏ボード", 0.220, 830, 0.012);

      WallLayer[] inSWL = new WallLayer[1];  //内壁_テナント間仕切用仮想壁
      inSWL[0] = new WallLayer("仮想壁", 10000, 1, 0.01);

      WallLayer[] inUDWL = new WallLayer[1];
      inUDWL[0] = new WallLayer("仮想天井", 0.001 * 1, 0.001, 0.001 * 1); //内壁_上下空間接続用:(1.0W/m2K)
      
      //ゾーンを作成/////////////////////////
      Zone[] znSs = new Zone[25];
      Zone[] znNs = new Zone[29];
      double[] SZN_AREAS = new double[] { 26, 26, 26, 65, 65, 65, 20, 20, 26, 50, 50, 65 };
      double[] NZN_AREAS = new double[] { 26, 26, 26, 65, 65, 65, 20, 20, 26, 26, 50, 50, 65, 65 };
      double sznASum = 0;
      double nznASum = 0;
      for (int i = 0; i < SZN_AREAS.Length; i++)
      {
        sznASum += SZN_AREAS[i];
        znSs[i] = new Zone("S" + i, SZN_AREAS[i] * L_ZONE_HEIGHT * 1.2, SZN_AREAS[i]);
        znSs[i + 12] = new Zone("S" + i + "_Up", SZN_AREAS[i] * U_ZONE_HEIGHT * 1.2, SZN_AREAS[i]);
      }
      for (int i = 0; i < NZN_AREAS.Length; i++)
      {
        nznASum += NZN_AREAS[i];
        znNs[i] = new Zone("N" + i, NZN_AREAS[i] * L_ZONE_HEIGHT * 1.2, NZN_AREAS[i]);
        znNs[i + 14] = new Zone("N" + i + "_Up", NZN_AREAS[i] * U_ZONE_HEIGHT * 1.2, NZN_AREAS[i]);
      }
      znSs[24] = new Zone("S_Attic", sznASum * 1.5 * 1.2, sznASum);
      znNs[28] = new Zone("N_Attic", nznASum * 1.5 * 1.2, nznASum);

      //壁体の作成***************************************************************************************
      Wall[] walls = new Wall[135];
      const double WAL_HEIGHT = 0.7;
      const double WIN_HEIGHT_HIGH = 1.0;
      const double WIN_HEIGHT_LOW = 1.0;
      const double CEL_HEIGHT = 1.5;
      //外壁（南）
      walls[0] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[1] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[2] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[3] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[4] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[5] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[6] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[7] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[8] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[9] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[10] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[11] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      //外壁（南西）
      walls[12] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[13] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[14] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[15] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //外壁（北）
      walls[16] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[17] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[18] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[19] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[20] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[21] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[22] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[23] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[24] = new Wall(5.0 * WAL_HEIGHT, exWL);
      walls[25] = new Wall(5.0 * CEL_HEIGHT, exbmWL);
      walls[26] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[27] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      walls[28] = new Wall(6.5 * WAL_HEIGHT, exWL);
      walls[29] = new Wall(6.5 * CEL_HEIGHT, exbmWL);
      //外壁（北西）
      walls[30] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[31] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[32] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[33] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //外壁（北東）
      walls[34] = new Wall(4.0 * WAL_HEIGHT, exWL);
      walls[35] = new Wall(4.0 * CEL_HEIGHT, exbmWL);
      walls[36] = new Wall(10.0 * WAL_HEIGHT, exWL);
      walls[37] = new Wall(10.0 * CEL_HEIGHT, exbmWL);
      //内壁*********************************
      //南側内壁
      walls[38] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[39] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[40] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[41] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[42] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[43] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[44] = new Wall(4.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL); //WC側1
      walls[45] = new Wall(10.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL); //WC側2
      //北側内壁
      walls[46] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[47] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[48] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[49] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[50] = new Wall(5.0 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[51] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      walls[52] = new Wall(6.5 * (WAL_HEIGHT + WIN_HEIGHT_LOW + CEL_HEIGHT), inWL);
      //南側床
      walls[53] = new Wall(26, flrWL);
      walls[54] = new Wall(26, flrWL);
      walls[55] = new Wall(26, flrWL);
      walls[56] = new Wall(65, flrWL);
      walls[57] = new Wall(65, flrWL);
      walls[58] = new Wall(65, flrWL);
      walls[59] = new Wall(20, flrWL);
      walls[60] = new Wall(20, flrWL);
      walls[61] = new Wall(26, flrWL);
      walls[62] = new Wall(50, flrWL);
      walls[63] = new Wall(50, flrWL);
      walls[64] = new Wall(65, flrWL);
      //北側床
      walls[65] = new Wall(26, flrWL);
      walls[66] = new Wall(26, flrWL);
      walls[67] = new Wall(26, flrWL);
      walls[68] = new Wall(65, flrWL);
      walls[69] = new Wall(65, flrWL);
      walls[70] = new Wall(65, flrWL);
      walls[71] = new Wall(20, flrWL);
      walls[72] = new Wall(20, flrWL);
      walls[73] = new Wall(26, flrWL);
      walls[74] = new Wall(26, flrWL);
      walls[75] = new Wall(50, flrWL);
      walls[76] = new Wall(50, flrWL);
      walls[77] = new Wall(65, flrWL);
      walls[78] = new Wall(65, flrWL);
      //南側天井
      walls[79] = new Wall(26, clWL);
      walls[80] = new Wall(26, clWL);
      walls[81] = new Wall(26, clWL);
      walls[82] = new Wall(65, clWL);
      walls[83] = new Wall(65, clWL);
      walls[84] = new Wall(65, clWL);
      walls[85] = new Wall(20, clWL);
      walls[86] = new Wall(20, clWL);
      walls[87] = new Wall(26, clWL);
      walls[88] = new Wall(50, clWL);
      walls[89] = new Wall(50, clWL);
      walls[90] = new Wall(65, clWL);
      //北側天井
      walls[91] = new Wall(26, clWL);
      walls[92] = new Wall(26, clWL);
      walls[93] = new Wall(26, clWL);
      walls[94] = new Wall(65, clWL);
      walls[95] = new Wall(65, clWL);
      walls[96] = new Wall(65, clWL);
      walls[97] = new Wall(20, clWL);
      walls[98] = new Wall(20, clWL);
      walls[99] = new Wall(26, clWL);
      walls[100] = new Wall(26, clWL);
      walls[101] = new Wall(50, clWL);
      walls[102] = new Wall(50, clWL);
      walls[103] = new Wall(65, clWL);
      walls[104] = new Wall(65, clWL);
      //南側上下下層間仕切り
      walls[105] = new Wall(26, inUDWL);
      walls[106] = new Wall(26, inUDWL);
      walls[107] = new Wall(26, inUDWL);
      walls[108] = new Wall(65, inUDWL);
      walls[109] = new Wall(65, inUDWL);
      walls[110] = new Wall(65, inUDWL);
      walls[111] = new Wall(20, inUDWL);
      walls[112] = new Wall(20, inUDWL);
      walls[113] = new Wall(26, inUDWL);
      walls[114] = new Wall(50, inUDWL);
      walls[115] = new Wall(50, inUDWL);
      walls[116] = new Wall(65, inUDWL);
      //北側上下下層間仕切り
      walls[117] = new Wall(26, inUDWL);
      walls[118] = new Wall(26, inUDWL);
      walls[119] = new Wall(26, inUDWL);
      walls[120] = new Wall(65, inUDWL);
      walls[121] = new Wall(65, inUDWL);
      walls[122] = new Wall(65, inUDWL);
      walls[123] = new Wall(20, inUDWL);
      walls[124] = new Wall(20, inUDWL);
      walls[125] = new Wall(26, inUDWL);
      walls[126] = new Wall(26, inUDWL);
      walls[127] = new Wall(50, inUDWL);
      walls[128] = new Wall(50, inUDWL);
      walls[129] = new Wall(65, inUDWL);
      walls[130] = new Wall(65, inUDWL);
      //テナント間間仕切り
      walls[131] = new Wall(4.0 * CEL_HEIGHT, inWL); //南側事務室南
      walls[132] = new Wall(10.0 * CEL_HEIGHT, inWL); //南側事務室北
      walls[133] = new Wall(10.0 * CEL_HEIGHT, inWL); //北側事務室北
      walls[134] = new Wall(4.0 * CEL_HEIGHT, inWL); //北側事務室南

      //壁の初期化
      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].RadiativeCoefficientF = walls[i].RadiativeCoefficientB = 5;
        string nm = walls[i].Layers[0].Name;
        if (nm == "コンクリート" || nm == "タイル") walls[i].ConvectiveCoefficientF = 18;
        else walls[i].ConvectiveCoefficientF = 4;
        walls[i].ConvectiveCoefficientB = 4;
        walls[i].Initialize(20);
        if (nm == "仮想天井")
        {
          walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.0;
          walls[i].ConvectiveCoefficientF = walls[i].ConvectiveCoefficientB = 10000;
        }
      }

      //窓を作成***************************************************************************************
      double[] TAU_WIN, RHO_WIN;
      TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
      RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]     
      Window[] winSs = new Window[16];
      Window[] winNs = new Window[22];
      //南
      winSs[0] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[1] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[2] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[3] = new Window(5.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[4] = new Window(5.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[5] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incS);
      winSs[6] = new Window(4.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incW);
      winSs[7] = new Window(10.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incW);
      winSs[8] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[9] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[10] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[11] = new Window(5.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[12] = new Window(5.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[13] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incS);
      winSs[14] = new Window(4.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incW);
      winSs[15] = new Window(10.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incW);
      //北
      winNs[0] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[1] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[2] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[3] = new Window(5.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[4] = new Window(5.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[5] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[6] = new Window(6.5 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incN);
      winNs[7] = new Window(4.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incW);
      winNs[8] = new Window(10.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incW);
      winNs[9] = new Window(4.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incE);
      winNs[10] = new Window(10.0 * WIN_HEIGHT_LOW, TAU_WIN, RHO_WIN, incE);
      winNs[11] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[12] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[13] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[14] = new Window(5.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[15] = new Window(5.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[16] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[17] = new Window(6.5 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incN);
      winNs[18] = new Window(4.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incW);
      winNs[19] = new Window(10.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incW);
      winNs[20] = new Window(4.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incE);
      winNs[21] = new Window(10.0 * WIN_HEIGHT_HIGH, TAU_WIN, RHO_WIN, incE);

      //初期化
      void initWin(Window[] wins)
      {
        for (int i = 0; i < wins.Length; i++)
        {
          VenetianBlind blind = new VenetianBlind(25, 22.5, 0, 0, 0.80, 0.90);
          blind.SlatAngle = 0;
          wins[i].SetShadingDevice(0, blind);
          wins[i].ConvectiveCoefficientF = 18;
          wins[i].ConvectiveCoefficientB = 4;
          wins[i].LongWaveEmissivityF = wins[i].LongWaveEmissivityB = 0.9;
        }
      }
      initWin(winSs);
      initWin(winNs);

      //多数室の作成************************************************************************************
      MultiRooms[] mRm = new MultiRooms[2];
      //北側事務室
      mRm[0] = new MultiRooms(2, znSs, walls, winSs);
      for (int i = 0; i < 6; i++)
      {
        mRm[0].AddZone(0, i); //西側下部
        mRm[0].AddZone(0, i + 12); //西側上部
        mRm[0].AddZone(1, i + 6); //東側下部
        mRm[0].AddZone(1, i + 18); //東側上部
      }

      //南側事務室
      mRm[1] = new MultiRooms(2, znNs, walls, winNs);
      for (int i = 0; i < 6; i++)
      {
        mRm[1].AddZone(0, i); //西側下部
        mRm[1].AddZone(0, i + 14); //西側上部
        mRm[1].AddZone(1, i + 6); //東側下部
        mRm[1].AddZone(1, i + 20); //東側上部
      }
      //東側N12, N13
      mRm[1].AddZone(1, 12);
      mRm[1].AddZone(1, 13);
      mRm[1].AddZone(1, 26);
      mRm[1].AddZone(1, 27);

      //外壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(0, 0, false); mRm[0].SetOutsideWall(0, true, incS);
      mRm[0].AddWall(24, 1, false); mRm[0].SetOutsideWall(1, true, incS);
      mRm[0].AddWall(1, 2, false); mRm[0].SetOutsideWall(2, true, incS);
      mRm[0].AddWall(24, 3, false); mRm[0].SetOutsideWall(3, true, incS);
      mRm[0].AddWall(2, 4, false); mRm[0].SetOutsideWall(4, true, incS);
      mRm[0].AddWall(24, 5, false); mRm[0].SetOutsideWall(5, true, incS);
      mRm[0].AddWall(6, 6, false); mRm[0].SetOutsideWall(6, true, incS);
      mRm[0].AddWall(24, 7, false); mRm[0].SetOutsideWall(7, true, incS);
      mRm[0].AddWall(7, 8, false); mRm[0].SetOutsideWall(8, true, incS);
      mRm[0].AddWall(24, 9, false); mRm[0].SetOutsideWall(9, true, incS);
      mRm[0].AddWall(8, 10, false); mRm[0].SetOutsideWall(10, true, incS);
      mRm[0].AddWall(24, 11, false); mRm[0].SetOutsideWall(11, true, incS);
      //南西側
      mRm[0].AddWall(0, 12, false); mRm[0].SetOutsideWall(12, true, incS);
      mRm[0].AddWall(24, 13, false); mRm[0].SetOutsideWall(13, true, incS);
      mRm[0].AddWall(3, 14, false); mRm[0].SetOutsideWall(14, true, incE);
      mRm[0].AddWall(24, 15, false); mRm[0].SetOutsideWall(15, true, incE);
      //北側
      mRm[1].AddWall(0, 16, false); mRm[1].SetOutsideWall(16, true, incN);
      mRm[1].AddWall(28, 17, false); mRm[1].SetOutsideWall(17, true, incN);
      mRm[1].AddWall(1, 18, false); mRm[1].SetOutsideWall(18, true, incN);
      mRm[1].AddWall(28, 19, false); mRm[1].SetOutsideWall(19, true, incN);
      mRm[1].AddWall(2, 20, false); mRm[1].SetOutsideWall(20, true, incN);
      mRm[1].AddWall(28, 21, false); mRm[1].SetOutsideWall(21, true, incN);
      mRm[1].AddWall(6, 22, false); mRm[1].SetOutsideWall(22, true, incN);
      mRm[1].AddWall(28, 23, false); mRm[1].SetOutsideWall(23, true, incN);
      mRm[1].AddWall(7, 24, false); mRm[1].SetOutsideWall(24, true, incN);
      mRm[1].AddWall(28, 25, false); mRm[1].SetOutsideWall(25, true, incN);
      mRm[1].AddWall(8, 26, false); mRm[1].SetOutsideWall(26, true, incN);
      mRm[1].AddWall(28, 27, false); mRm[1].SetOutsideWall(27, true, incN);
      mRm[1].AddWall(9, 28, false); mRm[1].SetOutsideWall(28, true, incN);
      mRm[1].AddWall(28, 29, false); mRm[1].SetOutsideWall(29, true, incN);
      //北西
      mRm[1].AddWall(0, 30, false); mRm[1].SetOutsideWall(30, true, incW);
      mRm[1].AddWall(28, 31, false); mRm[1].SetOutsideWall(31, true, incW);
      mRm[1].AddWall(3, 32, false); mRm[1].SetOutsideWall(32, true, incW);
      mRm[1].AddWall(28, 33, false); mRm[1].SetOutsideWall(33, true, incW);
      //北東
      mRm[1].AddWall(9, 34, false); mRm[1].SetOutsideWall(34, true, incE);
      mRm[1].AddWall(28, 35, false); mRm[1].SetOutsideWall(35, true, incE);
      mRm[1].AddWall(13, 36, false); mRm[1].SetOutsideWall(36, true, incE);
      mRm[1].AddWall(28, 37, false); mRm[1].SetOutsideWall(37, true, incE);

      //内壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(3, 38, true); mRm[0].UseAdjacentSpaceFactor(38, false, 0.7); //機械室
      mRm[0].AddWall(4, 39, true); mRm[0].UseAdjacentSpaceFactor(39, false, 0.5); //廊下
      mRm[0].AddWall(5, 40, true); mRm[0].UseAdjacentSpaceFactor(40, false, 0.5); //廊下
      mRm[0].AddWall(9, 41, true); mRm[0].UseAdjacentSpaceFactor(41, false, 0.5); //廊下
      mRm[0].AddWall(10, 42, true); mRm[0].UseAdjacentSpaceFactor(42, false, 0.5); //廊下
      mRm[0].AddWall(11, 43, true); mRm[0].UseAdjacentSpaceFactor(43, false, 0.5); //廊下
      mRm[0].AddWall(8, 44, true); mRm[0].UseAdjacentSpaceFactor(44, false, 0.5); //WC
      mRm[0].AddWall(11, 45, true); mRm[0].UseAdjacentSpaceFactor(45, false, 0.5); //WC
      //北側
      mRm[1].AddWall(3, 46, true); mRm[1].UseAdjacentSpaceFactor(46, false, 0.5); //機械室
      mRm[1].AddWall(4, 47, true); mRm[1].UseAdjacentSpaceFactor(47, false, 0.5); //廊下
      mRm[1].AddWall(5, 48, true); mRm[1].UseAdjacentSpaceFactor(48, false, 0.5); //廊下
      mRm[1].AddWall(10, 49, true); mRm[1].UseAdjacentSpaceFactor(49, false, 0.5); //廊下
      mRm[1].AddWall(11, 50, true); mRm[1].UseAdjacentSpaceFactor(50, false, 0.5); //廊下
      mRm[1].AddWall(12, 51, true); mRm[1].UseAdjacentSpaceFactor(51, false, 0.5); //廊下
      mRm[1].AddWall(13, 52, true); mRm[1].UseAdjacentSpaceFactor(52, false, 0.5); //階段室
      //床・天井
      for (int i = 0; i < 12; i++)
      {
        mRm[0].AddWall(i, 53 + i, true); //床
        mRm[0].AddWall(24, 53 + i, false); //床
        mRm[0].AddWall(24, 79 + i, true); //天井
        mRm[0].AddWall(i + 12, 79 + i, false); //天井
        mRm[0].AddWall(i + 12, 105 + i, true); //上下空間下層間仕切り
        mRm[0].AddWall(i, 105 + i, false); //上下空間下層間仕切り
      }
      for (int i = 0; i < 14; i++)
      {
        mRm[1].AddWall(i, 65 + i, true); //床
        mRm[1].AddWall(28, 65 + i, false); //床
        mRm[1].AddWall(28, 91 + i, true); //天井
        mRm[1].AddWall(i + 14, 91 + i, false); //天井
        mRm[1].AddWall(i + 14, 117 + i, true); //上下空間下層間仕切り
        mRm[1].AddWall(i, 117 + i, false); //上下空間下層間仕切り
      }
      //テナント間仕切り
      mRm[0].AddWall(2, 131, true); mRm[0].AddWall(6, 131, false); //南側事務室南
      mRm[0].AddWall(5, 132, true); mRm[0].AddWall(9, 132, false); //南側事務室北
      mRm[1].AddWall(2, 133, true); mRm[1].AddWall(6, 133, false); //北側事務室北
      mRm[1].AddWall(5, 134, true); mRm[1].AddWall(9, 134, false); //北側事務室南

      //窓を登録***************************************************************************************
      mRm[0].AddWindow(0, 0);
      mRm[0].AddWindow(1, 1);
      mRm[0].AddWindow(2, 2);
      mRm[0].AddWindow(6, 3);
      mRm[0].AddWindow(7, 4);
      mRm[0].AddWindow(8, 5);
      mRm[0].AddWindow(0, 6);
      mRm[0].AddWindow(3, 7);
      mRm[0].AddWindow(12, 8);
      mRm[0].AddWindow(13, 9);
      mRm[0].AddWindow(14, 10);
      mRm[0].AddWindow(18, 11);
      mRm[0].AddWindow(19, 12);
      mRm[0].AddWindow(20, 13);
      mRm[0].AddWindow(12, 14);
      mRm[0].AddWindow(15, 15);
      mRm[1].AddWindow(0, 0);
      mRm[1].AddWindow(1, 1);
      mRm[1].AddWindow(2, 2);
      mRm[1].AddWindow(6, 3);
      mRm[1].AddWindow(7, 4);
      mRm[1].AddWindow(8, 5);
      mRm[1].AddWindow(9, 6);
      mRm[1].AddWindow(0, 7);
      mRm[1].AddWindow(3, 8);
      mRm[1].AddWindow(9, 9);
      mRm[1].AddWindow(13, 10);
      mRm[1].AddWindow(14, 11);
      mRm[1].AddWindow(15, 12);
      mRm[1].AddWindow(16, 13);
      mRm[1].AddWindow(20, 14);
      mRm[1].AddWindow(21, 15);
      mRm[1].AddWindow(22, 16);
      mRm[1].AddWindow(23, 17);
      mRm[1].AddWindow(14, 18);
      mRm[1].AddWindow(17, 19);
      mRm[1].AddWindow(23, 20);
      mRm[1].AddWindow(27, 21);

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      //南側
      mRm[0].SetSWDistributionRateToFloor(0, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(1, 53 + 1, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(2, 53 + 2, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(3, 53 + 6, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(4, 53 + 7, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(5, 53 + 8, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(6, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(7, 53 + 3, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(8, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(9, 53 + 1, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(10, 53 + 2, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(11, 53 + 6, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(12, 53 + 7, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(13, 53 + 8, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(14, 53 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(15, 53 + 3, true, SW_RATE_TO_FLOOR);
      //北側
      mRm[1].SetSWDistributionRateToFloor(0, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(1, 65 + 1, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(2, 65 + 2, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(3, 65 + 6, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(4, 65 + 7, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(5, 65 + 8, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(6, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(7, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(8, 65 + 3, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(9, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(10, 65 + 13, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(11, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(12, 65 + 1, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(13, 65 + 2, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(14, 65 + 6, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(15, 65 + 7, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(16, 65 + 8, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(17, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(18, 65 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(19, 65 + 3, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(20, 65 + 9, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(21, 65 + 13, true, SW_RATE_TO_FLOOR);

      //隙間風と熱容量設定*************************************************************************
      void initZone(Zone[] zns)
      {
        int half = zns.Length / 2; //半分までは下部空間
        for (int i = 0; i < zns.Length; i++)
        {
          zns[i].VentilationRate = zns[i].AirMass * LEAK_RATE / 3600d;
          zns[i].InitializeAirState(22, 0.0105);
          zns[i].HeatCapacity = zns[i].AirMass * 1006 * (
            (i < half || i == zns.Length - 1 ) ? HCAP_FURNITURE : 3); //下部空間は家具の熱容量を考慮。天井裏も同等とみなす
        }
      }
      initZone(znNs);
      initZone(znSs);

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(mRm);

      //ゾーン間換気の設定
      const double cvRate = 150d * 1.2 / 3600d;
      //南：下部空間
      bModel.SetCrossVentilation(0, 0, 0, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 2, 4.0 * cvRate);
      //bModel.SetCrossVentilation(0, 2, 0, 6, 4.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(0, 6, 0, 7, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 7, 0, 8, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 0, 0, 3, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 1, 0, 4, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 2, 0, 5, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 6, 0, 9, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 7, 0, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 8, 0, 11, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 3, 0, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 4, 0, 5, 10.0 * cvRate);
      //bModel.SetCrossVentilation(0, 5, 0, 9, 10.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(0, 9, 0, 10, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 10, 0, 11, 10.0 * cvRate);
      //南：上部空間
      bModel.SetCrossVentilation(0, 12, 0, 13, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 13, 0, 14, 4.0 * cvRate);
      //bModel.SetCrossVentilation(0, 14, 0, 18, 4.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(0, 18, 0, 19, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 19, 0, 20, 4.0 * cvRate);
      bModel.SetCrossVentilation(0, 12, 0, 15, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 13, 0, 16, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 14, 0, 17, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 18, 0, 21, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 19, 0, 22, 5.0 * cvRate);
      bModel.SetCrossVentilation(0, 20, 0, 23, 8.5 * cvRate);
      bModel.SetCrossVentilation(0, 15, 0, 16, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 16, 0, 17, 10.0 * cvRate);
      //bModel.SetCrossVentilation(0, 17, 0, 21, 10.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(0, 21, 0, 22, 10.0 * cvRate);
      bModel.SetCrossVentilation(0, 22, 0, 23, 10.0 * cvRate);
      //北：下部空間
      bModel.SetCrossVentilation(1, 0, 1, 1, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 2, 4.0 * cvRate);
      //bModel.SetCrossVentilation(1, 2, 1, 6, 4.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(1, 6, 1, 7, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 7, 1, 8, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 8, 1, 9, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 0, 1, 3, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 1, 1, 4, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 2, 1, 5, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 6, 1, 10, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 7, 1, 11, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 8, 1, 12, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 9, 1, 13, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 3, 1, 4, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 4, 1, 5, 10.0 * cvRate);
      //bModel.SetCrossVentilation(1, 5, 1, 10, 10.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(1, 10, 1, 11, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 11, 1, 12, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 12, 1, 13, 10.0 * cvRate);
      //北：上部空間
      bModel.SetCrossVentilation(1, 14, 1, 15, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 15, 1, 16, 4.0 * cvRate);
      //bModel.SetCrossVentilation(1, 16, 1, 20, 4.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(1, 20, 1, 21, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 21, 1, 22, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 22, 1, 23, 4.0 * cvRate);
      bModel.SetCrossVentilation(1, 14, 1, 17, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 15, 1, 18, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 16, 1, 19, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 20, 1, 24, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 21, 1, 25, 5.0 * cvRate);
      bModel.SetCrossVentilation(1, 22, 1, 16, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 23, 1, 27, 8.5 * cvRate);
      bModel.SetCrossVentilation(1, 17, 1, 18, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 18, 1, 19, 10.0 * cvRate);
      //bModel.SetCrossVentilation(1, 19, 1, 24, 10.0 * cvRate); //テナント間のため、削除
      bModel.SetCrossVentilation(1, 24, 1, 25, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 25, 1, 26, 10.0 * cvRate);
      bModel.SetCrossVentilation(1, 26, 1, 27, 10.0 * cvRate);

      return bModel;
    }

  }
}
