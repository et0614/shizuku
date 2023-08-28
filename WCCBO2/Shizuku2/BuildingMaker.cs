using Popolo.ThermalLoad;
using Popolo.Weather;

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
      inUDWL[0] = new WallLayer("仮想間仕切り", 0.001 * 1, 0.001, 0.001 * 1); //内壁_上下空間接続用:(1.0W/m2K)
      
      //ゾーンを作成/////////////////////////
      Zone[] znSs = new Zone[19]; //下部9, 上部9, 天井裏1
      Zone[] znNs = new Zone[19];
      double[] ZN_AREAS = new double[] { 26, 26, 26, 32.5, 32.5, 32.5, 32.5, 32.5, 32.5 };
      double znASum = 0;
      for (int i = 0; i < ZN_AREAS.Length; i++)
      {
        znASum += ZN_AREAS[i];
        znSs[i] = new Zone("S" + (i + 1), ZN_AREAS[i] * L_ZONE_HEIGHT * 1.2, ZN_AREAS[i]);
        znSs[i + 9] = new Zone("S" + (i + 1) + "_Up", ZN_AREAS[i] * U_ZONE_HEIGHT * 1.2, ZN_AREAS[i]);
        znNs[i] = new Zone("N" + (i + 1), ZN_AREAS[i] * L_ZONE_HEIGHT * 1.2, ZN_AREAS[i]);
        znNs[i + 9] = new Zone("N" + (i + 1) + "_Up", ZN_AREAS[i] * U_ZONE_HEIGHT * 1.2, ZN_AREAS[i]);
      }
      znSs[18] = new Zone("S_Attic", znASum * 1.5 * 1.2, znASum);
      znNs[18] = new Zone("N_Attic", znASum * 1.5 * 1.2, znASum);

      //窓を作成***************************************************************************************
      const double WIN_AREA_UP = 5.32 * (1.0 / 1.4);
      const double WIN_AREA_LOW = 5.32 * (0.4 / 1.4);
      double[] TAU_WIN, RHO_WIN;
      TAU_WIN = new double[] { 0.815 }; //ガラスの透過率リスト[-]
      RHO_WIN = new double[] { 0.072 }; //ガラスの反射率リスト[-]     
      Window[] winSs = new Window[12];
      Window[] winNs = new Window[12];
      //南
      winSs[0] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incS);//S1
      winSs[1] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incS);//S2
      winSs[2] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incS);//S3
      winSs[3] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S1（西側は2つの窓を3ゾーンで分ける）
      winSs[4] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S4
      winSs[5] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S5
      winSs[6] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incS);//S1
      winSs[7] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incS);//S2
      winSs[8] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incS);//S3
      winSs[9] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S1
      winSs[10] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S4
      winSs[11] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//S5
      //北
      winNs[0] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incN);//N1
      winNs[1] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incN);//N2
      winNs[2] = new Window(WIN_AREA_LOW, TAU_WIN, RHO_WIN, incN);//N3
      winNs[3] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N1
      winNs[4] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N4
      winNs[5] = new Window(WIN_AREA_LOW * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N5
      winNs[6] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incN);//N1
      winNs[7] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incN);//N2
      winNs[8] = new Window(WIN_AREA_UP, TAU_WIN, RHO_WIN, incN);//N3
      winNs[9] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N1
      winNs[10] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N4
      winNs[11] = new Window(WIN_AREA_UP * (2.0 / 3.0), TAU_WIN, RHO_WIN, incW);//N5

      //初期化:ブラインド無し
      for (int i = 0; i < winSs.Length; i++)
      {
        winSs[i].ConvectiveCoefficientF = winNs[i].ConvectiveCoefficientF = 18;
        winSs[i].ConvectiveCoefficientB = winNs[i].ConvectiveCoefficientB = 4;
        winSs[i].LongWaveEmissivityF = winSs[i].LongWaveEmissivityB = 
          winNs[i].LongWaveEmissivityF = winNs[i].LongWaveEmissivityB = 0.9;
      }

      //壁体の作成***************************************************************************************
      const double CEL_HEIGHT = 1.3;
      const double WAL_HEIGHT = 2.7;
      Wall[] walls = new Wall[90];
      
      //外壁（南）
      walls[0] = new Wall(6.5 * WAL_HEIGHT - winSs[0].Area, exWL);
      walls[1] = new Wall(6.5 * CEL_HEIGHT - winSs[6].Area, exbmWL);
      walls[2] = new Wall(6.5 * WAL_HEIGHT - winSs[1].Area, exWL);
      walls[3] = new Wall(6.5 * CEL_HEIGHT - winSs[7].Area, exbmWL);
      walls[4] = new Wall(6.5 * WAL_HEIGHT - winSs[2].Area, exWL);
      walls[5] = new Wall(6.5 * CEL_HEIGHT - winSs[8].Area, exbmWL);
      //外壁（南西）
      walls[6] = new Wall(4.0 * WAL_HEIGHT - winSs[3].Area, exWL);
      walls[7] = new Wall(4.0 * CEL_HEIGHT - winSs[9].Area, exbmWL);
      walls[8] = new Wall(5.0 * WAL_HEIGHT - winSs[4].Area, exWL);
      walls[9] = new Wall(5.0 * CEL_HEIGHT - winSs[10].Area, exbmWL);
      walls[10] = new Wall(5.0 * WAL_HEIGHT - winSs[5].Area, exWL);
      walls[11] = new Wall(5.0 * CEL_HEIGHT - winSs[11].Area, exbmWL);
      //外壁（北）
      walls[12] = new Wall(6.5 * WAL_HEIGHT - winNs[0].Area, exWL);
      walls[13] = new Wall(6.5 * CEL_HEIGHT - winNs[6].Area, exbmWL);
      walls[14] = new Wall(6.5 * WAL_HEIGHT - winNs[1].Area, exWL);
      walls[15] = new Wall(6.5 * CEL_HEIGHT - winNs[7].Area, exbmWL);
      walls[16] = new Wall(6.5 * WAL_HEIGHT - winNs[2].Area, exWL);
      walls[17] = new Wall(6.5 * CEL_HEIGHT - winNs[8].Area, exbmWL);
      //外壁（北西）
      walls[18] = new Wall(4.0 * WAL_HEIGHT - winNs[3].Area, exWL);
      walls[19] = new Wall(4.0 * CEL_HEIGHT - winNs[9].Area, exbmWL);
      walls[20] = new Wall(5.0 * WAL_HEIGHT - winNs[4].Area, exWL);
      walls[21] = new Wall(5.0 * CEL_HEIGHT - winNs[10].Area, exbmWL);
      walls[22] = new Wall(5.0 * WAL_HEIGHT - winNs[5].Area, exWL);
      walls[23] = new Wall(5.0 * CEL_HEIGHT - winNs[11].Area, exbmWL);
      //内壁*********************************
      //南側内壁
      walls[24] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[25] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[26] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[27] = new Wall(4.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[28] = new Wall(5.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[29] = new Wall(5.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      //北側内壁
      walls[30] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[31] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[32] = new Wall(6.5 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[33] = new Wall(4.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[34] = new Wall(5.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      walls[35] = new Wall(5.0 * (WAL_HEIGHT + CEL_HEIGHT), inWL);
      //床・天井・上下空間仮想間仕切り
      for (int i = 0; i < ZN_AREAS.Length; i++)
      {
        walls[36 + i] = new Wall(ZN_AREAS[i], flrWL); //南側床
        walls[45 + i] = new Wall(ZN_AREAS[i], flrWL); //北側床
        walls[54 + i] = new Wall(ZN_AREAS[i], clWL); //南側天井
        walls[63 + i] = new Wall(ZN_AREAS[i], clWL); //北側天井
        walls[72 + i] = new Wall(ZN_AREAS[i], inUDWL); //南側上下空間仮想間仕切り
        walls[81 + i] = new Wall(ZN_AREAS[i], inUDWL); //北側上下空間仮想間仕切り
      }

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
        if (nm == "仮想間仕切り")
        {
          walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.0;
          walls[i].ConvectiveCoefficientF = walls[i].ConvectiveCoefficientB = 10000;
        }
      }

      //多数室の作成************************************************************************************
      MultiRooms[] mRm = new MultiRooms[2];
      mRm[0] = new MultiRooms(2, znSs, walls, winSs);
      mRm[1] = new MultiRooms(2, znNs, walls, winNs);
      for (int i = 0; i < 9; i++)
      {
        mRm[0].AddZone(0, i); //南側下部
        mRm[0].AddZone(0, i + 9); //南側上部
        mRm[1].AddZone(0, i); //北側下部
        mRm[1].AddZone(0, i + 9); //北側上部
      }
      mRm[0].AddZone(1, 18); //南側天井裏
      mRm[1].AddZone(1, 18); //北側天井裏

      //外壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(0, 0, false); mRm[0].SetOutsideWall(0, true, incS);
      mRm[0].AddWall(18, 1, false); mRm[0].SetOutsideWall(1, true, incS);
      mRm[0].AddWall(1, 2, false); mRm[0].SetOutsideWall(2, true, incS);
      mRm[0].AddWall(18, 3, false); mRm[0].SetOutsideWall(3, true, incS);
      mRm[0].AddWall(2, 4, false); mRm[0].SetOutsideWall(4, true, incS);
      mRm[0].AddWall(18, 5, false); mRm[0].SetOutsideWall(5, true, incS);
      //南西側
      mRm[0].AddWall(0, 6, false); mRm[0].SetOutsideWall(6, true, incW);
      mRm[0].AddWall(18, 7, false); mRm[0].SetOutsideWall(7, true, incW);
      mRm[0].AddWall(3, 8, false); mRm[0].SetOutsideWall(8, true, incW);
      mRm[0].AddWall(18, 9, false); mRm[0].SetOutsideWall(9, true, incW);
      mRm[0].AddWall(4, 10, false); mRm[0].SetOutsideWall(10, true, incW);
      mRm[0].AddWall(18, 11, false); mRm[0].SetOutsideWall(11, true, incW);
      //北側
      mRm[1].AddWall(0, 12, false); mRm[1].SetOutsideWall(12, true, incN);
      mRm[1].AddWall(18, 13, false); mRm[1].SetOutsideWall(13, true, incN);
      mRm[1].AddWall(1, 14, false); mRm[1].SetOutsideWall(14, true, incN);
      mRm[1].AddWall(18, 15, false); mRm[1].SetOutsideWall(15, true, incN);
      mRm[1].AddWall(2, 16, false); mRm[1].SetOutsideWall(16, true, incN);
      mRm[1].AddWall(18, 17, false); mRm[1].SetOutsideWall(17, true, incN);
      //北西
      mRm[1].AddWall(0, 18, false); mRm[1].SetOutsideWall(18, true, incW);
      mRm[1].AddWall(18, 19, false); mRm[1].SetOutsideWall(19, true, incW);
      mRm[1].AddWall(3, 20, false); mRm[1].SetOutsideWall(20, true, incW);
      mRm[1].AddWall(18, 21, false); mRm[1].SetOutsideWall(21, true, incW);
      mRm[1].AddWall(4, 22, false); mRm[1].SetOutsideWall(22, true, incW);
      mRm[1].AddWall(18, 23, false); mRm[1].SetOutsideWall(23, true, incW);

      //内壁を登録***************************************************************************************
      //南側
      mRm[0].AddWall(4, 24, true); mRm[0].UseAdjacentSpaceFactor(24, false, 0.7); //機械室
      mRm[0].AddWall(6, 25, true); mRm[0].UseAdjacentSpaceFactor(25, false, 0.5); //廊下
      mRm[0].AddWall(8, 26, true); mRm[0].UseAdjacentSpaceFactor(26, false, 0.5); //廊下
      mRm[0].AddWall(2, 27, true); mRm[0].AddWall(2, 27, false); //対称形の隣室を仮定
      mRm[0].AddWall(7, 28, true); mRm[0].AddWall(7, 28, false); //対称形の隣室を仮定
      mRm[0].AddWall(8, 29, true); mRm[0].AddWall(8, 29, false); //対称形の隣室を仮定
      //北側
      mRm[1].AddWall(4, 30, true); mRm[1].UseAdjacentSpaceFactor(30, false, 0.7); //機械室
      mRm[1].AddWall(6, 31, true); mRm[1].UseAdjacentSpaceFactor(31, false, 0.5); //廊下
      mRm[1].AddWall(8, 32, true); mRm[1].UseAdjacentSpaceFactor(32, false, 0.5); //廊下
      mRm[1].AddWall(2, 33, true); mRm[1].AddWall(2, 33, false); //対称形の隣室を仮定
      mRm[1].AddWall(7, 34, true); mRm[1].AddWall(7, 34, false); //対称形の隣室を仮定
      mRm[1].AddWall(8, 35, true); mRm[1].AddWall(8, 35, false); //対称形の隣室を仮定
      //床・天井・上下空間仮想間仕切り
      for (int i = 0; i < ZN_AREAS.Length; i++)
      {
        mRm[0].AddWall(i, 36 + i, true); mRm[0].AddWall(18, 36 + i, false); //南側床
        mRm[1].AddWall(i, 45 + i, true); mRm[1].AddWall(18, 45 + i, false); //北側床
        mRm[0].AddWall(18, 54 + i, true); mRm[0].AddWall(i + 9, 54 + i, false); //南側天井
        mRm[1].AddWall(18, 63 + i, true); mRm[1].AddWall(i + 9, 63 + i, false); //北側天井
        mRm[0].AddWall(i + 9, 72 + i, true); mRm[0].AddWall(i, 72 + i, false); //南側上下空間仮想間仕切り
        mRm[1].AddWall(i + 9, 81 + i, true); mRm[1].AddWall(i, 81 + i, false); //北側上下空間仮想間仕切り
      }

      //窓を登録***************************************************************************************
      for (int i = 0; i < 2; i++)
      {
        mRm[i].AddWindow(0, 0);
        mRm[i].AddWindow(1, 1);
        mRm[i].AddWindow(2, 2);
        mRm[i].AddWindow(0, 3);
        mRm[i].AddWindow(3, 4);
        mRm[i].AddWindow(4, 5);
        mRm[i].AddWindow(9, 6);
        mRm[i].AddWindow(10, 7);
        mRm[i].AddWindow(11, 8);
        mRm[i].AddWindow(9, 9);
        mRm[i].AddWindow(12, 10);
        mRm[i].AddWindow(13, 11);
      }

      //ペリメータ床に短波長優先配分
      const double SW_RATE_TO_FLOOR = 0.7;
      //南側
      mRm[0].SetSWDistributionRateToFloor(0, 36 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(1, 36 + 1, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(2, 36 + 2, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(3, 36 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(4, 36 + 4, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(5, 36 + 5, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(6, 36 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(7, 36 + 1, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(8, 36 + 2, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(9, 36 + 0, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(10, 36 + 4, true, SW_RATE_TO_FLOOR);
      mRm[0].SetSWDistributionRateToFloor(11, 36 + 5, true, SW_RATE_TO_FLOOR);
      //北側
      mRm[1].SetSWDistributionRateToFloor(0, 45 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(1, 45 + 1, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(2, 45 + 2, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(3, 45 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(4, 45 + 4, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(5, 45 + 5, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(6, 45 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(7, 45 + 1, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(8, 45 + 2, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(9, 45 + 0, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(10, 45 + 4, true, SW_RATE_TO_FLOOR);
      mRm[1].SetSWDistributionRateToFloor(11, 45 + 5, true, SW_RATE_TO_FLOOR);

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

          //最大能力（デバッグ用設定）
          zns[i].CoolingCapacity = 200 * zns[i].FloorArea * 0.5; //上下空間に能力を分けるので半分
          zns[i].HeatingCapacity = 200 * zns[i].FloorArea * 0.5;
          zns[i].DehumidifyingCapacity = 200 / 2500000d * zns[i].FloorArea * 0.5;
          zns[i].HumidifyingCapacity = 200 / 2500000d * zns[i].FloorArea * 0.5;
        }
      }
      initZone(znNs);
      initZone(znSs);

      //建物モデルの作成
      BuildingThermalModel bModel = new BuildingThermalModel(mRm);

      //ゾーン間換気の設定：境界長さあたりで150CMH
      const double rLow = L_ZONE_HEIGHT / (L_ZONE_HEIGHT + U_ZONE_HEIGHT);
      const double cvRateUp = 150d * 1.2 / 3600d * (1.0 - rLow);
      const double cvRateLow = 150d * 1.2 / 3600d * rLow;
      for (int i = 0; i < 2; i++) //i=0:南, i=1:北
      {
        //下部空間
        bModel.SetCrossVentilation(i, 0, i, 1, 4.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 1, i, 2, 4.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 3, i, 5, 5.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 5, i, 7, 5.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 4, i, 6, 5.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 6, i, 8, 5.0 * cvRateLow);
        bModel.SetCrossVentilation(i, 0, i, 3, 6.5 * cvRateLow);
        bModel.SetCrossVentilation(i, 1, i, 5, 6.5 * cvRateLow);
        bModel.SetCrossVentilation(i, 2, i, 7, 6.5 * cvRateLow);
        bModel.SetCrossVentilation(i, 3, i, 4, 6.5 * cvRateLow);
        bModel.SetCrossVentilation(i, 5, i, 6, 6.5 * cvRateLow);
        bModel.SetCrossVentilation(i, 7, i, 10, 6.5 * cvRateLow);
        //上部空間
        bModel.SetCrossVentilation(i, 9, i, 10, 4.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 10, i, 11, 4.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 12, i, 14, 5.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 14, i, 16, 5.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 13, i, 15, 5.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 15, i, 17, 5.0 * cvRateUp);
        bModel.SetCrossVentilation(i, 9, i, 12, 6.5 * cvRateUp);
        bModel.SetCrossVentilation(i, 10, i, 14, 6.5 * cvRateUp);
        bModel.SetCrossVentilation(i, 11, i, 16, 6.5 * cvRateUp);
        bModel.SetCrossVentilation(i, 12, i, 13, 6.5 * cvRateUp);
        bModel.SetCrossVentilation(i, 14, i, 15, 6.5 * cvRateUp);
        bModel.SetCrossVentilation(i, 16, i, 17, 6.5 * cvRateUp);
      }      

      return bModel;
    }

  }
}
